#ifndef VOXELPLAY_COMMON_CORE
#define VOXELPLAY_COMMON_CORE

// disables warning of pow(f,e) with negative values
#pragma warning (disable : 3571)

#include "VPCommonOptions.cginc"
#include "VPCommonVertexModifier.cginc"
#include "VPCommonSeeThrough.cginc"
#include "VPCommonInput.cginc"
#include "VPCommonSky.cginc"
#include "VPCommonURPSSAO.cginc"

/* cube coords
   
  7+------+6
  /.   3 /|
2+------+ |
 |4.....|.+5
 |/     |/
0+------+1

*/

// Samplers  =====================================================================

SamplerState sampler_Point_Repeat;
SamplerState sampler_Linear_Repeat;
SamplerState sampler_Trilinear_Repeat;

#if FILTER_MODE == TRILINEAR_FILTER
	#define SAMPLER_NAME sampler_Trilinear_Repeat
	#define SAMPLER_MODE _Trilinear_Repeat
#elif FILTER_MODE == BILINEAR_FILTER
	#define SAMPLER_NAME sampler_Linear_Repeat
	#define SAMPLER_MODE _Linear_Repeat
#else
	#define SAMPLER_NAME sampler_Point_Repeat
	#define SAMPLER_MODE _Point_Repeat
#endif

sampler2D _VPMatProps;
float4 _vp_matProps;
#define NORMAL_MAP_OFFSET _vp_matProps.x
#define HEIGHT_MAP_OFFSET _vp_matProps.x
#define PBR_MAP_OFFSET _vp_matProps.y
#define EMISSION_MAP_OFFSET _vp_matProps.z

#define VOXELPLAY_MATPROPS_DATA half4 matProps : COLOR1;
#define VOXELPLAY_INITIALIZE_MATPROPS(o, uv) { int __iuvz = (int)uv.z; float __uvz = (float)(__iuvz & 16383); o.matProps = _vp_matProps = tex2Dlod(_VPMatProps, float4((__uvz + 0.5) * _VPMatProps_TexelSize.x, 0.5, 0, 0)) * 255; }
#define VOXELPLAY_READ_MATPROPS(i) _vp_matProps = i.matProps;

// Tint Gradient system =====================================================================

sampler2D _VPGradientLUT;
float4 _VPGradientLUT_TexelSize; // x=1/64, y=1/textureCount, z=64, w=textureCount

#define VOXELPLAY_GRADIENT_WPOS_DATA(idx) float3 gradWpos : TEXCOORD##idx;
#define VOXELPLAY_SET_GRADIENT_WPOS(o, wpos) o.gradWpos = wpos;

float vpGradientHash(float2 p) {
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

float vpGradientNoise(float2 p) {
    float2 i = floor(p);
    float2 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float a = vpGradientHash(i);
    float b = vpGradientHash(i + float2(1, 0));
    float c = vpGradientHash(i + float2(0, 1));
    float d = vpGradientHash(i + float2(1, 1));
    return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
}

float vpGradientNoise3D(float3 p) {
    float xy = vpGradientNoise(p.xy);
    float yz = vpGradientNoise(p.yz + 17.0);
    float xz = vpGradientNoise(p.xz + 31.0);
    return (xy + yz + xz) / 3.0;
}

#define VOXELPLAY_APPLY_GRADIENT_TINT(color, uv, wpos) { \
    float __gp = _vp_matProps.w; \
    UNITY_BRANCH if (__gp > 0.5) { \
        float __v = __gp - 1.0; \
        float __mode = floor(__v / 85.0); \
        float __rem = __v - __mode * 85.0; \
        float __scale = floor(__rem / 9.0) / 8.0 * 0.5; \
        float __intensity = (__rem - floor(__rem / 9.0) * 9.0) / 8.0; \
        float __noise; \
        if (__mode < 0.5) { \
            __noise = vpGradientNoise3D(wpos * __scale * 4.0); \
        } else if (__mode < 1.5) { \
            __noise = vpGradientNoise(float2(wpos.y * __scale * 10.0, 0.5)); \
        } else { \
            __noise = vpGradientNoise(wpos.xz * __scale * 4.0); \
        } \
        float __t = lerp(0.5, __noise, __intensity); \
        int __ti = (int)uv.z; \
        float __uvz = (float)(__ti & 16383); \
        float2 __lutUV = float2(__t, (__uvz + 0.5) * _VPGradientLUT_TexelSize.y); \
        color.rgb *= tex2D(_VPGradientLUT, __lutUV).rgb; \
    } \
}

// Globals  =====================================================================

#if defined(NO_AMBIENT)
    #define _VPAmbientLight 0
#else
    fixed _VPAmbientLight;
#endif

fixed2 _VPDiffuseWrap;

fixed _VPGrassWindSpeed, _VPTreeWindSpeed;
float3 _VPWorldPivot;

inline float3 VPGetWorldUVPosition(float3 wpos) {
    return wpos - _VPWorldPivot;
}

inline float2 VPGetWorldSpaceUV(float3 wpos, float3 normal) {
    float3 uvWPos = VPGetWorldUVPosition(wpos);
    return uvWPos.xz * float2(abs(normal.y), normal.y) + uvWPos.xy * float2(-normal.z, abs(normal.z)) + uvWPos.zy * float2(normal.x, abs(normal.x));
}

// Tinting =====================================================================

#if defined(USES_TINTING)
	#define VOXELPLAY_TINTCOLOR_DATA fixed4 color : COLOR;
	#define VOXELPLAY_SET_TINTCOLOR(color, i) i.color = color;
	#define VOXELPLAY_OUTPUT_TINTCOLOR(o) o.color = v.color;
	#if defined(VP_CUTOUT)
		#define VOXELPLAY_APPLY_TINTCOLOR(color, i) color.rgb *= i.color.rgb;
	#else
		#define VOXELPLAY_APPLY_TINTCOLOR(color, i) color *= i.color;
	#endif
#else
	#define VOXELPLAY_TINTCOLOR_DATA
	#define VOXELPLAY_SET_TINTCOLOR(color, i)
	#define VOXELPLAY_OUTPUT_TINTCOLOR(o)
	#define VOXELPLAY_APPLY_TINTCOLOR(color, i)
#endif


// Sampling =====================================================================


#if VOXELPLAY_USE_AA
	#if defined(SHADER_API_D3D11) || defined(SHADER_API_XBOXONE) || defined(SHADER_API_GLES3) || defined(SHADER_API_GLCORE) || defined(SHADER_API_METAL)
		#define UNITY_SAMPLE_TEX2DARRAY_GRAD(tex,coord,dx,dy) tex.SampleGrad (sampler##tex,coord,dx,dy)
	#elif defined(UNITY_COMPILER_HLSL2GLSL) || defined(SHADER_TARGET_SURFACE_ANALYSIS)
		#define UNITY_SAMPLE_TEX2DARRAY_GRAD(tex,coord,dx,dy) tex2DArray(tex,coord,dx,dy)
	#else
		#define UNITY_SAMPLE_TEX2DARRAY_GRAD(tex,coord,dx,dy) UNITY_SAMPLE_TEX2DARRAY(tex,coord)
	#endif

    #if defined(NON_ARRAY_TEXTURE)
        inline fixed4 ReadSmoothTexel2D(sampler tex, float2 textureSize, float2 uv) {
            float2 ruv = uv.xy * textureSize - 0.5;
            float2 f = fwidth(ruv);
            uv.xy = (floor(ruv) + 0.5 + saturate( (frac(ruv) - 0.5 + f ) / f)) / textureSize; 
            return tex2D(tex, uv);
        }
        #define VOXELPLAY_GET_TEXEL(uv) ReadSmoothTexel2D(_MainTex, _MainTex_TexelSize.zw, uv.xy)
        #define VOXELPLAY_GET_TEXEL_DD(uv) ReadSmoothTexel2D(_MainTex, _MainTex_TexelSize.zw, uv.xy)
        #define VOXELPLAY_GET_TEXEL_DD_X(tex, textureSize, uv) ReadSmoothTexel2D(tex, textureSize, uv.xy)
    #else
        inline fixed4 ReadSmoothTexel(float3 uv) {
			#if FILTER_MODE != POINT_FILTER
				return UNITY_SAMPLE_TEX2DARRAY_SAMPLER(_MainTex, SAMPLER_MODE, uv);
			#else
				float2 ruv = uv.xy * _MainTex_TexelSize.zw - 0.5;
				float2 f = fwidth(ruv);
				uv.xy = (floor(ruv) + 0.5 + saturate( (frac(ruv) - 0.5 + f ) / f)) / _MainTex_TexelSize.zw; 
				return UNITY_SAMPLE_TEX2DARRAY(_MainTex, uv);
			#endif
        }
        inline fixed4 ReadSmoothTexelWithDerivatives(float3 uv) {
			#if FILTER_MODE != POINT_FILTER
				return UNITY_SAMPLE_TEX2DARRAY_SAMPLER(_MainTex, SAMPLER_MODE, uv);
			#else
	            float2 ruv = frac(uv.xy) * _MainTex_TexelSize.zw - 0.5;
		        float2 f = fwidth(ruv);
			    float2 nuv = (floor(ruv) + 0.5 + saturate( (frac(ruv) - 0.5 + f ) / f)) / _MainTex_TexelSize.zw;    
				return UNITY_SAMPLE_TEX2DARRAY_GRAD(_MainTex, float3(nuv, uv.z), ddx(uv.xy), ddy(uv.xy));
			#endif
        }
	    #define VOXELPLAY_GET_TEXEL(uv) ReadSmoothTexel(uv)
	    #define VOXELPLAY_GET_TEXEL_DD(uv) ReadSmoothTexelWithDerivatives(uv)
    #endif
    #define VOXELPLAY_GET_TEXEL_2D(uv) ReadSmoothTexel2D(uv)

#else // no AA pixels

    #if defined(NON_ARRAY_TEXTURE)
        #define VOXELPLAY_GET_TEXEL(uv) tex2D(_MainTex, uv.xy)
        #define VOXELPLAY_GET_TEXEL_DD(uv) tex2D(_MainTex, uv.xy)
        #define VOXELPLAY_GET_TEXEL_DD_X(sampler, textureSize, uv) tex2D(sampler, uv.xy)
    #else
    	#define VOXELPLAY_GET_TEXEL(uv) UNITY_SAMPLE_TEX2DARRAY(_MainTex, uv.xyz)
   	    #define VOXELPLAY_GET_TEXEL_DD(uv) UNITY_SAMPLE_TEX2DARRAY_SAMPLER(_MainTex, SAMPLER_MODE, uv)
    #endif
    #define VOXELPLAY_GET_TEXEL_2D(uv) tex2D(_MainTex, uv.xy)

#endif

// Outline  =====================================================================

fixed4 _OutlineColor;
fixed _OutlineThreshold;

inline void ApplyOutlineSimple(inout fixed4 color, float2 uv) {
	float2 grd = abs(frac(uv + 0.5) - 0.5);
	grd /= fwidth(uv) * _OutlineThreshold;
	float  lin = 1.0 - saturate(min(grd.x, grd.y));
	color.rgb = lerp(color.rgb, _OutlineColor.rgb, lin * _OutlineColor.a);
}

// Bump Map =====================================================================

half3 _vp_normalMap;

#define VOXELPLAY_NEEDS_TANGENT_SPACE VOXELPLAY_USE_PARALLAX || VOXELPLAY_USE_NORMAL || VOXELPLAY_USE_PBR || defined(USE_CUSTOM_BUMP_MAP)
#if VOXELPLAY_NEEDS_TANGENT_SPACE
	float3x3 objectToTangent;
	#define VOXELPLAY_SET_TANGENT_SPACE(tang,norm) float3 tang = float3( dot(float2(1,-1), norm.yz), 0, norm.x ); objectToTangent = float3x3( tang, cross(tang, norm), norm );
#else
	#define VOXELPLAY_SET_TANGENT_SPACE(tang,norm)
#endif

UNITY_DECLARE_TEX2D_NOSAMPLER(_BumpMap);

#if defined(USE_CUSTOM_BUMP_MAP)
	// normal map is provided in the material

	inline fixed GetPerVoxelNdotL(float3 normal) {
		return saturate(1.0 + _WorldSpaceLightPos0.y * 2.0);
	}

	#define VOXELPLAY_BUMPMAP_DATA(idx1) float3 tlightDir : TEXCOORD##idx1;
	#define VOXELPLAY_OUTPUT_BUMPMAP_DATA(uv, i) i.tlightDir = mul(objectToTangent, _WorldSpaceLightPos0.xyz);

	fixed GetPerPixelNdotL(float3 tlightDir, float4 rawNorm) {
		float3 nrm  = UnpackNormal(rawNorm);
		nrm.y *= -1.0;
		return saturate(dot(nrm, tlightDir));
	}

	#define VOXELPLAY_APPLY_BUMPMAP(i) _vp_normalMap = _BumpMap.Sample(SAMPLER_NAME, i.uv.xy).xyz; i.light.xz *= GetPerPixelNdotL(i.tlightDir, float4(_vp_normalMap, 1.0));
	#define VOXELPLAY_APPLY_CUSTOM_BUMPMAP(i, rawNorm) _vp_normalMap = rawNorm.xyz; i.light.x *= GetPerPixelNdotL(i.tlightDir, rawNorm);


#elif VOXELPLAY_USE_NORMAL
	// normal map is included in the texture array

	inline fixed GetPerVoxelNdotL(float3 normal) {
		return saturate(1.0 + _WorldSpaceLightPos0.y * 2.0);
	}

	#define VOXELPLAY_BUMPMAP_DATA(idx1) float3 tlightDir : TEXCOORD##idx1;
	#define VOXELPLAY_OUTPUT_BUMPMAP_DATA(uv, i) i.tlightDir = mul(objectToTangent, _WorldSpaceLightPos0.xyz);

	float GetPerPixelNdotL(float3 tlightDir, float3 uv) {
		UNITY_BRANCH
		if (NORMAL_MAP_OFFSET > 0.5) {
			uv.z += NORMAL_MAP_OFFSET;
			// Sample normal map once and store for reuse in PBR
			_vp_normalMap = UNITY_SAMPLE_TEX2DARRAY(_MainTex, uv).xyz;
		} else {
			_vp_normalMap = half3(0.5, 0.5, 1.0);
		}
		float3 nrm = _vp_normalMap * 2.0 - 1.0;
		nrm.y *= -1.0;
		float NdotL = saturate(dot(nrm, tlightDir));
		NdotL = (NdotL + _VPDiffuseWrap.x) * _VPDiffuseWrap.y;
		return NdotL;
	}
	// frac is necessary because uv are in world space
	#define VOXELPLAY_APPLY_BUMPMAP(i) i.light.xz *= GetPerPixelNdotL(i.tlightDir, float3(frac(i.uv.xy), i.uv.z));
	#define VOXELPLAY_APPLY_CUSTOM_BUMPMAP(i, rawNorm)
#else
	// no normal mapping

	inline fixed GetPerVoxelNdotL(float3 normal) {
        #if defined(SUN_SCATTERING)
            const float NdotL = 0.7;
        #else
		    float NdotL = saturate(dot(_WorldSpaceLightPos0.xyz, normal));
			NdotL = (NdotL + _VPDiffuseWrap.x) * _VPDiffuseWrap.y;
        #endif
		return NdotL * saturate(1.0 + _WorldSpaceLightPos0.y * 2.0);
	}

	#define VOXELPLAY_BUMPMAP_DATA(idx1)
	#define VOXELPLAY_OUTPUT_BUMPMAP_DATA(uv, i)
	#define VOXELPLAY_APPLY_BUMPMAP(i)
	#define VOXELPLAY_APPLY_CUSTOM_BUMPMAP(i, rawNorm)
#endif

#if VOXELPLAY_PIXEL_LIGHTS || defined(USE_NORMAL)
	#define VOXELPLAY_NORMAL_DATA float3 norm: NORMAL;
#else
	#define VOXELPLAY_NORMAL_DATA
#endif

// Metallic Smoothness =====================================================================

half4 _vp_metallicMap;

#if VOXELPLAY_PIXEL_LIGHTS && VOXELPLAY_USE_PBR
	
		#define VOXELPLAY_SMOOTHNESS_DATA(idx) float3x3 tangentToObject : TEXCOORD##idx;
		#define VOXELPLAY_OUTPUT_SMOOTHNESS_DATA(o) float3 bitang = objectToTangent[1]; float3 norm = objectToTangent[2]; o.tangentToObject = float3x3(tang.x, tang.y, tang.z, bitang.x, bitang.y, bitang.z, norm.x, norm.y, norm.z);
			
		// URP's Cook-Torrance BRDF implementation
		
		// Constants
		#ifndef kDielectricSpec
			#define kDielectricSpec half4(0.04, 0.04, 0.04, 1.0 - 0.04) // standard dielectric reflectivity coef at incident angle (= 4%)
		#endif
		#ifndef HALF_MIN
			#define HALF_MIN 6.103515625e-5  // 2^-14, the same value for 10, 11 and 16-bit: https://www.khronos.org/opengl/wiki/Small_Float_Formats
		#endif
		#ifndef HALF_MIN_SQRT
			#define HALF_MIN_SQRT 0.0078125  // 2^-7 == sqrt(HALF_MIN), useful for ensuring HALF_MIN after x^2
		#endif
		
		// Perceptual roughness conversions (simplified versions since we don't have access to Core RP)
		inline half VPPerceptualSmoothnessToPerceptualRoughness(half smoothness) {
			return 1.0 - smoothness;
		}
		
		inline half VPPerceptualRoughnessToRoughness(half perceptualRoughness) {
			return perceptualRoughness * perceptualRoughness;
		}
		
		inline half VPOneMinusReflectivityMetallic(half metallic) {
			half oneMinusDielectricSpec = kDielectricSpec.a;
			return oneMinusDielectricSpec - metallic * oneMinusDielectricSpec;
		}
		
		// Split approach: metallic for energy conservation, smoothness for specular
		void ApplyMetallicOnly(inout half4 color, float3 uv) {
			UNITY_BRANCH
			if (PBR_MAP_OFFSET < 0.5) {
				_vp_metallicMap = half4(0,0,1,0); // metallic/smoothness 0, occlusion 1
			} else {
				float metallicUvz = uv.z + PBR_MAP_OFFSET;
				_vp_metallicMap = VOXELPLAY_GET_TEXEL_DD(float3(uv.xy, metallicUvz)); 
				// Apply energy conservation: reduce diffuse for metals
				color.rgb *= (1.0 - _vp_metallicMap.r);
			}
		}
		
		inline half VPPow5(half x) {
			half x2 = x * x;
			half x5 = x2 * x2 * x;
			return x5;
		}
		// Helper function for Schlick's fresnel approximation
		inline half3 CalculateFresnel(half3 f0, half cosTheta) {
			return f0 + (1.0 - f0) * VPPow5(1.0 - cosTheta);
		}
		
		// Helper function to get final world space normal
		inline float3 GetFinalWorldSpaceNormal(float3 normOS, float3x3 tangentToObject) {

			#if VOXELPLAY_USE_NORMAL
				// Get normal from bump map (in tangent space)
				float3 normTS = _vp_normalMap * 2.0 - 1.0;
				normTS.y *= -1.0;
				normOS = mul(tangentToObject, normTS);
			#endif
			return normOS;  // object and world space normals are the same
		}		
		
		void ApplySmoothnessSpecular(inout half4 color, float3 norm, float3 wpos, half4 metallicMap, fixed atten, half3 albedo) {

			float metallic = metallicMap.r;
			float smoothness = metallicMap.g;

			// Initialize BRDF data
			half3 brdfSpecular = lerp(kDielectricSpec.rgb, albedo, metallic);
			
			// Calculate roughness terms
			half perceptualRoughness = VPPerceptualSmoothnessToPerceptualRoughness(smoothness);
			half roughness = max(VPPerceptualRoughnessToRoughness(perceptualRoughness), HALF_MIN_SQRT);

			// Calculate lighting vectors
			half3 viewDir = normalize(_WorldSpaceCameraPos - wpos);
			
			#if defined(USES_URP)
				// Add environment reflections for URP
				half3 reflectVec = reflect(-viewDir, norm);
				#if UNITY_VERSION >= 202230 && defined(_FORWARD_PLUS)
				 	half3 reflection = SampleSH(reflectVec); // in F+/D+ we use SH for reflections
				#else
					half3 reflection = GlossyEnvironmentReflection(reflectVec, perceptualRoughness, _vp_metallicMap.b);
				#endif

				// Apply fresnel to environment reflections based on viewing angle (NoV)
				// This controls how much environment is reflected based on viewing angle
				half NoV = saturate(dot(norm, viewDir));
				half3 fresnelRefl = CalculateFresnel(brdfSpecular, NoV);
                fresnelRefl *= smoothness;
				color.rgb += reflection * fresnelRefl;
			#endif
			
			half roughness2 = max(roughness * roughness, HALF_MIN);
			half normalizationTerm = roughness * 4.0 + 2.0;
			half roughness2MinusOne = roughness2 - 1.0;
			
			half3 lightDir = _WorldSpaceLightPos0.xyz; 
			half3 halfDir = normalize(lightDir + viewDir);
			
			float NoH = saturate(dot(norm, halfDir));
			half LoH = saturate(dot(lightDir, halfDir));
			
			// URP's optimized specular calculation
			float d = NoH * NoH * roughness2MinusOne + 1.00001f;
			half LoH2 = LoH * LoH;
			half specularTerm = roughness2 / ((d * d) * max(0.1h, LoH2) * normalizationTerm);
			
			#if defined (SHADER_API_MOBILE) || defined (SHADER_API_SWITCH)
				specularTerm = specularTerm - HALF_MIN;
				specularTerm = clamp(specularTerm, 0.0, 100.0);
			#endif

			// Calculate fresnel term for direct specular based on half-vector angle (LoH)
			// This is separate from environment fresnel - both are needed for correct PBR
			half3 fresnel = CalculateFresnel(brdfSpecular, LoH);
			
			// Add specular with shadow attenuation and fresnel
			float NdotL = max(dot(norm, lightDir), 0.0);
			if (NdotL > 0.0) {
				color.rgb += specularTerm * fresnel * _LightColor0.rgb * NdotL * atten;
			}
		}
		
	#define VOXELPLAY_APPLY_METALLIC(color, i) half3 _vp_albedo = color.rgb; ApplyMetallicOnly(color, i.uv.xyz);
	#define VOXELPLAY_APPLY_SMOOTHNESS(color, i) UNITY_BRANCH if (PBR_MAP_OFFSET > 0.5) { float3 finalWorldNormal = GetFinalWorldSpaceNormal(i.norm, i.tangentToObject); ApplySmoothnessSpecular(color, finalWorldNormal, i.wpos, _vp_metallicMap, atten, _vp_albedo); }
#else
	#define VOXELPLAY_SMOOTHNESS_DATA(idx)
	#define VOXELPLAY_OUTPUT_SMOOTHNESS_DATA(o)
	#define VOXELPLAY_APPLY_METALLIC(color, i) _vp_metallicMap = 0;
	#define VOXELPLAY_APPLY_SMOOTHNESS(color, i)
#endif

// Point Lights =====================================================================

CBUFFER_START(VoxelPlayLightBuffers)
float4 _VPPointLightPosition[MAX_LIGHTS];
half4 _VPPointLightColor[MAX_LIGHTS];
int _VPPointLightCount;
float _VPPointMaxDistanceSqr;
CBUFFER_END

float2 uvScreen;
#define VOXELPLAY_COMPUTE_SCREEN_UV(i) uvScreen = i.pos.xy / _ScreenParams.xy;

#if defined(USES_BRIGHT_POINT_LIGHTS) && defined(USES_URP) && defined(USES_URP_NATIVE_LIGHTS)
#define VOXELPLAY_URP_NATIVE_LIGHTS_DATA(idx1) float3 urpWorldPos: TEXCOORD##idx1;
#define VOXELPLAY_OUTPUT_URP_NATIVE_LIGHTS_DATA(o, wpos) o.urpWorldPos = wpos;

half3 ComputeURPNativeLights(float3 worldPos) {
	half3 color = 0;
	#if USE_FORWARD_PLUS
		// additional directional lights
		for (uint lightIndex = 0; lightIndex < URP_FP_DIRECTIONAL_LIGHTS_COUNT; lightIndex++) {
			Light light = GetAdditionalLight(lightIndex, worldPos, 1.0.xxxx);
			color += light.color * (light.distanceAttenuation * light.shadowAttenuation);
		}
		// clustered lights
		{
			uint lightIndex;
			ClusterIterator _urp_internal_clusterIterator = ClusterInit(uvScreen, worldPos, 0);
			[loop] while (ClusterNext(_urp_internal_clusterIterator, lightIndex)) { 
				lightIndex += URP_FP_DIRECTIONAL_LIGHTS_COUNT;
		        Light light = GetAdditionalLight(lightIndex, worldPos, 1.0.xxxx);
			    color += light.color * (light.distanceAttenuation * light.shadowAttenuation);
			}
		}
	#else
		#if USE_FORWARD_PLUS
			uint additionalLightCount = min(URP_FP_PROBES_BEGIN, MAX_VISIBLE_LIGHTS);
		#else
		    uint additionalLightCount = GetAdditionalLightsCount();
		#endif
		for (uint i = 0; i < additionalLightCount; ++i) {
			#if UNITY_VERSION >= 202030
				Light light = GetAdditionalLight(i, worldPos, 1.0.xxxx);
			#else
				Light light = GetAdditionalLight(i, worldPos);
			#endif
			color += light.color * light.distanceAttenuation * light.shadowAttenuation;
		}
	#endif
	return color;
}
#define VOXELPLAY_ADD_URP_NATIVE_LIGHTS_TO_VERTEX_LIGHT(i) VOXELPLAY_COMPUTE_SCREEN_UV(i); i.vertexLightColor += ComputeURPNativeLights(i.urpWorldPos);
#else
	#define VOXELPLAY_URP_NATIVE_LIGHTS_DATA(idx1)
	#define VOXELPLAY_OUTPUT_URP_NATIVE_LIGHTS_DATA(o, wpos)
	#define VOXELPLAY_ADD_URP_NATIVE_LIGHTS_TO_VERTEX_LIGHT(i)
#endif


half3 ShadePointLights(float3 worldPos, float3 normal) {

	half3 color = 0;

    #if defined(USES_BRIGHT_POINT_LIGHTS)
		float distSqr = dot(worldPos - _WorldSpaceCameraPos, worldPos - _WorldSpaceCameraPos);
		if (distSqr < _VPPointMaxDistanceSqr) {
			#if defined(USES_URP) && defined(USES_URP_NATIVE_LIGHTS)
				color += ComputeURPNativeLights(worldPos);
			#else
				for (int k=0;k<MAX_LIGHTS;k++) {
					if (k<_VPPointLightCount) {
						float3 toLight = _VPPointLightPosition[k].xyz - worldPos;
						float dist = dot(toLight, toLight);
						toLight *= rcp(dist + 0.0001);
						float lightAtten = dist * rcp(_VPPointLightPosition[k].w);
						float NdL = saturate((dot(normal, toLight) - 1.0) * _VPPointLightColor[k].a + 1.0);
						color += _VPPointLightColor[k].rgb * (NdL * rcp(1.0 + lightAtten));
					}
				}
			#endif
		}
    #endif

	return color;
}


half3 ShadePointLightsWithoutNormal(float3 worldPos) {
    half3 color = 0;
    #if defined(USES_BRIGHT_POINT_LIGHTS) && !(defined(USES_URP) && defined(USES_URP_NATIVE_LIGHTS))
		for (int k=0;k<MAX_LIGHTS;k++) {
			if (k<_VPPointLightCount) {
				float3 toLight = _VPPointLightPosition[k].xyz - worldPos;
		        float dist = dot(toLight, toLight);
			    toLight *= rcp(dist + 0.0001);
	            float atten = dist * rcp(_VPPointLightPosition[k].w);
				float NdL = max(abs(toLight.x), abs(toLight.z));
				color += _VPPointLightColor[k].rgb * (NdL * rcp(1.0 + atten));
			}
		}
	#endif
    return color;
}

// Lighting setup  =====================================================================

#if defined(UNITY_COLORSPACE_GAMMA) 
	#define SMOOTH_TORCH_LINEAR_LIGHTING(x)
#else
	#define SMOOTH_TORCH_LINEAR_LIGHTING(x) x = (x*x)
#endif

void UnpackVoxelLight(int voxelLight, out fixed sunLight, out fixed torchLight) {
	sunLight = (voxelLight & 0x1FF) / 15.0;
	torchLight = voxelLight/(4096.0*15.0);
}


#if VOXELPLAY_GLOBAL_USE_FOG
    #define VOXELPLAY_FOG_DATA(idx1) fixed4 skyColor: TEXCOORD##idx1;
    #define VOXELPLAY_APPLY_FOG(color, i) color.rgb = lerp(color.rgb, i.skyColor.rgb, i.skyColor.a);
	#if defined(IS_CLOUD)
		#define CLOUD_FOG_DIST_MULTIPLIER 0.25
	#else
		#define CLOUD_FOG_DIST_MULTIPLIER 1.0
	#endif

    #define VOXELPLAY_INITIALIZE_LIGHT_AND_FOG_NORMAL(uv, worldPos, normal) float3 viewDir = worldPos - _WorldSpaceCameraPos; float3 nviewDir = normalize(viewDir); o.skyColor = fixed4(getSkyColor(nviewDir), saturate( (dot(viewDir * CLOUD_FOG_DIST_MULTIPLIER, viewDir * CLOUD_FOG_DIST_MULTIPLIER) - _VPFogData.x) / _VPFogData.y)); o.light = fixed3(GetPerVoxelNdotL(normal), uv.w/(4096.0*15.0), 1.0); SMOOTH_TORCH_LINEAR_LIGHTING(o.light.y); uv.w = ((int)uv.w & 0x1FF) / 15.0;
    #define VOXELPLAY_INITIALIZE_LIGHT_AND_FOG_NORMAL_NO_GI(worldPos, normal) float3 viewDir = worldPos - _WorldSpaceCameraPos; float3 nviewDir = normalize(viewDir); o.skyColor = fixed4(getSkyColor(nviewDir), saturate( (dot(viewDir * CLOUD_FOG_DIST_MULTIPLIER, viewDir * CLOUD_FOG_DIST_MULTIPLIER) - _VPFogData.x) / _VPFogData.y)); o.light = fixed3(GetPerVoxelNdotL(normal), 0, 1.0);
    #define VOXELPLAY_INITIALIZE_LIGHT_AND_FOG(uv, worldPos) float3 viewDir = worldPos - _WorldSpaceCameraPos; float3 nviewDir = normalize(viewDir); float3 normal = -nviewDir; o.skyColor = fixed4(getSkyColor(nviewDir), saturate((dot(viewDir * CLOUD_FOG_DIST_MULTIPLIER, viewDir * CLOUD_FOG_DIST_MULTIPLIER) - _VPFogData.x) / _VPFogData.y)); o.light = fixed3(GetPerVoxelNdotL(normal), uv.w/(4096.0*15.0), 1.0); SMOOTH_TORCH_LINEAR_LIGHTING(o.light.y); uv.w = ((int)uv.w & 0x1FF) / 15.0;

#else // fallbacks when fog is disabled

    #define VOXELPLAY_FOG_DATA(idx1)
    #define VOXELPLAY_APPLY_FOG(color, i)
    #define VOXELPLAY_INITIALIZE_LIGHT_AND_FOG_NORMAL(uv, worldPos, normal) o.light = fixed3(GetPerVoxelNdotL(normal), uv.w/(4096.0*15.0), 1.0); SMOOTH_TORCH_LINEAR_LIGHTING(o.light.y); uv.w = ((int)uv.w & 0x1FF) / 15.0;
    #define VOXELPLAY_INITIALIZE_LIGHT_AND_FOG_NORMAL_NO_GI(worldPos, normal) o.light = fixed3(GetPerVoxelNdotL(normal), 0, 1.0);
    #define VOXELPLAY_INITIALIZE_LIGHT_AND_FOG(uv, worldPos) float3 viewDir = _WorldSpaceCameraPos - worldPos; float3 normal = normalize(viewDir); o.light = fixed3(GetPerVoxelNdotL(normal), uv.w/(4096.0*15.0), 1.0); SMOOTH_TORCH_LINEAR_LIGHTING(o.light.y); uv.w = ((int)uv.w & 0x1FF) / 15.0;

#endif // VOXELPLAY_GLOBAL_USE_FOG

#define TORCH_LIGHT_CONTRIBUTION i.light.yyy

#if VOXELPLAY_PIXEL_LIGHTS
    // light.x = classic NdL lighting term, light.y = voxel lighting, light.z = bump map influence on shadows
	#define VOXELPLAY_LIGHT_DATA(idx1,idx2) fixed3 light: TEXCOORD##idx1; float3 wpos: TEXCOORD##idx2;
	#define VOXELPLAY_SET_FACE_LIGHT(i, worldPos, normal) i.wpos = worldPos; i.norm = normal;
	#define VOXELPLAY_SET_LIGHT(i, worldPos, normal) i.wpos = worldPos; i.norm = normal;
	#define VOXELPLAY_SET_LIGHT_WITHOUT_NORMAL(i, worldPos) i.wpos = worldPos;
	#define VOXELPLAY_VERTEX_LIGHT_COLOR(specularAtten) ShadePointLights(i.wpos, i.norm)
#else
	#define VOXELPLAY_LIGHT_DATA(idx1,idx2) fixed3 light: TEXCOORD##idx1; fixed3 vertexLightColor: TEXCOORD##idx2;
	#define VOXELPLAY_SET_FACE_LIGHT(i, worldPos, normal) i.vertexLightColor = ShadePointLights(worldPos, normal);
	#define VOXELPLAY_SET_LIGHT(i, worldPos, normal) i.vertexLightColor = ShadePointLights(worldPos, normal);
	#define VOXELPLAY_SET_LIGHT_WITHOUT_NORMAL(i, worldPos) i.vertexLightColor = ShadePointLightsWithoutNormal(worldPos);
	#define VOXELPLAY_VERTEX_LIGHT_COLOR(atten) i.vertexLightColor
#endif

#if !defined(SUBTLE_SELF_SHADOWS)
    fixed _VPDaylightShadowAtten;
#endif

#if defined(USES_COLORED_SHADOWS)
	fixed3 _VPShadowTintColor;
	#define SHADOW_COLOR lerp(_VPShadowTintColor, 1.0, atten)
#else
	#define SHADOW_COLOR atten
#endif

// Better Ambient Lighting =============================================

// Function to get spherical harmonics ambient lighting
// Works in both Built-in and URP pipelines
inline fixed3 GetAmbientLighting(float3 normal) {
    fixed3 ambient = _VPAmbientLight;
    #if defined(USES_URP)
        ambient *= SampleSH(normal);
    #else
        // Built-in pipeline path
        #if UNITY_SHOULD_SAMPLE_SH
            // Use Unity's built-in SH9 function for spherical harmonics
            ambient *= ShadeSH9(half4(normal, 1.0));
        #else
            // Fallback to simple ambient
            ambient *= UNITY_LIGHTMODEL_AMBIENT.rgb;
        #endif
    #endif
    
    #if VOXELPLAY_PIXEL_LIGHTS && VOXELPLAY_USE_PBR
		ambient *= 1.0 - _vp_metallicMap.r; // no ambient lighting for metallic surfaces
        ambient *= _vp_metallicMap.b; // occlusion map
    #endif

    return ambient;
}

#if VOXELPLAY_PIXEL_LIGHTS || defined(USE_NORMAL)
    #define VOXELPLAY_APPLY_LIGHTING(color,i) fixed atten = VOXELPLAY_LIGHT_ATTENUATION(i); fixed3 ambientLight = GetAmbientLighting(i.norm); color.rgb *= min((SHADOW_COLOR + ambientLight) * _LightColor0.rgb + TORCH_LIGHT_CONTRIBUTION, 1.2) + VOXELPLAY_VERTEX_LIGHT_COLOR(atten * UNITY_SHADOW_ATTEN(i) );
    #define VOXELPLAY_APPLY_LIGHTING_AO_AND_GI(color,i) fixed atten = VOXELPLAY_LIGHT_ATTENUATION(i); float ao = i.uv.w; AO_FUNCTION; fixed3 ambientLight = GetAmbientLighting(i.norm); color.rgb *= min((SHADOW_COLOR * ao + ambientLight) * _LightColor0.rgb + TORCH_LIGHT_CONTRIBUTION, 1.2) + VOXELPLAY_VERTEX_LIGHT_COLOR(i.uv.w * UNITY_SHADOW_ATTEN(i) );
    #define VOXELPLAY_APPLY_LIGHTING_AND_GI(color,i) fixed atten = VOXELPLAY_LIGHT_ATTENUATION(i); float ao = i.uv.w; AO_FUNCTION; fixed3 ambientLight = GetAmbientLighting(i.norm); color.rgb *= min((SHADOW_COLOR * ao + ambientLight) * _LightColor0.rgb + TORCH_LIGHT_CONTRIBUTION, 1.2) + VOXELPLAY_VERTEX_LIGHT_COLOR(i.uv.w * UNITY_SHADOW_ATTEN(i) );
#else
    // Fallback when normal is not available - use up vector for ambient
    #define VOXELPLAY_APPLY_LIGHTING(color,i) fixed atten = VOXELPLAY_LIGHT_ATTENUATION(i); fixed3 ambientLight = GetAmbientLighting(float3(0,1,0)); color.rgb *= min((SHADOW_COLOR + ambientLight) * _LightColor0.rgb + TORCH_LIGHT_CONTRIBUTION, 1.2) + VOXELPLAY_VERTEX_LIGHT_COLOR(atten * UNITY_SHADOW_ATTEN(i) );
    #define VOXELPLAY_APPLY_LIGHTING_AO_AND_GI(color,i) fixed atten = VOXELPLAY_LIGHT_ATTENUATION(i); float ao = i.uv.w; AO_FUNCTION; fixed3 ambientLight = GetAmbientLighting(float3(0,1,0)); color.rgb *= min((SHADOW_COLOR * ao + ambientLight) * _LightColor0.rgb + TORCH_LIGHT_CONTRIBUTION, 1.2) + VOXELPLAY_VERTEX_LIGHT_COLOR(i.uv.w * UNITY_SHADOW_ATTEN(i) );
    #define VOXELPLAY_APPLY_LIGHTING_AND_GI(color,i) fixed atten = VOXELPLAY_LIGHT_ATTENUATION(i); float ao = i.uv.w; AO_FUNCTION; fixed3 ambientLight = GetAmbientLighting(float3(0,1,0)); color.rgb *= min((SHADOW_COLOR * ao + ambientLight) * _LightColor0.rgb + TORCH_LIGHT_CONTRIBUTION, 1.2) + VOXELPLAY_VERTEX_LIGHT_COLOR(i.uv.w * UNITY_SHADOW_ATTEN(i) );
#endif

// Integrated emission with animation =============================================

#if defined(USE_EMISSION)
    fixed _VPEmissionIntensity;
    
    #define VOXELPLAY_COMPUTE_EMISSION(color, uv) half3 emissionColor = 0; UNITY_BRANCH if (EMISSION_MAP_OFFSET > 0.5) { half4 emissionTexel = VOXELPLAY_GET_TEXEL_DD(float3(uv.xy, uv.z + EMISSION_MAP_OFFSET)); emissionColor = emissionTexel.rgb * (emissionTexel.a * 8.0 * _VPEmissionIntensity); }
    #define VOXELPLAY_ADD_EMISSION(color) color.rgb += emissionColor;
#else
    #define VOXELPLAY_COMPUTE_EMISSION(color, uv)
    #define VOXELPLAY_ADD_EMISSION(color)
#endif // EMISSION


#define VOXELPLAY_OUTPUT_UV(x, o) o.uv = (x); 



#if VOXELPLAY_USE_PARALLAX

	float GetParallaxHeight (float3 uv, float2 uvOffset) {
		return UNITY_SAMPLE_TEX2DARRAY_LOD(_MainTex, float3(uv.xy + uvOffset, uv.z), 0).a;
	}

	void ApplyParallax(float4 tangentViewDir, inout float3 uv) {

		if (tangentViewDir.w == 0) return; // do not apply parallax to remote pixels

		float3 tviewDir = normalize(tangentViewDir.xyz);
		float2 uvDir = tviewDir.xy / (tviewDir.z + 0.42);
		float stepSize = 1.0 / _VPParallaxIterations;
		float2 uvInc = uvDir * (stepSize * _VPParallaxStrength);

		float2 uvOffset = 0;

		float stepHeight = 1;

		// get the texture index for displacement map
		float baseSlice = uv.z;
		uv.z += HEIGHT_MAP_OFFSET;
		float surfaceHeight = UNITY_SAMPLE_TEX2DARRAY_LOD(_MainTex, uv, 0).a;

		float2 prevUVOffset = uvOffset;
		float prevStepHeight = stepHeight;
		float prevSurfaceHeight = surfaceHeight;

		for (int i1 = 1; i1 < _VPParallaxIterations && stepHeight > surfaceHeight; i1++) {
			prevUVOffset = uvOffset;
			prevStepHeight = stepHeight;
			prevSurfaceHeight = surfaceHeight;
			uvOffset -= uvInc;
			stepHeight -= stepSize;
			surfaceHeight = GetParallaxHeight(uv, uvOffset);
		}

		for (int i2 = 0; i2 < _VPParallaxIterationsBinarySearch; i2++) {
			uvInc *= 0.5;
			stepSize *= 0.5;

			if (stepHeight < surfaceHeight) {
				uvOffset += uvInc;
				stepHeight += stepSize;
			} else {
				uvOffset -= uvInc;
				stepHeight -= stepSize;
			}
			surfaceHeight = GetParallaxHeight(uv, uvOffset);
		}

		uv.xy += uvOffset;
		uv.z = baseSlice;
	}

	#define VOXELPLAY_PARALLAX_DATA(idx1) float4 tviewDir : TEXCOORD##idx1; 
	#define VOXELPLAY_OUTPUT_PARALLAX_DATA(wpos, v, uv, i) float pdistSqr = dot(wpos - _WorldSpaceCameraPos.xyz, wpos - _WorldSpaceCameraPos.xyz); float3 invViewDir = mul(unity_WorldToObject, float4(_WorldSpaceCameraPos.xyz, 1)).xyz - v.vertex.xyz; i.tviewDir = float4(mul(objectToTangent, invViewDir), step(pdistSqr, _VPParallaxMaxDistanceSqr)); if (HEIGHT_MAP_OFFSET < 0.5) i.tviewDir.w = 0;
	#define VOXELPLAY_APPLY_PARALLAX(i) ApplyParallax(i.tviewDir, i.uv.xyz);
#else
	#define VOXELPLAY_PARALLAX_DATA(idx1) 
	#define VOXELPLAY_OUTPUT_PARALLAX_DATA(wpos, v, uv, i) 
	#define VOXELPLAY_APPLY_PARALLAX(i)
#endif // VOXELPLAY_USE_PARALLAX

#if VOXELPLAY_USE_OUTLINE
	#define VOXELPLAY_APPLY_OUTLINE_SIMPLE(color, i) ApplyOutlineSimple(color, i.uv.xy);
#else
	#define VOXELPLAY_APPLY_OUTLINE_SIMPLE(color, i)
#endif

#endif // VOXELPLAY_COMMON_CORE

