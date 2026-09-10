#version 430 core

layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec3 aNormal;
layout(location = 2) in vec2 aTexCoord;
layout(location = 3) in vec4 aTangent;  // xyz = tangent, w = bitangent handedness (+1/-1)
layout(location = 4) in vec4 aBoneIndices;
layout(location = 5) in vec4 aBoneWeights;

uniform mat4 uView;
uniform mat4 uProjection;
uniform int uBoneCountPerInstance;

// Same instancing scheme as 13-GpuSkinnedInstancing: one bone palette and one placement matrix per
// instance, both indexed by gl_InstanceID.
layout(std430, binding = 0) readonly buffer BoneBuffer
{
    mat4 bBones[];
};

layout(std430, binding = 1) readonly buffer InstanceBuffer
{
    mat4 bInstanceTransform[];
};

out vec3 vWorldPosition;
out vec3 vWorldNormal;
out vec3 vWorldTangent;
out float vTangentHandedness;
out vec2 vTexCoord;

void main()
{
    int boneBase = gl_InstanceID * uBoneCountPerInstance;
    mat4 skin =
        aBoneWeights.x * bBones[boneBase + int(aBoneIndices.x)] +
        aBoneWeights.y * bBones[boneBase + int(aBoneIndices.y)] +
        aBoneWeights.z * bBones[boneBase + int(aBoneIndices.z)] +
        aBoneWeights.w * bBones[boneBase + int(aBoneIndices.w)];

    vec4 skinnedPosition = vec4(aPosition, 1.0) * skin;
    vec3 skinnedNormal = aNormal * mat3(skin);
    vec3 skinnedTangent = aTangent.xyz * mat3(skin);

    mat4 instanceTransform = bInstanceTransform[gl_InstanceID];
    vec4 worldPosition = skinnedPosition * instanceTransform;
    vWorldPosition = worldPosition.xyz;
    vWorldNormal = normalize(skinnedNormal * mat3(instanceTransform));
    vWorldTangent = normalize(skinnedTangent * mat3(instanceTransform));
    vTangentHandedness = aTangent.w;
    vTexCoord = aTexCoord;
    gl_Position = worldPosition * uView * uProjection;
}
