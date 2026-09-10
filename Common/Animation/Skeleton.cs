using System;
using System.Collections.Generic;
using OpenTK.Mathematics;

namespace LearnOpenTK.Common.Animation;

/// <summary>A bone hierarchy in parent-before-child order, with each bone's bind-pose inverse precomputed for skinning.</summary>
public sealed class Skeleton
{
    public Skeleton(IReadOnlyList<Bone> bones)
    {
        ArgumentNullException.ThrowIfNull(bones);
        Bones = bones;
        InverseBindPoses = ComputeInverseBindPoses(bones);
    }

    public IReadOnlyList<Bone> Bones { get; }
    public int BoneCount => Bones.Count;

    /// <summary>Model-space inverse of each bone's bind pose, indexed the same as <see cref="Bones"/>.</summary>
    public IReadOnlyList<Matrix4> InverseBindPoses { get; }

    /// <summary>Finds a bone by name, case-insensitively. Used to merge a wearable's skeleton onto a hero body's pose.</summary>
    public int FindBoneIndex(string name)
    {
        for (var i = 0; i < Bones.Count; i++)
        {
            if (string.Equals(Bones[i].Name, name, StringComparison.OrdinalIgnoreCase)) return i;
        }
        return -1;
    }

    /// <summary>Walks the hierarchy turning each bone's local pose into a model-space world matrix.</summary>
    public void ComputeWorldMatrices(IReadOnlyList<BonePose> localPose, Matrix4[] worldMatrices)
    {
        for (var i = 0; i < Bones.Count; i++)
        {
            var local = localPose[i].ToMatrix();
            var parentIndex = Bones[i].ParentIndex;
            worldMatrices[i] = parentIndex < 0 ? local : local * worldMatrices[parentIndex];
        }
    }

    /// <summary>The skeleton's own bind pose, expressed as local poses (identity animation).</summary>
    public BonePose[] BindLocalPose()
    {
        var pose = new BonePose[Bones.Count];
        for (var i = 0; i < Bones.Count; i++) pose[i] = new BonePose(Bones[i].BindPosition, Bones[i].BindRotation, Bones[i].BindScale);
        return pose;
    }

    /// <summary>The skeleton's own bind pose, expressed as model-space world matrices.</summary>
    public Matrix4[] BindWorldMatrices()
    {
        var world = new Matrix4[Bones.Count];
        ComputeWorldMatrices(BindLocalPose(), world);
        return world;
    }

    private static Matrix4[] ComputeInverseBindPoses(IReadOnlyList<Bone> bones)
    {
        var world = new Matrix4[bones.Count];
        for (var i = 0; i < bones.Count; i++)
        {
            var bone = bones[i];
            var local = new BonePose(bone.BindPosition, bone.BindRotation, bone.BindScale).ToMatrix();
            world[i] = bone.ParentIndex < 0 ? local : local * world[bone.ParentIndex];
        }

        var inverse = new Matrix4[bones.Count];
        for (var i = 0; i < bones.Count; i++) inverse[i] = Matrix4.Invert(world[i]);
        return inverse;
    }
}
