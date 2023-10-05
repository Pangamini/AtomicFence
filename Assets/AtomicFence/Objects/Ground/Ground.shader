Shader "Custom/Ground"
{
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        
        _MainTex ("Grass Albedo (RGB)", 2D) = "white" {}
        [NormalMap][NoScaleOffset] _BumpMap ("Grass Normal", 2D) = "white" {}
        [NoScaleOffset]_OcclusionMap ("Grass Occlusion (g)", 2D) = "white" {}
        [NoScaleOffset]_MetallicGlossMap ("Grass Metallic", 2D) = "black" {}
        
        _MudAlbedo ("Mud Albedo (RGB)", 2D) = "white" {}
        [NormalMap][NoScaleOffset] _MudBumpMap ("Mud Normal", 2D) = "white" {}
        [NoScaleOffset]_MudOcclusionMap ("Mud Occlusion (g)", 2D) = "white" {}
        [NoScaleOffset]_MudMetallicGlossMap ("Mud Metallic", 2D) = "black" {}
        _Smoothness("Smoothness", Range(0,2)) = 1
        
        _MudMap ("Mud Map", 2D) = "black" {}
        _MudNoise("Mud Noise", 2D) = "white" {}
        [PowerSlider(2)]_MudPower("Mud Power", Range(1, 50)) = 1
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 200

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.0

        float _MudPower;
        float4 _MainTex_ST;
        sampler2D _MainTex;
        sampler2D _BumpMap;
        sampler2D _OcclusionMap;
        sampler2D _MudMap;
        sampler2D _MudNoise;
        float4 _MudNoise_ST;
        sampler2D _MetallicGlossMap;

        sampler2D _MudAlbedo;
        float4 _MudAlbedo_ST;
        sampler2D _MudBumpMap;
        sampler2D _MudOcclusionMap;
        sampler2D _MudMetallicGlossMap;
        float4 _MudMap_TexelSize;
        float _Smoothness;
        
        struct Input
        {
            float2 mainUv;
            float2 mudUv;
            float2 mudMaskUv;
            float2 mudNoiseUv;
        };
        
        fixed4 _Color;

        float4 cubic(float v)
        {
            float4 n = float4(1.0, 2.0, 3.0, 4.0) - v;
            float4 s = n * n * n;
            float x = s.x;
            float y = s.y - 4.0 * s.x;
            float z = s.z - 4.0 * s.y + 6.0 * s.x;
            float w = 6.0 - x - y - z;
            return float4(x, y, z, w) * (1.0 / 6.0);
        }

        float4 BicubicSample(sampler2D mainTex, float4 textureInfo = float4(1, 1, 1, 1), float2 uv = float2(0, 0))
        {
            float2 invTexSize = textureInfo.xy;
            float2 texCoords = uv * textureInfo.zw - 0.5;
            float2 fxy = frac(texCoords);
            texCoords -= fxy;

            float4 xcubic = cubic(fxy.x);
            float4 ycubic = cubic(fxy.y);
            float4 c = texCoords.xxyy + float2(-0.5, +1.5).xyxy;
            float4 s = float4(xcubic.xz + xcubic.yw, ycubic.xz + ycubic.yw);
            float sx = s.x / (s.x + s.y);
            float sy = s.z / (s.z + s.w);

            float4 offset = c + float4(xcubic.yw, ycubic.yw) / s;

            offset *= invTexSize.xxyy;

            float4 sample0 = tex2D(mainTex, offset.xz);
            float4 sample1 = tex2D(mainTex, offset.yz);
            float4 sample2 = tex2D(mainTex, offset.xw);
            float4 sample3 = tex2D(mainTex, offset.yw);

            return lerp(lerp(sample3, sample2, sx), lerp(sample1, sample0, sx), sy);
        }
        
        float2 TransformTex(float2 uv, float4 st)
        {
            return uv.xy * st.xy + st.zw;
        }
        
        void vert (inout appdata_full v, out Input o)
        {
            o.mainUv = TransformTex(v.texcoord, _MainTex_ST);
            o.mudUv = TransformTex(v.texcoord, _MudAlbedo_ST);
            o.mudNoiseUv = TransformTex(v.texcoord, _MudNoise_ST);
            o.mudMaskUv = v.texcoord;
        }
        
        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            const float mudNoise = pow(tex2D(_MudNoise, IN.mudNoiseUv)*5,4);
            const float mudBase = pow(1-pow(BicubicSample(_MudMap, _MudMap_TexelSize, IN.mudMaskUv), _MudPower), 2.2);
            const float mudMask = saturate(saturate(1-mudBase-mudNoise*(mudBase))*2);
            
            // o.Albedo = mudMask;
            // return;
            const fixed4 grassAlbedo = tex2D (_MainTex, IN.mainUv) * _Color;
            const fixed4 grassMetalGloss = tex2D(_MetallicGlossMap, IN.mainUv);
            const float3 grassNormal = UnpackNormal(tex2D(_BumpMap, IN.mainUv));
            const fixed grassOcclusion = tex2D(_OcclusionMap, IN.mainUv).g;
            
            const fixed4 mudAlbedo = tex2D (_MudAlbedo, IN.mudUv) * _Color;
            const fixed4 mudMetalGloss = tex2D(_MudMetallicGlossMap, IN.mudUv);
            const float3 mudNormal = UnpackNormal(tex2D(_MudBumpMap, IN.mudUv));
            const fixed mudOcclusion = tex2D(_MudOcclusionMap, IN.mainUv).g;
            
            o.Albedo = lerp(grassAlbedo.rgb, mudAlbedo.rgb, mudMask);
            o.Normal = normalize(lerp(grassNormal, mudNormal, mudMask));
            o.Occlusion = lerp(grassOcclusion, mudOcclusion, mudMask);
            o.Smoothness = (lerp(grassMetalGloss.a, mudMetalGloss.a, mudMask)) * _Smoothness;
            o.Metallic = lerp(grassMetalGloss.r,  mudMetalGloss.a*0.5, mudMask);
            o.Alpha = grassAlbedo.a;
        }
        ENDCG
    }
    FallBack "Diffuse"
}
