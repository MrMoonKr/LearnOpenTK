using System;
using LearnOpenTK.Common.Graphics;
using OpenTK.Graphics.OpenGL4;

namespace LearnOpenTK.Common.Rendering;

/// <summary>Owns the GPU geometry needed for one indexed mesh.</summary>
public sealed class Mesh : IDisposable
{
    public Mesh(float[] vertices, uint[] indices, int floatsPerVertex, int positionOffset = 0, int colorOffset = 3, int? normalOffset = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(floatsPerVertex, 1);
        VertexArray = new VertexArray();
        VertexArray.Bind();
        VertexBuffer = new VertexBuffer(vertices);
        IndexBuffer = new IndexBuffer(indices);
        var stride = floatsPerVertex * sizeof(float);
        VertexArray.SetFloatAttribute(0, 3, stride, positionOffset * sizeof(float));
        VertexArray.SetFloatAttribute(1, 3, stride, colorOffset * sizeof(float));
        if (normalOffset.HasValue)
        {
            VertexArray.SetFloatAttribute(2, 3, stride, normalOffset.Value * sizeof(float));
        }
    }

    public VertexArray VertexArray { get; }
    public VertexBuffer VertexBuffer { get; }
    public IndexBuffer IndexBuffer { get; }

    public void Draw()
    {
        VertexArray.Bind();
        GL.DrawElements(PrimitiveType.Triangles, IndexBuffer.Count, DrawElementsType.UnsignedInt, IntPtr.Zero);
    }

    public void Dispose()
    {
        IndexBuffer.Dispose();
        VertexBuffer.Dispose();
        VertexArray.Dispose();
        GC.SuppressFinalize(this);
    }
}
