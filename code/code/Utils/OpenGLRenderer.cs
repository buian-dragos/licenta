using System;
using System.IO;
using System.Collections.Generic;
using code.Models;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;

namespace code.Utils
{
    public class OpenGLRenderer : IRenderer
    {
        private readonly Geometry[] _geometries;
        private readonly Light[] _lights;

        // Full-screen quad data
        private static readonly float[] QuadVerts = {
            -1f, -1f,
             1f, -1f,
             1f,  1f,
            -1f,  1f
        };
        private static readonly uint[] QuadIdxs = { 0, 1, 2, 2, 3, 0 };

        // Vertex shader: 2D quad -> UV
        private const string VertexSrc = @"#version 330 core
layout(location = 0) in vec2 aPos;
out vec2 vUV;
void main()
{
    vUV = aPos * 0.5 + 0.5;
    gl_Position = vec4(aPos, 0.0, 1.0);
}";

        // Fragment shader: volume ray march
        private const string FragmentSrc = @"#version 330 core
in vec2 vUV;
out vec4 FragColor;

uniform mat4 uInvViewProj;
uniform vec3 uCenter;
uniform vec3 uAxes;
uniform float uBaseDensity;
uniform vec4 uCloudColor;
uniform vec3 uLightPos;
uniform float uStep;
uniform float uNear;
uniform float uFar;

// ---- 3D Perlin Noise (ashima) ----
vec4 permute(vec4 x) { return mod(((x*34.0)+1.0)*x, 289.0); }
vec4 taylorInvSqrt(vec4 r) { return 1.79284291400159 - 0.85373472095314 * r; }
float fade(float t) { return t*t*t*(t*(t*6.0-15.0)+10.0); }

float perlinNoise3D(vec3 P) {
    vec3 Pi0 = floor(P);
    vec3 Pi1 = Pi0 + vec3(1.0);
    Pi0 = mod(Pi0, 289.0);
    Pi1 = mod(Pi1, 289.0);
    vec3 Pf0 = fract(P);
    vec3 Pf1 = Pf0 - vec3(1.0);
    vec4 ix = vec4(Pi0.x, Pi1.x, Pi0.x, Pi1.x);
    vec4 iy = vec4(Pi0.y, Pi0.y, Pi1.y, Pi1.y);
    vec4 iz0 = vec4(Pi0.z);
    vec4 iz1 = vec4(Pi1.z);

    vec4 ixy = permute(permute(ix) + iy);
    vec4 ixy0 = permute(ixy + iz0);
    vec4 ixy1 = permute(ixy + iz1);

    vec4 gx0 = ixy0 / 7.0;
    vec4 gy0 = fract(floor(gx0) / 7.0) - 0.5;
    gx0 = fract(gx0);
    vec4 gz0 = vec4(0.5) - abs(gx0) - abs(gy0);
    vec4 sz0 = step(gz0, vec4(0.0));
    gx0 -= sz0 * (step(0.0, gx0) - 0.5);
    gy0 -= sz0 * (step(0.0, gy0) - 0.5);

    vec4 gx1 = ixy1 / 7.0;
    vec4 gy1 = fract(floor(gx1) / 7.0) - 0.5;
    gx1 = fract(gx1);
    vec4 gz1 = vec4(0.5) - abs(gx1) - abs(gy1);
    vec4 sz1 = step(gz1, vec4(0.0));
    gx1 -= sz1 * (step(0.0, gx1) - 0.5);
    gy1 -= sz1 * (step(0.0, gy1) - 0.5);

    vec3 g000 = vec3(gx0.x,gy0.x,gz0.x);
    vec3 g100 = vec3(gx0.y,gy0.y,gz0.y);
    vec3 g010 = vec3(gx0.z,gy0.z,gz0.z);
    vec3 g110 = vec3(gx0.w,gy0.w,gz0.w);
    vec3 g001 = vec3(gx1.x,gy1.x,gz1.x);
    vec3 g101 = vec3(gx1.y,gy1.y,gz1.y);
    vec3 g011 = vec3(gx1.z,gy1.z,gz1.z);
    vec3 g111 = vec3(gx1.w,gy1.w,gz1.w);

    vec4 norm0 = taylorInvSqrt(vec4(dot(g000,g000), dot(g010,g010), dot(g100,g100), dot(g110,g110)));
    g000 *= norm0.x; g010 *= norm0.y; g100 *= norm0.z; g110 *= norm0.w;
    vec4 norm1 = taylorInvSqrt(vec4(dot(g001,g001), dot(g011,g011), dot(g101,g101), dot(g111,g111)));
    g001 *= norm1.x; g011 *= norm1.y; g101 *= norm1.z; g111 *= norm1.w;

    float n000 = dot(g000, Pf0);
    float n100 = dot(g100, vec3(Pf1.x, Pf0.y, Pf0.z));
    float n010 = dot(g010, vec3(Pf0.x, Pf1.y, Pf0.z));
    float n110 = dot(g110, vec3(Pf1.x, Pf1.y, Pf0.z));
    float n001 = dot(g001, vec3(Pf0.x, Pf0.y, Pf1.z));
    float n101 = dot(g101, vec3(Pf1.x, Pf0.y, Pf1.z));
    float n011 = dot(g011, vec3(Pf0.x, Pf1.y, Pf1.z));
    float n111 = dot(g111, Pf1);

    vec3 fade_xyz = vec3(fade(Pf0.x), fade(Pf0.y), fade(Pf0.z));
    float n_z0 = mix(mix(n000, n100, fade_xyz.x), mix(n010, n110, fade_xyz.x), fade_xyz.y);
    float n_z1 = mix(mix(n001, n101, fade_xyz.x), mix(n011, n111, fade_xyz.x), fade_xyz.y);
    return 2.2 * mix(n_z0, n_z1, fade_xyz.z);
}

// fbm helper (fractal brownian motion, multiple noise layers)
float fbm(vec3 p) {
    float sum = 0.0;
    float amp = 0.5;
    float freq = 1.0;
    for(int i = 0; i < 4; ++i) {
        sum += perlinNoise3D(p * freq) * amp;
        freq *= 2.0;
        amp *= 0.5;
    }
    return sum;
}

float boundaryNoise(vec3 lp) {
    return 1.0 + 0.3*fbm(lp*2.0) + 0.15*fbm(lp*4.0);
}
float noisyEllipsoidSDF(vec3 p) {
    vec3 lp = (p - uCenter) / uAxes;
    float r = length(lp);
    float boundary = boundaryNoise(lp);
    float minAxis = min(uAxes.x, min(uAxes.y, uAxes.z));
    return (r - boundary) * minAxis;
}

float findBoundary(vec3 ro, vec3 rd, float tStart, float tEnd, bool findEntry)
{
    float t = tStart;
    for(int i = 0; i < 128; ++i) {
        vec3 p = ro + rd * t;
        float d = noisyEllipsoidSDF(p);
        if(findEntry) {
            if(d < 0.0) return t;
        } else {
            if(d > 0.0) return t;
        }
        t += max(abs(d), 0.002);
        if(t > tEnd) break;
    }
    return tEnd;
}

float densityAt(vec3 p)
{
    vec3 lp = (p - uCenter) / uAxes;
    float r = length(lp);
    float boundary = boundaryNoise(lp);
    const float outMargin = 0.75;
    if (r > boundary + outMargin) return 0.0;
    float envelope = (boundary - r) / outMargin;
    if (envelope <= 0.0) return 0.0;
    if (envelope > 1.0) envelope = 1.0;
    float nLarge = fbm(p * 0.05);
    float nMed   = fbm(p * 0.15);
    float nSmall = fbm(p * 0.30);
    float n = 0.6 + 0.35*nLarge + 0.25*nMed + 0.15*nSmall;
    n = max(0.0, n);
    float fade = pow(max(0.0, 1.0 - r/boundary), 1.5);
    float height = max(0.0, 1.0 - pow(max(0.0, lp.y + 0.3), 2.0));
    float rawDensity = uBaseDensity * height * (n * fade + 0.03 * (1.0 - fade));
    return rawDensity * envelope;
}

float lightTransmittance(vec3 p)
{
    vec3 toL = uLightPos - p;
    float dist = length(toL);
    vec3 ld = normalize(toL);
    float t = 0.0, tau = 0.0;
    while(t < dist)
    {
        tau += densityAt(p + ld * t) * (uStep * 2.0 / 0.4); // also scale here
        if(tau > 10.0) return 0.0;
        t += uStep * 2.0;
    }
    return exp(-tau);
}

void main()
{
    vec4 clip0 = vec4(vUV*2.0 - 1.0, -1.0, 1.0);
    vec4 ndc = uInvViewProj * clip0; ndc /= ndc.w;
    vec3 ro = ndc.xyz;
    vec4 clip1 = vec4(vUV*2.0 - 1.0, 1.0, 1.0);
    vec4 ndcFar = uInvViewProj * clip1; ndcFar /= ndcFar.w;
    vec3 rd = normalize(ndcFar.xyz - ro);

    float tEntry = findBoundary(ro, rd, uNear, uFar, true);
    float tExit = findBoundary(ro, rd, tEntry + uStep, uFar, false);
    if(tEntry >= tExit) discard;

    vec3 accum = vec3(0.0);
    float Tview = 1.0;
    const float kRefStep = 0.4;
    for(float t = tEntry; t < tExit; t += uStep)
    {
        vec3 p = ro + rd * t;
        float dens = densityAt(p);
        float tau = dens * uStep / kRefStep;
        float absorb = exp(-tau);
        float scatter = 1.0 - absorb;
        float Tlight = lightTransmittance(p);
        accum += uCloudColor.rgb * scatter * Tview * Tlight;
        Tview *= absorb;
        if(Tview < 0.02) break;
    }
    FragColor = vec4(accum, 1.0 - Tview);
}

";

        public OpenGLRenderer(Geometry[] geoms, Light[] lights)
        {
            _geometries = geoms;
            _lights = lights;
        }

        // Compile shader source
        private static int CompileShader(string src, ShaderType type)
        {
            int id = GL.CreateShader(type);
            GL.ShaderSource(id, src);
            GL.CompileShader(id);
            GL.GetShader(id, ShaderParameter.CompileStatus, out int success);
            if(success == 0)
                throw new Exception($"Shader compile error ({type}): {GL.GetShaderInfoLog(id)}");
            return id;
        }

        // Link program
        private static int LinkProgram(int vertId, int fragId)
        {
            int program = GL.CreateProgram();
            GL.AttachShader(program, vertId);
            GL.AttachShader(program, fragId);
            GL.LinkProgram(program);
            GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int success);
            if(success == 0)
                throw new Exception($"Program link error: {GL.GetProgramInfoLog(program)}");
            GL.DeleteShader(vertId);
            GL.DeleteShader(fragId);
            return program;
        }

        public void Render(Camera camera, int width, int height, string filename)
        {
            // create hidden window/context
            var settings = new NativeWindowSettings {
                Size = new Vector2i(width, height),
                StartVisible = false,
                APIVersion = new Version(4,1),
                Profile = ContextProfile.Core
            };
            using var win = new NativeWindow(settings);
            win.MakeCurrent();
            GL.Viewport(0,0,width,height);

            // compile & link
            int vertId = CompileShader(VertexSrc, ShaderType.VertexShader);
            int fragId = CompileShader(FragmentSrc, ShaderType.FragmentShader);
            int programId = LinkProgram(vertId, fragId);
            GL.UseProgram(programId);

            // quad VAO/VBO/EBO
            int vao = GL.GenVertexArray();
            int vbo = GL.GenBuffer();
            int ebo = GL.GenBuffer();
            GL.BindVertexArray(vao);
            GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
            GL.BufferData(BufferTarget.ArrayBuffer, QuadVerts.Length * sizeof(float), QuadVerts, BufferUsageHint.StaticDraw);
            GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);
            GL.EnableVertexAttribArray(0);
            GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
            GL.BufferData(BufferTarget.ElementArrayBuffer, QuadIdxs.Length * sizeof(uint), QuadIdxs, BufferUsageHint.StaticDraw);
            GL.BindVertexArray(0);

            // camera matrices
            camera.Normalize();
            var view = Matrix4.LookAt(
                new Vector3(camera.Position.X, camera.Position.Y, camera.Position.Z),
                new Vector3(camera.Position.X + camera.Direction.X,
                            camera.Position.Y + camera.Direction.Y,
                            camera.Position.Z + camera.Direction.Z),
                new Vector3(camera.Up.X, camera.Up.Y, camera.Up.Z)
            );
            float aspect = width / (float)height;
            float fovRad = MathHelper.DegreesToRadians(camera.ViewPlaneDistance);
            var proj = Matrix4.CreatePerspectiveFieldOfView(fovRad, aspect, camera.FrontPlaneDistance, camera.BackPlaneDistance);
            Matrix4 invViewProj;
            Matrix4.Invert(view * proj, out invViewProj);

            // set uniforms (first cloud only)
            if(_geometries.Length > 0 && _geometries[0] is SingleScatterCloud ssc)
            {
                GL.UniformMatrix4(GL.GetUniformLocation(programId, "uInvViewProj"), false, ref invViewProj);
                GL.Uniform3(GL.GetUniformLocation(programId, "uCenter"), new Vector3(ssc.Center.X, ssc.Center.Y, ssc.Center.Z));
                GL.Uniform3(GL.GetUniformLocation(programId, "uAxes"),   new Vector3(ssc.Axes.X, ssc.Axes.Y, ssc.Axes.Z));
                GL.Uniform1(GL.GetUniformLocation(programId, "uBaseDensity"), ssc.BaseDensity);
                GL.Uniform4(GL.GetUniformLocation(programId, "uCloudColor"), new Vector4(ssc.CloudColor.Red, ssc.CloudColor.Green, ssc.CloudColor.Blue, ssc.CloudColor.Alpha));
            }
            // light
            System.Numerics.Vector3 sysLp = _lights.Length > 0
                ? _lights[0].Position
                : new System.Numerics.Vector3();
            var lp = new OpenTK.Mathematics.Vector3(sysLp.X, sysLp.Y, sysLp.Z);

// then upload to the shader
            GL.Uniform3(
                GL.GetUniformLocation(programId, "uLightPos"),
                lp
            );
            // step & planes
            float step = 0.4f;
            if (_geometries.Length > 0 && _geometries[0] is SingleScatterCloud sc)
            {
                // Choose quality factor: smaller = more detail, bigger = faster but chunkier
                const float qualityDivisor = 8.0f; // tweak 6-10 to taste

                var axes = sc.Axes;
                step = Math.Min(axes.X, Math.Min(axes.Y, axes.Z));
                step = Math.Clamp(step, 0.2f, 20.0f); // avoid crazy small/large steps
                Console.WriteLine(step);
            }

            GL.Uniform1(GL.GetUniformLocation(programId, "uStep"), step);

            GL.Uniform1(GL.GetUniformLocation(programId, "uStep"), step);
            GL.Uniform1(GL.GetUniformLocation(programId, "uNear"), camera.FrontPlaneDistance);
            GL.Uniform1(GL.GetUniformLocation(programId, "uFar"), camera.BackPlaneDistance);

            // draw
            GL.Disable(EnableCap.DepthTest);
            GL.ClearColor(0.1f, 0.1f, 0.1f, 1f);
            GL.Clear(ClearBufferMask.ColorBufferBit);
            GL.UseProgram(programId);
            GL.BindVertexArray(vao);
            GL.DrawElements(PrimitiveType.Triangles, QuadIdxs.Length, DrawElementsType.UnsignedInt, 0);

            // read to PNG
            GL.Finish();
            GL.ReadBuffer(ReadBufferMode.Back);
            GL.PixelStore(PixelStoreParameter.PackAlignment,1);
            using var outFs = File.OpenWrite(filename);
            using var bmp = new SkiaSharp.SKBitmap(width, height, SkiaSharp.SKColorType.Rgba8888, SkiaSharp.SKAlphaType.Premul);
            GL.ReadPixels(0,0,width,height, PixelFormat.Rgba, PixelType.UnsignedByte, bmp.GetPixels());
            using var img = SkiaSharp.SKImage.FromBitmap(bmp);
            using var pngData = img.Encode(SkiaSharp.SKEncodedImageFormat.Png,100);
            pngData.SaveTo(outFs);

            // cleanup
            GL.DeleteBuffer(vbo);
            GL.DeleteBuffer(ebo);
            GL.DeleteVertexArray(vao);
            GL.DeleteProgram(programId);
        }
    }
}
