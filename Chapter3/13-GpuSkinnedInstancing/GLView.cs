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

namespace LearnOpenTK.GpuSkinnedInstancing;

/// <summary>One draw call belonging to a <see cref="ModelRole"/>: its GPU mesh and albedo texture, shared by every instance.</summary>
public sealed record RolePart(Mesh Mesh, Texture2D? Texture);

/// <summary>
/// One kind of part every hero instance has (the body, or one default-loadout wearable). All
/// instances of a role share the same geometry/texture/skeleton and are drawn together with a
/// single <see cref="Mesh.DrawInstanced"/> call; only the per-instance bone palette differs.
/// </summary>
public sealed class ModelRole
{
    public required string Name { get; init; }
    public required IReadOnlyList<RolePart> Parts { get; init; }
    public required Skeleton? Skeleton { get; init; }

    /// <summary>One entry per instance, in the same order as the instance transform buffer. Null where the part has no skeleton (drawn rigidly).</summary>
    public required Animator?[] Animators { get; init; }
    public required int BoneCount { get; init; }
    public required StorageBuffer BoneBuffer { get; init; }

    /// <summary>Reused every frame: BoneScratch[instance * BoneCount + bone] is that instance's skinning matrix for this role.</summary>
    public required Matrix4[] BoneScratch { get; init; }
}

public sealed class GLView : GLControl
{
    private static readonly VertexAttribute[] VertexLayout =
    [
        new VertexAttribute(Location: 0, ComponentCount: 3, OffsetFloats: SubMesh.PositionOffset),
        new VertexAttribute(Location: 1, ComponentCount: 3, OffsetFloats: SubMesh.NormalOffset),
        new VertexAttribute(Location: 2, ComponentCount: 2, OffsetFloats: SubMesh.UvOffset),
        new VertexAttribute(Location: 3, ComponentCount: 4, OffsetFloats: SubMesh.BoneIndexOffset),
        new VertexAttribute(Location: 4, ComponentCount: 4, OffsetFloats: SubMesh.BoneWeightOffset),
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
    private readonly List<ModelRole> _roles = [];
    private StorageBuffer? _instanceBuffer;
    private int _instanceCount;
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
    public IReadOnlyList<string> RoleNames => _roles.Select(r => r.Name).ToList();
    public int InstanceCount => _instanceCount;
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
            var (min, max) = LoadHeroInstances();
            var center = (min + max) / 2f;
            var radius = MathF.Max((max - min).Length / 2f, 1f);
            _camera.MaxDistance = MathF.Max(radius * 6f, 30f);
            _camera.FarPlane = MathF.Max(radius * 10f, 100f);
            _camera.SetView(center, radius * 2.2f);
            StatusChanged?.Invoke(this, $"{_instanceCount} instance(s) x {_roles.Count} role(s) | Right: orbit | Middle: pan | Wheel: zoom");
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

    /// <summary>
    /// Loads the hero body and its default wearables once, then instances that whole set across a
    /// rows x columns grid. Every instance shares the same GPU mesh/texture per role; only the bone
    /// palette each role uploads to the GPU differs per instance.
    /// </summary>
    private (Vector3 Min, Vector3 Max) LoadHeroInstances()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "config.ini");
        var config = Dota2Config.Load(configPath);
        using var archive = GameArchive.Open(config.GameRoot);

        var rows = Math.Max(config.InstanceRows ?? 5, 1);
        var columns = Math.Max(config.InstanceColumns ?? 5, 1);
        var spacing = config.InstanceSpacing ?? 250f;
        _instanceCount = rows * columns;

        var instanceTransforms = new Matrix4[_instanceCount];
        var index = 0;
        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var worldOffset = new Vector3((column - (columns - 1) / 2f) * spacing, 0f, (row - (rows - 1) / 2f) * spacing);
                instanceTransforms[index] = SourceToWorldUp * Matrix4.CreateTranslation(worldOffset);
                index++;
            }
        }

        _instanceBuffer = new StorageBuffer();
        _instanceBuffer.AllocateMatrices(instanceTransforms, BufferUsageHint.StaticDraw);

        var body = ModelAsset.Load(archive, config.ModelPath);
        var clipNames = body.Animations.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();
        AddRole(Path.GetFileNameWithoutExtension(config.ModelPath), body, animatorIndex =>
        {
            if (body.Skeleton is null || clipNames.Count == 0) return null;
            var animator = new Animator(body.Skeleton);
            animator.Play(body.Animations[clipNames[animatorIndex % clipNames.Count]]);
            animator.Update(animatorIndex * .37f); // desync every instance's start time, even repeats of the same clip
            return animator;
        });

        var localMin = new Vector3(float.MaxValue);
        var localMax = new Vector3(float.MinValue);
        void ExpandLocalBounds(ModelAsset asset)
        {
            foreach (var subMesh in asset.SubMeshes)
            {
                for (var i = 0; i < subMesh.Vertices.Length; i += SubMesh.FloatsPerVertex)
                {
                    var position = new Vector3(subMesh.Vertices[i], subMesh.Vertices[i + 1], subMesh.Vertices[i + 2]);
                    localMin = Vector3.ComponentMin(localMin, position);
                    localMax = Vector3.ComponentMax(localMax, position);
                }
            }
        }
        ExpandLocalBounds(body);

        var heroName = HeroLoadout.HeroNpcNameFromModelPath(config.ModelPath);
        if (heroName is not null)
        {
            foreach (var item in HeroLoadout.LoadDefaultLoadout(archive, heroName))
            {
                var wearable = ModelAsset.Load(archive, item.ModelPlayerPath);
                ExpandLocalBounds(wearable);
                AddRole(item.Name, wearable, _ => wearable.Skeleton is not null ? new Animator(wearable.Skeleton) : null);
            }
        }

        return ComputeWorldBounds(localMin, localMax, instanceTransforms);
    }

    private void AddRole(string name, ModelAsset asset, Func<int, Animator?> createAnimator)
    {
        var animators = new Animator?[_instanceCount];
        for (var i = 0; i < _instanceCount; i++) animators[i] = createAnimator(i);

        var boneCount = Math.Max(asset.Skeleton?.BoneCount ?? 1, 1);
        var boneScratch = new Matrix4[_instanceCount * boneCount];
        Array.Fill(boneScratch, Matrix4.Identity);
        var boneBuffer = new StorageBuffer();
        boneBuffer.AllocateMatrices(boneScratch, BufferUsageHint.DynamicDraw);

        var parts = new List<RolePart>();
        foreach (var subMesh in asset.SubMeshes)
        {
            var mesh = new Mesh(subMesh.Vertices, subMesh.Indices, SubMesh.FloatsPerVertex, VertexLayout);
            var texture = subMesh.Albedo is not null ? new Texture2D(subMesh.Albedo.Width, subMesh.Albedo.Height, subMesh.Albedo.PixelsBgra) : null;
            parts.Add(new RolePart(mesh, texture));
        }

        _roles.Add(new ModelRole
        {
            Name = name,
            Parts = parts,
            Skeleton = asset.Skeleton,
            Animators = animators,
            BoneCount = boneCount,
            BoneBuffer = boneBuffer,
            BoneScratch = boneScratch,
        });
    }

    /// <summary>Transforms one hero's local (Source-space) bounding box by every instance's placement and unions the results.</summary>
    private static (Vector3 Min, Vector3 Max) ComputeWorldBounds(Vector3 localMin, Vector3 localMax, IReadOnlyList<Matrix4> instanceTransforms)
    {
        Span<Vector3> corners =
        [
            new(localMin.X, localMin.Y, localMin.Z), new(localMax.X, localMin.Y, localMin.Z),
            new(localMin.X, localMax.Y, localMin.Z), new(localMax.X, localMax.Y, localMin.Z),
            new(localMin.X, localMin.Y, localMax.Z), new(localMax.X, localMin.Y, localMax.Z),
            new(localMin.X, localMax.Y, localMax.Z), new(localMax.X, localMax.Y, localMax.Z),
        ];

        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        foreach (var transform in instanceTransforms)
        {
            foreach (var corner in corners)
            {
                var worldCorner = Vector3.TransformPosition(corner, transform);
                min = Vector3.ComponentMin(min, worldCorner);
                max = Vector3.ComponentMax(max, worldCorner);
            }
        }

        return (min, max);
    }

    /// <summary>Advances the body's animation per instance, merges each wearable role onto that instance's pose by bone name, then refills every role's bone buffer.</summary>
    private void UpdateAnimation(float deltaSeconds)
    {
        if (_roles.Count == 0) return;
        var body = _roles[0];

        for (var i = 0; i < _instanceCount; i++)
        {
            var bodyAnimator = body.Animators[i];
            if (bodyAnimator is null || body.Skeleton is null) continue;
            bodyAnimator.Update(deltaSeconds);
            var bodyBoneMatrixByName = SkeletonMerge.BuildBoneMatrixByName(body.Skeleton, bodyAnimator.BoneWorldMatrices);

            for (var r = 1; r < _roles.Count; r++)
            {
                var role = _roles[r];
                var wearableAnimator = role.Animators[i];
                if (wearableAnimator is null || role.Skeleton is null) continue;
                wearableAnimator.SetWorldMatrices(SkeletonMerge.MergePose(role.Skeleton, bodyBoneMatrixByName));
            }
        }

        foreach (var role in _roles)
        {
            for (var i = 0; i < _instanceCount; i++)
            {
                var animator = role.Animators[i];
                var scratchOffset = i * role.BoneCount;
                if (animator is null)
                {
                    continue; // BoneScratch keeps its identity default from AddRole for rigid (unskinned) roles.
                }

                for (var bone = 0; bone < role.BoneCount; bone++)
                {
                    role.BoneScratch[scratchOffset + bone] = animator.SkinningMatrices[bone];
                }
            }

            role.BoneBuffer.UpdateMatrices(role.BoneScratch);
        }
    }

    private bool _reportedRenderError;

    private void RenderScene()
    {
        if (!_loaded) return;
        MakeCurrent();

        var now = _clock.Elapsed;
        var deltaSeconds = (float)(now - _lastUpdate).TotalSeconds;
        _lastUpdate = now;

        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        // Instancing pushes CPU-side pose math (UpdateAnimation) and several new GPU calls (SSBO
        // binding, an instanced draw) through in one frame; if either throws, still reach SwapBuffers
        // below so the window shows this frame's clear color instead of freezing on a stale one, and
        // surface the failure once instead of leaving a silently blank viewport with no clue why.
        try
        {
            UpdateAnimation(deltaSeconds);
            DrawInstances();
            ReportFirstGlError();
        }
        catch (Exception ex) when (!_reportedRenderError)
        {
            _reportedRenderError = true;
            StatusChanged?.Invoke(this, $"Render error: {ex.Message}");
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

    private void DrawInstances()
    {
        if (_roles.Count == 0 || _instanceBuffer is null) return;

        _material!.Bind();
        _shader!.SetMatrix4("uView", _camera.GetViewMatrix());
        _shader.SetMatrix4("uProjection", _camera.GetProjectionMatrix());
        _shader.SetVector3("uCameraPosition", _camera.Position);
        _shader.SetVector3("uSunDirection", Vector3.Normalize(new(-1, -1, -.5f)));
        _shader.SetVector3("uFillPosition", _camera.Target + new Vector3(-200, 100, 200));
        _shader.SetVector3("uRimPosition", _camera.Target + new Vector3(200, 150, -200));

        _instanceBuffer.BindBase(1);
        foreach (var role in _roles)
        {
            _shader.SetInt("uBoneCountPerInstance", role.BoneCount);
            role.BoneBuffer.BindBase(0);
            foreach (var part in role.Parts)
            {
                part.Texture?.Bind();
                part.Mesh.DrawInstanced(_instanceCount);
            }
        }
    }

    /// <summary>OpenGL calls do not throw for bad state (wrong binding, block layout mismatch, ...); check once so a silently blank viewport still tells the status bar why.</summary>
    private void ReportFirstGlError()
    {
        if (_reportedRenderError) return;
        var error = GL.GetError();
        if (error == ErrorCode.NoError) return;
        _reportedRenderError = true;
        StatusChanged?.Invoke(this, $"OpenGL reported an error while drawing: {error}");
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
        foreach (var role in _roles)
        {
            foreach (var part in role.Parts)
            {
                part.Mesh.Dispose();
                part.Texture?.Dispose();
            }
            role.BoneBuffer.Dispose();
        }
        _instanceBuffer?.Dispose();
        if (_shader is not null) GL.DeleteProgram(_shader.Handle);
        _loaded = false;
    }
}
