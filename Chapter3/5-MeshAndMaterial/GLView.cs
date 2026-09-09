using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using LearnOpenTK.Common;
using LearnOpenTK.Common.Rendering;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.WinForms;
namespace LearnOpenTK.MeshAndMaterial;

public sealed class GLView : GLControl
{
    // Each vertex contains position (xyz) followed by color (rgb).
    private static readonly float[] Vertices =
    [
        -.55f, -.55f,  .55f, 1f, 0f, 0f,  // front-bottom-left
         .55f, -.55f,  .55f, 1f, 1f, 0f,
         .55f,  .55f,  .55f, 1f, 1f, 1f,
        -.55f,  .55f,  .55f, 1f, 0f, 1f,
        -.55f, -.55f, -.55f, 0f, 0f, 1f,  // back-bottom-left
         .55f, -.55f, -.55f, 0f, 1f, 0f,
         .55f,  .55f, -.55f, 0f, 1f, 1f,
        -.55f,  .55f, -.55f, 1f, 0f, 1f,
    ];
    private static readonly uint[] Indices =
    [
        0, 1, 2, 2, 3, 0, // front
        5, 4, 7, 7, 6, 5, // back
        4, 0, 3, 3, 7, 4, // left
        1, 5, 6, 6, 2, 1, // right
        3, 2, 6, 6, 7, 3, // top
        4, 5, 1, 1, 0, 4, // bottom
    ];
    private readonly Stopwatch _clock = Stopwatch.StartNew(); private Mesh? _mesh; private Material? _material; private Shader? _shader; private Color _clearColor = Color.FromArgb(31, 52, 79); private TimeSpan _sampleStart; private int _frames; private int _swapInterval; private bool _loaded;
    public GLView() { Dock = DockStyle.Fill; Load += (_, _) => LoadResources(); Paint += (_, _) => RenderFrame(); Resize += (_, _) => ResizeViewport(); Disposed += (_, _) => UnloadResources(); }
    public event EventHandler<string>? StatusChanged; public double FramesPerSecond { get; private set; }
    public bool VSync { get => _swapInterval != 0; set { _swapInterval = value ? 1 : 0; if (_loaded && Context is not null) Context.SwapInterval = _swapInterval; } }
    public Color ClearColor { get => _clearColor; set { _clearColor = value; if (_loaded) { MakeCurrent(); ApplyClearColor(); Invalidate(); } } }
    private void LoadResources() { MakeCurrent(); ApplyClearColor(); GL.Enable(EnableCap.DepthTest); if (Context is not null) Context.SwapInterval = _swapInterval; _shader = new Shader(Path.Combine(AppContext.BaseDirectory, "Shaders", "shader.vert"), Path.Combine(AppContext.BaseDirectory, "Shaders", "shader.frag")); _material = new Material(_shader); _mesh = new Mesh(Vertices, Indices, 6); _loaded = true; _sampleStart = _clock.Elapsed; ResizeViewport(); StatusChanged?.Invoke(this, "Loaded: Mesh owns geometry; Material owns shader selection."); }
    private void RenderFrame() { if (!_loaded || IsDisposed) return; MakeCurrent(); GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit); _material!.Bind(); var time = (float)_clock.Elapsed.TotalSeconds; var model = Matrix4.CreateRotationY(time * .8f) * Matrix4.CreateRotationX(time * .5f); _shader!.SetMatrix4("uModel", model); _mesh!.Draw(); SwapBuffers(); _frames++; var elapsed = _clock.Elapsed - _sampleStart; if (elapsed.TotalSeconds >= 1) { FramesPerSecond = _frames / elapsed.TotalSeconds; _frames = 0; _sampleStart = _clock.Elapsed; } Invalidate(); }
    private void ResizeViewport() { if (!_loaded || ClientSize.Width <= 0 || ClientSize.Height <= 0) return; MakeCurrent(); GL.Viewport(0, 0, ClientSize.Width, ClientSize.Height); }
    private void UnloadResources() { if (!_loaded) return; MakeCurrent(); GL.BindVertexArray(0); GL.UseProgram(0); _mesh?.Dispose(); if (_shader is not null) GL.DeleteProgram(_shader.Handle); _loaded = false; }
    private void ApplyClearColor() => GL.ClearColor(_clearColor.R / 255f, _clearColor.G / 255f, _clearColor.B / 255f, 1f);
}
