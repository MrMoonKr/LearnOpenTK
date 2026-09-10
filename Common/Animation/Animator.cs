using System;
using System.Collections.Generic;
using OpenTK.Mathematics;

namespace LearnOpenTK.Common.Animation;

/// <summary>
/// Drives one skeleton's pose over time and derives the skinning matrix palette from it.
/// A hero body plays an <see cref="AnimationClip"/> directly; a wearable instead receives its
/// pose each frame from <see cref="SkeletonMerge"/> via <see cref="SetWorldMatrices"/>.
/// </summary>
public sealed class Animator
{
    private readonly Skeleton _skeleton;
    private readonly BonePose[] _samplePose;
    private readonly Matrix4[] _worldMatrices;
    private readonly Matrix4[] _skinningMatrices;
    private AnimationClip? _clip;
    private float _time;

    public Animator(Skeleton skeleton)
    {
        ArgumentNullException.ThrowIfNull(skeleton);
        _skeleton = skeleton;
        _samplePose = skeleton.BindLocalPose();
        _worldMatrices = skeleton.BindWorldMatrices();
        _skinningMatrices = new Matrix4[skeleton.BoneCount];
        RecomputeSkinningMatrices();
    }

    public Skeleton Skeleton => _skeleton;

    /// <summary>Model-space world matrix of each bone for the current pose.</summary>
    public IReadOnlyList<Matrix4> BoneWorldMatrices => _worldMatrices;

    /// <summary>Per-bone matrix (InverseBindPose * BoneWorld) ready to upload as the GPU skinning palette.</summary>
    public IReadOnlyList<Matrix4> SkinningMatrices => _skinningMatrices;

    public void Play(AnimationClip clip)
    {
        ArgumentNullException.ThrowIfNull(clip);
        _clip = clip;
        _time = 0f;
    }

    /// <summary>Advances the current clip (if any) and recomputes bone world and skinning matrices.</summary>
    public void Update(float deltaSeconds)
    {
        if (_clip is null) return;
        _time += deltaSeconds;
        _clip.Sample(_time, _samplePose);
        _skeleton.ComputeWorldMatrices(_samplePose, _worldMatrices);
        RecomputeSkinningMatrices();
    }

    /// <summary>Overrides this frame's bone world matrices with a pose merged from another skeleton (see <see cref="SkeletonMerge"/>).</summary>
    public void SetWorldMatrices(IReadOnlyList<Matrix4> worldMatrices)
    {
        for (var i = 0; i < _worldMatrices.Length; i++) _worldMatrices[i] = worldMatrices[i];
        RecomputeSkinningMatrices();
    }

    private void RecomputeSkinningMatrices()
    {
        var inverseBindPoses = _skeleton.InverseBindPoses;
        for (var i = 0; i < _skinningMatrices.Length; i++) _skinningMatrices[i] = inverseBindPoses[i] * _worldMatrices[i];
    }
}
