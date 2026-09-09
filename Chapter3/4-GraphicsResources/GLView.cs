using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using LearnOpenTK.Common;
using LearnOpenTK.Common.Graphics;
using OpenTK.Graphics.OpenGL4;
using OpenTK.WinForms;

namespace LearnOpenTK.GraphicsResources;

/// <summary>Renders an indexed quad using Common.Graphics resource owners.</summary>
public sealed class GLView : GLControl
{
    private static readonly float[] Vertices =
    [
        -0.70f, -0.70f, 0f, 1f, 0f, 0f,
         0.70f, -0.70f, 0f, 1f, 1f, 0f,
         0.70f,  0.70f, 0f, 1f, 1f, 1f,
        -0.70f,  0.70f, 0f, 1f, 0f, 1f,
    ];
    private static readonly uint[] Indices = [0, 1, 2, 2, 3, 0];
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private VertexBuffer? _vertexBuffer;
    private IndexBuffer? _indexBuffer;
    private VertexArray? _vertexArray;
    private Shader? _shader;
    private Color _clearColor = Color.FromArgb(31, 52, 79);
    private TimeSpan _sampleStart;
    private int _frames;
    private int _swapInterval;
    private bool _loaded;

    public GLView()
    {
        Dock = DockStyle.Fill;
        Load += (_, _) => LoadResources();
        Paint += (_, _) => RenderFrame();
        Resize += (_, _) => ResizeViewport();
        Disposed += (_, _) => UnloadResources();
    }

    public event EventHandler<string>? StatusChanged;
    public double FramesPerSecond { get; private set; }
    public bool VSync { get => _swapInterval != 0; set { _swapInterval = value ? 1 : 0; if (_loaded && Context is not null) Context.SwapInterval = _swapInterval; } }
    public Color ClearColor { get => _clearColor; set { _clearColor = value; if (_loaded) { MakeCurrent(); ApplyClearColor(); Invalidate(); } } }

    private void LoadResources()
    {
        MakeCurrent();
        ApplyClearColor();
        GL.Enable(EnableCap.DepthTest);
        if (Context is not null) Context.SwapInterval = _swapInterval;
        OnLoad();
        _loaded = true;
        _sampleStart = _clock.Elapsed;
        ResizeViewport();
        StatusChanged?.Invoke(this, "Loaded: VAO, VBO, EBO, and shader program created.");
    }

    private void OnLoad()
    {
        _vertexArray = new VertexArray();
        _vertexArray.Bind();
        _vertexBuffer = new VertexBuffer(Vertices);
        _indexBuffer = new IndexBuffer(Indices);
        const int stride = 6 * sizeof(float);
        _vertexArray.SetFloatAttribute(0, 3, stride, 0);
        _vertexArray.SetFloatAttribute(1, 3, stride, 3 * sizeof(float));
        _shader = new Shader(Path.Combine(AppContext.BaseDirectory, "Shaders", "shader.vert"), Path.Combine(AppContext.BaseDirectory, "Shaders", "shader.frag"));
    }

    private void RenderFrame()
    {
        if (!_loaded || IsDisposed) return;
        MakeCurrent();
        OnRenderFrame();
        SwapBuffers();
        _frames++;
        var elapsed = _clock.Elapsed - _sampleStart;
        if (elapsed.TotalSeconds >= 1)
        {
            FramesPerSecond = _frames / elapsed.TotalSeconds;
            _frames = 0;
            _sampleStart = _clock.Elapsed;
        }
        Invalidate();
    }

    private void OnRenderFrame()
    {
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);
        _shader!.Use();
        _vertexArray!.Bind();
        GL.DrawElements(PrimitiveType.Triangles, Indices.Length, DrawElementsType.UnsignedInt, IntPtr.Zero);
    }

    private void ResizeViewport()
    {
        if (!_loaded || ClientSize.Width <= 0 || ClientSize.Height <= 0) return;
        MakeCurrent();
        GL.Viewport(0, 0, ClientSize.Width, ClientSize.Height);
    }

    private void UnloadResources()
    {
        if (!_loaded) return;
        MakeCurrent();
        OnUnload();
        _loaded = false;
    }

    private void OnUnload()
    {
        GL.BindVertexArray(0);
        GL.UseProgram(0);
        _indexBuffer?.Dispose();
        _vertexBuffer?.Dispose();
        _vertexArray?.Dispose();
        if (_shader is not null) GL.DeleteProgram(_shader.Handle);
        StatusChanged?.Invoke(this, "Unloaded: GPU resources released.");
    }

    private void ApplyClearColor() => GL.ClearColor(_clearColor.R / 255f, _clearColor.G / 255f, _clearColor.B / 255f, 1f);
}
