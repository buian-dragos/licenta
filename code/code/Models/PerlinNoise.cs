using System;

namespace code.Services;

public class PerlinNoise
{
    private readonly int[] perm;

    public PerlinNoise(int seed = 0)
    {
        var rand = new Random(seed);
        perm = new int[512];
        int[] p = new int[256];
        for (int i = 0; i < 256; i++) p[i] = i;
        for (int i = 0; i < 256; i++)
        {
            int j = rand.Next(256);
            int tmp = p[i];
            p[i] = p[j];
            p[j] = tmp;
        }
        for (int i = 0; i < 512; i++) perm[i] = p[i & 255];
    }

    private static float Fade(float t) => t * t * t * (t * (t * 6 - 15) + 10);
    private static float Lerp(float t, float a, float b) => a + t * (b - a);
    private static float Grad(int hash, float x, float y, float z)
    {
        int h = hash & 15;
        float u = h < 8 ? x : y;
        float v = h < 4 ? y : h == 12 || h == 14 ? x : z;
        return ((h & 1) == 0 ? u : -u) + ((h & 2) == 0 ? v : -v);
    }

    public float Noise(float x, float y, float z)
    {
        int X = (int)MathF.Floor(x) & 255;
        int Y = (int)MathF.Floor(y) & 255;
        int Z = (int)MathF.Floor(z) & 255;
        x -= MathF.Floor(x);
        y -= MathF.Floor(y);
        z -= MathF.Floor(z);
        float u = Fade(x), v = Fade(y), w = Fade(z);
        int A = perm[X] + Y, AA = perm[A] + Z, AB = perm[A + 1] + Z;
        int B = perm[X + 1] + Y, BA = perm[B] + Z, BB = perm[B + 1] + Z;

        return Lerp(w, Lerp(v, Lerp(u, Grad(perm[AA], x, y, z),
                                      Grad(perm[BA], x - 1, y, z)),
                              Lerp(u, Grad(perm[AB], x, y - 1, z),
                                      Grad(perm[BB], x - 1, y - 1, z))),
                      Lerp(v, Lerp(u, Grad(perm[AA + 1], x, y, z - 1),
                                      Grad(perm[BA + 1], x - 1, y, z - 1)),
                              Lerp(u, Grad(perm[AB + 1], x, y - 1, z - 1),
                                      Grad(perm[BB + 1], x - 1, y - 1, z - 1))));
    }
}