using UnityEngine;
using UnityEngine.Rendering;

[GenerateHLSL(needAccessors = false)]
unsafe struct RainDrop
{
    public Vector3 position;
    public Vector3 velocity;
    //public float state;
}

[GenerateHLSL(needAccessors = false, generateCBuffer = true)]
unsafe struct RainSimulationCB
{
    public Matrix4x4 _ToHeight;
    public Vector3 _BoundCenter;
    public float _DeltaTime;
    public Vector3 _BoundHalfSize;
    public float _WindVariation;
    public Vector2 _WindForce;
    public float _VerticalSpeed;
    public float _HeightMapSize;
}

[GenerateHLSL(needAccessors = false, generateCBuffer = true)]
unsafe struct RainRenderCB
{
    public Vector3 _ViewDir;
    public float _RainScale;
    public Color _RainAmbientColor;
}
