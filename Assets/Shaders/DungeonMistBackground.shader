Shader "Custom/DungeonMistBackground"
{
    Properties
    {
        [Header(Base Settings)]
        _MainTex ("Main Texture (Optional)", 2D) = "white" {}
        _UseTexture ("Use Main Texture", Range(0, 1)) = 0
        
        [Header(Mist Colors)]
        _MistColor1 ("Mist Color 1", Color) = (0.2, 0.15, 0.3, 1.0)
        _MistColor2 ("Mist Color 2", Color) = (0.1, 0.1, 0.2, 1.0)
        _MistColor3 ("Mist Color 3", Color) = (0.3, 0.2, 0.4, 1.0)
        _AmbientColor ("Ambient Color", Color) = (0.05, 0.05, 0.1, 1.0)
        
        [Header(Mist Movement)]
        _MistSpeed ("Mist Speed", Range(0.1, 2.0)) = 0.5
        _SwirlingIntensity ("Swirling Intensity", Range(0.1, 1.0)) = 0.3
        _MistScale ("Mist Scale", Range(0.5, 5.0)) = 2.0
        _MistDensity ("Mist Density", Range(0.1, 2.0)) = 1.0
        
        [Header(Gradient Control)]
        _GradientSpeed ("Gradient Speed", Range(0.05, 1.0)) = 0.2
        _GradientScale ("Gradient Scale", Range(1.0, 10.0)) = 3.0
        _VerticalGradient ("Vertical Gradient Strength", Range(0.0, 1.0)) = 0.4
        
        [Header(Visual Effects)]
        _Pixelation ("Pixelation", Range(1, 512)) = 1
        
        [Header(2D Lighting)]
        _LightSensitivity ("Light Sensitivity", Range(0.0, 2.0)) = 1.0
        _LightScattering ("Light Scattering", Range(0.0, 1.0)) = 0.3
        _ShadowStrength ("Shadow Strength", Range(0.0, 1.0)) = 0.5
        _NormalStrength ("Normal Strength", Range(0.0, 2.0)) = 0.5
        
        [Header(Final Output)]
        _OverallOpacity ("Overall Opacity", Range(0.0, 1.0)) = 0.8
    }
    
    SubShader
    {
        Tags 
        { 
            "RenderType" = "Transparent" 
            "Queue" = "Background"
            "RenderPipeline" = "UniversalPipeline"
        }
        
        Pass
        {
            Name "DungeonMistPass"
            Tags { "LightMode" = "Universal2D" }
            
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest Always
            Cull Off
            
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            
            // 2D Lighting support - simplified approach
            #pragma multi_compile _ _LIGHT2D_SHADOW
            #pragma multi_compile _ _LIGHT2D_COOKIES
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            
            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };
            
            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
                float3 positionWS : TEXCOORD1;
            };
            
            // Texture and Sampler
            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            
            // Properties
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MistColor1;
                float4 _MistColor2;
                float4 _MistColor3;
                float4 _AmbientColor;
                float _MistSpeed;
                float _SwirlingIntensity;
                float _MistScale;
                float _MistDensity;
                float _GradientSpeed;
                float _GradientScale;
                float _VerticalGradient;
                float _Pixelation;
                float _LightSensitivity;
                float _LightScattering;
                float _ShadowStrength;
                float _NormalStrength;
                float _UseTexture;
                float _OverallOpacity;
            CBUFFER_END
            
            // Improved noise functions with clamping to prevent flashing
            float2 hash22(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)), dot(p, float2(269.5, 183.3)));
                return -1.0 + 2.0 * frac(sin(p) * 43758.5453123);
            }
            
            float noise(float2 p)
            {
                const float K1 = 0.366025404; // (sqrt(3)-1)/2
                const float K2 = 0.211324865; // (3-sqrt(3))/6
                
                float2 i = floor(p + (p.x + p.y) * K1);
                float2 a = p - i + (i.x + i.y) * K2;
                float2 o = step(a.yx, a.xy);
                float2 b = a - o + K2;
                float2 c = a - 1.0 + 2.0 * K2;
                
                float3 h = max(0.5 - float3(dot(a, a), dot(b, b), dot(c, c)), 0.0);
                float3 n = h * h * h * h * float3(dot(a, hash22(i + 0.0)), dot(b, hash22(i + o)), dot(c, hash22(i + 1.0)));
                
                // Clamp the result to prevent extreme values
                return saturate(dot(n, float3(70.0, 70.0, 70.0)) * 0.5 + 0.5);
            }
            
            // Fractal noise with better stability
            float fbm(float2 p, int octaves)
            {
                float value = 0.0;
                float amplitude = 0.5;
                float frequency = 1.0;
                float maxValue = 0.0;
                
                for (int i = 0; i < octaves; i++)
                {
                    value += amplitude * noise(p * frequency);
                    maxValue += amplitude;
                    amplitude *= 0.5;
                    frequency *= 2.0;
                }
                
                // Normalize to prevent values exceeding [0,1]
                return saturate(value / maxValue);
            }
            
            // Swirling motion function
            float2 swirl(float2 uv, float time, float intensity)
            {
                float2 center = float2(0.5, 0.5);
                float2 toCenter = uv - center;
                float distance = length(toCenter);
                float angle = atan2(toCenter.y, toCenter.x);
                
                // Create gentle swirling motion
                float swirling = sin(time * 0.3 + distance * 3.0) * intensity * 0.1;
                angle += swirling;
                
                return center + distance * float2(cos(angle), sin(angle));
            }
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                
                VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionHCS = vertexInput.positionCS;
                output.positionWS = vertexInput.positionWS;
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.color = input.color;
                
                return output;
            }
            
            half4 frag(Varyings input) : SV_Target
            {
                float time = _Time.y;
                float2 uv = input.uv;
                
                // Apply pixelation effect - simplified version
                float2 workingUV = uv;
                if (_Pixelation > 1.0)
                {
                    workingUV = floor(uv * _Pixelation) / _Pixelation;
                }
                
                // Create multiple layers of moving mist using pixelated coordinates
                float2 mistUV1 = swirl(workingUV * _MistScale, time * _MistSpeed, _SwirlingIntensity);
                float2 mistUV2 = swirl(workingUV * _MistScale * 0.7, time * _MistSpeed * 0.8, _SwirlingIntensity * 0.7);
                float2 mistUV3 = swirl(workingUV * _MistScale * 1.3, time * _MistSpeed * 1.2, _SwirlingIntensity * 0.5);
                
                // Add slow drift to the coordinates
                mistUV1 += float2(time * _MistSpeed * 0.1, time * _MistSpeed * 0.05);
                mistUV2 += float2(time * _MistSpeed * 0.07, time * _MistSpeed * 0.12);
                mistUV3 += float2(time * _MistSpeed * 0.13, time * _MistSpeed * 0.08);
                
                // Generate layered noise for mist (with stability fixes)
                float mist1 = fbm(mistUV1, 3);
                float mist2 = fbm(mistUV2, 2);
                float mist3 = fbm(mistUV3, 2);
                
                // Combine mist layers with clamping
                float combinedMist = saturate((mist1 * 0.5 + mist2 * 0.3 + mist3 * 0.2) * _MistDensity);
                
                // Create ambient gradient that moves slowly
                float2 gradientUV = workingUV * _GradientScale + float2(time * _GradientSpeed * 0.1, time * _GradientSpeed * 0.05);
                float gradientNoise = fbm(gradientUV, 2);
                
                // Vertical gradient for depth (using original UV to maintain smooth gradient)
                float verticalGrad = lerp(1.0, 1.0 - uv.y, _VerticalGradient);
                
                // Color mixing based on mist density and gradients (clamped)
                float colorMix1 = smoothstep(0.2, 0.8, saturate(combinedMist + gradientNoise * 0.3));
                float colorMix2 = smoothstep(0.4, 0.9, saturate(combinedMist + sin(time * _GradientSpeed + workingUV.x * 2.0) * 0.2));
                
                // Interpolate between mist colors
                half3 mistColor = lerp(_MistColor1.rgb, _MistColor2.rgb, colorMix1);
                mistColor = lerp(mistColor, _MistColor3.rgb, colorMix2);
                
                // Apply ambient color as base
                half3 baseColor = lerp(_AmbientColor.rgb, mistColor, combinedMist);
                
                // Simple custom lighting simulation (no external dependencies)
                float lightEffect = 1.0;
                
                // Optional: Add simple directional lighting based on position
                if (_LightSensitivity > 0.001)
                {
                    // Create a simple directional light effect using working UV
                    float2 lightDir = normalize(float2(0.5, 0.8));
                    float lightDot = dot(normalize(workingUV - 0.5), lightDir);
                    lightEffect = lerp(1.0, 1.0 + lightDot * 0.3, _LightSensitivity);
                    
                    // Add subtle scattering in brighter areas
                    float scatteringEffect = lightEffect * _LightScattering * combinedMist * 0.2;
                    baseColor += scatteringEffect;
                }
                
                baseColor *= lightEffect;
                
                // Apply vertical gradient
                baseColor *= verticalGrad;
                
                // Optional texture blending (only if _UseTexture > 0)
                if (_UseTexture > 0.001)
                {
                    half4 mainTex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, workingUV);
                    baseColor = lerp(baseColor, mainTex.rgb, _UseTexture);
                }
                
                // Calculate alpha with soft edges (clamped)
                float alpha = saturate(combinedMist * 0.8 + 0.2) * _OverallOpacity;
                
                // Final color clamping to prevent any anomalies
                half3 finalColor = saturate(baseColor);
                
                return half4(finalColor, alpha);
            }
            
            ENDHLSL
        }
    }
    
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}