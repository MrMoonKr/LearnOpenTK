#version 330 core

layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec3 aNormal;
layout(location = 2) in vec2 aTexCoord;
layout(location = 3) in vec4 aBoneIndices;
layout(location = 4) in vec4 aBoneWeights;

uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uProjection;
uniform mat4 uBones[64];

out vec3 vWorldPosition;
out vec3 vWorldNormal;
out vec2 vTexCoord;

void main()
{
    // Linear-blend skinning: each bone contributes its (inverse bind pose * current world) matrix,
    // weighted, instead of the CPU doing this same blend per vertex every frame (compare 11-SkeletalAnimation).
    mat4 skin =
        aBoneWeights.x * uBones[int(aBoneIndices.x)] +
        aBoneWeights.y * uBones[int(aBoneIndices.y)] +
        aBoneWeights.z * uBones[int(aBoneIndices.z)] +
        aBoneWeights.w * uBones[int(aBoneIndices.w)];

    vec4 skinnedPosition = vec4(aPosition, 1.0) * skin;
    vec3 skinnedNormal = aNormal * mat3(skin);

    vec4 worldPosition = skinnedPosition * uModel;
    vWorldPosition = worldPosition.xyz;
    vWorldNormal = skinnedNormal * mat3(uModel);
    vTexCoord = aTexCoord;
    gl_Position = worldPosition * uView * uProjection;
}
