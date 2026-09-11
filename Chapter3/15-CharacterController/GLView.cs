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
using LearnOpenTK.Common.Geometry;
using LearnOpenTK.Common.Graphics;
using LearnOpenTK.Common.Rendering;
using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;
using OpenTK.WinForms;

namespace LearnOpenTK.CharacterController;

/// <summary>One draw call belonging to a <see cref="ModelRole"/>: its GPU mesh and its four material maps.</summary>
public sealed record RolePart(Mesh Mesh, Texture2D Albedo, Texture2D Normal, Texture2D Mask1, Texture2D Mask2);

/// <summary>One part of a character (the body, or one default-loadout wearable). Its bone buffer holds exactly one instance's palette (this project only ever draws the one selected hero).</summary>
public sealed class ModelRole
{
    public required string Name { get; init; }
    public required IReadOnlyList<RolePart> Parts { get; init; }
    public required Skeleton? Skeleton { get; init; }
    public required Animator? Animator { get; init; }
    public required int BoneCount { get; init; }
    public required StorageBuffer BoneBuffer { get; init; }

    /// <summary>Reused every frame: BoneScratch[bone] is this role's current skinning matrix for that bone.</summary>
    public required Matrix4[] BoneScratch { get; init; }
}

/// <summary>
/// One loaded hero: its body role plus every default-loadout wearable role, its inferred Idle/Run/Attack
/// clips (see <see cref="HeroAnimationClassifier"/>), and its local-space bounds (for camera framing).
/// Kept in <see cref="GLView"/>'s in-memory cache so re-selecting a previously visited hero is instant.
/// </summary>
public sealed class Character
{
    public required string HeroName { get; init; }
    public required string NpcName { get; init; }
    public required IReadOnlyList<ModelRole> Roles { get; init; }
    public required StorageBuffer InstanceBuffer { get; init; }
    public required AnimationClip? IdleClip { get; init; }
    public required AnimationClip? RunClip { get; init; }
    public required AnimationClip? AttackClip { get; init; }
    public required AnimationClip? JumpClip { get; init; }
    public required AnimationClip? FallClip { get; init; }
    public required Vector3 LocalMin { get; init; }
    public required Vector3 LocalMax { get; init; }
}

public sealed class GLView : GLControl
{
    private static readonly VertexAttribute[] VertexLayout =
    [
        new VertexAttribute(Location: 0, ComponentCount: 3, OffsetFloats: SubMesh.PositionOffset),
        new VertexAttribute(Location: 1, ComponentCount: 3, OffsetFloats: SubMesh.NormalOffset),
        new VertexAttribute(Location: 2, ComponentCount: 2, OffsetFloats: SubMesh.UvOffset),
        new VertexAttribute(Location: 3, ComponentCount: 4, OffsetFloats: SubMesh.TangentOffset),
        new VertexAttribute(Location: 4, ComponentCount: 4, OffsetFloats: SubMesh.BoneIndexOffset),
        new VertexAttribute(Location: 5, ComponentCount: 4, OffsetFloats: SubMesh.BoneWeightOffset),
    ];

    /// <summary>
    /// Source 2 meshes are authored right-handed Z-up: +X forward, +Y left, +Z up. This repository's
    /// cameras/lighting assume Y-up, so every hero is rotated -90 degrees about X once here (same fixed
    /// matrix as 14-GpuPbr). The hero's neutral (unrotated) facing after this conversion is world +X,
    /// which is why <see cref="CharacterController"/> measures yaw from +X rather than +Z.
    /// </summary>
    private static readonly Matrix4 SourceToWorldUp = new(
        new Vector4(1, 0, 0, 0),
        new Vector4(0, 0, -1, 0),
        new Vector4(0, 1, 0, 0),
        new Vector4(0, 0, 0, 1));

    // Same flat sky/ground hemisphere ambient fallback as 14-GpuPbr, so shadowed hero surfaces aren't pure black.
    private static readonly Vector3 AmbientSkyColor = new(.18f, .21f, .28f);
    private static readonly Vector3 AmbientGroundColor = new(.09f, .08f, .07f);
    private static readonly Vector3 GroundColor = new(.22f, .32f, .16f);

    private readonly OrbitCamera _camera = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly Dictionary<string, Character> _loadedCharacters = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<Keys> _heldKeys = [];

    private Shader? _shader;
    private Material? _material;
    private Shader? _groundShader;
    private Material? _groundMaterial;
    private Mesh? _groundMesh;
    private float _groundHalfSize = 2000f;

    // A flat-normal / all-zero-mask 1x1 texture stands in for materials missing that map, so the
    // fragment shader can always sample all four maps unconditionally instead of branching on
    // per-material "has this map" flags.
    private Texture2D? _fallbackAlbedo;
    private Texture2D? _fallbackNormal;
    private Texture2D? _fallbackMask1;
    private Texture2D? _fallbackMask2;

    private GameArchive? _archive;
    private IReadOnlyList<HeroRosterEntry> _roster = [];
    private CharacterController? _controller;
    private Character? _activeCharacter;
    private CharacterMotionState _lastAppliedState = CharacterMotionState.Idle;
    private Vector3 _cameraFollowOffset;
    private string _cacheDirectory = string.Empty;
    private bool _attackKeyDown;
    private bool _jumpKeyDown;

    private bool _loaded;
    private bool _orbit;
    private bool _pan;
    private Point _last;
    private int _swap;
    private int _frames;
    private TimeSpan _sample;
    private TimeSpan _lastUpdate;
    private bool _reportedRenderError;

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
        KeyDown += HandleKeyDown;
        KeyUp += HandleKeyUp;
        Disposed += (_, _) => UnloadScene();
    }

    // Arrow keys are otherwise treated as dialog-navigation keys and never reach KeyDown/KeyUp.
    protected override bool IsInputKey(Keys keyData) =>
        keyData is Keys.Up or Keys.Down or Keys.Left or Keys.Right || base.IsInputKey(keyData);

    public event EventHandler<string>? StatusChanged;

    /// <summary>The full playable roster (npc name, display name), populated once in <see cref="LoadScene"/> for the hero-picker tree.</summary>
    public IReadOnlyList<(string NpcName, string DisplayName)> Roster { get; private set; } = [];

    public double FramesPerSecond { get; private set; }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool VSync { get => _swap != 0; set { _swap = value ? 1 : 0; if (_loaded && Context is not null) Context.SwapInterval = _swap; } }

    /// <summary>Selects (loading and caching in memory on first use) the hero to control, spawning it at the world origin.</summary>
    public void SelectHero(string npcName)
    {
        if (_archive is null || _controller is null) return;
        MakeCurrent();

        try
        {
            if (!_loadedCharacters.TryGetValue(npcName, out var character))
            {
                character = LoadCharacter(npcName);
                _loadedCharacters[npcName] = character;
            }

            _activeCharacter = character;
            _controller.Reset(Vector3.Zero, 0f);
            _lastAppliedState = CharacterMotionState.Idle;
            PlayClipForState(character, CharacterMotionState.Idle);

            var min = new Vector3(float.MaxValue);
            var max = new Vector3(float.MinValue);
            foreach (var corner in BoxCorners(character.LocalMin, character.LocalMax))
            {
                var worldCorner = Vector3.TransformPosition(corner, SourceToWorldUp);
                min = Vector3.ComponentMin(min, worldCorner);
                max = Vector3.ComponentMax(max, worldCorner);
            }
            FrameCamera(min, max);
            _cameraFollowOffset = (min + max) / 2f;

            StatusChanged?.Invoke(this, $"{character.HeroName} | WASD/Arrows: move (camera-relative), Space: jump, F: attack | Right: orbit, Middle: pan, Wheel: zoom");
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, $"Could not load {npcName}: {ex.Message}");
        }
    }

    private void LoadScene()
    {
        MakeCurrent();
        GL.ClearColor(.08f, .1f, .13f, 1);
        GL.Enable(EnableCap.DepthTest);
        if (Context is not null) Context.SwapInterval = _swap;

        _shader = new Shader(Path.Combine(AppContext.BaseDirectory, "Shaders", "shader.vert"), Path.Combine(AppContext.BaseDirectory, "Shaders", "shader.frag"));
        _material = new Material(_shader);
        _shader.SetInt("uAlbedo", 0);
        _shader.SetInt("uNormal", 1);
        _shader.SetInt("uMask1", 2);
        _shader.SetInt("uMask2", 3);

        // BGRA bytes: white albedo; a flat tangent-space normal (unpacks from .ag, see shader.frag);
        // an all-zero mask (metalness/self-illum/specular/rim/tint/exponent-scale all default to 0).
        _fallbackAlbedo = new Texture2D(1, 1, [255, 255, 255, 255]);
        _fallbackNormal = new Texture2D(1, 1, [0, 128, 0, 128]);
        _fallbackMask1 = new Texture2D(1, 1, [0, 0, 0, 0]);
        _fallbackMask2 = new Texture2D(1, 1, [0, 0, 0, 0]);

        _groundShader = new Shader(Path.Combine(AppContext.BaseDirectory, "Shaders", "ground.vert"), Path.Combine(AppContext.BaseDirectory, "Shaders", "ground.frag"));
        _groundMaterial = new Material(_groundShader);

        try
        {
            var config = Dota2Config.Load(Path.Combine(AppContext.BaseDirectory, "config.ini"));
            _archive = GameArchive.Open(config.GameRoot);
            _groundHalfSize = config.GroundHalfSize ?? 2000f;
            _groundMesh = new Mesh(PlaneGeometry.LitVertices(_groundHalfSize), PlaneGeometry.Indices, 9, 0, 3, 6);

            _roster = HeroRoster.LoadAll(_archive);
            Roster = _roster.Select(h => (h.NpcName, ToDisplayName(h.NpcName))).OrderBy(h => h.Item2, StringComparer.OrdinalIgnoreCase).ToList();

            _controller = new CharacterController(config.MoveSpeed ?? 220f, config.JumpSpeed ?? 320f, config.Gravity ?? 800f);
            _cacheDirectory = Path.Combine(AppContext.BaseDirectory, ".cache", "animations");

            var initialHero = _roster.FirstOrDefault(h => h.NpcName.Equals(config.InitialHeroNpcName, StringComparison.OrdinalIgnoreCase)) ?? _roster.FirstOrDefault();
            if (initialHero is not null) SelectHero(initialHero.NpcName);
            else StatusChanged?.Invoke(this, "No playable heroes found in scripts/npc/npc_heroes.txt.");
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, $"Could not load Dota 2 data: {ex.Message}");
        }

        _loaded = true;
        _sample = _clock.Elapsed;
        _lastUpdate = _clock.Elapsed;
        ResizeViewport();
        Focus();
    }

    private void FrameCamera(Vector3 min, Vector3 max)
    {
        var center = (min + max) / 2f;
        var radius = MathF.Max((max - min).Length / 2f, 1f);
        _camera.MaxDistance = MathF.Max(radius * 6f, 30f);
        _camera.FarPlane = MathF.Max(MathF.Max(radius * 10f, _groundHalfSize * 1.5f), 100f);

        // A single standing hero's bounding-box diagonal is dominated by its height, not its (much
        // smaller) width/depth, so 14-GpuPbr's multi-character-grid framing multiplier (radius * 2.2)
        // is too tight here - it fills the frame edge-to-edge with no headroom. A wider multiplier
        // leaves room to see the whole body without requiring the user to zoom out immediately.
        _camera.SetView(center, radius * 4f);
    }

    /// <summary>Loads one hero's body, default wearables, and its cached/inferred Idle/Run/Attack clips. GL resources are created here, so callers must have already called MakeCurrent().</summary>
    private Character LoadCharacter(string npcName)
    {
        var hero = _roster.FirstOrDefault(h => h.NpcName.Equals(npcName, StringComparison.OrdinalIgnoreCase))
            ?? throw new ArgumentException($"'{npcName}' is not in the loaded hero roster.", nameof(npcName));

        var body = ModelAsset.Load(_archive!, hero.ModelPath);

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

        var roles = new List<ModelRole>
        {
            BuildRole(Path.GetFileNameWithoutExtension(hero.ModelPath), body, () => body.Skeleton is not null ? new Animator(body.Skeleton) : null),
        };

        foreach (var item in HeroLoadout.LoadDefaultLoadout(_archive!, hero.NpcName))
        {
            var wearable = ModelAsset.Load(_archive!, item.ModelPlayerPath);
            ExpandLocalBounds(wearable);
            roles.Add(BuildRole(item.Name, wearable, () => wearable.Skeleton is not null ? new Animator(wearable.Skeleton) : null));
        }

        var classification = HeroAnimationClassifier.ClassifyOrLoadCached(body, hero.NpcName, _cacheDirectory);
        AnimationClip? ResolveClip(string? clipName) => clipName is not null && body.Animations.TryGetValue(clipName, out var clip) ? clip : null;

        var instanceBuffer = new StorageBuffer();
        instanceBuffer.AllocateMatrices([Matrix4.Identity], BufferUsageHint.DynamicDraw);

        return new Character
        {
            HeroName = ToDisplayName(hero.NpcName),
            NpcName = hero.NpcName,
            Roles = roles,
            InstanceBuffer = instanceBuffer,
            IdleClip = ResolveClip(classification.PrimaryIdleClip),
            RunClip = ResolveClip(classification.PrimaryRunClip),
            AttackClip = ResolveClip(classification.PrimaryAttackClip),
            JumpClip = ResolveClip(classification.PrimaryJumpClip),
            FallClip = ResolveClip(classification.PrimaryFallClip),
            LocalMin = localMin,
            LocalMax = localMax,
        };
    }

    private ModelRole BuildRole(string name, ModelAsset asset, Func<Animator?> createAnimator)
    {
        var boneCount = Math.Max(asset.Skeleton?.BoneCount ?? 1, 1);
        var boneScratch = new Matrix4[boneCount];
        Array.Fill(boneScratch, Matrix4.Identity);
        var boneBuffer = new StorageBuffer();
        boneBuffer.AllocateMatrices(boneScratch, BufferUsageHint.DynamicDraw);

        var parts = new List<RolePart>();
        foreach (var subMesh in asset.SubMeshes)
        {
            var mesh = new Mesh(subMesh.Vertices, subMesh.Indices, SubMesh.FloatsPerVertex, VertexLayout);
            var albedo = ToTexture(subMesh.Albedo) ?? _fallbackAlbedo!;
            var normal = ToTexture(subMesh.Normal) ?? _fallbackNormal!;
            var mask1 = ToTexture(subMesh.Mask1) ?? _fallbackMask1!;
            var mask2 = ToTexture(subMesh.Mask2) ?? _fallbackMask2!;
            parts.Add(new RolePart(mesh, albedo, normal, mask1, mask2));
        }

        return new ModelRole
        {
            Name = name,
            Parts = parts,
            Skeleton = asset.Skeleton,
            Animator = createAnimator(),
            BoneCount = boneCount,
            BoneBuffer = boneBuffer,
            BoneScratch = boneScratch,
        };
    }

    private static Texture2D? ToTexture(DecodedTexture? decoded) => decoded is null ? null : new Texture2D(decoded.Width, decoded.Height, decoded.PixelsBgra);

    private static IEnumerable<Vector3> BoxCorners(Vector3 min, Vector3 max)
    {
        yield return new Vector3(min.X, min.Y, min.Z);
        yield return new Vector3(max.X, min.Y, min.Z);
        yield return new Vector3(min.X, max.Y, min.Z);
        yield return new Vector3(max.X, max.Y, min.Z);
        yield return new Vector3(min.X, min.Y, max.Z);
        yield return new Vector3(max.X, min.Y, max.Z);
        yield return new Vector3(min.X, max.Y, max.Z);
        yield return new Vector3(max.X, max.Y, max.Z);
    }

    /// <summary>Turns "npc_dota_hero_crystal_maiden" into "Crystal Maiden".</summary>
    private static string ToDisplayName(string npcName)
    {
        const string prefix = "npc_dota_hero_";
        var suffix = npcName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? npcName[prefix.Length..] : npcName;
        var words = suffix.Split('_', StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', words.Select(w => char.ToUpperInvariant(w[0]) + w[1..]));
    }

    /// <summary>
    /// Plays the clip classified for <paramref name="state"/> on the body's Animator, falling back
    /// through the other states if the hero has no clip classified for it. Dota 2 has no jump mechanic,
    /// so most heroes have no Jump/Fall clip at all - Jump/Fall fall back to Run/Idle, which is an
    /// honest reflection of what the hero's own animation list actually contains, not a bug.
    /// </summary>
    private static void PlayClipForState(Character character, CharacterMotionState state)
    {
        if (character.Roles.Count == 0) return;
        var animator = character.Roles[0].Animator;
        if (animator is null) return;

        var clip = state switch
        {
            CharacterMotionState.Attack => character.AttackClip ?? character.RunClip ?? character.IdleClip,
            CharacterMotionState.Run => character.RunClip ?? character.IdleClip ?? character.AttackClip,
            CharacterMotionState.Jump => character.JumpClip ?? character.FallClip ?? character.RunClip ?? character.IdleClip,
            CharacterMotionState.Fall => character.FallClip ?? character.JumpClip ?? character.RunClip ?? character.IdleClip,
            _ => character.IdleClip ?? character.RunClip ?? character.AttackClip,
        };
        if (clip is not null) animator.Play(clip);
    }

    /// <summary>Advances the character controller, switches the played clip on a state change, re-poses the body then merges wearables onto it by bone name, refills every role's bone buffer, moves the instance placement, and keeps the camera pivoted on the character.</summary>
    private void UpdateCharacter(float deltaSeconds)
    {
        if (_activeCharacter is null || _controller is null) return;

        _controller.Update(deltaSeconds, _heldKeys, _attackKeyDown, _jumpKeyDown, _camera.Yaw, _activeCharacter.AttackClip?.Duration ?? 0f, _groundHalfSize);
        if (_controller.State != _lastAppliedState)
        {
            PlayClipForState(_activeCharacter, _controller.State);
            _lastAppliedState = _controller.State;
        }

        var body = _activeCharacter.Roles[0];
        var bodyAnimator = body.Animator;
        Dictionary<string, Matrix4>? bodyBoneMatrixByName = null;
        if (bodyAnimator is not null && body.Skeleton is not null)
        {
            bodyAnimator.Update(deltaSeconds);
            bodyBoneMatrixByName = SkeletonMerge.BuildBoneMatrixByName(body.Skeleton, bodyAnimator.BoneWorldMatrices);
        }

        for (var r = 1; r < _activeCharacter.Roles.Count; r++)
        {
            var role = _activeCharacter.Roles[r];
            if (role.Animator is null || role.Skeleton is null || bodyBoneMatrixByName is null) continue;
            role.Animator.SetWorldMatrices(SkeletonMerge.MergePose(role.Skeleton, bodyBoneMatrixByName));
        }

        foreach (var role in _activeCharacter.Roles)
        {
            if (role.Animator is not null)
            {
                for (var bone = 0; bone < role.BoneCount; bone++) role.BoneScratch[bone] = role.Animator.SkinningMatrices[bone];
            }
            role.BoneBuffer.UpdateMatrices(role.BoneScratch);
        }

        var placement = SourceToWorldUp * Matrix4.CreateRotationY(MathHelper.DegreesToRadians(_controller.YawDegrees)) * Matrix4.CreateTranslation(_controller.Position);
        _activeCharacter.InstanceBuffer.UpdateMatrices([placement]);

        _camera.SetView(_controller.Position + _cameraFollowOffset, _camera.Distance);
    }

    private void RenderScene()
    {
        if (!_loaded) return;
        MakeCurrent();

        var now = _clock.Elapsed;
        var deltaSeconds = (float)(now - _lastUpdate).TotalSeconds;
        _lastUpdate = now;

        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        // See 14-GpuPbr's GLView for why this is wrapped: still reach SwapBuffers on failure, and
        // surface the first error (C# exception or a silent OpenGL error) once.
        try
        {
            UpdateCharacter(deltaSeconds);
            DrawGround();
            DrawCharacter();
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

    private void DrawGround()
    {
        if (_groundMesh is null) return;

        _groundMaterial!.Bind();
        _groundShader!.SetMatrix4("uModel", Matrix4.Identity);
        _groundShader.SetMatrix4("uView", _camera.GetViewMatrix());
        _groundShader.SetMatrix4("uProjection", _camera.GetProjectionMatrix());
        _groundShader.SetVector3("uCameraPosition", _camera.Position);
        _groundShader.SetVector3("uSunDirection", Vector3.Normalize(new(-1, -1, -.5f)));
        _groundShader.SetVector3("uFillPosition", _camera.Target + new Vector3(-200, 100, 200));
        _groundShader.SetVector3("uRimPosition", _camera.Target + new Vector3(200, 150, -200));
        _groundShader.SetVector3("uGroundColor", GroundColor);
        _groundMesh.Draw();
    }

    private void DrawCharacter()
    {
        if (_activeCharacter is null) return;

        _material!.Bind();
        _shader!.SetMatrix4("uView", _camera.GetViewMatrix());
        _shader.SetMatrix4("uProjection", _camera.GetProjectionMatrix());
        _shader.SetVector3("uCameraPosition", _camera.Position);
        _shader.SetVector3("uSunDirection", Vector3.Normalize(new(-1, -1, -.5f)));
        _shader.SetVector3("uFillPosition", _camera.Target + new Vector3(-200, 100, 200));
        _shader.SetVector3("uRimPosition", _camera.Target + new Vector3(200, 150, -200));
        _shader.SetVector3("uAmbientSkyColor", AmbientSkyColor);
        _shader.SetVector3("uAmbientGroundColor", AmbientGroundColor);

        _activeCharacter.InstanceBuffer.BindBase(1);
        foreach (var role in _activeCharacter.Roles)
        {
            _shader.SetInt("uBoneCountPerInstance", role.BoneCount);
            role.BoneBuffer.BindBase(0);
            foreach (var part in role.Parts)
            {
                part.Albedo.Bind(TextureUnit.Texture0);
                part.Normal.Bind(TextureUnit.Texture1);
                part.Mask1.Bind(TextureUnit.Texture2);
                part.Mask2.Bind(TextureUnit.Texture3);
                part.Mesh.DrawInstanced(1);
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

    private void HandleKeyDown(object? _, KeyEventArgs e)
    {
        _heldKeys.Add(e.KeyCode);
        if (e.KeyCode == Keys.Space) _jumpKeyDown = true;
        if (e.KeyCode == Keys.F) _attackKeyDown = true;
    }

    private void HandleKeyUp(object? _, KeyEventArgs e)
    {
        _heldKeys.Remove(e.KeyCode);
        if (e.KeyCode == Keys.Space) _jumpKeyDown = false;
        if (e.KeyCode == Keys.F) _attackKeyDown = false;
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

    private void DisposeCharacters()
    {
        foreach (var character in _loadedCharacters.Values)
        {
            foreach (var role in character.Roles)
            {
                foreach (var part in role.Parts)
                {
                    part.Mesh.Dispose();
                    part.Albedo.Dispose();
                    part.Normal.Dispose();
                    part.Mask1.Dispose();
                    part.Mask2.Dispose();
                }
                role.BoneBuffer.Dispose();
            }
            character.InstanceBuffer.Dispose();
        }
        _loadedCharacters.Clear();
        _activeCharacter = null;
    }

    private void UnloadScene()
    {
        if (!_loaded) return;
        MakeCurrent();
        DisposeCharacters();
        _groundMesh?.Dispose();
        _fallbackAlbedo?.Dispose();
        _fallbackNormal?.Dispose();
        _fallbackMask1?.Dispose();
        _fallbackMask2?.Dispose();
        if (_shader is not null) GL.DeleteProgram(_shader.Handle);
        if (_groundShader is not null) GL.DeleteProgram(_groundShader.Handle);
        _archive?.Dispose();
        _loaded = false;
    }
}
