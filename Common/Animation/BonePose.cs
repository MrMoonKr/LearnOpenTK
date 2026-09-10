using OpenTK.Mathematics;

namespace LearnOpenTK.Common.Animation;

/// <summary>A bone's local transform relative to its parent, either a bind pose or one animation sample.</summary>
public readonly record struct BonePose(Vector3 Position, Quaternion Rotation, Vector3 Scale)
{
    public Matrix4 ToMatrix() => Matrix4.CreateScale(Scale) * Matrix4.CreateFromQuaternion(Rotation) * Matrix4.CreateTranslation(Position);
}
