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

namespace LearnOpenTK.GpuPbr;

/// <summary>One draw call belonging to a <see cref="ModelRole"/>: its GPU mesh and its four material maps.</summary>
public sealed record RolePart(Mesh Mesh, Texture2D Albedo, Texture2D Normal, Texture2D Mask1, Texture2D Mask2);

/// <summary>
/// One part of one character (the body, or one default-loadout wearable). Unlike
/// 13-GpuSkinnedInstancing/the earlier version of this project, a role here belongs to exactly one
/// character - its bone buffer holds one instance's palette, not many repeats of the same hero.
/// </summary>
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

/// <summary>One placed hero: its body role plus every default-loadout wearable role, and where it stands in the world.</summary>
public sealed class Character
{
    public required string HeroName { get; init; }
    public required IReadOnlyList<ModelRole> Roles { get; init; }
    public required StorageBuffer InstanceBuffer { get; init; }
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

    // The always-present ambient fallback dota2-projects' own hero viewport uses when it has no
    // environment cubemap loaded: a flat sky/ground gradient by world-space normal.y. A full
    // prefiltered-cubemap IBL pass (which that project also supports) is a possible follow-up; this
    // is its honest, zero-asset baseline, so shadowed surfaces are not pure black.
    private static readonly Vector3 AmbientSkyColor = new(.18f, .21f, .28f);
    private static readonly Vector3 AmbientGroundColor = new(.09f, .08f, .07f);

    private readonly OrbitCamera _camera = new();
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly List<Character> _characters = [];
    private Shader? _shader;
    private Material? _material;

    // A flat-normal / all-zero-mask 1x1 texture stands in for materials missing that map, so the
    // fragment shader can always sample all four maps unconditionally instead of branching on
    // per-material "has this map" flags.
    private Texture2D? _fallbackAlbedo;
    private Texture2D? _fallbackNormal;
    private Texture2D? _fallbackMask1;
    private Texture2D? _fallbackMask2;

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
        Disposed += (_, _) => UnloadScene();
    }

    public event EventHandler<string>? StatusChanged;

    /// <summary>One entry per placed character: its display name, and its role/part names for a Scene tree (character at the root, parts as children).</summary>
    public IReadOnlyList<(string HeroName, IReadOnlyList<string> PartNames)> Characters =>
        _characters.Select(c => (c.HeroName, (IReadOnlyList<string>)c.Roles.Select(r => r.Name).ToList())).ToList();

    public double FramesPerSecond { get; private set; }

    [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
    public bool VSync { get => _swap != 0; set { _swap = value ? 1 : 0; if (_loaded && Context is not null) Context.SwapInterval = _swap; } }

    /// <summary>Tears down the current cast and rolls a fresh random selection (still guaranteeing Axe).</summary>
    public void Reroll()
    {
        if (!_loaded) return;
        MakeCurrent();
        DisposeCharacters();
        try
        {
            var (min, max) = LoadCharacters();
            FrameCamera(min, max);
            StatusChanged?.Invoke(this, $"{_characters.Count} character(s) | Right: orbit | Middle: pan | Wheel: zoom");
        }
        catch (Exception ex)
        {
            StatusChanged?.Invoke(this, $"Could not load Dota 2 model: {ex.Message}");
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

        try
        {
            var (min, max) = LoadCharacters();
            FrameCamera(min, max);
            StatusChanged?.Invoke(this, $"{_characters.Count} character(s) | Right: orbit | Middle: pan | Wheel: zoom");
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

    private void FrameCamera(Vector3 min, Vector3 max)
    {
        var center = (min + max) / 2f;
        var radius = MathF.Max((max - min).Length / 2f, 1f);
        _camera.MaxDistance = MathF.Max(radius * 6f, 30f);
        _camera.FarPlane = MathF.Max(radius * 10f, 100f);
        _camera.SetView(center, radius * 2.2f);
    }

    /// <summary>
    /// Picks a random cast of heroes (Axe guaranteed) from the real roster in scripts/npc/npc_heroes.txt,
    /// loads each one's body and default wearables, and places them in a grid. Every character's
    /// geometry differs, so unlike 13-GpuSkinnedInstancing this does not batch copies of one hero into
    /// a single instanced draw - each role's "instance buffer" holds exactly one placement matrix.
    /// </summary>
    private (Vector3 Min, Vector3 Max) LoadCharacters()
    {
        var configPath = Path.Combine(AppContext.BaseDirectory, "config.ini");
        var config = Dota2Config.Load(configPath);
        using var archive = GameArchive.Open(config.GameRoot);

        var characterCount = Math.Max(config.CharacterCount ?? 7, 1);
        var spacing = config.CharacterSpacing ?? 300f;
        var random = config.CharacterSeed is int seed ? new Random(seed) : new Random();

        var roster = HeroRoster.LoadAll(archive);
        var axe = roster.FirstOrDefault(h => h.NpcName.Equals("npc_dota_hero_axe", StringComparison.OrdinalIgnoreCase));
        var others = roster.Where(h => h != axe).OrderBy(_ => random.Next()).Take(Math.Max(characterCount - 1, 0)).ToList();
        var selection = axe is not null ? new List<HeroRosterEntry> { axe } : [];
        selection.AddRange(others);
        selection = selection.OrderBy(_ => random.Next()).ToList();

        var columns = (int)Math.Ceiling(Math.Sqrt(selection.Count));
        var rows = (int)Math.Ceiling(selection.Count / (float)Math.Max(columns, 1));

        var min = new Vector3(float.MaxValue);
        var max = new Vector3(float.MinValue);
        for (var i = 0; i < selection.Count; i++)
        {
            var row = i / columns;
            var column = i % columns;
            var worldOffset = new Vector3((column - (columns - 1) / 2f) * spacing, 0f, (row - (rows - 1) / 2f) * spacing);
            var placement = SourceToWorldUp * Matrix4.CreateTranslation(worldOffset);

            var (character, localMin, localMax) = LoadCharacter(archive, selection[i], placement);
            _characters.Add(character);
            foreach (var corner in BoxCorners(localMin, localMax))
            {
                var worldCorner = Vector3.TransformPosition(corner, placement);
                min = Vector3.ComponentMin(min, worldCorner);
                max = Vector3.ComponentMax(max, worldCorner);
            }
        }

        return (min, max);
    }

    private (Character Character, Vector3 LocalMin, Vector3 LocalMax) LoadCharacter(GameArchive archive, HeroRosterEntry hero, Matrix4 placement)
    {
        var body = ModelAsset.Load(archive, hero.ModelPath);
        var clipNames = body.Animations.Keys.OrderBy(name => name, StringComparer.OrdinalIgnoreCase).ToList();
        var characterIndex = _characters.Count;

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
            BuildRole(Path.GetFileNameWithoutExtension(hero.ModelPath), body, () =>
            {
                if (body.Skeleton is null || clipNames.Count == 0) return null;
                var animator = new Animator(body.Skeleton);
                animator.Play(body.Animations[clipNames[characterIndex % clipNames.Count]]);
                animator.Update(characterIndex * .37f); // desync every character's start time, even repeats of the same clip
                return animator;
            }),
        };

        foreach (var item in HeroLoadout.LoadDefaultLoadout(archive, hero.NpcName))
        {
            var wearable = ModelAsset.Load(archive, item.ModelPlayerPath);
            ExpandLocalBounds(wearable);
            roles.Add(BuildRole(item.Name, wearable, () => wearable.Skeleton is not null ? new Animator(wearable.Skeleton) : null));
        }

        var instanceBuffer = new StorageBuffer();
        instanceBuffer.AllocateMatrices([placement], BufferUsageHint.StaticDraw);

        var character = new Character { HeroName = ToDisplayName(hero.NpcName), Roles = roles, InstanceBuffer = instanceBuffer };
        return (character, localMin, localMax);
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

    /// <summary>Advances each character's body animation, merges its wearables onto that pose by bone name, then refills every role's bone buffer.</summary>
    private void UpdateAnimation(float deltaSeconds)
    {
        foreach (var character in _characters)
        {
            if (character.Roles.Count == 0) continue;
            var body = character.Roles[0];
            var bodyAnimator = body.Animator;
            Dictionary<string, Matrix4>? bodyBoneMatrixByName = null;
            if (bodyAnimator is not null && body.Skeleton is not null)
            {
                bodyAnimator.Update(deltaSeconds);
                bodyBoneMatrixByName = SkeletonMerge.BuildBoneMatrixByName(body.Skeleton, bodyAnimator.BoneWorldMatrices);
            }

            for (var r = 1; r < character.Roles.Count; r++)
            {
                var role = character.Roles[r];
                if (role.Animator is null || role.Skeleton is null || bodyBoneMatrixByName is null) continue;
                role.Animator.SetWorldMatrices(SkeletonMerge.MergePose(role.Skeleton, bodyBoneMatrixByName));
            }

            foreach (var role in character.Roles)
            {
                if (role.Animator is not null)
                {
                    for (var bone = 0; bone < role.BoneCount; bone++) role.BoneScratch[bone] = role.Animator.SkinningMatrices[bone];
                }

                role.BoneBuffer.UpdateMatrices(role.BoneScratch);
            }
        }
    }

    private void RenderScene()
    {
        if (!_loaded) return;
        MakeCurrent();

        var now = _clock.Elapsed;
        var deltaSeconds = (float)(now - _lastUpdate).TotalSeconds;
        _lastUpdate = now;

        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        // See 13-GpuSkinnedInstancing's GLView for why this is wrapped: still reach SwapBuffers on
        // failure, and surface the first error (C# exception or a silent OpenGL error) once.
        try
        {
            UpdateAnimation(deltaSeconds);
            DrawCharacters();
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

    private void DrawCharacters()
    {
        if (_characters.Count == 0) return;

        _material!.Bind();
        _shader!.SetMatrix4("uView", _camera.GetViewMatrix());
        _shader.SetMatrix4("uProjection", _camera.GetProjectionMatrix());
        _shader.SetVector3("uCameraPosition", _camera.Position);
        _shader.SetVector3("uSunDirection", Vector3.Normalize(new(-1, -1, -.5f)));
        _shader.SetVector3("uFillPosition", _camera.Target + new Vector3(-200, 100, 200));
        _shader.SetVector3("uRimPosition", _camera.Target + new Vector3(200, 150, -200));
        _shader.SetVector3("uAmbientSkyColor", AmbientSkyColor);
        _shader.SetVector3("uAmbientGroundColor", AmbientGroundColor);

        foreach (var character in _characters)
        {
            character.InstanceBuffer.BindBase(1);
            foreach (var role in character.Roles)
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

    private void DisposeCharacters()
    {
        foreach (var character in _characters)
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
        _characters.Clear();
    }

    private void UnloadScene()
    {
        if (!_loaded) return;
        MakeCurrent();
        DisposeCharacters();
        _fallbackAlbedo?.Dispose();
        _fallbackNormal?.Dispose();
        _fallbackMask1?.Dispose();
        _fallbackMask2?.Dispose();
        if (_shader is not null) GL.DeleteProgram(_shader.Handle);
        _loaded = false;
    }
}
