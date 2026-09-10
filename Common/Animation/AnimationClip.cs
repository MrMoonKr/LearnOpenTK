using System;
using System.Collections.Generic;
using OpenTK.Mathematics;

namespace LearnOpenTK.Common.Animation;

/// <summary>A named, looping sequence of per-bone local poses sampled at a fixed frame rate.</summary>
public sealed class AnimationClip
{
    public AnimationClip(string name, float frameRate, IReadOnlyList<BonePose[]> frames)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(frameRate, 0);
        ArgumentNullException.ThrowIfNull(frames);
        ArgumentOutOfRangeException.ThrowIfZero(frames.Count);
        Name = name;
        FrameRate = frameRate;
        Frames = frames;
    }

    public string Name { get; }
    public float FrameRate { get; }

    /// <summary>Frames[frameIndex][boneIndex]. Every frame has one pose per bone in the target skeleton.</summary>
    public IReadOnlyList<BonePose[]> Frames { get; }
    public float Duration => Frames.Count / FrameRate;

    /// <summary>Samples the clip at <paramref name="time"/> seconds (looping) with linear/slerp interpolation between the surrounding frames.</summary>
    public void Sample(float time, Span<BonePose> outPose)
    {
        var frameCount = Frames.Count;
        var t = time * FrameRate % frameCount;
        if (t < 0) t += frameCount;
        var frameA = (int)t;
        var frameB = (frameA + 1) % frameCount;
        var blend = t - frameA;
        var poseA = Frames[frameA];
        var poseB = Frames[frameB];
        for (var i = 0; i < outPose.Length; i++)
        {
            outPose[i] = new BonePose(
                Vector3.Lerp(poseA[i].Position, poseB[i].Position, blend),
                Quaternion.Slerp(poseA[i].Rotation, poseB[i].Rotation, blend),
                Vector3.Lerp(poseA[i].Scale, poseB[i].Scale, blend));
        }
    }
}
