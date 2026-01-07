Shader "Unlit/DimpleGlow"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _GlowColor ("Glow Color", Color) = (1,1,0,1)
        _GlowPower ("Glow Power", Range(0, 10)) = 5.0
        _GlowFalloff ("Glow Falloff", Range(0, 5)) = 2.0
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" "RenderPipeline"="UniversalPipeline" }
        LOD 100

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float2 uv           : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;
            float4 _GlowColor;
            float _GlowPower;
            float _GlowFalloff;

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = TRANSFORM_TEX(IN.uv, _MainTex);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                half4 color = tex2D(_MainTex, IN.uv);
                
                // Clamp UV coordinates to prevent extreme values
                float2 centeredUV = clamp(IN.uv, 0.0, 1.0) - 0.5;
                float dist = 1.0 - length(centeredUV * 2.0);
                
                // Use saturate to ensure glow is between 0 and 1
                float glow = saturate(pow(dist, _GlowFalloff) * _GlowPower);

                half4 glowColor = _GlowColor * glow;
                
                // Modulate the glow by the texture's alpha to prevent glow in transparent areas
                color.rgb += glowColor.rgb * color.a;

                return color;
            }
            ENDHLSL
        }
    }
    FallBack "Sprites/Default"
}
