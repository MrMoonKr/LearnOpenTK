using System;
using System.Runtime.InteropServices;
using OpenTK.Graphics.OpenGL4;

namespace LearnOpenTK.Common.Graphics;

/// <summary>Base class that owns one OpenGL buffer object.</summary>
public abstract class Buffer : IDisposable
{
    private bool _disposed;

    protected Buffer(BufferTarget target)
    {
        Target = target;
        Handle = GL.GenBuffer();
    }

    public int Handle { get; }
    public BufferTarget Target { get; }

    public void Bind()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        GL.BindBuffer(Target, Handle);
    }

    protected void SetData<T>(T[] data, BufferUsageHint usage) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(data);
        Bind();
        GL.BufferData(Target, data.Length * Marshal.SizeOf<T>(), data, usage);
    }

    public void Dispose()
    {
        if (_disposed) return;
        GL.DeleteBuffer(Handle);
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
