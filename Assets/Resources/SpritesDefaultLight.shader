Shader "Custom/SpritesDefaultLight"
{
    Properties
    {
        _Color ("Tint", Color) = (0.2,0.2,0.2,1)
    	_LightPosition ("LightPosition", Vector) = (0,0,0,0)
    }

    SubShader
    {
        Tags
        { 
            "Queue"="Transparent" 
            //"IgnoreProjector"="True" 
            "RenderType"="Transparent" 
            //"PreviewType"="Plane"
            //"CanUseSpriteAtlas"="True"
        }

        //Cull Off
        Lighting Off
        ZWrite Off
        Fog { Mode Off }
        Blend One OneMinusSrcAlpha

        Pass
        {
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float3 normal   : NORMAL;
                float4 color    : COLOR;
            };

            struct v2f
            {
                float4 vertex   : SV_POSITION;
                float3 normal   : NORMAL;
                fixed4 color    : COLOR;
            	float4 posWorld : TEXCOORD0;
            };

            fixed4 _Color;
            
         	uniform float4 _LightPosition;
         	
         	
         	struct Lighting
			{
			    float3 Diffuse;
			    float3 Specular;
			};
			 
			struct PointLight
			{
				float3 position;
				float3 diffuseColor;
				float  diffusePower;
				float3 specularColor;
				float  specularPower;
			};
			 
			Lighting GetPointLight( PointLight light, float3 pos3D, float3 viewDir, float3 normal )
			{
				Lighting OUT;
				if( light.diffusePower > 0 )
				{
					float3 lightDir = light.position - pos3D; //3D position in space of the surface
					float distance = length( lightDir );
					lightDir = lightDir / distance; // = normalize( lightDir );
					distance = distance * distance; //This line may be optimised using Inverse square root
			 
					//Intensity of the diffuse light. Saturate to keep within the 0-1 range.
					float NdotL = clamp( dot( normal, lightDir ), 0.0, 1.0 );
					float intensity = NdotL;//saturate( NdotL );
			 
					// Calculate the diffuse light factoring in light color, power and the attenuation
					OUT.Diffuse = intensity * light.diffuseColor * light.diffusePower;// / distance;
			 
					//Calculate the half vector between the light vector and the view vector.
					//This is faster than calculating the actual reflective vector.
					float3 H = normalize( lightDir + viewDir );
			 
					//Intensity of the specular light
					float specularHardness = 4.0f;
					float NdotH = clamp( dot( normal, H ), 0.0, 1.0 );
					intensity = pow( NdotH, specularHardness );//saturate( NdotH ), specularHardness );
			 
					//Sum up the specular light factoring
					OUT.Specular = intensity * light.specularColor * light.specularPower;// / distance; 
				}
				return OUT;
			}
         	

            v2f vert(appdata_t IN)
            {
                v2f OUT;
                OUT.vertex = mul(UNITY_MATRIX_MVP, IN.vertex);
            	OUT.normal = normalize(mul(float4(IN.normal, 0.0), _World2Object).xyz);
            	OUT.posWorld = mul(_Object2World, IN.vertex);
            	OUT.color = IN.color * _Color;
                return OUT;
            }

            sampler2D _MainTex;

            fixed4 frag(v2f IN) : SV_Target
            {
            	fixed4 c = IN.color;
            	
            	PointLight light;
            	light.position = normalize(_LightPosition.xyz);
            	light.diffuseColor = float3(0.3,0.3,0.3);
            	light.diffusePower = 1.0;
            	light.specularColor = float3(1.0,1.0,1.0);
            	light.specularPower = 0.5;
            	float3 normalDirection = IN.normal;
            	float3 viewDirection = normalize(_LightPosition.xyz - IN.posWorld.xyz);
            	Lighting l = GetPointLight( light, IN.vertex, viewDirection, normalDirection );
            	c.rgb = c.rgb + l.Diffuse + l.Specular;
                c.rgb *= c.a;
                return c;
            }
        ENDCG
        }
    }
}