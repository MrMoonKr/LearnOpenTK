using OpenTK.Graphics.OpenGL4;

namespace LearnOpenTK.Common.Graphics;

/// <summary>GPU buffer for unsigned integer indices (OpenGL ElementArrayBuffer).</summary>
public sealed class IndexBuffer : Buffer
{
    public IndexBuffer(uint[] indices, BufferUsageHint usage = BufferUsageHint.StaticDraw)
        : base(BufferTarget.ElementArrayBuffer)
    {
        Count = indices.Length;
        SetData(indices, usage);
    }

    public int Count { get; }
}
