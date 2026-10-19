// İstanbul karakterleri (Samet, Berivan): Subway tarzı net görünüm. UnlitBlinking'in yerine geçer (_BlinkingValue ve sis
// aynı): üstten-önden yumuşak ışık, kenar parlaması, hafif renk doygunluğu ve ince koyu dış çizgi.
Shader "TabanlariYagla/KarakterNet"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_CizgiRenk ("Dis cizgi rengi", Color) = (0.13, 0.09, 0.11, 1)
		_CizgiKalinlik ("Dis cizgi kalinligi", Range(0, 0.05)) = 0.012
		_KenarRenk ("Kenar parlamasi", Color) = (1, 0.96, 0.88, 1)
		_KenarGuc ("Kenar parlamasi gucu", Range(0, 1)) = 0.35
		_Doygunluk ("Doygunluk", Range(0.5, 2)) = 1.15
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

			float _BlinkingValue = 0.0f;
			sampler2D _MainTex;
			float4 _MainTex_ST;
			fixed4 _KenarRenk;
			float _KenarGuc;
			float _Doygunluk;

			struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; float2 uv : TEXCOORD0; };
			struct v2f
			{
				float2 uv : TEXCOORD0;
				UNITY_FOG_COORDS(1)
				float3 normal : TEXCOORD2;
				float3 bakis : TEXCOORD3;
				float4 vertex : SV_POSITION;
			};

			v2f vert (appdata v)
			{
				v2f o;
				o.vertex = UnityObjectToClipPos(v.vertex);
				o.uv = TRANSFORM_TEX(v.uv, _MainTex);
				o.normal = UnityObjectToWorldNormal(v.normal);
				o.bakis = normalize(WorldSpaceViewDir(v.vertex));
				UNITY_TRANSFER_FOG(o, o.vertex);
				return o;
			}

			fixed4 frag (v2f i) : SV_Target
			{
				fixed4 col = tex2D(_MainTex, i.uv);
				float3 n = normalize(i.normal);
				// kamera arkadan-yukarıdan bakar: ışık da oradan, yumuşak (sarmalı) düşer; gölgeler hiç kararmaz
				float3 isik = normalize(float3(0.35, 0.85, -0.45));
				float yayilan = saturate(dot(n, isik) * 0.5 + 0.5);
				col.rgb *= lerp(0.72, 1.12, yayilan);
				// doygunluk
				float gri = dot(col.rgb, float3(0.299, 0.587, 0.114));
				col.rgb = lerp(gri.xxx, col.rgb, _Doygunluk);
				// kenar parlaması: silüet karanlık zemin üstünde ayrılsın
				float kenar = pow(1.0 - saturate(dot(n, normalize(i.bakis))), 3.0);
				col.rgb += _KenarRenk.rgb * kenar * _KenarGuc;
				col = saturate(lerp(col, float4(1, 1, 0.75, 1), _BlinkingValue));
				UNITY_APPLY_FOG(i.fogCoord, col);
				return col;
			}
			ENDCG
		}

		// dış çizgi: normal boyunca şişirilmiş, arka yüzleri çizilen kopya
		Pass
		{
			Tags { "LightMode" = "UniversalForward" }
			Cull Front
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_fog
			#include "UnityCG.cginc"

			fixed4 _CizgiRenk;
			float _CizgiKalinlik;

			struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
			struct v2f { UNITY_FOG_COORDS(0) float4 vertex : SV_POSITION; };

			v2f vert (appdata v)
			{
				v2f o;
				// kalınlık dünya biriminde (karakter ölçeğinden bağımsız)
				float3 nDunya = normalize(UnityObjectToWorldNormal(v.normal));
				float3 pDunya = mul(unity_ObjectToWorld, v.vertex).xyz + nDunya * _CizgiKalinlik;
				o.vertex = UnityWorldToClipPos(pDunya);
				UNITY_TRANSFER_FOG(o, o.vertex);
				return o;
			}

			fixed4 frag (v2f i) : SV_Target
			{
				fixed4 col = _CizgiRenk;
				UNITY_APPLY_FOG(i.fogCoord, col);
				return col;
			}
			ENDCG
		}
	}
}
