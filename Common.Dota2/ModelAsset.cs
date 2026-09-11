using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using LearnOpenTK.Common.Animation;
using ValveResourceFormat.IO;
using ValveResourceFormat.Serialization.KeyValues;
using VBIB = ValveResourceFormat.Blocks.VBIB;
using VMaterial = ValveResourceFormat.ResourceTypes.Material;
using VMesh = ValveResourceFormat.ResourceTypes.Mesh;
using VModel = ValveResourceFormat.ResourceTypes.Model;
using VTexture = ValveResourceFormat.ResourceTypes.Texture;
using VrfBone = ValveResourceFormat.ResourceTypes.ModelAnimation.Bone;
using VrfSkeleton = ValveResourceFormat.ResourceTypes.ModelAnimation.Skeleton;
using VSequenceAnimation = ValveResourceFormat.ResourceTypes.ModelAnimation.SequenceAnimation;

namespace LearnOpenTK.Common.Dota2;

/// <summary>
/// One raw Source-1-style acttable entry a clip carries (e.g. Name "ACT_DOTA_IDLE"). Weight is the
/// original acttable's selection weight - when several clips share the same activity (e.g. multiple
/// idle variants), the game picks among them more often in proportion to this value, so it is also a
/// reasonable signal for which one is the "main" variant vs. a rare/alternate one.
/// </summary>
public readonly record struct HeroAnimationActivity(string Name, int Weight);

/// <summary>A loaded Source 2 model: its geometry (already interleaved for GPU upload), skeleton, and animation clips.</summary>
public sealed class ModelAsset
{
    private const float BakedFrameRate = 30f;

    public required IReadOnlyList<SubMesh> SubMeshes { get; init; }
    public Skeleton? Skeleton { get; init; }
    public required IReadOnlyDictionary<string, AnimationClip> Animations { get; init; }

    /// <summary>
    /// Each clip's raw Source-1-style activity entries (e.g. "ACT_DOTA_IDLE"), used by
    /// <see cref="HeroAnimationClassifier"/> to infer idle/run/attack clips without guessing purely
    /// from clip-name keywords. Empty for a clip if it carries no activity data - real Dota 2 assets do
    /// not populate this for every clip, so it is a hint, not a complete taxonomy.
    /// </summary>
    public required IReadOnlyDictionary<string, IReadOnlyList<HeroAnimationActivity>> AnimationActivities { get; init; }

    /// <summary>Loads a model's highest-detail LOD: geometry, skeleton (if any), and every animation clip.</summary>
    public static ModelAsset Load(GameArchive archive, string modelPath)
    {
        ArgumentNullException.ThrowIfNull(archive);
        using var resource = archive.LoadCompiled(modelPath);
        var model = (VModel)resource.DataBlock!;

        var skeleton = ImportSkeleton(model.Skeleton);
        var (animations, activities) = ImportAnimations(model, archive.FileLoader, skeleton);
        var subMeshes = ImportSubMeshes(model, archive.FileLoader);
        return new ModelAsset { SubMeshes = subMeshes, Skeleton = skeleton, Animations = animations, AnimationActivities = activities };
    }

    private static Skeleton? ImportSkeleton(VrfSkeleton vrfSkeleton)
    {
        if (vrfSkeleton.Bones.Length == 0) return null;
        var bones = new Bone[vrfSkeleton.Bones.Length];
        foreach (VrfBone vrfBone in vrfSkeleton.Bones)
        {
            bones[vrfBone.Index] = new Bone(
                vrfBone.Name,
                vrfBone.Parent?.Index ?? -1,
                ToVector3(vrfBone.Position),
                ToQuaternion(vrfBone.Angle),
                OpenTK.Mathematics.Vector3.One);
        }
        return new Skeleton(bones);
    }

    private static (Dictionary<string, AnimationClip> Clips, Dictionary<string, IReadOnlyList<HeroAnimationActivity>> Activities) ImportAnimations(VModel model, IFileLoader fileLoader, Skeleton? skeleton)
    {
        var clips = new Dictionary<string, AnimationClip>(StringComparer.OrdinalIgnoreCase);
        var activities = new Dictionary<string, IReadOnlyList<HeroAnimationActivity>>(StringComparer.OrdinalIgnoreCase);
        if (skeleton is null) return (clips, activities);

        var vrfSkeleton = model.Skeleton;
        foreach (var animation in model.GetAllAnimations(fileLoader))
        {
            if (animation.FrameCount <= 0 || animation.Fps <= 0f) continue;

            // A fresh cache per clip avoids stale previous/next frames left over from the last clip sampled.
            var frameCache = new ValveResourceFormat.ResourceTypes.ModelAnimation.AnimationFrameCache(vrfSkeleton, model.FlexControllers);
            var bakedFrameCount = Math.Max(2, (int)MathF.Ceiling(animation.Duration * BakedFrameRate) + 1);
            var frames = new BonePose[bakedFrameCount][];
            for (var frameIndex = 0; frameIndex < bakedFrameCount; frameIndex++)
            {
                var frame = frameCache.GetInterpolatedFrame(animation, frameIndex / BakedFrameRate);
                var pose = new BonePose[vrfSkeleton.Bones.Length];
                for (var boneIndex = 0; boneIndex < pose.Length; boneIndex++)
                {
                    var frameBone = frame.Bones[boneIndex];
                    pose[boneIndex] = new BonePose(ToVector3(frameBone.Position), ToQuaternion(frameBone.Angle), new OpenTK.Mathematics.Vector3(frameBone.Scale));
                }
                frames[frameIndex] = pose;
            }

            clips[animation.Name] = new AnimationClip(animation.Name, BakedFrameRate, frames);
            activities[animation.Name] = animation is VSequenceAnimation sequence
                ? sequence.Activities.Select(a => new HeroAnimationActivity(a.Name, a.Weight)).ToList()
                : [];
        }

        return (clips, activities);
    }

    private static List<SubMesh> ImportSubMeshes(VModel model, IFileLoader fileLoader)
    {
        var subMeshes = new List<SubMesh>();
        var lowestLevel = model.LodInfo.LowestLevel;
        var materialCache = new Dictionary<string, HeroMaterial>();

        foreach (var (mesh, meshIndex, _, loDMask) in model.GetEmbeddedMeshesAndLoD())
        {
            if ((loDMask & (1L << lowestLevel)) == 0) continue;
            ImportMesh(mesh, model.GetRemapTable(meshIndex), fileLoader, materialCache, subMeshes);
        }

        foreach (var (meshIndex, meshName, loDMask) in model.GetReferenceMeshNamesAndLoD())
        {
            if ((loDMask & (1L << lowestLevel)) == 0) continue;
            using var meshResource = fileLoader.LoadFileCompiled(meshName);
            if (meshResource?.DataBlock is not VMesh referencedMesh) continue;
            ImportMesh(referencedMesh, model.GetRemapTable(meshIndex), fileLoader, materialCache, subMeshes);
        }

        return subMeshes;
    }

    private static void ImportMesh(VMesh mesh, int[]? remapTable, IFileLoader fileLoader,
        Dictionary<string, HeroMaterial> materialCache, List<SubMesh> output)
    {
        var vbib = mesh.VBIB;
        foreach (var sceneObject in mesh.Data.GetArray("m_sceneObjects"))
        {
            foreach (var drawCall in sceneObject.GetArray("m_drawCalls"))
            {
                // Most Source 2 meshes pack every attribute into a single vertex stream per draw call.
                var vertexBufferIndex = drawCall.GetArray("m_vertexBuffers")[0].GetInt32Property("m_hBuffer");
                var vertexBuffer = vbib.VertexBuffers[vertexBufferIndex];

                var indexBufferIndex = drawCall.GetSubCollection("m_indexBuffer").GetInt32Property("m_hBuffer");
                var indexBuffer = vbib.IndexBuffers[indexBufferIndex];

                var baseVertex = drawCall.GetInt32Property("m_nBaseVertex");
                var startIndex = drawCall.GetInt32Property("m_nStartIndex");
                var indexCount = drawCall.GetInt32Property("m_nIndexCount");
                var materialPath = drawCall.GetStringProperty("m_material") ?? drawCall.GetStringProperty("m_pMaterial");

                if (!materialCache.TryGetValue(materialPath, out var material))
                {
                    material = LoadHeroMaterial(fileLoader, materialPath);
                    materialCache[materialPath] = material;
                }

                output.Add(new SubMesh
                {
                    Vertices = BuildInterleavedVertices(vertexBuffer, remapTable),
                    Indices = ReadIndices(indexBuffer, startIndex, indexCount, baseVertex),
                    Albedo = material.Albedo,
                    Normal = material.Normal,
                    Mask1 = material.Mask1,
                    Mask2 = material.Mask2,
                });
            }
        }
    }

    private static float[] BuildInterleavedVertices(VBIB.OnDiskBufferData vertexBuffer, int[]? remapTable)
    {
        var vertexCount = (int)vertexBuffer.ElementCount;
        var positions = new System.Numerics.Vector3[vertexCount];
        var normals = Array.Empty<System.Numerics.Vector3>();
        var tangents = Array.Empty<System.Numerics.Vector4>();
        var uvs = Array.Empty<System.Numerics.Vector2>();
        ushort[]? blendIndices = null;
        System.Numerics.Vector4[]? blendWeights = null;

        foreach (var attribute in vertexBuffer.InputLayoutFields)
        {
            switch (attribute.SemanticName)
            {
                case "POSITION":
                    positions = VBIB.GetVector3AttributeArray(vertexBuffer, attribute);
                    break;
                case "NORMAL":
                    // Tangents (xyz + bitangent handedness in w) ride along with normals in the same
                    // attribute for most Source 2 meshes; 14-GpuPbr needs them for tangent-space normal mapping.
                    (normals, tangents) = VBIB.GetNormalTangentArray(vertexBuffer, attribute);
                    break;
                case "TEXCOORD" when VBIB.GetFormatInfo(attribute).ElementCount == 2:
                    uvs = VBIB.GetVector2AttributeArray(vertexBuffer, attribute);
                    break;
                case "BLENDINDICES":
                    // Assumes the common 4-bones-per-vertex layout; the rarer 8-bone packed format is not handled.
                    blendIndices = VBIB.GetBlendIndicesArray(vertexBuffer, attribute, remapTable);
                    break;
                case "BLENDWEIGHT" or "BLENDWEIGHTS":
                    blendWeights = VBIB.GetBlendWeightsArray(vertexBuffer, attribute);
                    break;
            }
        }

        var vertices = new float[vertexCount * SubMesh.FloatsPerVertex];
        for (var i = 0; i < vertexCount; i++)
        {
            var v = i * SubMesh.FloatsPerVertex;
            vertices[v + 0] = positions[i].X;
            vertices[v + 1] = positions[i].Y;
            vertices[v + 2] = positions[i].Z;
            vertices[v + 3] = i < normals.Length ? normals[i].X : 0f;
            vertices[v + 4] = i < normals.Length ? normals[i].Y : 0f;
            vertices[v + 5] = i < normals.Length ? normals[i].Z : 1f;
            vertices[v + 6] = i < uvs.Length ? uvs[i].X : 0f;
            vertices[v + 7] = i < uvs.Length ? uvs[i].Y : 0f;
            vertices[v + SubMesh.TangentOffset + 0] = i < tangents.Length ? tangents[i].X : 1f;
            vertices[v + SubMesh.TangentOffset + 1] = i < tangents.Length ? tangents[i].Y : 0f;
            vertices[v + SubMesh.TangentOffset + 2] = i < tangents.Length ? tangents[i].Z : 0f;
            vertices[v + SubMesh.TangentOffset + 3] = i < tangents.Length ? tangents[i].W : 1f;
            for (var bone = 0; bone < 4; bone++)
            {
                vertices[v + SubMesh.BoneIndexOffset + bone] = blendIndices != null && i * 4 + bone < blendIndices.Length ? blendIndices[i * 4 + bone] : 0f;
                vertices[v + SubMesh.BoneWeightOffset + bone] = blendWeights != null && i < blendWeights.Length
                    ? GetComponent(blendWeights[i], bone)
                    : bone == 0 ? 1f : 0f;
            }
        }

        return vertices;
    }

    private static float GetComponent(System.Numerics.Vector4 v, int component) => component switch
    {
        0 => v.X,
        1 => v.Y,
        2 => v.Z,
        _ => v.W,
    };

    private static uint[] ReadIndices(VBIB.OnDiskBufferData indexBuffer, int startIndex, int indexCount, int baseVertex)
    {
        var indices = new uint[indexCount];
        if (indexBuffer.ElementSizeInBytes == 2)
        {
            var source = MemoryMarshal.Cast<byte, ushort>(indexBuffer.Data);
            for (var i = 0; i < indexCount; i++) indices[i] = (uint)(source[startIndex + i] + baseVertex);
        }
        else
        {
            var source = MemoryMarshal.Cast<byte, uint>(indexBuffer.Data);
            for (var i = 0; i < indexCount; i++) indices[i] = (uint)(source[startIndex + i] + baseVertex);
        }

        return indices;
    }

    /// <summary>Every texture map 14-GpuPbr's shading model needs from one material, decoded once and reused for every draw call/instance that shares it.</summary>
    private sealed record HeroMaterial(DecodedTexture? Albedo, DecodedTexture? Normal, DecodedTexture? Mask1, DecodedTexture? Mask2);

    // TODO(14-GpuPbr): confirm these against ValveResourceFormat's ShaderDataProvider / the hero
    // material's actual m_textureParams once the mask-channel semantics are documented in the README.
    private const string AlbedoParam = "g_tColor";
    private const string NormalParam = "g_tNormal";
    private const string Mask1Param = "g_tMasks1";
    private const string Mask2Param = "g_tMasks2";

    private static HeroMaterial LoadHeroMaterial(IFileLoader fileLoader, string? materialPath)
    {
        if (string.IsNullOrEmpty(materialPath)) return new HeroMaterial(null, null, null, null);
        using var materialResource = fileLoader.LoadFileCompiled(materialPath);
        if (materialResource?.DataBlock is not VMaterial material) return new HeroMaterial(null, null, null, null);

        return new HeroMaterial(
            LoadTexture(fileLoader, material, AlbedoParam),
            LoadTexture(fileLoader, material, NormalParam),
            LoadTexture(fileLoader, material, Mask1Param),
            LoadTexture(fileLoader, material, Mask2Param));
    }

    private static DecodedTexture? LoadTexture(IFileLoader fileLoader, VMaterial material, string paramName)
    {
        if (!material.TextureParams.TryGetValue(paramName, out var texturePath)) return null;
        using var textureResource = fileLoader.LoadFileCompiled(texturePath);
        if (textureResource?.DataBlock is not VTexture texture) return null;
        using var bitmap = texture.GenerateBitmap();
        return new DecodedTexture(bitmap.Width, bitmap.Height, bitmap.Bytes);
    }

    private static OpenTK.Mathematics.Vector3 ToVector3(System.Numerics.Vector3 v) => new(v.X, v.Y, v.Z);
    private static OpenTK.Mathematics.Quaternion ToQuaternion(System.Numerics.Quaternion q) => new(q.X, q.Y, q.Z, q.W);
}
