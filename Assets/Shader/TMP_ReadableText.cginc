#include "UnityCG.cginc"
#include "UnityUI.cginc"

sampler2D _MainTex;
float4 _MainTex_ST;
fixed4 _FaceColor;
float4 _ClipRect;

struct TMPReadableInput
{
    float4 vertex : POSITION;
    fixed4 color : COLOR;
    float2 texcoord : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct TMPReadableOutput
{
    float4 vertex : SV_POSITION;
    fixed4 color : COLOR;
    float2 texcoord : TEXCOORD0;
    float4 worldPosition : TEXCOORD1;
    UNITY_VERTEX_OUTPUT_STEREO
};

TMPReadableOutput vert(TMPReadableInput input)
{
    TMPReadableOutput output;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    output.worldPosition = input.vertex;
    output.vertex = UnityObjectToClipPos(input.vertex);
    output.texcoord = TRANSFORM_TEX(input.texcoord, _MainTex);
    output.color = input.color * _FaceColor;
    return output;
}

fixed4 frag(TMPReadableOutput input) : SV_Target
{
    fixed4 tex = tex2D(_MainTex, input.texcoord);
    float glyphAlpha = smoothstep(0.42, 0.58, tex.a);

    fixed4 color = input.color;
    color.a *= glyphAlpha;

#ifdef UNITY_UI_CLIP_RECT
    color.a *= UnityGet2DClipping(input.worldPosition.xy, _ClipRect);
#endif

#ifdef UNITY_UI_ALPHACLIP
    clip(color.a - 0.001);
#endif

    return color;
}
