using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using LearnOpenTK.Common;
using LearnOpenTK.Common.Animation;
using LearnOpenTK.Common.Dota2;
using LearnOpenTK.Common.Graphics;
using LearnOpenTK.Common.Rendering;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.WinForms;

namespace LearnOpenTK.SkeletalAnimation;

/// <summary>One drawable draw call of a loaded model: its bind-pose vertices (for CPU skinning), the GPU mesh that gets re-uploaded every frame, and a reusable scratch buffer for the skinned result.</summary>
public sealed record ModelPart(float[] BindVertices, Mesh Mesh, Texture2D? Texture, float[] SkinnedScratch);

/// <summary>One loaded .vmdl_c: its draw calls plus, if it has a skeleton, the animator driving its pose.</summary>
public sealed record LoadedModel(string Name, IReadOnlyList<ModelPart> Parts, Skeleton? Skeleton, Animator? Animator);

public sealed class GLView : GLControl
{
    private const int OutputFloatsPerVertex = 8;

    private static readonly VertexAttribute[] VertexLayout =
    [
        new VertexAttribute(Location: 0, ComponentCount: 3, OffsetFloats: 0),
        new VertexAttribute(Location: 1, ComponentCount: 3, OffsetFloats: 3),
        new VertexAttribute(Location: 2, ComponentCount: 2, OffsetFloats: 6),
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
    private TimeSpan _lastUpdate;
    private IReadOnlyDictionary<string, AnimationClip>? _bodyAnimations;

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
    public IReadOnlyList<string> AnimationNames { get; private set; } = [];
    public double FramesPerSecond { get; private set; }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool VSync { get => _swap != 0; set { _swap = value ? 1 : 0; if (_loaded && Context is not null) Context.SwapInterval = _swap; } }

    public void PlayClip(string clipName)
    {
        var body = _models.FirstOrDefault();
        if (body?.Animator is null || _bodyAnimations is null) return;
        if (_bodyAnimations.TryGetValue(clipName, out var clip)) body.Animator.Play(clip);
    }

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
            StatusChanged?.Invoke(this, $"Loaded {_models.Count} model(s) | Right: orbit | Middle: pan | Wheel: zoom");
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, $"Could not load Dota 2 model: {ex.Message}");
        }

        _loaded = true;
        _sample = _clock.Elapsed;
        _lastUpdate = _clock.Elapsed;
        ResizeViewport();
    }

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
        _bodyAnimations = body.Animations;
        AnimationNames = body.Animations.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();

        var startClipName = config.AnimationClip is not null && body.Animations.ContainsKey(config.AnimationClip)
            ? config.AnimationClip
            : body.Animations.Keys.FirstOrDefault();
        if (startClipName is not null) _models[0].Animator?.Play(body.Animations[startClipName]);

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
        var parts = new List<ModelPart>();
        foreach (var subMesh in asset.SubMeshes)
        {
            var vertexCount = subMesh.Vertices.Length / SubMesh.FloatsPerVertex;
            var initialVertices = new float[vertexCount * OutputFloatsPerVertex];
            CopyBindPoseToOutput(subMesh.Vertices, initialVertices);

            var mesh = new Mesh(initialVertices, subMesh.Indices, OutputFloatsPerVertex, VertexLayout, BufferUsageHint.DynamicDraw);
            var texture = subMesh.Albedo is not null ? new Texture2D(subMesh.Albedo.Width, subMesh.Albedo.Height, subMesh.Albedo.PixelsBgra) : null;
            parts.Add(new ModelPart(subMesh.Vertices, mesh, texture, initialVertices));
        }

        var animator = asset.Skeleton is not null ? new Animator(asset.Skeleton) : null;
        return new LoadedModel(name, parts, asset.Skeleton, animator);
    }

    /// <summary>Fills the GPU's initial position/normal/uv buffer straight from the bind pose, before any animation has run.</summary>
    private static void CopyBindPoseToOutput(float[] bindVertices, float[] output)
    {
        for (var i = 0; i < bindVertices.Length; i += SubMesh.FloatsPerVertex)
        {
            var o = (i / SubMesh.FloatsPerVertex) * OutputFloatsPerVertex;
            Array.Copy(bindVertices, i, output, o, OutputFloatsPerVertex);
        }
    }

    /// <summary>Linear-blend skins one submesh's bind-pose vertices on the CPU using up to four bone weights per vertex.</summary>
    private static void SkinVertices(float[] bindVertices, IReadOnlyList<Matrix4> skinningMatrices, float[] output)
    {
        for (var i = 0; i < bindVertices.Length; i += SubMesh.FloatsPerVertex)
        {
            var bindPosition = new Vector3(bindVertices[i + SubMesh.PositionOffset], bindVertices[i + SubMesh.PositionOffset + 1], bindVertices[i + SubMesh.PositionOffset + 2]);
            var bindNormal = new Vector3(bindVertices[i + SubMesh.NormalOffset], bindVertices[i + SubMesh.NormalOffset + 1], bindVertices[i + SubMesh.NormalOffset + 2]);

            var skinnedPosition = Vector3.Zero;
            var skinnedNormal = Vector3.Zero;
            for (var bone = 0; bone < 4; bone++)
            {
                var weight = bindVertices[i + SubMesh.BoneWeightOffset + bone];
                if (weight <= 0f) continue;
                var boneIndex = (int)bindVertices[i + SubMesh.BoneIndexOffset + bone];
                var skin = skinningMatrices[boneIndex];
                skinnedPosition += weight * Vector3.TransformPosition(bindPosition, skin);
                skinnedNormal += weight * Vector3.TransformNormal(bindNormal, skin);
            }

            var o = (i / SubMesh.FloatsPerVertex) * OutputFloatsPerVertex;
            output[o + 0] = skinnedPosition.X;
            output[o + 1] = skinnedPosition.Y;
            output[o + 2] = skinnedPosition.Z;
            var normal = skinnedNormal.LengthSquared > 0f ? Vector3.Normalize(skinnedNormal) : bindNormal;
            output[o + 3] = normal.X;
            output[o + 4] = normal.Y;
            output[o + 5] = normal.Z;
            output[o + 6] = bindVertices[i + SubMesh.UvOffset];
            output[o + 7] = bindVertices[i + SubMesh.UvOffset + 1];
        }
    }

    private void UpdateAnimation(float deltaSeconds)
    {
        var body = _models.Count > 0 ? _models[0] : null;
        if (body?.Skeleton is null || body.Animator is null) return;

        body.Animator.Update(deltaSeconds);
        var bodyBoneMatrixByName = SkeletonMerge.BuildBoneMatrixByName(body.Skeleton, body.Animator.BoneWorldMatrices);

        foreach (var model in _models)
        {
            if (model.Animator is null || model.Skeleton is null) continue;
            if (model != body)
            {
                var mergedPose = SkeletonMerge.MergePose(model.Skeleton, bodyBoneMatrixByName);
                model.Animator.SetWorldMatrices(mergedPose);
            }

            foreach (var part in model.Parts)
            {
                SkinVertices(part.BindVertices, model.Animator.SkinningMatrices, part.SkinnedScratch);
                part.Mesh.VertexBuffer.Update(part.SkinnedScratch);
            }
        }
    }

    private void RenderScene()
    {
        if (!_loaded) return;
        MakeCurrent();

        var now = _clock.Elapsed;
        UpdateAnimation((float)(now - _lastUpdate).TotalSeconds);
        _lastUpdate = now;

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
                foreach (var part in model.Parts)
                {
                    part.Texture?.Bind();
                    part.Mesh.Draw();
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
            foreach (var part in model.Parts)
            {
                part.Mesh.Dispose();
                part.Texture?.Dispose();
            }
        }
        if (_shader is not null) GL.DeleteProgram(_shader.Handle);
        _loaded = false;
    }
}
