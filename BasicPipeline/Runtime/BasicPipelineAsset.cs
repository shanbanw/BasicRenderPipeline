using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(menuName = "Rendering/Basic Pipeline Asset", fileName = "DefaultBasicPipelineAsset", order = 6)]
public class BasicPipelineAsset : RenderPipelineAsset
{
    protected override RenderPipeline CreatePipeline()
    {
        return new BasicPipeline(this);
    }


    public AmbientLightData ambientLightData = new AmbientLightData(new Color(0.1f, 0.5f, 0.1f), new Color(0.1f, 0.2f, 0.5f));

    [SerializeField]
    private bool m_HDR = true;
    public bool hdr { get { return m_HDR; } }

    [SerializeField]
    private bool m_VisualizeGBuffer = true;
    public bool visualizeGBuffer { get { return m_VisualizeGBuffer;} }

    [SerializeField]
    private bool m_VisualizeLightVolume = true;
    public bool visualizeLightVolume { get {  return m_VisualizeLightVolume; } }


    [SerializeField]
    private Material m_DefaultMaterial = null;
    public override Material defaultMaterial => m_DefaultMaterial;


    [SerializeField]
    private bool m_AntiFlickerOn = true;
    public bool antiFlickerOn { get {  return m_AntiFlickerOn; } }

    [SerializeField]
    private float m_ShadowDistance = 200.0f;
    public float shadowDistance { get { return m_ShadowDistance; } }

    [SerializeField]
    [Range(1, 4)]
    private int m_CascadeCount = 4;
    public int cascadeCount { get { return m_CascadeCount; } }

    [SerializeField]
    private Vector4 m_CascadeRanges = new(20f, 60f, 100f, 200f);
    public Vector4 cascadeRanges { get {  return m_CascadeRanges; } }

    [SerializeField]
    private Mesh m_SunMesh = null;
    public Mesh sunMesh { get { return m_SunMesh; } }

    [SerializeField]
    private bool m_EnableSSAO = false;
    public bool enableSSAO { get {  return m_EnableSSAO; } }
    [SerializeField]
    [Range(1, 20)]
    private int m_SSAOSampleRadius = 10;
    public int aoSampleRadius { get { return m_SSAOSampleRadius; } }
    [SerializeField]
    [Range(1f, 50f)]
    private float m_SSAORadius = 13f;
    public float aoRadius { get { return m_SSAORadius; } }

    [SerializeField]
    [Tooltip("fog base color, the color's brightness should match the overall intensity so it won't get blown by the bloom")]
    private Color m_FogColor = new Color(0.5f, 0.5f, 0.5f);
    public Color fogColor { get {  return m_FogColor; } }
    [SerializeField]
    [Tooltip("The color used for highlighting pixels with pixel to camera vector that is close to parallel with the camera to sun")]
    private Color m_FogHighlightColor = new Color(0.8f, 0.7f, 0.4f);
    public Color fogHighlightColor { get { return m_FogHighlightColor; } }
    [SerializeField]
    [Range (0f, 100f)]
    private float m_FogStartDepth = 37.0f;
    public float fogStartDepth { get {  return m_FogStartDepth; } }
    [SerializeField]
    [Range(0f, 2f)]
    private float m_FogGlobalDensity = 1.5f;
    public float fogGlobalDensity { get {  return m_FogGlobalDensity; } }
    [SerializeField]
    [Range(0f, 1f)]
    [Tooltip("The higher this value, the lower is the height at which the fog disappear will be")]
    private float m_FogHeightFalloff = 0.2f;
    public float fogHeightFalloff { get { return m_FogHeightFalloff; } }

    [SerializeField]
    private bool m_EnableSSLR = false;
    public bool enableSSLR { get { return m_EnableSSLR; } }
    [SerializeField]
    [Range(0f, 0.75f)]
    private float m_SSLRIntensity = 0.2f;
    public float sslrIntensity { get { return m_SSLRIntensity; } }
    [SerializeField]
    private bool m_EnableSSR = false;
    public bool enableSSR { get { return m_EnableSSR; } }
}
