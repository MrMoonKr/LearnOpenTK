using System;
using OpenTK.Graphics.OpenGL4;

namespace LearnOpenTK.Common.Graphics;

/// <summary>Owns a vertex array object and its vertex attribute layout.</summary>
public sealed class VertexArray : IDisposable
{
    private bool _disposed;

    public VertexArray()
    {
        Handle = GL.GenVertexArray();
    }

    public int Handle { get; }

    public void Bind()
    {
        ThrowIfDisposed();
        GL.BindVertexArray(Handle);
    }

    public void SetFloatAttribute(int location, int componentCount, int strideInBytes, int offsetInBytes, bool normalized = false)
    {
        Bind();
        GL.EnableVertexAttribArray(location);
        GL.VertexAttribPointer(location, componentCount, VertexAttribPointerType.Float, normalized, strideInBytes, offsetInBytes);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        GL.DeleteVertexArray(Handle);
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
