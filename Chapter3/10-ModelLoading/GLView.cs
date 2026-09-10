using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using LearnOpenTK.Common;
using LearnOpenTK.Common.Dota2;
using LearnOpenTK.Common.Graphics;
using LearnOpenTK.Common.Rendering;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.WinForms;

namespace LearnOpenTK.ModelLoading;

/// <summary>One loaded .vmdl_c, already converted to drawable parts: a GPU mesh plus its albedo texture per material.</summary>
public sealed record LoadedModel(string Name, IReadOnlyList<(Mesh Mesh, Texture2D? Texture)> Parts);

public sealed class GLView : GLControl
{
    private static readonly VertexAttribute[] VertexLayout =
    [
        new VertexAttribute(Location: 0, ComponentCount: 3, OffsetFloats: SubMesh.PositionOffset),
        new VertexAttribute(Location: 1, ComponentCount: 3, OffsetFloats: SubMesh.NormalOffset),
        new VertexAttribute(Location: 2, ComponentCount: 2, OffsetFloats: SubMesh.UvOffset),
    ];

    /// <summary>
    /// Source 2 (ValveResourceFormat) meshes are authored right-handed Z-up: +X forward, +Y left, +Z up.
    /// This repository's cameras and lighting assume Y-up, so every model is rotated -90 degrees about
    /// X once here (source Z becomes our Y, source -Y becomes our Z) rather than converting each vertex,
    /// bone, and animation sample at import time. Skinning happens in the source space before this
    /// matrix is applied, so it stays correct without touching Common.Dota2 or Common.Animation.
    /// </summary>
    private static readonly Matrix4 SourceToWorldUp = new(
        new Vector4(1, 0, 0, 0),
        new Vector4(0, 0, -1, 0),
        new Vector4(0, 1, 0, 0),
        new Vector4(0, 0, 0, 1));

    private readonly OrbitCamera _camera = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly List<LoadedModel> _models = [];
    private Shader? _shader;
    private Material? _material;
    private bool _loaded;
    private bool _orbit;
    private bool _pan;
    private Point _last;
    private int _swap;
    private int _frames;
    private TimeSpan _sample;

    public GLView()
    {
        Dock = DockStyle.Fill;
        TabStop = true;
        Load += (_, _) => LoadScene();
        Paint += (_, _) => RenderScene();
        Resize += (_, _) => ResizeViewport();
        MouseDown += HandleMouseDown;
        MouseUp += (_, _) => { _orbit = _pan = false; Cursor = Cursors.Default; };
        MouseMove += HandleMouseMove;
        MouseWheel += (_, e) => { _camera.Zoom(e.Delta * .1f); Invalidate(); };
        Disposed += (_, _) => UnloadScene();
    }

    public event EventHandler<string>? StatusChanged;
    public IReadOnlyList<string> ModelNames => _models.Select(m => m.Name).ToList();
    public double FramesPerSecond { get; private set; }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool VSync { get => _swap != 0; set { _swap = value ? 1 : 0; if (_loaded && Context is not null) Context.SwapInterval = _swap; } }

    private void LoadScene()
    {
        MakeCurrent();
        GL.ClearColor(.08f, .1f, .13f, 1);
        GL.Enable(EnableCap.DepthTest);
        if (Context is not null) Context.SwapInterval = _swap;
        _shader = new Shader(Path.Combine(AppContext.BaseDirectory, "Shaders", "shader.vert"), Path.Combine(AppContext.BaseDirectory, "Shaders", "shader.frag"));
        _material = new Material(_shader);

        try
        {
            var (min, max) = LoadHeroAndDefaultWearables();
            // Bounds are measured in Source's Z-up space; rotate the center the same way uModel rotates the geometry.
            var center = Vector3.TransformPosition((min + max) / 2f, SourceToWorldUp);
            var radius = MathF.Max((max - min).Length / 2f, 1f);
            _camera.MaxDistance = MathF.Max(radius * 6f, 30f);
            _camera.FarPlane = MathF.Max(radius * 10f, 100f);
            _camera.SetView(center, radius * 2.2f);
            StatusChanged?.Invoke(this, $"Loaded {_models.Count} model(s), {_models.Sum(m => m.Parts.Count)} draw call(s) | Right: orbit | Middle: pan | Wheel: zoom");
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, $"Could not load Dota 2 model: {ex.Message}");
        }

        _loaded = true;
        _sample = _clock.Elapsed;
        ResizeViewport();
    }

    /// <summary>Loads the configured hero body plus its default (non-persona) wearables, returning the bounds of every vertex loaded.</summary>
    private (Vector3 Min, Vector3 Max) LoadHeroAndDefaultWearables()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "config.ini");
        var config = Dota2Config.Load(configPath);
        using var archive = GameArchive.Open(config.GameRoot);

        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);

        void AddModel(string name, ModelAsset asset)
        {
            _models.Add(BuildLoadedModel(name, asset));
            foreach (var subMesh in asset.SubMeshes)
            {
                for (var i = 0; i < subMesh.Vertices.Length; i += SubMesh.FloatsPerVertex)
                {
                    var position = new Vector3(subMesh.Vertices[i], subMesh.Vertices[i + 1], subMesh.Vertices[i + 2]);
                    min = Vector3.ComponentMin(min, position);
                    max = Vector3.ComponentMax(max, position);
                }
            }
        }

        var body = ModelAsset.Load(archive, config.ModelPath);
        AddModel(Path.GetFileNameWithoutExtension(config.ModelPath), body);

        var heroName = HeroLoadout.HeroNpcNameFromModelPath(config.ModelPath);
        if (heroName is not null)
        {
            foreach (var item in HeroLoadout.LoadDefaultLoadout(archive, heroName))
            {
                AddModel(item.Name, ModelAsset.Load(archive, item.ModelPlayerPath));
            }
        }

        return (min, max);
    }

    private static LoadedModel BuildLoadedModel(string name, ModelAsset asset)
    {
        var parts = new List<(Mesh Mesh, Texture2D? Texture)>();
        foreach (var subMesh in asset.SubMeshes)
        {
            var mesh = new Mesh(subMesh.Vertices, subMesh.Indices, SubMesh.FloatsPerVertex, VertexLayout);
            var texture = subMesh.Albedo is not null ? new Texture2D(subMesh.Albedo.Width, subMesh.Albedo.Height, subMesh.Albedo.PixelsBgra) : null;
            parts.Add((mesh, texture));
        }

        return new LoadedModel(name, parts);
    }

    private void RenderScene()
    {
        if (!_loaded) return;
        MakeCurrent();
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        if (_models.Count > 0)
        {
            _material!.Bind();
            _shader!.SetMatrix4("uModel", SourceToWorldUp);
            _shader.SetMatrix4("uView", _camera.GetViewMatrix());
            _shader.SetMatrix4("uProjection", _camera.GetProjectionMatrix());
            _shader.SetVector3("uCameraPosition", _camera.Position);
            _shader.SetVector3("uSunDirection", Vector3.Normalize(new(-1, -1, -.5f)));
            _shader.SetVector3("uFillPosition", _camera.Target + new Vector3(-100, 50, 100));
            _shader.SetVector3("uRimPosition", _camera.Target + new Vector3(100, 80, -100));

            foreach (var model in _models)
            {
                foreach (var (mesh, texture) in model.Parts)
                {
                    texture?.Bind();
                    mesh.Draw();
                }
            }
        }

        SwapBuffers();
        _frames++;
        var elapsed = _clock.Elapsed - _sample;
        if (elapsed.TotalSeconds >= 1)
        {
            FramesPerSecond = _frames / elapsed.TotalSeconds;
            _frames = 0;
            _sample = _clock.Elapsed;
        }
        Invalidate();
    }

    private void HandleMouseDown(object? _, MouseEventArgs e)
    {
        Focus();
        _last = e.Location;
        _orbit = e.Button == MouseButtons.Right;
        _pan = e.Button == MouseButtons.Middle;
        if (_orbit || _pan) Cursor = Cursors.SizeAll;
    }

    private void HandleMouseMove(object? _, MouseEventArgs e)
    {
        var dx = e.X - _last.X;
        var dy = e.Y - _last.Y;
        if (_orbit) _camera.Rotate(dx * .35f, -dy * .35f);
        if (_pan) _camera.Pan(-dx * .2f, dy * .2f);
        _last = e.Location;
    }

    private void ResizeViewport()
    {
        if (!_loaded || ClientSize.Height == 0) return;
        MakeCurrent();
        GL.Viewport(0, 0, ClientSize.Width, ClientSize.Height);
        _camera.AspectRatio = ClientSize.Width / (float)ClientSize.Height;
    }

    private void UnloadScene()
    {
        if (!_loaded) return;
        MakeCurrent();
        foreach (var model in _models)
        {
            foreach (var (mesh, texture) in model.Parts)
            {
                mesh.Dispose();
                texture?.Dispose();
            }
        }
        if (_shader is not null) GL.DeleteProgram(_shader.Handle);
        _loaded = false;
    }
}
