using System.Collections.Generic;
using OpenTK.Mathematics;

namespace LearnOpenTK.Common.Scene;
public sealed class SceneNode
{
    public SceneNode(string name) => Name = name;
    public string Name { get; }
    public Vector3 Position { get; set; }
    public Vector3 Scale { get; set; } = Vector3.One;
    public Matrix4 Rotation { get; set; } = Matrix4.Identity;
    public List<SceneNode> Children { get; } = [];
    public Matrix4 GetWorldMatrix(Matrix4 parentWorld) => Matrix4.CreateScale(Scale) * Rotation * Matrix4.CreateTranslation(Position) * parentWorld;
}
