namespace LearnOpenTK.Common.Geometry;

/// <summary>Cube geometry with per-face normals for lit rendering.</summary>
public static class CubeGeometry
{
    // position xyz, white color rgb, face normal xyz; four independent vertices per face.
    public static readonly float[] LitVertices =
    [
        -.5f,-.5f,.5f,1,1,1,0,0,1, .5f,-.5f,.5f,1,1,1,0,0,1, .5f,.5f,.5f,1,1,1,0,0,1, -.5f,.5f,.5f,1,1,1,0,0,1,
        .5f,-.5f,-.5f,1,1,1,0,0,-1, -.5f,-.5f,-.5f,1,1,1,0,0,-1, -.5f,.5f,-.5f,1,1,1,0,0,-1, .5f,.5f,-.5f,1,1,1,0,0,-1,
        -.5f,-.5f,-.5f,1,1,1,-1,0,0, -.5f,-.5f,.5f,1,1,1,-1,0,0, -.5f,.5f,.5f,1,1,1,-1,0,0, -.5f,.5f,-.5f,1,1,1,-1,0,0,
        .5f,-.5f,.5f,1,1,1,1,0,0, .5f,-.5f,-.5f,1,1,1,1,0,0, .5f,.5f,-.5f,1,1,1,1,0,0, .5f,.5f,.5f,1,1,1,1,0,0,
        -.5f,.5f,.5f,1,1,1,0,1,0, .5f,.5f,.5f,1,1,1,0,1,0, .5f,.5f,-.5f,1,1,1,0,1,0, -.5f,.5f,-.5f,1,1,1,0,1,0,
        -.5f,-.5f,-.5f,1,1,1,0,-1,0, .5f,-.5f,-.5f,1,1,1,0,-1,0, .5f,-.5f,.5f,1,1,1,0,-1,0, -.5f,-.5f,.5f,1,1,1,0,-1,0,
    ];
    public static readonly uint[] Indices = [0,1,2,2,3,0,4,5,6,6,7,4,8,9,10,10,11,8,12,13,14,14,15,12,16,17,18,18,19,16,20,21,22,22,23,20];
}
