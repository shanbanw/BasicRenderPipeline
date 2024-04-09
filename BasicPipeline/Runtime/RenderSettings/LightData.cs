using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct AmbientLightData
{
    public Color lowerColor;
    public Color upperColor;

    public AmbientLightData(Color lowerColor, Color upperColor)
    {
        this.lowerColor = lowerColor;
        this.upperColor = upperColor;
    }
}
    
