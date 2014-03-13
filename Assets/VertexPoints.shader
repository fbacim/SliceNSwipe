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
		_PointSize("PointSize", Float) = 3.0
		_Alpha("Alpha", Float) = 0.5
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
			StructuredBuffer<float3> buf_Colors;
			StructuredBuffer<float3> buf_Positions;
			float _PointSize;
			float _Alpha;

			struct GS_INPUT
			{
				float4	pos		: POSITION;
				float4  color   : COLOR;
			};

			struct FS_INPUT
			{
				float4	pos		: POSITION;
				float4  color   : COLOR;
			};

			GS_INPUT vert (uint id : SV_VertexID, uint inst : SV_InstanceID)
			{
				GS_INPUT output = (GS_INPUT)0;

				// calculate the position, make the offset in screen coordinates
				float3 worldPos = buf_Points[id] + mul(UNITY_MATRIX_T_MV,float4(buf_Positions[inst].x*buf_Positions[inst].z,buf_Positions[inst].y*buf_Positions[inst].z,0.0f,1.0f));
				output.pos =  float4(worldPos,1.0f);
				
				// redetermine alpha based on screen position
				float alpha = _Alpha;
				float4 screenCoord = mul(mul(UNITY_MATRIX_MVP, _World2Object),float4(worldPos,1.0f));
				screenCoord /= screenCoord.w; // perspective divide
				screenCoord.x = (screenCoord.x+1.0f)*_ScreenParams.x/2.0f;// + fViewport[0]; // viewport transformation
				screenCoord.y = (screenCoord.y+1.0f)*_ScreenParams.y/2.0f;// + fViewport[1]; // viewport transformation
				
				if(inst == 0)
				{
					if(screenCoord.x > _ScreenParams.x*0.5f)
						alpha = 0.0f;
					else
						alpha -= abs(buf_Positions[0].x-buf_Positions[1].x)/2.0f*clamp(((screenCoord.x-_ScreenParams.x*0.4f)/(_ScreenParams.x*0.1f))*_Alpha,0.0f,_Alpha);
				}
				else if(inst == 1)
				{
					if(screenCoord.x < _ScreenParams.x*0.5f)
						alpha = 0.0f;
					else
						alpha -= abs(buf_Positions[0].x-buf_Positions[1].x)/2.0f*clamp(((_ScreenParams.x-screenCoord.x-_ScreenParams.x*0.4f)/(_ScreenParams.x*0.1f))*_Alpha,0.0f,_Alpha);
				}
				//output.color  =  float4(buf_Colors[id], alpha);
				if(inst == 0)
					output.color  =  float4(1,0,0,alpha);
				else
					output.color  =  float4(0,1,0,alpha);

				return output;
			}
			
			// Geometry Shader -----------------------------------------------------
			[maxvertexcount(4)]
			void geom(point GS_INPUT p[1], inout TriangleStream<FS_INPUT> triStream)
			{
				if(p[0].color.w == 0.0f)
					return;
				
				// calculate billboard vectors
				float4x4 vp = mul(UNITY_MATRIX_MVP, _World2Object);
				float3 up = float3(0, 1, 0);
				float3 look = _WorldSpaceCameraPos - p[0].pos;
				look.y = 0;
				look = normalize(look);
				float3 right = cross(up, look);
				
				float4 pos = p[0].pos;
				
				// calculate size of each point in the pointcloud based on screenspace point size defined in the material properties
				float dist = distance(_WorldSpaceCameraPos, mul(_Object2World, pos));
				float scale = 2.0f * dist * tan(1.04719755f / 2.0f); // dist * 1.15470053678f;  (considering fov is always 60)
				float halfS = 0.5f * _PointSize / _ScreenParams.x * scale; // calculate size in 
				
				// create four vertices
				float4 v[4];
				v[0] = float4(pos + halfS * right - halfS * up, 1.0f);
				v[1] = float4(pos + halfS * right + halfS * up, 1.0f);
				v[2] = float4(pos - halfS * right - halfS * up, 1.0f);
				v[3] = float4(pos - halfS * right + halfS * up, 1.0f);

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

