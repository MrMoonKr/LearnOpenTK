using System;
using System.Diagnostics;
using System.IO;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using LearnOpenTK.Common;
using OpenTK.WinForms;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Windowing.Common;

namespace LearnOpenTK.RenderQuad;

/// <summary>Draws an indexed quad with an interpolated color at each vertex.</summary>
public class GLView : GLControl
{
    // Each vertex is: x, y, z, r, g, b.
    private static readonly float[] Vertices =
    [
        // The four colors form one continuous color plane, so the diagonal shared
        // by the two triangles has no visible seam.
        -0.70f, -0.70f, 0.0f, 1.0f, 0.0f, 0.0f, // bottom-left: red
         0.70f, -0.70f, 0.0f, 1.0f, 1.0f, 0.0f, // bottom-right: yellow
         0.70f,  0.70f, 0.0f, 1.0f, 1.0f, 1.0f, // top-right: white
        -0.70f,  0.70f, 0.0f, 1.0f, 0.0f, 1.0f, // top-left: magenta
    ];

    private static readonly uint[] Indices = [0, 1, 2, 2, 3, 0];

    private int _vao;
    private int _vbo;
    private int _ebo;
    private Shader? _shader;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private TimeSpan _previousUpdateTime;
    private TimeSpan _previousRenderTime;
    private TimeSpan _fpsSampleStartTime;
    private int _updatesSinceLastSample;
    private int _rendersSinceLastSample;
    private bool _loaded;
    private bool _unloaded;
    private bool _rendering;
    private int _swapInterval;
    private Color _clearColor = Color.FromArgb(31, 52, 79);

    public GLView()
    {
        Dock = DockStyle.Fill;
        TabStop = true;
        Load += HandleLoad;
        Paint += HandlePaint;
        Resize += HandleResize;
        Disposed += HandleDisposed;
    }

    public event EventHandler<string>? StatusChanged;
    public double UpdateFramesPerSecond { get; private set; }
    public double RenderFramesPerSecond { get; private set; }

    public bool VSync
    {
        get => _swapInterval != 0;
        set
        {
            _swapInterval = value ? 1 : 0;
            if (_loaded && Context is not null) Context.SwapInterval = _swapInterval;
        }
    }

    public Color ClearColor
    {
        get => _clearColor;
        set
        {
            _clearColor = value;
            if (_loaded)
            {
                MakeCurrent();
                ApplyClearColor();
                Invalidate();
            }
        }
    }

    protected void ReportStatus(string message) => StatusChanged?.Invoke(this, message);

    protected virtual void OnLoad()
    {
        _vao = GL.GenVertexArray();
        _vbo = GL.GenBuffer();
        _ebo = GL.GenBuffer();

        GL.BindVertexArray(_vao);
        GL.BindBuffer(BufferTarget.ArrayBuffer, _vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, Vertices.Length * sizeof(float), Vertices, BufferUsageHint.StaticDraw);

        GL.BindBuffer(BufferTarget.ElementArrayBuffer, _ebo);
        GL.BufferData(BufferTarget.ElementArrayBuffer, Indices.Length * sizeof(uint), Indices, BufferUsageHint.StaticDraw);

        const int floatsPerVertex = 6;
        var stride = floatsPerVertex * sizeof(float);
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, stride, 0);
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, stride, 3 * sizeof(float));
        GL.EnableVertexAttribArray(1);

        _shader = new Shader(
            Path.Combine(AppContext.BaseDirectory, "Shaders", "shader.vert"),
            Path.Combine(AppContext.BaseDirectory, "Shaders", "shader.frag"));
        ReportStatus("Quad loaded: four vertex colors are interpolated across two triangles.");
    }

    protected virtual void OnRenderFrame(FrameEventArgs e)
    {
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        _shader!.Use();
        GL.BindVertexArray(_vao);
        GL.DrawElements(PrimitiveType.Triangles, Indices.Length, DrawElementsType.UnsignedInt, IntPtr.Zero);
    }

    protected virtual void OnUnload()
    {
        GL.BindVertexArray(0);
        GL.UseProgram(0);
        GL.DeleteBuffer(_ebo);
        GL.DeleteBuffer(_vbo);
        GL.DeleteVertexArray(_vao);
        if (_shader is not null)
        {
            GL.DeleteProgram(_shader.Handle);
        }

    }

    private void HandleLoad(object? sender, EventArgs e)
    {
        MakeCurrent();
        ApplyClearColor();
        if (Context is not null) Context.SwapInterval = _swapInterval;
        GL.Enable(EnableCap.DepthTest);
        _previousUpdateTime = _clock.Elapsed;
        _previousRenderTime = _clock.Elapsed;
        _fpsSampleStartTime = _clock.Elapsed;
        _loaded = true;
        OnLoad();
        Application.Idle += HandleApplicationIdle;
        ReportStatus($"GLView loaded - VSync: {(VSync ? "On" : "Off")}");
    }

    private void HandleApplicationIdle(object? sender, EventArgs e)
    {
        while (_loaded && !IsDisposed && IsApplicationIdle()) RunFrame();
    }

    private void HandlePaint(object? sender, PaintEventArgs e)
    {
        if (_loaded && !IsDisposed) RunFrame();
    }

    private void HandleResize(object? sender, EventArgs e)
    {
        if (!_loaded || ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
        MakeCurrent();
        GL.Viewport(0, 0, ClientSize.Width, ClientSize.Height);
        ReportStatus($"Viewport: {ClientSize.Width} x {ClientSize.Height}");
    }

    private void RunFrame()
    {
        if (_rendering) return;
        _rendering = true;
        try
        {
            var now = _clock.Elapsed;
            var updateElapsed = now - _previousUpdateTime;
            _previousUpdateTime = now;
            _updatesSinceLastSample++;
            OnUpdateFrame(new FrameEventArgs(updateElapsed.TotalSeconds));
            MakeCurrent();
            var renderElapsed = now - _previousRenderTime;
            _previousRenderTime = now;
            _rendersSinceLastSample++;
            OnRenderFrame(new FrameEventArgs(renderElapsed.TotalSeconds));
            SwapBuffers();
            UpdateFrameRates(now);
        }
        finally { _rendering = false; }
    }

    private void UpdateFrameRates(TimeSpan now)
    {
        var elapsed = now - _fpsSampleStartTime;
        if (elapsed.TotalSeconds < 1) return;
        UpdateFramesPerSecond = _updatesSinceLastSample / elapsed.TotalSeconds;
        RenderFramesPerSecond = _rendersSinceLastSample / elapsed.TotalSeconds;
        _updatesSinceLastSample = 0;
        _rendersSinceLastSample = 0;
        _fpsSampleStartTime = now;
    }

    private void HandleDisposed(object? sender, EventArgs e)
    {
        if (_unloaded) return;
        _unloaded = true;
        Application.Idle -= HandleApplicationIdle;
        if (_loaded)
        {
            MakeCurrent();
            OnUnload();
        }
    }

    private void ApplyClearColor() => GL.ClearColor(_clearColor.R / 255f, _clearColor.G / 255f, _clearColor.B / 255f, 1f);
    private static bool IsApplicationIdle() => !PeekMessage(out _, IntPtr.Zero, 0, 0, 0);

    [DllImport("user32.dll")]
    private static extern bool PeekMessage(out NativeMessage message, IntPtr windowHandle, uint minimumMessage, uint maximumMessage, uint removeMessage);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeMessage
    {
        public IntPtr Handle;
        public uint Message;
        public IntPtr WParam;
        public IntPtr LParam;
        public uint Time;
        public Point Point;
    }

    protected virtual void OnUpdateFrame(FrameEventArgs e) { }
}
