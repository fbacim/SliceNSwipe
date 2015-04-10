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
		_SelectedSize("Selected Size", Float) = 0.0
		_DeselectedSize("Deselected Size", Float) = 0.0
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

			// common point cloud rendering structures
			StructuredBuffer<float3> buf_Points;
			StructuredBuffer<float4> buf_Colors;
			StructuredBuffer<float3> buf_ColorsOffset;
			StructuredBuffer<float3> buf_Normals;
			StructuredBuffer<float4> buf_Positions;
			StructuredBuffer<float>  buf_Sizes;
			StructuredBuffer<int>    buf_Selected;
			StructuredBuffer<int>    buf_Highlighted;
			
			// used for lasso
			StructuredBuffer<float3> buf_Lasso;
			StructuredBuffer<int>    buf_UseLasso;
			
			fixed4 _OffsetColorMask1;
			fixed4 _OffsetColorMask2;
			float _SelectedSize;
			float _DeselectedSize;
			
			struct VS_INPUT
			{
				float4 vertex : POSITION;
                float3 normal : NORMAL;
                float3 tangent : TANGENT;
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
			

			float3 GetSpecularColor(float3 vVertexNormal, float3 vVertexPosition, float3 vLightPosition);
			float3 GetAmbientColor();
			float3 GetDiffuseColor(float3 vVertexNormal, float3 vLightPosition);
			
			int isLeft( float3 P0, float3 P1, float3 P2 );
//			int wn_PnPoly( float3 P, float3 V, int n );

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
				if(input.inst == 0)
				{
					alpha -= abs(buf_Positions[0].w-buf_Positions[1].w)/2.0f*clamp(((screenCoord.x-_ScreenParams.x*0.4f)/(_ScreenParams.x*0.1f))*alpha,0.0f,alpha);
				}
				else if(input.inst == 1)
				{
					alpha -= abs(buf_Positions[0].w-buf_Positions[1].w)/2.0f*clamp(((_ScreenParams.x-screenCoord.x-_ScreenParams.x*0.4f)/(_ScreenParams.x*0.1f))*alpha,0.0f,alpha);
				}
				
				float3 oldColorOffset = buf_ColorsOffset[input.id];
				if(buf_UseLasso[0] > 0 && buf_Selected[input.id] == 1)
				{
					float3 point2D = screenCoord;
					
					int wn = 0;    // the  winding number counter
					// loop through all edges of the polygon
					for (int i=0; i<buf_UseLasso[0]-1; i++) {   // edge from V[i] to  V[i+1]
						// calculate screen coordinates for lasso points
						float4 Vi = mul(mul(UNITY_MATRIX_MVP, _World2Object),float4(buf_Lasso[i],1.0f));
						Vi /= Vi.w; // perspective divide
						Vi.x = (Vi.x+1.0f)*_ScreenParams.x/2.0f;// + fViewport[0]; // viewport transformation
						Vi.y = (Vi.y+1.0f)*_ScreenParams.y/2.0f;// + fViewport[1]; // viewport transformation
						float4 Vi1 = mul(mul(UNITY_MATRIX_MVP, _World2Object),float4(buf_Lasso[i+1],1.0f));
						Vi1 /= Vi1.w; // perspective divide
						Vi1.x = (Vi1.x+1.0f)*_ScreenParams.x/2.0f;// + fViewport[0]; // viewport transformation
						Vi1.y = (Vi1.y+1.0f)*_ScreenParams.y/2.0f;// + fViewport[1]; // viewport transformation
						if (Vi.y <= point2D.y) {          // start y <= P.y
							if (Vi1.y  > point2D.y)      // an upward crossing
								if (isLeft( Vi, Vi1, point2D) > 0)  // P left of  edge
									++wn;            // have  a valid up intersect
						}
						else {                        // start y > P.y (no test needed)
							if (Vi1.y  <= point2D.y)     // a downward crossing
								if (isLeft( Vi, Vi1, point2D) < 0)  // P right of  edge
									--wn;            // have  a valid down intersect
						}
					}
					
					if(wn == 0)//wn_PnPoly(point2D,buf_Lasso[0],buf_UseLasso[0]-1) == 0) // outside
					{
						oldColorOffset.x = 0.5F;
						oldColorOffset.y = -0.5F;
						oldColorOffset.z = -0.5F;
					}
					else
					{
						oldColorOffset.x = -0.5F;
						oldColorOffset.y = -0.5F;
						oldColorOffset.z = 0.5F;
					}
				}
				
				float3 colorOffset = float3(0,0,0);
				if(buf_Selected[input.id] == 1)
				{
					// make sure we remove (80%) color if negative offset
					if(oldColorOffset.x < 0.0f && buf_Positions[1].w == 0)
						colorOffset.x = -buf_Colors[input.id].x*0.8f;
					else
						colorOffset.x = oldColorOffset.x*(1.0f-buf_Positions[1].w);
					if(oldColorOffset.y < 0.0f && buf_Positions[1].w == 0)
						colorOffset.y = -buf_Colors[input.id].y*0.8f;
					else
						colorOffset.y = oldColorOffset.y*(1.0f-buf_Positions[1].w);
					if(oldColorOffset.z < 0.0f && buf_Positions[1].w == 0)
						colorOffset.z = -buf_Colors[input.id].z*0.8f;
					else
						colorOffset.z = oldColorOffset.z*(1.0f-buf_Positions[1].w);
				}
				else
				{
					// if not selected, make it grey
					colorOffset = float3(0.4f-buf_Colors[input.id].x,0.4f-buf_Colors[input.id].y,0.4f-buf_Colors[input.id].z);
				}
				// from 0 to 1, 0 should be both colors, 1 just the one from my instance, interpolate in between
				
				// this is for the swipe portion, when we have two options on screen
				float sizeOffset = 0.0f;
				if(buf_Selected[input.id] == 1)
				{
					if(input.inst == 0)
					{
						// since we are calculating alpha based on the side, use it to determine side for colors size offset
						float newAlpha = clamp(alpha-0.4f*2.0f*oldColorOffset.x*abs(buf_Positions[input.inst].w),0,alpha);
						if(newAlpha < alpha)
						{
							sizeOffset = -(_SelectedSize-_DeselectedSize)*abs(buf_Positions[input.inst].w);
							colorOffset = float3(0.4f-buf_Colors[input.id].x,0.4f-buf_Colors[input.id].y,0.4f-buf_Colors[input.id].z);
						}
						alpha = newAlpha;
					}
					else
					{
						float newAlpha = clamp(alpha-0.4f*2.0f*oldColorOffset.z*buf_Positions[input.inst].w,0,alpha);
						if(newAlpha < alpha)
						{
							sizeOffset = -(_SelectedSize-_DeselectedSize)*abs(buf_Positions[input.inst].w);
							colorOffset = float3(0.4f-buf_Colors[input.id].x,0.4f-buf_Colors[input.id].y,0.4f-buf_Colors[input.id].z);
						}
						alpha = newAlpha;
					}
				}
				
				float3 outputColor = (buf_Colors[input.id]+colorOffset);
								
				// make it yellow if highlighted
				if(buf_Highlighted[input.id] == 1)
				{
					outputColor.x = 0.9f;
					outputColor.y = 0.9f;
					outputColor.z = 0.0f;
				}
				else if(buf_Highlighted[input.id] == 2)
				{
					outputColor.x -= 0.3f;
					outputColor.y -= 0.3f;
					outputColor.z -= 0.3f;
				}
				
				// if normals are available, use phong shading
				if(length(buf_Normals[input.id]) > 0.0f)
				{
					float3 viewDir = mul(UNITY_MATRIX_T_MV,float4(0,0,-1,1));
					float angle = acos(clamp(dot(normalize(buf_Normals[input.id]), normalize(viewDir)),-1.0f,1.0f));
					float3 twoSidedNormal = normalize(abs(angle) > 1.57f ? buf_Normals[input.id] : -buf_Normals[input.id]);
				
					float3 ambientColor = outputColor * GetAmbientColor();
					float3 diffuseColor = outputColor * GetDiffuseColor(twoSidedNormal,_WorldSpaceCameraPos);
					float3 specularColor = GetSpecularColor(twoSidedNormal, buf_Points[input.id],_WorldSpaceCameraPos);

					// Compute the color per vertex				
					output.color = saturate(float4(ambientColor + diffuseColor + specularColor, alpha));
				}
				else 
				{
					output.color = saturate(float4(outputColor, alpha));
				}
				output.psize = buf_Sizes[input.id] + sizeOffset; // need to be sending selected/deselected as values
				
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
			
			float3 GetSpecularColor(float3 vVertexNormal, float3 vVertexPosition, float3 vLightPosition)
			{
			    // Transform the Vertex and corresponding Normal into Model space
			    float3 vTransformedNormal = mul(_Object2World, float4( vVertexNormal, 1 ));
			    float3 vTransformedVertex = mul(_Object2World, float4( vVertexPosition, 1 ));
			 
			    // Get the directional vector to the light and to the camera
			    // originating from the vertex position
			    float3 vLightDirection = normalize( vLightPosition - vTransformedVertex );
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
			    return float3( 0.5f, 0.5f, 0.5f );
			}
 
			float3 GetDiffuseColor(float3 vVertexNormal, float3 vLightPosition)
			{
			    // Transform the normal from Object to Model space
			    // we also normalize the vector just to be sure ...
			    float3 vTransformedNormal = normalize( mul( _Object2World, float4( vVertexNormal, 1 )));
			 
			    // Get direction of light in Model space
			    float3 vLightDirection = normalize( vLightPosition - vTransformedNormal );
			 
			    // Calculate Diffuse intensity
			    float fDiffuseIntensity = max( 0.0, dot( vTransformedNormal, vLightDirection ));
			 
			    // Calculate resulting Color
			    float3 vDiffuseColor = float3( 1.0, 1.0, 1.0 ) * fDiffuseIntensity;
			 
			    return vDiffuseColor;
			}
			
			int isLeft( float3 P0, float3 P1, float3 P2 )
			{
				return (int)( (P1.x - P0.x) * (P2.y - P0.y) - (P2.x -  P0.x) * (P1.y - P0.y) );
			}
			
//			int wn_PnPoly( float3 P, float3 V[], int n )
//			{
//				int wn = 0;    // the  winding number counter
//				
//				// loop through all edges of the polygon
//				for (int i=0; i<n; i++) {   // edge from V[i] to  V[i+1]
//					if (V[i].y <= P.y) {          // start y <= P.y
//						if (V[i+1].y  > P.y)      // an upward crossing
//							if (isLeft( V[i], V[i+1], P) > 0)  // P left of  edge
//								++wn;            // have  a valid up intersect
//					}
//					else {                        // start y > P.y (no test needed)
//						if (V[i+1].y  <= P.y)     // a downward crossing
//							if (isLeft( V[i], V[i+1], P) < 0)  // P right of  edge
//								--wn;            // have  a valid down intersect
//					}
//				}
//				return wn;
//			}
			

			ENDCG

		}
	}

	Fallback Off
}

