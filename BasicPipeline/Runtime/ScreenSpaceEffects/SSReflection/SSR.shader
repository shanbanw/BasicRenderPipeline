Shader "Basic/ScreenSpaceEffects/SSR"
{
    Properties
    {
        _ViewAngleThreshold ("View Angle Threshold", Range(-0.25, 1.0)) = 0.2
        _EdgeDistThreshold ("Edge Dist Threshold", Range(0, 0.999)) = 0.45
        _DepthBias ("Depth Bias", Range(0, 1.5)) = 0.5
        _ReflectionScale ("Reflection Scale", Range(0, 1)) = 1
    }
    SubShader
    {

        Pass
        {
            Tags {"LightMode" = "ScreenSpaceReflection"}
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #include "Assets/BasicPipeline/Runtime/ShaderLibrary/Common.hlsl"
            #include "Assets/BasicPipeline/Runtime/ShaderLibrary/ShaderVariables.hlsl"
            #include "Assets/BasicPipeline/Runtime/ShaderLibrary/PickingSpaceTransforms.hlsl"

            TEXTURE2D(_HDRTexture);
            CBUFFER_START(UnityPerMaterial)
            float _ViewAngleThreshold;
            float _EdgeDistThreshold;
            float _DepthBias;
            float _ReflectionScale;
            CBUFFER_END

            struct appdata
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct v2f
            {
                float4 positionCS : SV_POSITION;
                float3 positionVS : TEXCOORD0;
                real3 normalVS : TEXCOORD1;
                noperspective float3 csPos : TEXCOORD2;
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.positionCS = TransformObjectToHClip(v.positionOS.xyz);
                
                o.positionVS = TransformWorldToView(TransformObjectToWorld(v.positionOS.xyz));
                o.normalVS = TransformWorldToViewNormal((real3)TransformObjectToWorldNormal(v.normalOS));

                o.csPos = o.positionCS.xyz / o.positionCS.w;
                return o;
            }

            float4 frag (v2f i) : SV_Target
            {
                 float3 positionVS = i.positionVS.xyz;
                 float3 normalVS = normalize((float3)i.normalVS);
                 float3 eyeToPixel = normalize(positionVS);
                 float3 reflectVS = reflect(eyeToPixel, normalVS);
                 float4 reflectColor = float4(0.0, 0.0, 0.0, 0.0);
                 // We don't have the information behind the camera, we can't find the reflection color for any
                 // pixel with a normal pointing towards the camera
                 if (reflectVS.z <= -_ViewAngleThreshold)
                 {
                    // Fade the reflection as the view angles gets close to the threshold
                    float viewAngleThresholdInv = 1.0 - _ViewAngleThreshold;
                    float viewAngleFade = saturate(3.0 * (abs(reflectVS.z) - _ViewAngleThreshold) / viewAngleThresholdInv);

                    // Transform the view space reflection to clip space
                    float3 posReflectVS = positionVS + reflectVS;
                    float3 posReflectCS = TransformWViewToHClip(posReflectVS).xyz / abs(posReflectVS.z);
                    float3 csReflect = posReflectCS - i.csPos;

                    // Resize Screen Space Reflection to an appropriate length
                    float pixelSize = 2 * max(_ViewportSize.z, _ViewportSize.w);
                    float reflectScale = pixelSize / length(csReflect.xy);
                    csReflect *= reflectScale;
                    // Calculate the first sampling position in screen space
                    float2 ssSampPos = (i.csPos + csReflect).xy;
                    //#if 
                    ssSampPos = ssSampPos * float2(0.5, -0.5) + 0.5;
                    // Find each iteration step in screen space
                    float2 ssStep = csReflect.xy * float2(0.5, -0.5);

                    // Build a plane laying on the reflection vector
                    // Use the eye to pixel direction to build the tangent vector
                    float4 rayPlane;
                    float3 right = cross(eyeToPixel, reflectVS);
                    rayPlane.xyz = normalize(cross(reflectVS, right));
                    rayPlane.w = dot(rayPlane.xyz, positionVS);

                    int numSteps = max(_ViewportSize.x, _ViewportSize.y);
                    // Iterate over the HDR texture searching for intersection
                    for(int curStep = 0; curStep < numSteps; curStep++)
                    {
                        float2 uv = ssSampPos * _RTHandleScale.xy;
                        // Sample from depth buffer
                        float curDepth = SAMPLE_TEXTURE2D_LOD(_DepthTex, sampler_Point_Clamp, uv, 0).x;
                        float curLinearDepth = ConvertZToLinearDepth(curDepth);
                        float3 curPos = CalcViewPos(i.csPos.xy + csReflect.xy * ((float)curStep + 1.0), curLinearDepth);

                        // Find the intersection between the ray and the scene
                        // The intersection happens between two positions on the oposite sides of the plane
                        if (rayPlane.w >= dot(rayPlane.xyz, curPos) + _DepthBias)
                        {
                            float3 finalPosVS = positionVS + (reflectVS / abs(reflectVS.z)) * abs(curLinearDepth - abs(positionVS.z) + _DepthBias);
                            float2 finalPosCS = finalPosVS.xy / _PerspectiveValues.xy / abs(finalPosVS.z);
                            ssSampPos = finalPosCS.xy * float2(0.5, -0.5) + 0.5;
                            float2 uv = ssSampPos * _RTHandleScale.xy;
                            reflectColor.xyz = SAMPLE_TEXTURE2D_LOD(_HDRTexture, sampler_Linear_Clamp, uv, 0).xyz;
                            // Fade out samples as they get close to the texture edges
                            float edgeFade = saturate(distance(ssSampPos, float2(0.5, 0.5)) * 2.0 - _EdgeDistThreshold);
                            // Calculate the fade value
                            reflectColor.w = min(viewAngleFade, 1.0 - edgeFade * edgeFade);

                            // Apply the reflection Scale
                            reflectColor.w *= _ReflectionScale;
                            curStep = numSteps;
                        }

                        ssSampPos += ssStep;
                    }

                 }
                 return reflectColor;
            }
            ENDHLSL
        }

    }
}
