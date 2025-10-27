Shader "Custom/OutlineMesh" {
  Properties {
    _Color ("Main Color", Color) = (1,1,1,1)
  }
  SubShader {
    Tags {"Queue"="Geometry" "IgnoreProjector"="True" "RenderType"="Geometry"}
    LOD 100

    CGPROGRAM
    #pragma surface surf Lambert vertex:vert nofog

    fixed4 _Color;

    struct Input {
      float4 color : COLOR;
    };

    void vert (inout appdata_full v) {
    }

    void surf (Input IN, inout SurfaceOutput o) {
      o.Albedo = 0;
      o.Emission = _Color * IN.color.rgb;
      o.Alpha = 1;
    }
    ENDCG
  }

  FallBack "Unlit/Diffuse"
}
