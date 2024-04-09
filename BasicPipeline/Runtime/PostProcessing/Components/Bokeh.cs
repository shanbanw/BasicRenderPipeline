using System;
using UnityEngine;
using UnityEngine.Rendering;

[Serializable, VolumeComponentMenuForRenderPipeline("Post-processing/Bokeh", typeof(BasicPipeline))]
public sealed class Bokeh : VolumeComponent, IPostProcessComponent
{
    public NoInterpClampedFloatParameter bokehLumThreshold = new(7.65f, 0.0f, 25f);

    public NoInterpClampedFloatParameter bokehBlurThreshold = new(0.43f, 0.0f, 1f);

    public NoInterpClampedFloatParameter bokehRadiusScale = new(0.05f, 0.0f, 0.1f);

    public NoInterpClampedFloatParameter bokehColorScale = new(0.05f, 0.0f, 0.25f);

    public bool IsActive()
    {
        return true;
    }
}