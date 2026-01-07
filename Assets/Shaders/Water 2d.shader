Shader "Unlit/2DWater"
{
    Properties
    {
        [Header(Base)]
        _BaseColor("Base Color", Color) = (0, 0.3, 1, 1)
        _Alpha("Alpha", Range(0, 1)) = 0.5

        [Header(Waves)]
        _WaveSpeed("Wave Speed", Range(0, 5)) = 0.1
        _WaveSize("Wave Size", Range(1, 32)) = 8

        [Header(Ripples)]
        _RippleColor("Ripple Color", Color) = (0.1, 0.5, 0.9, 1)
        _RippleSpeed("Ripple Speed", Range(0, 8)) = 2
        _RippleDensity("Ripple Density", Range(0, 20)) = 5
        _RippleSlimness("Ripple Slimness", Range(0, 10)) = 8

        [Header(Depth Foam)]
        _FoamColor("Foam Color", Color) = (0.9, 0.9, 0.9, 1)
        _FoamDistance("Foam Distance", Range(0, 2)) = 0.4
        _FoamScale("Foam Scale", Range(0, 48)) = 16
        _FoamSpeed("Foam Speed", Range(0, 1)) = 0.1
        _FoamCutoff("Foam Cutoff", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags
        {
            "RenderType"="Transparent"
            "Queue"="Transparent"
            "RenderPipeline"="UniversalPipeline"
            "DisableBatching"="True" // Required for depth texture sampling
        }

        Pass
        {
            // Shader State
            Blend SrcAlpha OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            ZTest LEqual

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // Includes
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Hashes.hlsl"

            // Structs
            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                float3 normalOS     : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS   : SV_POSITION;
                float2 uv           : TEXCOORD0;
                float4 screenPos    : TEXCOORD1; // For depth calculations
                UNITY_VERTEX_OUTPUT_STEREO
            };

            // CBUFFER for Material Properties
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float4 _RippleColor;
                float4 _FoamColor;
                float _Alpha;
                float _WaveSpeed;
                float _WaveSize;
                float _RippleSpeed;
                float _RippleDensity;
                float _RippleSlimness;
                float _FoamDistance;
                float _FoamSpeed;
                float _FoamScale;
                float _FoamCutoff;
            CBUFFER_END

            // Textures
            TEXTURE2D(_CameraDepthTexture);
            SAMPLER(sampler_CameraDepthTexture);

            // Noise Functions (from original Shader Graph)
            float2 Unity_GradientNoise_Deterministic_Dir(float2 p)
            {
                float x;
                Hash_Tchou_2_1_float(p, x);
                return normalize(float2(x - floor(x + 0.5), abs(x) - 0.5));
            }
            
            float Unity_GradientNoise_Deterministic(float2 UV, float Scale)
            {
                float2 p = UV * Scale;
                float2 ip = floor(p);
                float2 fp = frac(p);
                float d00 = dot(Unity_GradientNoise_Deterministic_Dir(ip), fp);
                float d01 = dot(Unity_GradientNoise_Deterministic_Dir(ip + float2(0, 1)), fp - float2(0, 1));
                float d10 = dot(Unity_GradientNoise_Deterministic_Dir(ip + float2(1, 0)), fp - float2(1, 0));
                float d11 = dot(Unity_GradientNoise_Deterministic_Dir(ip + float2(1, 1)), fp - float2(1, 1));
                fp = fp * fp * fp * (fp * (fp * 6 - 15) + 10);
                return lerp(lerp(d00, d01, fp.y), lerp(d10, d11, fp.y), fp.x) + 0.5;
            }

            float2 Unity_Voronoi_RandomVector_Deterministic(float2 UV, float offset)
            {
                Hash_Tchou_2_2_float(UV, UV);
                return float2(sin(UV.y * offset), cos(UV.x * offset)) * 0.5 + 0.5;
            }

            void Unity_Voronoi_Deterministic(float2 UV, float AngleOffset, float CellDensity, out float Out, out float Cells)
            {
                float2 g = floor(UV * CellDensity);
                float2 f = frac(UV * CellDensity);
                float3 res = float3(8.0, 0.0, 0.0);

                for (int y = -1; y <= 1; y++)
                {
                    for (int x = -1; x <= 1; x++)
                    {
                        float2 lattice = float2(x, y);
                        float2 offset = Unity_Voronoi_RandomVector_Deterministic(lattice + g, AngleOffset);
                        float d = distance(lattice + offset, f);
                        if (d < res.x)
                        {
                            res = float3(d, offset.x, offset.y);
                        }
                    }
                }
                Out = res.x;
                Cells = res.y;
            }
            
            // Vertex Shader
            Varyings vert(Attributes v)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                
                float timeOffset = _Time.y * _WaveSpeed;
                float2 waveUV = v.uv + timeOffset;
                float noise = Unity_GradientNoise_Deterministic(waveUV, _WaveSize);
                float3 displacedPos = v.positionOS + (v.normalOS * noise * 0.1);

                o.positionCS = TransformObjectToHClip(displacedPos);
                o.uv = v.uv;
                o.screenPos = ComputeScreenPos(o.positionCS);
                return o;
            }

            // Fragment Shader
            float4 frag(Varyings i) : SV_Target
            {
                // -- RIPPLE CALCULATION --
                float rippleTime = _Time.y * _RippleSpeed;
                float voronoiOut, voronoiCells;
                Unity_Voronoi_Deterministic(i.uv, rippleTime, _RippleDensity, voronoiOut, voronoiCells);
                
                float ripple = pow(1.0 - voronoiOut, _RippleSlimness);
                float4 rippleColor = ripple * _RippleColor;

                // --- FIX IS HERE ---
                // Clamp the result of the color addition to prevent values > 1.0
                float4 baseColor = saturate(_BaseColor + rippleColor);
                
                // -- FOAM CALCULATION --
                float sceneRawDepth = SAMPLE_TEXTURE2D_X(_CameraDepthTexture, sampler_CameraDepthTexture, i.screenPos.xy / i.screenPos.w).r;
                float sceneLinearEyeDepth = LinearEyeDepth(sceneRawDepth, _ZBufferParams);
                float objectLinearEyeDepth = LinearEyeDepth(i.screenPos.z, _ZBufferParams);
                float depthDifference = sceneLinearEyeDepth - objectLinearEyeDepth;
                
                float foamLine = 1.0 - saturate(depthDifference / _FoamDistance);
                
                float2 foamUV = i.uv;
                foamUV.x += _Time.y * _FoamSpeed;
                float foamNoise = Unity_GradientNoise_Deterministic(foamUV, _FoamScale);
                
                float foamMask = step(_FoamCutoff, foamNoise * foamLine);

                // -- FINAL COLOR --
                float4 finalColor = lerp(baseColor, _FoamColor, foamMask);
                finalColor.a = _Alpha;

                return finalColor;
            }
            ENDHLSL
        }
    }
}