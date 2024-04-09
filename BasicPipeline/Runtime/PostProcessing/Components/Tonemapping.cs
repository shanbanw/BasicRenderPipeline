using UnityEngine.Rendering;
using UnityEngine;
using System;

public enum TonemappingMode
{
    None,
    Custom,
}

[Serializable]
public sealed class TonemappingModeParameter : VolumeParameter<TonemappingMode>
{
    public TonemappingModeParameter(TonemappingMode mode, bool overrideState = false) : base(mode, overrideState) { }
}

[Serializable, VolumeComponentMenuForRenderPipeline("Post-processing/Tonemapping", typeof(BasicPipeline))]
public sealed class Tonemapping : VolumeComponent, IPostProcessComponent
{
    public TonemappingModeParameter mode = new TonemappingModeParameter(TonemappingMode.None);

    public NoInterpClampedFloatParameter middleGrey = new(0.0025f, 0.000001f, 6.0f);

    public NoInterpClampedFloatParameter white = new(1.5f, 0.000001f, 6.0f);

    public NoInterpClampedFloatParameter adaptation = new(1f, 0f, 10f);

    public bool IsActive()
    {
        return mode != TonemappingMode.None;
    }
}
