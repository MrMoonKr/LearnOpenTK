using System;
using OpenTK.Graphics.OpenGL4;

namespace LearnOpenTK.Common.Graphics;

/// <summary>Owns a 2D OpenGL texture uploaded once from raw BGRA8 pixel data.</summary>
public sealed class Texture2D : IDisposable
{
    private bool _disposed;

    public Texture2D(int width, int height, byte[] bgraPixels)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(width, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(height, 1);
        ArgumentNullException.ThrowIfNull(bgraPixels);
        Handle = GL.GenTexture();
        Bind();
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Repeat);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Repeat);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.LinearMipmapLinear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba8, width, height, 0, PixelFormat.Bgra, PixelType.UnsignedByte, bgraPixels);
        GL.GenerateMipmap(GenerateMipmapTarget.Texture2D);
    }

    public int Handle { get; }

    public void Bind(TextureUnit unit = TextureUnit.Texture0)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        GL.ActiveTexture(unit);
        GL.BindTexture(TextureTarget.Texture2D, Handle);
    }

    public void Dispose()
    {
        if (_disposed) return;
        GL.DeleteTexture(Handle);
        _disposed = true;
        GC.SuppressFinalize(this);
    }
}
