namespace LearnOpenTK.Common.Geometry;

/// <summary>A flat, single-quad XZ ground plane with an upward normal.</summary>
public static class PlaneGeometry
{
    // position xyz, white color rgb, upward normal xyz; one quad centered at the origin.
    public static float[] LitVertices(float halfSize) =>
    [
        -halfSize, 0, -halfSize, 1, 1, 1, 0, 1, 0,
         halfSize, 0, -halfSize, 1, 1, 1, 0, 1, 0,
         halfSize, 0,  halfSize, 1, 1, 1, 0, 1, 0,
        -halfSize, 0,  halfSize, 1, 1, 1, 0, 1, 0,
    ];

    public static readonly uint[] Indices = [0, 1, 2, 2, 3, 0];
}
