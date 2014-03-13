﻿Shader "Custom/DiffuseZTop" {
	SubShader {
		Tags {"Queue"="Overlay" "RenderType"="Opaque"}
		LOD 200
		ZTest Always
		
		CGPROGRAM
		#pragma surface surf Lambert finalcolor:mycolor

		struct Input {
			float4 color : COLOR;
		};
		
		void mycolor (Input IN, SurfaceOutput o, inout fixed4 color)
		{
			color = IN.color*0.5;
		}
		
		void surf (Input IN, inout SurfaceOutput o) {
			fixed4 c = IN.color;
			o.Albedo = c.rgb;
			o.Alpha = c.a;
		}
		ENDCG
	}

	Fallback "Transparent/VertexLit"
}