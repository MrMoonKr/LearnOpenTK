#version 330 core

layout(location = 0) in vec3 aPosition;
layout(location = 1) in vec3 aColor;
layout(location = 2) in vec3 aNormal;

uniform mat4 uModel;
uniform mat4 uView;
uniform mat4 uProjection;

out vec3 vWorldPosition;
out vec3 vWorldNormal;
out vec3 vBaseColor;

void main()
{
    vec4 worldPosition = vec4(aPosition, 1.0) * uModel;
    vWorldPosition = worldPosition.xyz;
    vWorldNormal = aNormal * mat3(uModel);
    vBaseColor = aColor;
    gl_Position = worldPosition * uView * uProjection;
}
