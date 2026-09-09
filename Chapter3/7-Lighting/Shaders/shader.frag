#version 330 core
in vec3 worldPosition; in vec3 worldNormal; in vec3 baseColor; out vec4 fragmentColor;
uniform vec3 uCameraPosition; uniform vec3 uSunDirection; uniform vec3 uFillPosition; uniform vec3 uRimPosition;
vec3 pointLight(vec3 position, vec3 color, float strength, vec3 n, vec3 viewDir){vec3 l=normalize(position-worldPosition);float diffuse=max(dot(n,l),0.0);vec3 h=normalize(l+viewDir);float specular=pow(max(dot(n,h),0.0),32.0);float d=length(position-worldPosition);return (baseColor*diffuse+vec3(specular))*color*strength/(1.0+d*d*.15);}
void main(){vec3 n=normalize(worldNormal);vec3 viewDir=normalize(uCameraPosition-worldPosition);float sunDiffuse=max(dot(n,normalize(-uSunDirection)),0.0);vec3 sun=baseColor*(.12+sunDiffuse*.85);vec3 fill=pointLight(uFillPosition,vec3(.35,.45,1),.8,n,viewDir);vec3 rim=pointLight(uRimPosition,vec3(1,.25,.15),.7,n,viewDir);fragmentColor=vec4(sun+fill+rim,1.0);}
