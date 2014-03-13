Shader "Custom/DiffuseNoTransparency" {
	SubShader {
		Tags {"RenderType"="Opaque"}
		LOD 200
		
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
			o.Alpha = 1.0;
		}
		
		ENDCG
	}

	Fallback "Transparent/VertexLit"
}