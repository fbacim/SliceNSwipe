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

			GS_INPUT vert (uint id : SV_VertexID, uint inst : SV_InstanceID)
			{
				GS_INPUT output;

				// calculate the position, make the offset in screen coordinates
				float3 worldPos = buf_Points[id] + mul(UNITY_MATRIX_T_MV,float4(buf_Positions[inst].x,buf_Positions[inst].y,buf_Positions[inst].z,1.0f));
				output.pos =  float4(worldPos,1.0f);
				
				// redetermine alpha based on screen position
				float alpha = buf_Colors[id].w;
				float4 screenCoord = mul(mul(UNITY_MATRIX_MVP, _World2Object),float4(worldPos,1.0f));
				screenCoord /= screenCoord.w; // perspective divide
				screenCoord.x = (screenCoord.x+1.0f)*_ScreenParams.x/2.0f;// + fViewport[0]; // viewport transformation
				screenCoord.y = (screenCoord.y+1.0f)*_ScreenParams.y/2.0f;// + fViewport[1]; // viewport transformation
				//if(buf_Positions[0].w == 1)
				{
					if(inst == 0)
					{
						
						alpha -= abs(buf_Positions[0].w-buf_Positions[1].w)/2.0f*clamp(((screenCoord.x-_ScreenParams.x*0.4f)/(_ScreenParams.x*0.1f))*alpha,0.0f,alpha);
					}
					else if(inst == 1)
					{
						alpha -= abs(buf_Positions[0].w-buf_Positions[1].w)/2.0f*clamp(((_ScreenParams.x-screenCoord.x-_ScreenParams.x*0.4f)/(_ScreenParams.x*0.1f))*alpha,0.0f,alpha);
					}
				}
				float3 colorOffset = float3(0,0,0);
				if(buf_Selected[id] == 1)
				{					
					if(inst == 0)
						colorOffset = float3(buf_ColorsOffset[id].x*(1.0-buf_Positions[1].w),//_OffsetColorMask1.x*buf_Positions[1].w),
											 buf_ColorsOffset[id].y*(1.0-buf_Positions[1].w),//_OffsetColorMask1.y*buf_Positions[1].w),
											 buf_ColorsOffset[id].z*(1.0-buf_Positions[1].w));//_OffsetColorMask1.z*buf_Positions[1].w));
					else
						colorOffset = float3(buf_ColorsOffset[id].x*(1.0-buf_Positions[1].w),//_OffsetColorMask2.x*buf_Positions[1].w),
											 buf_ColorsOffset[id].y*(1.0-buf_Positions[1].w),//_OffsetColorMask2.y*buf_Positions[1].w),
											 buf_ColorsOffset[id].z*(1.0-buf_Positions[1].w));//_OffsetColorMask2.z*buf_Positions[1].w));

//					if(inst == 0)
//						colorOffset = float3(buf_ColorsOffset[id].x*(1.0-_OffsetColorMask1.x*buf_Positions[1].w),
//											 buf_ColorsOffset[id].y*(1.0-_OffsetColorMask1.y*buf_Positions[1].w),
//											 buf_ColorsOffset[id].z*(1.0-_OffsetColorMask1.z*buf_Positions[1].w));
//					else
//						colorOffset = float3(buf_ColorsOffset[id].x*(1.0-_OffsetColorMask2.x*buf_Positions[1].w),
//											 buf_ColorsOffset[id].y*(1.0-_OffsetColorMask2.y*buf_Positions[1].w),
//											 buf_ColorsOffset[id].z*(1.0-_OffsetColorMask2.z*buf_Positions[1].w));
				}
				else
				{
					colorOffset = float3(0.4-buf_Colors[id].x,0.4-buf_Colors[id].y,0.4-buf_Colors[id].z);//_OffsetColorMask1.z*buf_Positions[1].w));
				}
				// from 0 to 1, 0 should be both colors, 1 just the one from my instance, interpolate in between
				
				float pointOffset = 0.0;
				if(buf_Selected[id] == 1)
				{
					if(inst == 0)
					{
						pointOffset = -3.0*2.0*buf_ColorsOffset[id].x*buf_Positions[1].w;
						alpha = clamp(alpha-0.4*2.0*buf_ColorsOffset[id].x*buf_Positions[1].w,0,alpha);
					}
					else
					{
						pointOffset = -3.0*2.0*buf_ColorsOffset[id].z*buf_Positions[1].w;
						alpha = clamp(alpha-0.4*2.0*buf_ColorsOffset[id].z*buf_Positions[1].w,0,alpha);
					}
				}
				
				// transform normal to camera space and normalize it
				float3 normalDir = normalize(mul(UNITY_MATRIX_T_MV,float4(buf_Normals[id].x,buf_Normals[id].y,buf_Normals[id].z,1.0f)));
				float3 lightDir = normalize(mul(UNITY_MATRIX_MVP,float4(0.0f,0.0f,-1.0f,1.0f)));

				// compute the intensity as the dot product
				//s the max prevents negative intensity values
				float intensity = 1.0f;
				
				if(length(buf_Normals[id]) > 0.0)
				    intensity = max(dot(normalDir, lightDir), 0.0);

				// Compute the color per vertex				
				output.color = float4(intensity + buf_Colors[id].x+colorOffset.x, intensity + buf_Colors[id].y+colorOffset.y, intensity + buf_Colors[id].z+colorOffset.z, alpha);
				output.psize = buf_Sizes[id] + pointOffset; // need to be sending selected/deselected as values
				
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

			float4 frag (FS_INPUT i) : COLOR
			{
				return i.color;
			}

			ENDCG

		}
	}

	Fallback Off
}

