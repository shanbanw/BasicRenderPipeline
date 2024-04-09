using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[Serializable, VolumeComponentMenuForRenderPipeline("EnvironmentEffects/Rain", typeof(BasicPipeline))]
public class RainVolume : VolumeComponent
{
    public BoolParameter pauseRainSimulation = new BoolParameter(true);
    public NoInterpClampedFloatParameter rainDensity = new NoInterpClampedFloatParameter(0.5f, 0f, 1f);
    public NoInterpClampedFloatParameter rainSimulationSpeed = new NoInterpClampedFloatParameter(1.0f, 0f, 2.0f);
    // Half size of the rain emitter box
    public NoInterpClampedFloatParameter rainBoundHalfSizeX = new NoInterpClampedFloatParameter(15f, 1f, 200f);
    public NoInterpClampedFloatParameter rainBoundHalfSizeY = new NoInterpClampedFloatParameter(10f, 1f, 200f);
    public NoInterpClampedFloatParameter rainBoundHalfSizeZ = new NoInterpClampedFloatParameter(15f, 1f, 200f);
    public NoInterpClampedFloatParameter rainTimeScale = new NoInterpClampedFloatParameter(1f, 0f, 2f);
}
