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

namespace LearnOpenTK.KeyboardMouse;

public sealed class GLView : GLControl
{
    private static readonly float[] V =
    [
        -.5f, -.5f,  .5f, 1, 0, 0,
         .5f, -.5f,  .5f, 1, 1, 0,
         .5f,  .5f,  .5f, 1, 1, 1,
        -.5f,  .5f,  .5f, 1, 0, 1,
        -.5f, -.5f, -.5f, 0, 0, 1,
         .5f, -.5f, -.5f, 0, 1, 0,
         .5f,  .5f, -.5f, 0, 1, 1,
        -.5f,  .5f, -.5f, 1, 0, 1,
    ];
    private static readonly uint[] I =
    [
        0, 1, 2, 2, 3, 0,
        5, 4, 7, 7, 6, 5,
        4, 0, 3, 3, 7, 4,
        1, 5, 6, 6, 2, 1,
        3, 2, 6, 6, 7, 3,
        4, 5, 1, 1, 0, 4,
    ];
    private readonly OrbitCamera _camera = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private Mesh? _mesh;
    private Shader? _shader;
    private Material? _material;
    private bool _loaded;
    private bool _orbit;
    private bool _pan;
    private Point _last;
    private int _frames;
    private int _swap;
    private TimeSpan _sample;

    public GLView()
    {
        Dock = DockStyle.Fill;
        TabStop = true;
        Load += (_, _) => LoadR();
        Paint += (_, _) => Draw();
        Resize += (_, _) => ResizeR();
        MouseDown += OnMouseDown;
        MouseUp += (_, _) => { _orbit = _pan = false; Cursor = Cursors.Default; };
        MouseMove += OnMouseMove;
        MouseWheel += (_, e) => { _camera.Zoom(e.Delta * .002f); Invalidate(); };
        Disposed += (_, _) => Unload();
    }

    public event EventHandler<string>? StatusChanged;
    public double FramesPerSecond { get; private set; }
    public bool VSync { get => _swap != 0; set { _swap = value ? 1 : 0; if (_loaded && Context is not null) Context.SwapInterval = _swap; } }

    private void LoadR()
    {
        MakeCurrent();
        GL.ClearColor(.12f, .2f, .31f, 1);
        GL.Enable(EnableCap.DepthTest);
        if (Context is not null) Context.SwapInterval = _swap;
        _shader = new Shader(Path.Combine(AppContext.BaseDirectory, "Shaders", "shader.vert"), Path.Combine(AppContext.BaseDirectory, "Shaders", "shader.frag"));
        _material = new Material(_shader);
        _mesh = new Mesh(V, I, 6);
        _loaded = true;
        _sample = _clock.Elapsed;
        ResizeR();
        StatusChanged?.Invoke(this, "Right drag: orbit | Middle drag: pan | Wheel: zoom");
    }

    private void Draw()
    {
        if (!_loaded) return;
        MakeCurrent();
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        _material!.Bind();
        _shader!.SetMatrix4("uModel", Matrix4.CreateRotationY((float)_clock.Elapsed.TotalSeconds * .2f));
        _shader.SetMatrix4("uView", _camera.GetViewMatrix());
        _shader.SetMatrix4("uProjection", _camera.GetProjectionMatrix());
        _mesh!.Draw();
        SwapBuffers();
        _frames++;
        var e = _clock.Elapsed - _sample;
        if (e.TotalSeconds >= 1)
        {
            FramesPerSecond = _frames / e.TotalSeconds;
            _frames = 0;
            _sample = _clock.Elapsed;
        }
        Invalidate();
    }

    private void ResizeR()
    {
        if (!_loaded || ClientSize.Height == 0) return;
        MakeCurrent();
        GL.Viewport(0, 0, ClientSize.Width, ClientSize.Height);
        _camera.AspectRatio = ClientSize.Width / (float)ClientSize.Height;
    }

    private void OnMouseDown(object? s, MouseEventArgs e)
    {
        Focus();
        _last = e.Location;
        _orbit = e.Button == MouseButtons.Right;
        _pan = e.Button == MouseButtons.Middle;
        if (_orbit || _pan) Cursor = Cursors.SizeAll;
    }

    private void OnMouseMove(object? s, MouseEventArgs e)
    {
        var dx = e.X - _last.X;
        var dy = e.Y - _last.Y;
        if (_orbit) _camera.Rotate(dx * .35f, -dy * .35f);
        if (_pan) _camera.Pan(-dx * .01f, dy * .01f);
        _last = e.Location;
    }

    private void Unload()
    {
        if (!_loaded) return;
        MakeCurrent();
        _mesh?.Dispose();
        if (_shader is not null) GL.DeleteProgram(_shader.Handle);
        _loaded = false;
    }
}
