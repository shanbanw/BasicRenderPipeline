using System;
using UnityEngine;
using UnityEngine.Rendering;

[Serializable, VolumeComponentMenuForRenderPipeline("Post-processing/DepthOfField", typeof(BasicPipeline))]
public sealed class DepthOfField : VolumeComponent, IPostProcessComponent
{
    public NoInterpClampedFloatParameter dofFarStart = new(40f, 0.0f, 400f);

    public NoInterpClampedFloatParameter dofFarRange = new(60f, 0.0f, 150f);

    public bool IsActive()
    {
        return true;
    }
}