using OpenTK.Mathematics;

namespace LearnOpenTK.Common.Animation;

/// <summary>One joint in a <see cref="Skeleton"/>: a name, its parent, and its bind-pose local transform.</summary>
public sealed class Bone
{
    public Bone(string name, int parentIndex, Vector3 bindPosition, Quaternion bindRotation, Vector3 bindScale)
    {
        Name = name;
        ParentIndex = parentIndex;
        BindPosition = bindPosition;
        BindRotation = bindRotation;
        BindScale = bindScale;
    }

    public string Name { get; }

    /// <summary>Index into the owning <see cref="Skeleton"/>'s bone list, or -1 for a root bone.</summary>
    public int ParentIndex { get; }
    public Vector3 BindPosition { get; }
    public Quaternion BindRotation { get; }
    public Vector3 BindScale { get; }
}
