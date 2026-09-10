#version 330 core

in vec3 vWorldPosition;
in vec3 vWorldNormal;
in vec2 vTexCoord;

uniform sampler2D uAlbedo;
uniform vec3 uCameraPosition;
uniform vec3 uSunDirection;
uniform vec3 uFillPosition;
uniform vec3 uRimPosition;

out vec4 fragmentColor;

vec3 pointLight(vec3 position, vec3 color, float strength, vec3 albedo, vec3 normal, vec3 viewDir)
{
    vec3 lightDir = normalize(position - vWorldPosition);
    float diffuse = max(dot(normal, lightDir), 0.0);
    vec3 halfway = normalize(lightDir + viewDir);
    float specular = pow(max(dot(normal, halfway), 0.0), 32.0);
    float distance = length(position - vWorldPosition);
    return (albedo * diffuse + vec3(specular)) * color * strength / (1.0 + distance * distance * 0.15);
}

void main()
{
    vec3 albedo = texture(uAlbedo, vTexCoord).rgb;
    vec3 normal = normalize(vWorldNormal);
    vec3 viewDir = normalize(uCameraPosition - vWorldPosition);

    float sunDiffuse = max(dot(normal, normalize(-uSunDirection)), 0.0);
    vec3 sun = albedo * (0.12 + sunDiffuse * 0.85);
    vec3 fill = pointLight(uFillPosition, vec3(0.35, 0.45, 1.0), 0.8, albedo, normal, viewDir);
    vec3 rim = pointLight(uRimPosition, vec3(1.0, 0.25, 0.15), 0.7, albedo, normal, viewDir);

    fragmentColor = vec4(sun + fill + rim, 1.0);
}
