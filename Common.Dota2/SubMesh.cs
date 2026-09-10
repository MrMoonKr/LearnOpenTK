namespace LearnOpenTK.Common.Dota2;

/// <summary>A texture already decoded to raw BGRA8 pixels, ready for a GL texture upload.</summary>
public sealed record DecodedTexture(int Width, int Height, byte[] PixelsBgra);

/// <summary>
/// The geometry of one draw call, already interleaved to a fixed vertex layout so every chapter's
/// <c>Mesh</c> can describe it the same way: position(3), normal(3), uv(2), tangent+handedness(4),
/// bone indices(4), bone weights(4). Chapters that do not need tangents or bones (e.g.
/// 10-ModelLoading) simply ignore the offsets they don't use.
/// </summary>
public sealed class SubMesh
{
    public const int FloatsPerVertex = 20;
    public const int PositionOffset = 0;
    public const int NormalOffset = 3;
    public const int UvOffset = 6;

    /// <summary>xyz = tangent direction, w = bitangent handedness (+1/-1); see 14-GpuPbr for how this feeds normal mapping.</summary>
    public const int TangentOffset = 8;
    public const int BoneIndexOffset = 12;
    public const int BoneWeightOffset = 16;

    public required float[] Vertices { get; init; }
    public required uint[] Indices { get; init; }

    /// <summary>The material's base color/diffuse texture ("g_tColor"), or null if it has none.</summary>
    public DecodedTexture? Albedo { get; init; }

    /// <summary>Tangent-space normal map ("g_tNormal"), or null if the material has none.</summary>
    public DecodedTexture? Normal { get; init; }

    /// <summary>Dota 2 hero material's first packed mask texture, or null if the material has none. See 14-GpuPbr's README for what each channel holds.</summary>
    public DecodedTexture? Mask1 { get; init; }

    /// <summary>Dota 2 hero material's second packed mask texture, or null if the material has none. See 14-GpuPbr's README for what each channel holds.</summary>
    public DecodedTexture? Mask2 { get; init; }
}
