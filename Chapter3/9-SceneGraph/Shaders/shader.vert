#version 330 core
layout(location=0)in vec3 aPosition;layout(location=1)in vec3 aColor;layout(location=2)in vec3 aNormal;uniform mat4 uModel;uniform mat4 uView;uniform mat4 uProjection;out vec3 worldPosition;out vec3 worldNormal;void main(){vec4 p=vec4(aPosition,1)*uModel;worldPosition=p.xyz;worldNormal=aNormal*mat3(uModel);gl_Position=p*uView*uProjection;}
