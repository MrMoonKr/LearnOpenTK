#version 330 core

layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec3 aNormal;
layout(location = 2) in vec2 aTexCoord;

uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uProjection;

out vec3 vWorldPosition;
out vec3 vWorldNormal;
out vec2 vTexCoord;

void main()
{
    vec4 worldPosition = vec4(aPosition, 1.0) * uModel;
    vWorldPosition = worldPosition.xyz;
    vWorldNormal = aNormal * mat3(uModel);
    vTexCoord = aTexCoord;
    gl_Position = worldPosition * uView * uProjection;
}
