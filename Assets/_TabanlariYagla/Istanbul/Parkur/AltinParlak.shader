// Altın sikke: Subway'deki gibi parlak ve net. Kavisli dünyaya uyar (CurvedUnlit ile aynı bükme), renk 1,35 kat
// parlatılır, kenarda sıcak sarı ışıltı ve zamanla gezen parıltı bandı.
Shader "TabanlariYagla/AltinParlak"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_Parlaklik ("Parlaklik", Range(1, 2)) = 1.35
		_KenarRenk ("Kenar isiltisi", Color) = (1, 0.85, 0.35, 1)
	}
	SubShader
	{
		Tags { "RenderType" = "Opaque" }
		LOD 100
		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_fog
			#include "UnityCG.cginc"

			sampler2D _MainTex; float4 _MainTex_ST;
			float _CurveStrength, _Parlaklik;
			fixed4 _KenarRenk;

			struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; };
			struct v2f { float2 uv : TEXCOORD0; UNITY_FOG_COORDS(1) float3 n : TEXCOORD2; float3 v : TEXCOORD3; float3 w : TEXCOORD4; float4 vertex : SV_POSITION; };

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				float dist = UNITY_Z_0_FAR_FROM_CLIPSPACE(o.vertex.z);
				o.vertex.y -= _CurveStrength * dist * dist * _ProjectionParams.x;
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				o.n = UnityObjectToWorldNormal(v.normal);
				o.v = normalize(WorldSpaceViewDir(v.vertex));
				o.w = mul(unity_ObjectToWorld, v.vertex).xyz;
				UNITY_TRANSFER_FOG(o, o.vertex);
				return o;
			}

			fixed4 frag (v2f i) : SV_Target
			{
				fixed4 col = tex2D(_MainTex, i.uv) * _Parlaklik;
				float3 n = normalize(i.n);
				float kenar = pow(1.0 - saturate(abs(dot(n, normalize(i.v)))), 2.0);
				col.rgb += _KenarRenk.rgb * kenar * 0.6;
				// gezen parıltı bandı
				float bant = saturate(1.0 - abs(frac(i.w.y * 0.8 + i.w.x * 0.3 - _Time.y * 0.9) - 0.5) * 8.0);
				col.rgb += bant * 0.35;
				col.rgb = saturate(col.rgb);
				UNITY_APPLY_FOG(i.fogCoord, col);
				return col;
			}
			ENDCG
		}
	}
}
