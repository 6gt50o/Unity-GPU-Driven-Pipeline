﻿Shader "Hidden/MUber"
{
    SubShader
    {
        CGINCLUDE
        #pragma target 5.0

	#include "UnityCG.cginc"
	#include "UnityDeferredLibrary.cginc"
	#include "UnityPBSLighting.cginc"
	#include "CGINC/VoxelLight.cginc"
	#include "CGINC/Shader_Include/Common.hlsl"
	#include "CGINC/Random.cginc"
	#include "CGINC/Shader_Include/BSDF_Library.hlsl"
	#include "CGINC/Shader_Include/AreaLight.hlsl"
	#include "CGINC/Lighting.cginc"
	#include "CGINC/Reflection.cginc"
	#include "CGINC/VolumetricLight.cginc"
	#include "CGINC/Sunlight.cginc"
	#pragma multi_compile __ ENABLE_SUN
	#pragma multi_compile __ ENABLE_SUNSHADOW
	#pragma multi_compile __ ENABLE_VOLUMETRIC
	#pragma multi_compile __ ENABLE_REFLECTION
	#pragma multi_compile _ POINTLIGHT
	#pragma multi_compile _ SPOTLIGHT
			float4x4 _InvNonJitterVP;
			
			Texture2D<float4> _CameraGBufferTexture0; SamplerState sampler_CameraGBufferTexture0;
			Texture2D<float4> _CameraGBufferTexture1; SamplerState sampler_CameraGBufferTexture1;
			Texture2D<float4> _CameraGBufferTexture2; SamplerState sampler_CameraGBufferTexture2;
			Texture2D<float> _CopyedDepthTexture; SamplerState sampler_CopyedDepthTexture;
            
            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = v.vertex;
                o.uv = v.uv;
                return o;
            }

        ENDCG
        Pass
        {
            Cull Off ZWrite Off ZTest Greater
            Blend one one
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
	
            float3 frag (v2f i) : SV_Target
            {
                float4 gbuffer0 = _CameraGBufferTexture0.Sample(sampler_CameraGBufferTexture0, i.uv);
    			float4 gbuffer1 = _CameraGBufferTexture1.Sample(sampler_CameraGBufferTexture1, i.uv);
    			float4 gbuffer2 = _CameraGBufferTexture2.Sample(sampler_CameraGBufferTexture2, i.uv);
				float depth = _CopyedDepthTexture.Sample(sampler_CopyedDepthTexture, i.uv);
				float4 wpos = mul(_InvNonJitterVP, float4(i.uv * 2 - 1, depth, 1));
				wpos /= wpos.w;
				float3 viewDir = normalize(wpos.xyz - _WorldSpaceCameraPos);
				UnityStandardData data = UnityStandardDataFromGbuffer(gbuffer0, gbuffer1, gbuffer2);
                float roughness = clamp(1 - data.smoothness, 0.02, 1);
                float linearEyeDepth = LinearEyeDepth(depth);
				float linear01Depth = Linear01Depth(depth);
                float3 finalColor = 0;
				#if ENABLE_SUNSHADOW
					finalColor += CalculateSunLight(data, depth, wpos, viewDir, i.uv);
				#else
					finalColor += CalculateSunLight_NoShadow(data, viewDir);
				#endif
                #if ENABLE_REFLECTION
					finalColor += CalculateReflection(linearEyeDepth, wpos.xyz, viewDir, gbuffer1, data.normalWorld, 1, i.uv);
				#endif
                #if SPOTLIGHT || POINTLIGHT
				finalColor += CalculateLocalLight(i.uv, wpos, linearEyeDepth, data.diffuseColor, data.normalWorld, gbuffer1, roughness, viewDir);
                #endif
                #if ENABLE_VOLUMETRIC
					float4 volumeFog = Fog(linear01Depth, i.uv);
                    finalColor = lerp(volumeFog.rgb, finalColor, volumeFog.a);
				#endif
                return finalColor;
            }
            ENDCG
        }

            Pass
            {
            Cull Off ZWrite Off ZTest Equal
            Blend oneMinusSrcAlpha srcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
	
            float4 frag (v2f i) : SV_Target
            {
				
                #if ENABLE_VOLUMETRIC
                float depth = _CopyedDepthTexture.Sample(sampler_CopyedDepthTexture, i.uv);
                float linear01Depth = Linear01Depth(depth);
					float4 volumeFog = Fog(linear01Depth, i.uv);
                    return volumeFog;
				#endif
                return 1;
            }
            ENDCG
        }
    }
}
