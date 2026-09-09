using OpenTK.Graphics.OpenGL4;

namespace LearnOpenTK.Common.Graphics;

/// <summary>GPU buffer for vertex attributes (OpenGL ArrayBuffer).</summary>
public sealed class VertexBuffer : Buffer
{
    public VertexBuffer(float[] vertices, BufferUsageHint usage = BufferUsageHint.StaticDraw)
        : base(BufferTarget.ArrayBuffer)
    {
        SetData(vertices, usage);
    }
}
