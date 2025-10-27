Shader "Custom/ColorPicker" {
  Properties {
    _Color ("Main Color", Color) = (1,1,1,1)
    _MainTex ("Base (RGB)", 2D) = "white" {}
    _Cutoff ("Alpha cutoff", Range(0,1)) = 0.5
  }
  SubShader {
    Tags {"Queue"="AlphaTest+20" "IgnoreProjector"="True" "RenderType"="TransparentCutout"}

    Lighting Off

    CGPROGRAM
    #pragma surface surf Lambert nofog

    sampler2D _MainTex;
    fixed4 _Color;
    fixed _Cutoff;

    struct Input {
      float2 uv_MainTex;
    };

    void surf (Input IN, inout SurfaceOutput o) {
      half4 tex = tex2D (_MainTex, IN.uv_MainTex);
      float4 c = tex * _Color;
      o.Emission = c.rgb;
      o.Albedo = 0;
      if (c.a < _Cutoff) {
        discard;
      }
      o.Alpha = 1;
    }
    ENDCG
  }
  FallBack "Unlit/Diffuse"
}
