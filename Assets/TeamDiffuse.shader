Shader "Custom/Team Diffuse" {
	Properties {
		_MainTex ("Base Texture", 2D) = "white" {}
		_AuxTex ("Auxiliary map", 2D) = "white" {}
		_Color ("Highlight Color", Color) = (1,1,1,0)
		_TeamColor ("Team Color", Color) = (1,1,1,1)
		_SpecColor ("Specular Color", Color) = (0.5, 0.5, 0.5, 1)
	}

	SubShader {
		Tags { "RenderType"="Opaque" }
		LOD 300

		CGPROGRAM
		#pragma surface surf BlinnPhong

		sampler2D _MainTex;
		sampler2D _AuxTex;
		sampler2D _BumpMap;
		fixed4 _TeamColor;
                fixed4 _Color;

		struct Input {
			float2 uv_MainTex;
			float2 uv_BumpMap;
		};

		void surf(Input IN, inout SurfaceOutput o) {
			fixed4 c = tex2D(_MainTex, IN.uv_MainTex);
			fixed4 a = tex2D(_AuxTex, IN.uv_MainTex);
			fixed3 teamRGB = _TeamColor.rgb * a.r * a.g;
			o.Albedo = c.rgb * (1 - a.r) + teamRGB + _Color.rgb * _Color.a;
			o.Gloss = a.b * 3;
			o.Specular = 0.25;
		}
		ENDCG
	}
	FallBack "Diffuse"
}
