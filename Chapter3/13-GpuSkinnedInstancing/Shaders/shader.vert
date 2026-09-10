#version 430 core

layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec3 aNormal;
layout(location = 2) in vec2 aTexCoord;
layout(location = 3) in vec4 aBoneIndices;
layout(location = 4) in vec4 aBoneWeights;

uniform mat4 uView;
uniform mat4 uProjection;
uniform int uBoneCountPerInstance;

// One bone matrix palette per instance, back to back: instance i occupies
// bBones[i * uBoneCountPerInstance .. i * uBoneCountPerInstance + uBoneCountPerInstance - 1].
// Unlike a plain "uniform mat4 uBones[64]" (12-GpuSkinning) this array has no fixed size, so one
// draw call can skin any number of instances without a shader-side cap.
layout(std430, binding = 0) readonly buffer BoneBuffer
{
    mat4 bBones[];
};

// One world-placement matrix per instance (already includes the Source-to-Y-up axis fix, see
// GLView.SourceToWorldUp), indexed the same way as gl_InstanceID.
layout(std430, binding = 1) readonly buffer InstanceBuffer
{
    mat4 bInstanceTransform[];
};

out vec3 vWorldPosition;
out vec3 vWorldNormal;
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

    mat4 instanceTransform = bInstanceTransform[gl_InstanceID];
    vec4 worldPosition = skinnedPosition * instanceTransform;
    vWorldPosition = worldPosition.xyz;
    vWorldNormal = skinnedNormal * mat3(instanceTransform);
    vTexCoord = aTexCoord;
    gl_Position = worldPosition * uView * uProjection;
}
