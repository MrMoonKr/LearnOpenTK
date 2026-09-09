using System;

namespace LearnOpenTK.Common.Rendering;

/// <summary>Describes the shader program used to draw a mesh.</summary>
public sealed class Material
{
    public Material(Shader shader)
    {
        Shader = shader ?? throw new ArgumentNullException(nameof(shader));
    }

    public Shader Shader { get; }

    public void Bind() => Shader.Use();
}
