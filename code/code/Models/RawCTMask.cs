using System;
using System.IO;
using System.Numerics;
using System.Text.RegularExpressions;

namespace code.Services;

public class RawCtMask : Geometry
{
    private readonly Vector3 _position;
    private readonly float _scale;
    private readonly ColorMap _colorMap;
    private readonly byte[] _data;

    private readonly int[] _resolution = new int[3];
    private readonly float[] _thickness = new float[3];
    private readonly Vector3 _v0;
    private readonly Vector3 _v1;

    public RawCtMask(string datFile, string rawFile, Vector3 position, float scale, ColorMap colorMap) : base(Color.NONE)
    {
        _position = position;
        _scale = scale;
        _colorMap = colorMap;

        var lines = File.ReadLines(datFile);
        foreach (var line in lines)
        {
            var kv = Regex.Replace(line, "[:\\t ]+", ":").Split(":");
            if (kv[0] == "Resolution")
            {
                _resolution[0] = Convert.ToInt32(kv[1]);
                _resolution[1] = Convert.ToInt32(kv[2]);
                _resolution[2] = Convert.ToInt32(kv[3]);
            }
            else if (kv[0] == "SliceThickness")
            {
                _thickness[0] = (float)Convert.ToDouble(kv[1]);
                _thickness[1] = (float)Convert.ToDouble(kv[2]);
                _thickness[2] = (float)Convert.ToDouble(kv[3]);
            }
        }

        _v0 = position;
        _v1 = position + new Vector3(
            _resolution[0] * _thickness[0] * scale,
            _resolution[1] * _thickness[1] * scale,
            _resolution[2] * _thickness[2] * scale
        );

        var len = _resolution[0] * _resolution[1] * _resolution[2];
        _data = new byte[len];
        using FileStream f = new FileStream(rawFile, FileMode.Open, FileAccess.Read);
        if (f.Read(_data, 0, len) != len)
        {
            throw new InvalidDataException($"Failed to read the {len}-byte raw data");
        }
    }

    private ushort Value(int x, int y, int z)
    {
        if (x < 0 || y < 0 || z < 0 || x >= _resolution[0] || y >= _resolution[1] || z >= _resolution[2])
        {
            return 0;
        }

        return _data[z * _resolution[1] * _resolution[0] + y * _resolution[0] + x];
    }

public override Intersection GetIntersection(Line ray, float minDist, float maxDist)
{
    float tmin = (Math.Min(_v0.X, _v1.X) - ray.X0.X) / ray.Dx.X;
    float tmax = (Math.Max(_v0.X, _v1.X) - ray.X0.X) / ray.Dx.X;
    if (tmin > tmax) (tmin, tmax) = (tmax, tmin);

    float tymin = (Math.Min(_v0.Y, _v1.Y) - ray.X0.Y) / ray.Dx.Y;
    float tymax = (Math.Max(_v0.Y, _v1.Y) - ray.X0.Y) / ray.Dx.Y;
    if (tymin > tymax) (tymin, tymax) = (tymax, tymin);

    if ((tmin > tymax) || (tymin > tmax)) return Intersection.NONE;

    if (tymin > tmin) tmin = tymin;
    if (tymax < tmax) tmax = tymax;

    float tzmin = (Math.Min(_v0.Z, _v1.Z) - ray.X0.Z) / ray.Dx.Z;
    float tzmax = (Math.Max(_v0.Z, _v1.Z) - ray.X0.Z) / ray.Dx.Z;
    if (tzmin > tzmax) (tzmin, tzmax) = (tzmax, tzmin);

    if ((tmin > tzmax) || (tzmin > tmax)) return Intersection.NONE;

    if (tzmin > tmin) tmin = tzmin;
    if (tzmax < tmax) tmax = tzmax;

    if (tmin > maxDist || tmax < minDist) return Intersection.NONE;

    Vector3 currentPoint = ray.CoordinateToPosition(tmin);
    float stepSize = 0.2f;

    Color accumulatedColor = Color.NONE;
    float accumulatedAlpha = 0.0f;

    for (float t = tmin; t < tmax; t += stepSize)
    {
        int[] idx = GetIndexes(currentPoint);
        ushort voxelValue = Value(idx[0], idx[1], idx[2]);

        if (voxelValue != 0)
        {
            Color voxelColor = _colorMap.GetColor(voxelValue);
            float voxelAlpha = (float)voxelColor.Alpha;

            // Blending Logic
            // Console.WriteLine(voxelAlpha);
            accumulatedColor = accumulatedColor + (voxelColor * voxelAlpha * (1.0 - accumulatedAlpha));
            accumulatedAlpha += voxelAlpha;
            
            accumulatedColor.Alpha = accumulatedAlpha;
            
            // Console.WriteLine($"AccCol: R:{accumulatedColor.Red}, G:{accumulatedColor.Green}, B:{accumulatedColor.Blue}, Alpha:{accumulatedColor.Alpha}");

            // Clamp alpha to ensure it doesn't exceed 1.0
            if (accumulatedAlpha >= 0.99)
            {
                Vector3 hitPosition = ray.CoordinateToPosition(t);
                Vector3 normal = GetNormal(hitPosition);
                return new Intersection(true, true, this, ray, t, normal, Material, accumulatedColor);
            }
        }

        currentPoint = ray.CoordinateToPosition(t + stepSize);
    }

    return Intersection.NONE;
}






    private int[] GetIndexes(Vector3 v)
    {
        return new[]
        {
            (int)Math.Floor((v.X - _position.X) / _thickness[0] / _scale),
            (int)Math.Floor((v.Y - _position.Y) / _thickness[1] / _scale),
            (int)Math.Floor((v.Z - _position.Z) / _thickness[2] / _scale)
        };
    }

    private Color GetColor(Vector3 v)
    {
        int[] idx = GetIndexes(v);

        ushort value = Value(idx[0], idx[1], idx[2]);
        return _colorMap.GetColor(value);
    }

    private Vector3 GetNormal(Vector3 v)
    {
        int[] idx = GetIndexes(v);
        float x0 = Value(idx[0] - 1, idx[1], idx[2]);
        float x1 = Value(idx[0] + 1, idx[1], idx[2]);
        float y0 = Value(idx[0], idx[1] - 1, idx[2]);
        float y1 = Value(idx[0], idx[1] + 1, idx[2]);
        float z0 = Value(idx[0], idx[1], idx[2] - 1);
        float z1 = Value(idx[0], idx[1], idx[2] + 1);

        return Vector3.Normalize(new Vector3(x1 - x0, y1 - y0, z1 - z0));
    }
}
