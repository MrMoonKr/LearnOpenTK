#version 330 core

in vec3 vWorldPosition;
in vec3 vWorldNormal;
in vec3 vBaseColor;

uniform vec3 uCameraPosition;
uniform vec3 uSunDirection;
uniform vec3 uFillPosition;
uniform vec3 uRimPosition;
uniform vec3 uGroundColor;

out vec4 fragmentColor;

// Same Key+Fill+Rim point-light technique as every lit chapter since 7-Lighting, kept as its own tiny
// shader (instead of reusing shader.frag's PBR hero shader) so the ground's lighting stays legible on
// its own - it has no textures or mask maps, just a flat base color.
vec3 PointLight(vec3 lightPosition, vec3 lightColor, float lightStrength, vec3 normal, vec3 viewDir)
{
    vec3 lightDir = normalize(lightPosition - vWorldPosition);
    float diffuse = max(dot(normal, lightDir), 0.0);
    vec3 halfway = normalize(lightDir + viewDir);
    float specular = pow(max(dot(normal, halfway), 0.0), 32.0);
    float distance = length(lightPosition - vWorldPosition);
    return (vBaseColor * uGroundColor * diffuse + vec3(specular) * .1) * lightColor * lightStrength / (1.0 + distance * distance * .15);
}

void main()
{
    vec3 normal = normalize(vWorldNormal);
    vec3 viewDir = normalize(uCameraPosition - vWorldPosition);

    float sunDiffuse = max(dot(normal, normalize(-uSunDirection)), 0.0);
    vec3 sun = vBaseColor * uGroundColor * (.12 + sunDiffuse * .85);

    vec3 fill = PointLight(uFillPosition, vec3(.35, .45, 1.0), .8, normal, viewDir);
    vec3 rim = PointLight(uRimPosition, vec3(1.0, .25, .15), .7, normal, viewDir);

    fragmentColor = vec4(sun + fill + rim, 1.0);
}
