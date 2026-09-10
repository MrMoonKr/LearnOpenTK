#version 430 core

in vec3 vWorldPosition;
in vec3 vWorldNormal;
in vec3 vWorldTangent;
in float vTangentHandedness;
in vec2 vTexCoord;

uniform sampler2D uAlbedo;
uniform sampler2D uNormal;
uniform sampler2D uMask1;
uniform sampler2D uMask2;

uniform vec3 uCameraPosition;
uniform vec3 uSunDirection;
uniform vec3 uFillPosition;
uniform vec3 uRimPosition;
uniform vec3 uAmbientSkyColor;
uniform vec3 uAmbientGroundColor;

out vec4 fragmentColor;

// Dota 2 hero materials store normals DXT5nm-style: X in Alpha, Y in Green (not R/G, that would be
// the more common BC5 convention and produces a lighting seam at mirrored UV islands). Y is stored
// inverted, and Z is reconstructed since only two components are kept.
vec3 UnpackNormal(vec4 packedNormal)
{
    vec2 xy = packedNormal.ag * 2.0 - 1.0;
    xy.y = -xy.y;
    float z = sqrt(clamp(1.0 - dot(xy, xy), 0.0, 1.0));
    return vec3(xy, z);
}

// One point light's diffuse + specular contribution, falling off with distance. This is the same
// Key+Fill+Rim arrangement every chapter since 7-Lighting uses; what is new here is that the
// diffuse/specular color and the specular exponent all come from the material's mask textures
// instead of being fixed per example.
vec3 PointLight(vec3 lightPosition, vec3 lightColor, float lightStrength, vec3 diffuseColor, vec3 specularColor,
    float specularExponent, float specularIntensity, float illumination, vec3 normal, vec3 viewDir)
{
    vec3 lightDir = normalize(lightPosition - vWorldPosition);
    float diffuse = max(dot(normal, lightDir), 0.0);
    vec3 halfway = normalize(lightDir + viewDir);
    float specular = pow(max(dot(normal, halfway), 0.0), specularExponent) * illumination * specularIntensity;
    float distance = length(lightPosition - vWorldPosition);
    return (diffuseColor * diffuse + specularColor * specular) * lightColor * lightStrength / (1.0 + distance * distance * .15);
}

void main()
{
    vec4 albedoSample = texture(uAlbedo, vTexCoord);
    vec3 albedo = albedoSample.rgb;

    vec3 tangent = normalize(vWorldTangent);
    vec3 geometricNormal = normalize(vWorldNormal);
    vec3 bitangent = cross(geometricNormal, tangent) * vTangentHandedness;
    mat3 tangentToWorld = mat3(tangent, bitangent, geometricNormal);
    vec3 normal = normalize(tangentToWorld * UnpackNormal(texture(uNormal, vTexCoord)));

    // g_tMasks1: b = metalness (darkens diffuse, Dota heroes have no true metallic/dielectric split),
    // a = self-illumination (added straight into the lit fraction).
    vec4 mask1 = texture(uMask1, vTexCoord);
    float metalness = mask1.b;
    float selfIllumination = mask1.a;

    // g_tMasks2: r = specular intensity, g = a static per-material Fresnel rim mask (not a real
    // light, unlike this scene's own Rim point light below), b = how much the specular highlight
    // tints toward albedo, a = scales the base specular exponent.
    vec4 mask2 = texture(uMask2, vTexCoord);
    float specularIntensity = mask2.r;
    float materialRimMask = mask2.g;
    float specularTint = mask2.b;
    float specularExponent = max(mask2.a * 100.0, 1.0);

    vec3 diffuseColor = albedo * mix(1.0, 0.5, metalness);
    vec3 specularColor = mix(vec3(1.0), albedo, specularTint);
    vec3 viewDir = normalize(uCameraPosition - vWorldPosition);

    float sunDiffuse = max(dot(normal, normalize(-uSunDirection)), 0.0);
    float illumination = (.12 + sunDiffuse * .85) + selfIllumination;
    vec3 sun = diffuseColor * illumination;

    vec3 fill = PointLight(uFillPosition, vec3(.35, .45, 1.0), .8, diffuseColor, specularColor, specularExponent, specularIntensity, illumination, normal, viewDir);
    vec3 rim = PointLight(uRimPosition, vec3(1.0, .25, .15), .7, diffuseColor, specularColor, specularExponent, specularIntensity, illumination, normal, viewDir);

    // The material's own baked-in rim mask: a view-angle Fresnel term, always present regardless of
    // where any light sits, tinted by the albedo (matches dota_hero.frag.slang's rim term).
    float fresnel = pow(1.0 - max(dot(normal, viewDir), 0.0), 2.0) * materialRimMask;
    vec3 materialRim = albedo * fresnel;

    // Flat two-color hemisphere ambient by world-space normal.y, so the sides the three point/sun
    // lights above don't reach are not pure black. This is the same fallback dota2-projects' own
    // hero viewport uses when it has no environment cubemap loaded; a real prefiltered-cubemap IBL
    // pass (which that project also supports) is a possible upgrade but needs a real environment
    // texture, so this project starts with the honest zero-asset baseline.
    float skyWeight = clamp(normal.y * 0.5 + 0.5, 0.0, 1.0);
    vec3 ambient = mix(uAmbientGroundColor, uAmbientSkyColor, skyWeight) * diffuseColor;

    fragmentColor = vec4(ambient + sun + fill + rim + materialRim, albedoSample.a);
}
