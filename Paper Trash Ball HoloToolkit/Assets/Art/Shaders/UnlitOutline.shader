Shader "HololensByExample/UnlitOutline" {
	Properties {
		_Color ("Color", Color) = (1,1,1,1)		
		_Outline("Outline Width", Range(0.0, 0.3)) = 0.002
		_OutlineColor("Outline Color", Color) = (0,0,0,1)
	}
	
	CGINCLUDE
	#include "UnityCG.cginc"

	struct vertIn {
		float4  vertex : POSITION;
		float3 normal : NORMAL;
	};

	struct vertOut {
		float4  pos : POSITION;
		float4 color : COLOR;
	};

	uniform fixed4 _Color;
	uniform half _Outline;
	uniform float4 _OutlineColor;
	ENDCG	

	SubShader {
		Tags { 
			"Queue" = "Transparent" 
		}
		
		Pass{
			Name "Outline"
			Tags{ "LightMode" = "Always" }
			Cull Off 
			ZWrite On
			ZTest Less
			CGPROGRAM
			#pragma vertex vert 
			#pragma fragment frag 

			vertOut vert(vertIn v) {
				vertOut o; 
				o.pos = mul(UNITY_MATRIX_MVP, v.vertex);

				float3 norm = normalize(mul((float3x3)UNITY_MATRIX_IT_MV, v.normal));
				float2 offset = TransformViewToProjection(norm.xy);
				
				o.pos.xy += offset * o.pos.z * _Outline;
				o.color = _OutlineColor;
				return o; 
			}

			fixed4  frag(vertOut i) : COLOR{
				return i.color;
			}
			ENDCG
		}

		Pass{
			Name "Main"
			Tags{ "LightMode" = "Always" }
			Cull Off
			//ZWrite Off
			ZTest Always
			//Offset 15,15
			CGPROGRAM
			#pragma vertex vert 
			#pragma fragment frag 

			vertOut vert(vertIn v) {
				vertOut o;
				o.pos = mul(UNITY_MATRIX_MVP, v.vertex);				
				o.color = _Color;
				return o;
			}

			fixed4 frag(vertOut i) : COLOR{
				fixed4 c;
				c = i.color;
				return c;
			}
			ENDCG
		}
	}
	//FallBack "Diffuse"
}
