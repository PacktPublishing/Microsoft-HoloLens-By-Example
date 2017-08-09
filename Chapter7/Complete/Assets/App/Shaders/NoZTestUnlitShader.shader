Shader "Custom/NoZTestUnlitShader"
{
	Properties
	{
		_Color ("Color", Color) = (1.0, 1.0, 1.0, 1.0) 
	}
	SubShader
	{
		Tags { "Queue" = "Transparent" "IgnoreProjector" = "True" "RenderType" = "Transparent" }
		LOD 100

		Pass
		{
			ZWrite On
			ZTest Always 
		//Blend DstColor SrcColor // 2x Multiplicative

			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			
			#include "UnityCG.cginc"

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
			};

			struct v2f
			{
				float4 vertex : SV_POSITION;
			};

			float4 _Color;
			
			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{			
				return _Color;
			}
			ENDCG
		}
	}
}
