Shader "Metamorfose/CeuPorDoSol"
{
 Properties {
  _Zenith("Azul do alto", Color)=(.12,.28,.48,1)
  _Horizon("Cor do horizonte", Color)=(.94,.51,.30,1)
  _CloudLight("Bordas das nuvens", Color)=(1,.76,.52,1)
  _CloudShadow("Sombra das nuvens", Color)=(.34,.37,.48,1)
  _SunDirection("Direcao do sol", Vector)=(.24,.065,.97,0)
  _SunSize("Raio aparente do sol", Range(.003,.035))=.012
  _Coverage("Cobertura das nuvens", Range(.2,.8))=.49
  _Wind("Velocidade das nuvens", Range(0,.1))=.004
 }
 SubShader {
  Tags {"Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox"}
  Cull Off ZWrite Off
  Pass {
   CGPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #pragma target 3.0
   #include "UnityCG.cginc"
   float4 _Zenith,_Horizon,_CloudLight,_CloudShadow,_SunDirection;
   float _SunSize,_Coverage,_Wind;
   struct v2f { float4 pos:SV_POSITION; float3 ray:TEXCOORD0; };
   v2f vert(float4 p:POSITION) {v2f o; o.pos=UnityObjectToClipPos(p);o.ray=p.xyz;return o;}
   float hash(float2 p) {return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
   float noise(float2 p) {
    float2 a=floor(p),f=frac(p);f=f*f*(3-2*f);
    return lerp(lerp(hash(a),hash(a+float2(1,0)),f.x),lerp(hash(a+float2(0,1)),hash(a+1),f.x),f.y);
   }
   float fbm(float2 p) {
    float sum=0,amp=.53;
    [unroll] for(int k=0;k<5;k++){sum+=amp*noise(p);p=mul(float2x2(1.6,-1.2,1.2,1.6),p)+7.13;amp*=.48;}
    return sum;
   }
   fixed4 frag(v2f i):SV_Target {
    float3 d=normalize(i.ray),sun=normalize(_SunDirection.xyz);
    float elevation=saturate(d.y);
    float3 col=lerp(_Horizon.rgb,_Zenith.rgb,pow(elevation,.48));
    float angle=acos(clamp(dot(d,sun),-.999999,1));
    col+=float3(1,.40,.13)*exp(-angle*7)*.55;
    float disk=1-smoothstep(_SunSize*.85,_SunSize*1.15,angle);
    col=lerp(col,float3(1,.91,.62),disk);
    if(d.y>.006) {
     float2 p=d.xz/(d.y+.14)*1.65+float2(_Time.y*_Wind,3.2);
     float n=fbm(p),fine=fbm(p*2.7+12);
     float density=smoothstep(_Coverage-.05,_Coverage+.16,n*.85+fine*.15);
     density*=smoothstep(.008,.095,d.y);
     // Uma abertura suave entre nuvens mantém o pequeno sol visível.
     density*=smoothstep(_SunSize*2.5,_SunSize*7,angle);
     float shade=saturate(.55+(fbm(p+sun.xz*.12)-n)*6);
     float edge=pow(1-density,3)*exp(-angle*2.5);
     float3 cloud=lerp(_CloudShadow.rgb,_CloudLight.rgb,shade*.65+edge*.35);
     col=lerp(col,cloud,density*.92);
     float wisps=smoothstep(.57,.73,fbm(p*float2(.65,3)+25));
     col=lerp(col,_CloudLight.rgb,wisps*.10*smoothstep(.1,.4,d.y));
    }
    col=lerp(float3(.38,.32,.29),col,smoothstep(-.10,.015,d.y));
    return fixed4(col,1);
   }
   ENDCG
  }
 }
 Fallback Off
}
