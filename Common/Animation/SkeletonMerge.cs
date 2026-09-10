using System;
using System.Collections.Generic;
using OpenTK.Mathematics;

namespace LearnOpenTK.Common.Animation;

/// <summary>
/// Places a wearable's skeleton onto a hero body's current pose by matching bone names, so a
/// separately rigged model (weapon, cape, head piece) follows the body's animation with no
/// attachment sockets or per-mesh parent bone required.
/// </summary>
public static class SkeletonMerge
{
    /// <summary>Indexes a skeleton's current world matrices by lowercase bone name for lookup by a wearable.</summary>
    public static Dictionary<string, Matrix4> BuildBoneMatrixByName(Skeleton skeleton, IReadOnlyList<Matrix4> worldMatrices)
    {
        var byName = new Dictionary<string, Matrix4>(skeleton.BoneCount, StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < skeleton.BoneCount; i++) byName[skeleton.Bones[i].Name] = worldMatrices[i];
        return byName;
    }

    /// <summary>
    /// For each bone in <paramref name="wearableSkeleton"/>, uses the body's world matrix for a
    /// same-named bone if one exists, otherwise falls back to the wearable's own bind pose.
    /// </summary>
    public static Matrix4[] MergePose(Skeleton wearableSkeleton, IReadOnlyDictionary<string, Matrix4> bodyBoneMatrixByName)
    {
        var bindWorld = wearableSkeleton.BindWorldMatrices();
        var merged = new Matrix4[wearableSkeleton.BoneCount];
        for (var i = 0; i < wearableSkeleton.BoneCount; i++)
        {
            merged[i] = bodyBoneMatrixByName.TryGetValue(wearableSkeleton.Bones[i].Name, out var bodyMatrix) ? bodyMatrix : bindWorld[i];
        }
        return merged;
    }
}
