using System;
using UnityEngine;
using UnityEngine.Rendering;

[Serializable, VolumeComponentMenuForRenderPipeline("Post-processing/Bloom", typeof(BasicPipeline))]
public sealed class Bloom : VolumeComponent, IPostProcessComponent
{
    public NoInterpClampedFloatParameter bloomThreshold = new(1.1f, 0.0f, 2.5f);

    public NoInterpClampedFloatParameter bloomScale = new(0.74f, 0.0f, 2.0f);

    [SerializeField]
    private BoolParameter bloomActive = new BoolParameter(false);

    public bool IsActive()
    {
        return bloomActive.value;
    }
}