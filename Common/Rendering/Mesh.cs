using System;
using System.Collections.Generic;
using LearnOpenTK.Common.Graphics;
using OpenTK.Graphics.OpenGL4;

namespace LearnOpenTK.Common.Rendering;

/// <summary>One vertex shader input: its attribute location, component count, and float offset into the vertex.</summary>
public readonly record struct VertexAttribute(int Location, int ComponentCount, int OffsetFloats);

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

    /// <summary>Builds a mesh from an explicit vertex layout, for data that does not fit the position/color/normal shape above (e.g. imported models with UVs and skinning weights).</summary>
    public Mesh(float[] vertices, uint[] indices, int floatsPerVertex, IReadOnlyList<VertexAttribute> attributes, BufferUsageHint vertexUsage = BufferUsageHint.StaticDraw)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(floatsPerVertex, 1);
        VertexArray = new VertexArray();
        VertexArray.Bind();
        VertexBuffer = new VertexBuffer(vertices, vertexUsage);
        IndexBuffer = new IndexBuffer(indices);
        var stride = floatsPerVertex * sizeof(float);
        foreach (var attribute in attributes)
        {
            VertexArray.SetFloatAttribute(attribute.Location, attribute.ComponentCount, stride, attribute.OffsetFloats * sizeof(float));
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

    /// <summary>Draws <paramref name="instanceCount"/> copies of this mesh in one call; the vertex shader reads <c>gl_InstanceID</c> to tell them apart.</summary>
    public void DrawInstanced(int instanceCount)
    {
        VertexArray.Bind();
        GL.DrawElementsInstanced(PrimitiveType.Triangles, IndexBuffer.Count, DrawElementsType.UnsignedInt, IntPtr.Zero, instanceCount);
    }

    public void Dispose()
    {
        IndexBuffer.Dispose();
        VertexBuffer.Dispose();
        VertexArray.Dispose();
        GC.SuppressFinalize(this);
    }
}
