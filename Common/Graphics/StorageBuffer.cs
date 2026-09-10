using OpenTK.Graphics.OpenGL4;
using OpenTK.Mathematics;

namespace LearnOpenTK.Common.Graphics;

/// <summary>
/// GPU buffer for large per-instance arrays a shader reads through a shader storage block
/// (<c>layout(std430, binding = N) buffer ... { ... }</c>), e.g. one bone matrix palette per instance
/// in an instanced draw. Unlike a plain uniform array, its size is not fixed at shader compile time.
/// </summary>
public sealed class StorageBuffer : Buffer
{
    public StorageBuffer() : base(BufferTarget.ShaderStorageBuffer)
    {
    }

    /// <summary>(Re)allocates the buffer's storage and uploads its initial contents.</summary>
    public void Allocate<T>(T[] data, BufferUsageHint usage = BufferUsageHint.DynamicDraw) where T : unmanaged => SetData(data, usage);

    /// <summary>
    /// Allocates storage for a <c>mat4[]</c> shader storage block and uploads it, transposing each
    /// matrix first. <see cref="Matrix4"/> stores its data row-major to match this repository's
    /// row-vector convention (<c>vec4(position, 1.0) * uModel</c>), but a raw buffer upload has no
    /// "transpose" flag the way <see cref="GL.UniformMatrix4(int, bool, ref Matrix4)"/> does, so the
    /// GPU would otherwise read row-major bytes as GLSL's column-major <c>mat4</c> and silently use
    /// the transpose of every matrix (this scrambles skinning in particular: it visibly looks like
    /// bones are composed in the wrong order, not like a uniformly wrong pose).
    /// </summary>
    public void AllocateMatrices(Matrix4[] matrices, BufferUsageHint usage = BufferUsageHint.DynamicDraw) => Allocate(Transposed(matrices), usage);

    /// <summary>Overwrites a <c>mat4[]</c> shader storage block allocated with <see cref="AllocateMatrices"/>, transposing each matrix the same way.</summary>
    public void UpdateMatrices(Matrix4[] matrices) => Update(Transposed(matrices));

    /// <summary>Binds this buffer to an indexed binding point, matching a shader's <c>layout(std430, binding = bindingIndex)</c>.</summary>
    public void BindBase(int bindingIndex) => GL.BindBufferBase(BufferRangeTarget.ShaderStorageBuffer, bindingIndex, Handle);

    private static Matrix4[] Transposed(Matrix4[] matrices)
    {
        var transposed = new Matrix4[matrices.Length];
        for (var i = 0; i < matrices.Length; i++) transposed[i] = Matrix4.Transpose(matrices[i]);
        return transposed;
    }
}
