Shader "VoxelBuilder/URP/BlockAtlasTiledLit_2"
{
    Properties
    {
        _BaseMap("Atlas Texture", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1,1,1,1)
        _TileWidth("Atlas Tile Width", Float) = 0.11111111
        _TileHeight("Atlas Tile Height", Float) = 0.125
        _Cutoff("Alpha Cutoff", Range(0,1)) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Lit"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile_fog
            #pragma multi_compile_fragment _ _LIGHT_LAYERS
            #pragma multi_compile_fragment _ _CLUSTERED_RENDERING

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float fogFactor : TEXCOORD3;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float _TileWidth;
                float _TileHeight;
                float _Cutoff;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInputs = GetVertexNormalInputs(input.normalOS);

                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = NormalizeNormalPerVertex(normalInputs.normalWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.fogFactor = ComputeFogFactor(positionInputs.positionCS.z);
                return output;
            }

            float2 ComputeBlockTileUv(float2 atlasUv, float3 positionWS, float3 normalWS)
            {
                float2 tileSize = float2(max(_TileWidth, 1e-6), max(_TileHeight, 1e-6));

                // Base tile index from mesh UVs (which encode atlas sprite rect).
                float2 atlasTileMin = floor(atlasUv / tileSize) * tileSize;

                // Tile once per world unit on the dominant face plane.
                float3 absN = abs(normalWS);
                float2 planeUv;
                if (absN.y >= absN.x && absN.y >= absN.z)
                {
                    planeUv = positionWS.xz;
                }
                else if (absN.x >= absN.z)
                {
                    planeUv = positionWS.zy;
                }
                else
                {
                    planeUv = positionWS.xy;
                }

                float2 local = frac(planeUv);
                return atlasTileMin + local * tileSize;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 normalWS = normalize(input.normalWS);
                float2 atlasUv = ComputeBlockTileUv(input.uv, input.positionWS, normalWS);

                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, atlasUv) * _BaseColor;
                clip(albedo.a - _Cutoff);

                InputData lightingInput = (InputData)0;
                lightingInput.positionWS = input.positionWS;
                lightingInput.normalWS = normalWS;
                lightingInput.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);
                lightingInput.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                lightingInput.fogCoord = input.fogFactor;
                lightingInput.vertexLighting = VertexLighting(input.positionWS, normalWS);
                lightingInput.bakedGI = SampleSH(normalWS);
                lightingInput.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

                Light mainLight = GetMainLight(lightingInput.shadowCoord);
                half NdotL = saturate(dot(normalWS, mainLight.direction));

                half3 diffuse = albedo.rgb * (lightingInput.bakedGI + mainLight.color * (NdotL * mainLight.shadowAttenuation));

                #if defined(_ADDITIONAL_LIGHTS)
                uint additionalLightsCount = GetAdditionalLightsCount();
                for (uint lightIndex = 0u; lightIndex < additionalLightsCount; ++lightIndex)
                {
                    Light light = GetAdditionalLight(lightIndex, input.positionWS);
                    half nDotL = saturate(dot(normalWS, light.direction));
                    diffuse += albedo.rgb * light.color * (nDotL * light.distanceAttenuation * light.shadowAttenuation);
                }
                #endif

                half4 color = half4(diffuse, albedo.a);
                color.rgb = MixFog(color.rgb, input.fogFactor);
                return color;
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }

            ZWrite On
            ZTest LEqual
            ColorMask 0
            Cull Back

            HLSLPROGRAM
            #pragma vertex ShadowPassVertex
            #pragma fragment ShadowPassFragment

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float _TileWidth;
                float _TileHeight;
                float _Cutoff;
            CBUFFER_END

            Varyings ShadowPassVertex(Attributes input)
            {
                Varyings output;
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

                float3 positionWS = ApplyShadowBias(vertexInput.positionWS, normalInput.normalWS, _MainLightPosition.xyz);
                output.positionCS = TransformWorldToHClip(positionWS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 ShadowPassFragment(Varyings input) : SV_TARGET
            {
                // Keep clip behavior consistent with forward pass.
                float2 tileSize = float2(max(_TileWidth, 1e-6), max(_TileHeight, 1e-6));
                float2 atlasTileMin = floor(input.uv / tileSize) * tileSize;
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, atlasTileMin + 0.5 * tileSize) * _BaseColor;
                clip(albedo.a - _Cutoff);
                return 0;
            }
            ENDHLSL
        }
    }
}