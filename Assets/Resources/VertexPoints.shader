// Shader based on 
//   - PointCloudShader1 : http://unitycoder.com/blog/
//   - GS Billboard shader
// Custom code added
//   - Buffer to handle per point color
//   - Geometry shader to create billboard quads for each point
//   - Point size specified based on screen space

Shader "DX11/VertexColorPoints" 
{
	Properties 
	{
		_OffsetColorMask1("Offset Color 1", Color) = (1.0,0.0,0.0,0.0)
		_OffsetColorMask2("Offset Color 2", Color) = (0.0,0.0,1.0,0.0)
	}
	SubShader 
	{
		Tags { "RenderType"="Transparent" "Queue"="Transparent" }
		Blend SrcAlpha OneMinusSrcAlpha

		Pass 
		{
			CGPROGRAM
			#pragma target 5.0

			#pragma vertex vert
			#pragma geometry geom
			#pragma fragment frag
			#include "UnityCG.cginc"

			StructuredBuffer<float3> buf_Points;
			StructuredBuffer<float4> buf_Colors;
			StructuredBuffer<float3> buf_ColorsOffset;
			StructuredBuffer<float3> buf_Normals;
			StructuredBuffer<float4> buf_Positions;
			StructuredBuffer<float>  buf_Sizes;
			StructuredBuffer<int>    buf_Selected;
			
			fixed4 _OffsetColorMask1;
			fixed4 _OffsetColorMask2;
			
			struct VS_INPUT
			{
				float4 vertex : POSITION;
                float3 normal : NORMAL;
                float3 tangent : TANGENT;
                float3 viewDir : NORMAL;
				uint id   : SV_VertexID;
				uint inst : SV_InstanceID;
			};
			
			struct GS_INPUT
			{
				float4	pos   : POSITION;
				float4  color : COLOR;
				float   psize : PSIZE0;
			};

			struct FS_INPUT
			{
				float4	pos:POSITION;
				float4  color:COLOR;
			};
			

			float3 GetSpecularColor(float3 vVertexNormal, float3 vVertexPosition)
			{
			    // Transform the Vertex and corresponding Normal into Model space
			    float3 vTransformedNormal = mul(_Object2World, float4( vVertexNormal, 1 ));
			    float3 vTransformedVertex = mul(_Object2World, float4( vVertexPosition, 1 ));
			 
			    // Get the directional vector to the light and to the camera
			    // originating from the vertex position
			    float3 vLightDirection = normalize( _WorldSpaceCameraPos - vTransformedVertex );
			    float3 vCameraDirection = normalize( _WorldSpaceCameraPos - vTransformedVertex );
			 
			    // Calculate the reflection vector between the incoming light and the
			    // normal (incoming angle = outgoing angle)
			    // We have to use the invert of the light direction because "reflect"
			    // expects the incident vector as its first parameter
			    float3 vReflection = reflect( -vLightDirection, vTransformedNormal );
			 
			    // Calculate specular component
			    // Based on the dot product between the reflection vector and the camera
			    // direction
			    float spec = pow( max( 0.0, dot( vCameraDirection, vReflection )), 32 );
			 
			    return float3( spec, spec, spec );
			}
 
			float3 GetAmbientColor()
			{
			    // Ambient material is 0.2/0/0
			    // Ambient light is 0.2/0.2/0.2
			    return float3( 0.75f, 0.75f, 0.75f );
			}
 
			float3 GetDiffuseColor(float3 vVertexNormal)
			{
			    // Transform the normal from Object to Model space
			    // we also normalize the vector just to be sure ...
			    float3 vTransformedNormal = normalize( mul( _Object2World, float4( vVertexNormal, 1 )));
			 
			    // Get direction of light in Model space
			    float3 vLightDirection = normalize( _WorldSpaceCameraPos - vTransformedNormal );
			 
			    // Calculate Diffuse intensity
			    float fDiffuseIntensity = max( 0.0, dot( vTransformedNormal, vLightDirection ));
			 
			    // Calculate resulting Color
			    float3 vDiffuseColor = float3( 1.0, 1.0, 1.0 ) * fDiffuseIntensity;
			 
			    return vDiffuseColor;
			} 

			GS_INPUT vert(VS_INPUT input)
			{
				GS_INPUT output;

				// calculate the position, make the offset in screen coordinates
				float3 worldPos = buf_Points[input.id] + mul(UNITY_MATRIX_T_MV,float4(buf_Positions[input.inst].x,buf_Positions[input.inst].y,buf_Positions[input.inst].z,1.0f));
				output.pos =  float4(worldPos,1.0f);
				
				// redetermine alpha based on screen position
				float alpha = buf_Colors[input.id].w;
				float4 screenCoord = mul(mul(UNITY_MATRIX_MVP, _World2Object),float4(worldPos,1.0f));
				screenCoord /= screenCoord.w; // perspective divide
				screenCoord.x = (screenCoord.x+1.0f)*_ScreenParams.x/2.0f;// + fViewport[0]; // viewport transformation
				screenCoord.y = (screenCoord.y+1.0f)*_ScreenParams.y/2.0f;// + fViewport[1]; // viewport transformation
				//if(buf_Positions[0].w == 1)
				{
					if(input.inst == 0)
					{
						
						alpha -= abs(buf_Positions[0].w-buf_Positions[1].w)/2.0f*clamp(((screenCoord.x-_ScreenParams.x*0.4f)/(_ScreenParams.x*0.1f))*alpha,0.0f,alpha);
					}
					else if(input.inst == 1)
					{
						alpha -= abs(buf_Positions[0].w-buf_Positions[1].w)/2.0f*clamp(((_ScreenParams.x-screenCoord.x-_ScreenParams.x*0.4f)/(_ScreenParams.x*0.1f))*alpha,0.0f,alpha);
					}
				}
				float3 colorOffset = float3(0,0,0);
				if(buf_Selected[input.id] == 1)
				{					
					if(input.inst == 0)
						colorOffset = float3(buf_ColorsOffset[input.id].x*(1.0-buf_Positions[1].w),
											 buf_ColorsOffset[input.id].y*(1.0-buf_Positions[1].w),
											 buf_ColorsOffset[input.id].z*(1.0-buf_Positions[1].w));
					else
						colorOffset = float3(buf_ColorsOffset[input.id].x*(1.0-buf_Positions[1].w),
											 buf_ColorsOffset[input.id].y*(1.0-buf_Positions[1].w),
											 buf_ColorsOffset[input.id].z*(1.0-buf_Positions[1].w));
				}
				else
				{
					colorOffset = float3(0.4-buf_Colors[input.id].x,0.4-buf_Colors[input.id].y,0.4-buf_Colors[input.id].z);
				}
				// from 0 to 1, 0 should be both colors, 1 just the one from my instance, interpolate in between
				
				float pointOffset = 0.0;
				if(buf_Selected[input.id] == 1)
				{
					if(input.inst == 0)
					{
						pointOffset = -3.0*2.0*buf_ColorsOffset[input.id].x*buf_Positions[1].w;
						alpha = clamp(alpha-0.4*2.0*buf_ColorsOffset[input.id].x*buf_Positions[1].w,0,alpha);
					}
					else
					{
						pointOffset = -3.0*2.0*buf_ColorsOffset[input.id].z*buf_Positions[1].w;
						alpha = clamp(alpha-0.4*2.0*buf_ColorsOffset[input.id].z*buf_Positions[1].w,0,alpha);
					}
				}
				
				float3 ambientColor = (buf_Colors[input.id]+colorOffset) * GetAmbientColor();
				float3 diffuseColor = (buf_Colors[input.id]+colorOffset) * GetDiffuseColor(buf_Normals[input.id]);
				float3 specularColor = GetSpecularColor(buf_Normals[input.id], buf_Points[input.id]);

				// Compute the color per vertex				
				output.color = float4(ambientColor + diffuseColor + specularColor, alpha);//float4(buf_Colors[input.id].x+colorOffset.x, intensity+buf_Colors[input.id].y+colorOffset.y, intensity+buf_Colors[input.id].z+colorOffset.z, alpha);
				output.psize = buf_Sizes[input.id] + pointOffset; // need to be sending selected/deselected as values
				
				return output;
			}
			
			// Geometry Shader -----------------------------------------------------
			[maxvertexcount(4)]
			void geom(point GS_INPUT p[1], inout TriangleStream<FS_INPUT> triStream)
			{
				if(p[0].color.w == 0.0f)
					return;

				// calculate billboard vectors
				float3 up = float3(0, 1, 0);
				up = mul(UNITY_MATRIX_T_MV,up);
				float3 look = _WorldSpaceCameraPos - p[0].pos;
				look = normalize(look);
				float3 right = cross(up, look);
				
				float4 pos = p[0].pos;
				
				// calculate size of each point in the pointcloud based on screenspace point size defined in the material properties
				float dist = distance(_WorldSpaceCameraPos, mul(_Object2World, pos));
				float scale = 2.0f * dist * tan(1.04719755f / 2.0f); // dist * 1.15470053678f;  (considering fov is always 60)
				float halfS = 0.5f * p[0].psize / _ScreenParams.y * scale; // calculate size in 
				
				// create four vertices
				float4 v[4];
				v[0] = float4(pos + halfS * right - halfS * up, 1.0f);
				v[1] = float4(pos + halfS * right + halfS * up, 1.0f);
				v[2] = float4(pos - halfS * right - halfS * up, 1.0f);
				v[3] = float4(pos - halfS * right + halfS * up, 1.0f);
				
				// get current modelview matrix
				float4x4 vp = mul(UNITY_MATRIX_MVP, _World2Object);
				
				FS_INPUT pIn;
				pIn.pos = mul(vp, v[0]);
				pIn.color = p[0].color;
				triStream.Append(pIn);

				pIn.pos =  mul(vp, v[1]);
				pIn.color = p[0].color;
				triStream.Append(pIn);

				pIn.pos =  mul(vp, v[2]);
				pIn.color = p[0].color;
				triStream.Append(pIn);

				pIn.pos =  mul(vp, v[3]);
				pIn.color = p[0].color;
				triStream.Append(pIn);
			}

			float4 frag(FS_INPUT i) : COLOR
			{
				return i.color;
			}

			ENDCG

		}
	}

	Fallback Off
}

