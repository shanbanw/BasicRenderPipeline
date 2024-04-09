using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

// https://catlikecoding.com/unity/tutorials/custom-srp/point-and-spot-lights/
[CanEditMultipleObjects]
[CustomEditorForRenderPipeline(typeof(Light), typeof(BasicPipelineAsset))]
internal class BasicLightEditor : LightEditor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        if(!settings.lightType.hasMultipleDifferentValues && (LightType)settings.lightType.enumValueIndex == LightType.Spot)
        {
            settings.DrawInnerAndOuterSpotAngle();
            settings.ApplyModifiedProperties();
        }
    }
}
