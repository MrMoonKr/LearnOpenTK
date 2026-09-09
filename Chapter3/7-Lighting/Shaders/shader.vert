#version 330 core
layout(location=0) in vec3 aPosition; layout(location=1) in vec3 aColor; layout(location=2) in vec3 aNormal;
uniform mat4 uModel; uniform mat4 uView; uniform mat4 uProjection;
out vec3 worldPosition; out vec3 worldNormal; out vec3 baseColor;
void main(){vec4 p=vec4(aPosition,1.0)*uModel;worldPosition=p.xyz;worldNormal=aNormal*mat3(uModel);baseColor=vec3(1.0);gl_Position=p*uView*uProjection;}
