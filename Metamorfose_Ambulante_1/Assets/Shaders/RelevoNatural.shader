Shader "Metamorfose/RelevoNatural"
{
 Properties {
  _Rock("Rocha",Color)=(.38,.34,.29,1)
  _Grass("Vegetacao",Color)=(.27,.32,.17,1)
  _Haze("Tonalidade distante",Color)=(.52,.47,.48,1)
  _DistanceTint("Nevoa propria",Range(0,1))=.1
 }
 SubShader {
  Tags {"RenderType"="Opaque"}
  CGPROGRAM
  #pragma surface surf Lambert
  #pragma target 3.0
  fixed4 _Rock,_Grass,_Haze;half _DistanceTint;
  struct Input {float3 worldPos;float3 worldNormal;};
  float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
  float noise(float2 p){float2 a=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(a),hash(a+float2(1,0)),f.x),lerp(hash(a+float2(0,1)),hash(a+1),f.x),f.y);}
  void surf(Input IN,inout SurfaceOutput o){
   float n=noise(IN.worldPos.xz*.035),fine=noise(IN.worldPos.xz*.48);
   float slope=1-saturate(IN.worldNormal.y);
   float strata=sin(IN.worldPos.y*.33+n*5)*.04;
   float3 c=lerp(_Grass.rgb,_Rock.rgb,smoothstep(.18,.55,slope+n*.15));
   c*=.78+n*.30+fine*.13+strata;
   o.Albedo=lerp(c,_Haze.rgb,_DistanceTint);
   o.Emission=_Haze.rgb*_DistanceTint*.14;
   o.Alpha=1;
  }
  ENDCG
 }
 Fallback "Diffuse"
}
