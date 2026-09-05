Shader "TextMeshPro/Mobile/Distance Field - Masking"
{
    Properties
    {
        [HDR] _FaceColor ("Face Color", Color) = (1,1,1,1)
        _MainTex ("Font Atlas", 2D) = "white" {}
        _ClipRect ("Clip Rect", Vector) = (-32767,-32767,32767,32767)
        _MaskSoftnessX ("Mask SoftnessX", Float) = 0
        _MaskSoftnessY ("Mask SoftnessY", Float) = 0
        _MaskTex ("Mask Texture", 2D) = "white" {}
        _MaskInverse ("Inverse", Float) = 0
        _MaskEdgeColor ("Edge Color", Color) = (1,1,1,1)
        _MaskEdgeSoftness ("Edge Softness", Range(0, 1)) = 0.01
        _MaskWipeControl ("Wipe Position", Range(0, 1)) = 0.5
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _CullMode ("Cull Mode", Float) = 0
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull [_CullMode]
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP
            #include "Assets/Shader/TMP_ReadableText.cginc"
            ENDCG
        }
    }

    CustomEditor "TMPro.EditorUtilities.TMP_SDFShaderGUI"
}
