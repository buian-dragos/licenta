using System;
using System.IO;
using System.Linq;
using code.Models;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using SkiaSharp;

namespace code.Utils
{
    public class OpenGLRenderer : IRenderer
    {
        private readonly Geometry[] _geometries;
        private readonly Light[] _lights;

        // Full-screen quad data
        private static readonly float[] QuadVerts = { -1f, -1f, 1f, -1f, 1f, 1f, -1f, 1f };
        private static readonly uint[] QuadIdxs = { 0, 1, 2, 2, 3, 0 };

        #region GLSL Shaders
        private const string VertexSrc = @"#version 330 core
layout(location = 0) in vec2 aPos;
out vec2 vUV;
void main()
{
    vUV = aPos * 0.5 + 0.5;
    gl_Position = vec4(aPos, 0.0, 1.0);
}";

        private const string FragmentSrc = @"#version 330 core
in vec2 vUV;
out vec4 FragColor;

// --- Uniforms from C# ---
uniform mat4 uInvViewProj;
uniform vec3 uCameraPos;
uniform vec3 uCenter;
uniform vec3 uAxes;
uniform float uBaseDensity;
uniform vec4 uCloudColor;
uniform vec3 uLightPos;
uniform float uStep;
uniform float uNear;
uniform float uFar;

// --- Perlin Noise (Standard Ashima implementation) ---
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

// --- Cloud Modeling Functions ---
bool ellipsoidRayHit(vec3 ro, vec3 rd, float minDist, float maxDist, out float tEnter, out float tExit) {
    vec3 invA = 1.0 / uAxes;
    vec3 oc = (ro - uCenter) * invA;
    vec3 rd_s = rd * invA;
    float a = dot(rd_s, rd_s);
    float b = 2.0 * dot(oc, rd_s);
    float c = dot(oc, oc) - 1.0;
    float disc = b*b - 4.0*a*c;
    if (disc < 0.0) return false;
    float s = sqrt(disc);
    float t0 = (-b - s) / (2.0 * a);
    float t1 = (-b + s) / (2.0 * a);
    if (t1 < minDist || t0 > maxDist) return false;
    tEnter = max(t0, minDist);
    tExit = min(t1, maxDist);
    return tExit > tEnter;
}

float densityAt(vec3 p) {
    vec3 lp = (p - uCenter) / uAxes;
    float r = length(lp);
    const float outMargin = 0.75;
    if (r > 1.0 + outMargin) return 0.0;
    float boundary = 1.0 + 0.3*perlinNoise3D(lp*2.0) + 0.15*perlinNoise3D(lp*4.0);
    float envelope = (boundary - r) / outMargin;
    if (envelope <= 0.0) return 0.0;
    envelope = min(envelope, 1.0);
    float nLarge = perlinNoise3D(p * 0.05);
    float nMed   = perlinNoise3D(p * 0.15);
    float nSmall = perlinNoise3D(p * 0.30);
    float n = 0.6 + 0.35*nLarge + 0.25*nMed + 0.15*nSmall;
    n = max(0.0, n);
    float fade = pow(max(0.0, 1.0 - r/boundary), 1.5);
    float height = max(0.0, 1.0 - pow(max(0.0, lp.y + 0.3), 2.0));
    const float minBase = 0.03;
    float rawDensity = uBaseDensity * height * (n * fade + minBase * (1.0 - fade));
    return rawDensity * envelope;
}

float lightTransmittance(vec3 p) {
    vec3 toL = uLightPos - p;
    float dist = length(toL);
    vec3 ld = normalize(toL);
    float t = 0.0, tau = 0.0;
    float lightStep = uStep * 2.0;
    while (t < dist) {
        tau += densityAt(p + ld * t) * lightStep;
        if (tau > 10.0) return 0.0;
        t += lightStep;
    }
    return exp(-tau);
}

// --- Main Ray Marching Function ---
void main()
{
    vec4 clip = vec4(vUV * 2.0 - 1.0, 1.0, 1.0);
    vec4 world = uInvViewProj * clip;
    world /= world.w;
    vec3 rd = normalize(world.xyz - uCameraPos);
    vec3 ro = uCameraPos;

    float tEntry, tExit;
    if (!ellipsoidRayHit(ro, rd, uNear, uFar, tEntry, tExit)) {
        discard;
    }

    vec3 accum = vec3(0.0);
    float Tview = 1.0;

    for (float t = tEntry; t < tExit; t += uStep) {
        vec3 p = ro + rd * t;
        float dens = densityAt(p);
        if (dens <= 0.0) continue;
        float tau = dens * uStep;
        float absorb = exp(-tau);
        float scatter = 1.0 - absorb;
        float Tlight = lightTransmittance(p);
        accum += uCloudColor.rgb * scatter * Tview * Tlight;
        Tview *= absorb;
        if (Tview < 0.02) break;
    }
    
    vec3 bgColor = vec3(0.05, 0.05, 0.05);
    vec3 finalColor = bgColor * Tview + accum;
    float alpha = 1.0 - Tview;
    
    // Output the pre-multiplied color and the final alpha.
    FragColor = vec4(finalColor, alpha);
}";
        #endregion

        public OpenGLRenderer(Geometry[] geoms, Light[] lights)
        {
            _geometries = geoms;
            _lights = lights;
        }

        private static int CompileShader(string src, ShaderType type)
        {
            int id = GL.CreateShader(type);
            GL.ShaderSource(id, src);
            GL.CompileShader(id);
            GL.GetShader(id, ShaderParameter.CompileStatus, out int success);
            if (success == 0)
                throw new Exception($"Shader compile error ({type}): {GL.GetShaderInfoLog(id)}");
            return id;
        }

        private static int LinkProgram(int vertId, int fragId)
        {
            int program = GL.CreateProgram();
            GL.AttachShader(program, vertId);
            GL.AttachShader(program, fragId);
            GL.LinkProgram(program);
            GL.GetProgram(program, GetProgramParameterName.LinkStatus, out int success);
            if (success == 0)
                throw new Exception($"Program link error: {GL.GetProgramInfoLog(program)}");
            GL.DeleteShader(vertId);
            GL.DeleteShader(fragId);
            return program;
        }

        public void Render(Camera camera, int width, int height, string filename)
        {
            var settings = new NativeWindowSettings
            {
                Size = new Vector2i(width, height),
                StartVisible = false,
                APIVersion = new Version(4, 1),
                Profile = ContextProfile.Core,
                Flags = ContextFlags.Offscreen // Essential for headless rendering
            };
            using var win = new NativeWindow(settings);
            win.MakeCurrent();
            GL.Viewport(0, 0, width, height);

            int programId = 0, vao = 0, vbo = 0, ebo = 0;
            try
            {
                int vertId = CompileShader(VertexSrc, ShaderType.VertexShader);
                int fragId = CompileShader(FragmentSrc, ShaderType.FragmentShader);
                programId = LinkProgram(vertId, fragId);

                vao = GL.GenVertexArray();
                vbo = GL.GenBuffer();
                ebo = GL.GenBuffer();
                GL.BindVertexArray(vao);
                GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
                GL.BufferData(BufferTarget.ArrayBuffer, QuadVerts.Length * sizeof(float), QuadVerts, BufferUsageHint.StaticDraw);
                GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);
                GL.EnableVertexAttribArray(0);
                GL.BindBuffer(BufferTarget.ElementArrayBuffer, ebo);
                GL.BufferData(BufferTarget.ElementArrayBuffer, QuadIdxs.Length * sizeof(uint), QuadIdxs, BufferUsageHint.StaticDraw);

                camera.Normalize();
                var view = Matrix4.LookAt(
                    (OpenTK.Mathematics.Vector3)camera.Position,
                    (OpenTK.Mathematics.Vector3)(camera.Position + camera.Direction),
                    (OpenTK.Mathematics.Vector3)camera.Up
                );
                float aspect = width / (float)height;
                float fovRad = MathHelper.DegreesToRadians(camera.ViewPlaneDistance);
                var proj = Matrix4.CreatePerspectiveFieldOfView(fovRad, aspect, camera.FrontPlaneDistance, camera.BackPlaneDistance);
                Matrix4.Invert(view * proj, out Matrix4 invViewProj);
                
                var ssc = _geometries.OfType<SingleScatterCloud>().FirstOrDefault();
                if (ssc == null) throw new InvalidOperationException("OpenGLRenderer requires a SingleScatterCloud geometry.");

                GL.UseProgram(programId);
                GL.UniformMatrix4(GL.GetUniformLocation(programId, "uInvViewProj"), false, ref invViewProj);
                GL.Uniform3(GL.GetUniformLocation(programId, "uCameraPos"), (OpenTK.Mathematics.Vector3)camera.Position);
                GL.Uniform3(GL.GetUniformLocation(programId, "uCenter"), (OpenTK.Mathematics.Vector3)ssc.Center);
                GL.Uniform3(GL.GetUniformLocation(programId, "uAxes"), (OpenTK.Mathematics.Vector3)ssc.Axes);
                GL.Uniform1(GL.GetUniformLocation(programId, "uBaseDensity"), ssc.BaseDensity);
                GL.Uniform4(GL.GetUniformLocation(programId, "uCloudColor"), new Color4(ssc.CloudColor.Red, ssc.CloudColor.Green, ssc.CloudColor.Blue, ssc.CloudColor.Alpha));
                
                var lightPos = _lights.Length > 0 ? _lights[0].Position : new System.Numerics.Vector3(50, 80, 100);
                GL.Uniform3(GL.GetUniformLocation(programId, "uLightPos"), (OpenTK.Mathematics.Vector3)lightPos);

                GL.Uniform1(GL.GetUniformLocation(programId, "uStep"), ssc.Step);
                GL.Uniform1(GL.GetUniformLocation(programId, "uNear"), camera.FrontPlaneDistance);
                GL.Uniform1(GL.GetUniformLocation(programId, "uFar"), camera.BackPlaneDistance);

                // --- Draw and Read ---
                GL.Disable(EnableCap.DepthTest);
                GL.ClearColor(0.0f, 0.0f, 0.0f, 0.0f); // Clear to transparent black
                GL.Clear(ClearBufferMask.ColorBufferBit);
                
                // Set up blending for transparent objects
                GL.Enable(EnableCap.Blend);
                GL.BlendFunc(BlendingFactor.One, BlendingFactor.OneMinusSrcAlpha); // Correct for pre-multiplied alpha

                GL.BindVertexArray(vao);
                GL.DrawElements(PrimitiveType.Triangles, QuadIdxs.Length, DrawElementsType.UnsignedInt, 0);

                GL.Finish();
                using var bmp = new SKBitmap(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
                
                // *** CRITICAL FIX: Read from the BACK buffer, which is where we drew. ***
                GL.ReadBuffer(ReadBufferMode.Back);
                GL.ReadPixels(0, 0, width, height, PixelFormat.Rgba, PixelType.UnsignedByte, bmp.GetPixels());
                
                using var image = SKImage.FromBitmap(bmp);
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                using var stream = File.OpenWrite(filename);
                data.SaveTo(stream);
            }
            finally
            {
                GL.DeleteBuffer(vbo);
                GL.DeleteBuffer(ebo);
                GL.DeleteVertexArray(vao);
                if (programId != 0) GL.DeleteProgram(programId);
            }
        }
    }
}