Shader "AeroMorph/StateSurface"
{
    Properties
    {
        _BaseColor ("Cor Base", Color) = (0.1,0.8,1,1)
        _EmissionColor ("Emissão", Color) = (0,0,0,1)
        _State ("Estado", Range(0,2)) = 0
        _Pulse ("Pulso", Range(0,2)) = 1
        _Alpha ("Transparência", Range(0.05,1)) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite On
        CGPROGRAM
        #pragma surface surf Lambert alpha:fade
        #include "UnityCG.cginc"
        fixed4 _BaseColor;
        fixed4 _EmissionColor;
        half _State;
        half _Pulse;
        half _Alpha;
        struct Input { float3 worldPos; };
        void surf(Input IN, inout SurfaceOutput o)
        {
            half wave = sin(_Time.y * (2 + _Pulse) + IN.worldPos.y * 3) * 0.08;
            o.Albedo = _BaseColor.rgb;
            o.Emission = _EmissionColor.rgb * (0.25 + wave);
            o.Alpha = _Alpha;
        }
        ENDCG
    }
    Fallback "Diffuse"
}
