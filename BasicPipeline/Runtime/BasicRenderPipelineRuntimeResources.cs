
using System;
using UnityEngine;
using UnityEngine.Rendering;

[CreateAssetMenu(fileName = "BasicRenderPipelineRuntimeResources", menuName = "Rendering/Basic Runtime Resources", order = 10)]
internal class BasicRenderPipelineRuntimeResources : BasicRenderPipelineResources
{
    [Serializable, ReloadGroup]
    public sealed class ShaderResources
    {
        //[Reload("Runtime/Material/Lit/Lit.shader")]
        //public Shader defaultPS;
        [Reload("Runtime/PostProcessing/Shaders/DownScaleFX.compute")]
        public ComputeShader downScaleCS;

        [Reload("Runtime/PostProcessing/Shaders/Blur.compute")]
        public ComputeShader blurCS;

        [Reload("Runtime/PostProcessing/Shaders/BokehHighLightScan.compute")]
        public ComputeShader bokehHighlightScan;

        [Reload("Runtime/ScreenSpaceEffects/SSAO.compute")]
        public ComputeShader ssaoCS;

        [Reload("Runtime/EnvironmentEffects/RainSimulation/RainSimulation.compute")]
        public ComputeShader rainSimulationCS;

        [Reload("Runtime/ScreenSpaceEffects/ScreenSpaceLightRays/Occlusion.compute")]
        public ComputeShader occlusionCS;

        [Reload("Runtime/EnvironmentEffects/Decals/DecalGen.compute")]
        public ComputeShader decalGenCS;
    }

    [Serializable, ReloadGroup]
    public sealed class MaterialResources 
    {
        //[Reload("Runtime/RenderPipelineResources/Material/MaterialWaterExclusion.mat")]
        //public Material waterExclusionMaterial;
        [Reload("Runtime/PostProcessing/Materials/BokehRender.mat")]
        public Material bokehRenderMat;

        [Reload("Runtime/EnvironmentEffects/RainRenderer.mat")]
        public Material rainRenderer;

        [Reload("Runtime/ScreenSpaceEffects/ScreenSpaceLightRays/RayRender.mat")]
        public Material lightRayRender;

        [Reload("Runtime/ScreenSpaceEffects/ScreenSpaceLightRays/SSLRCombine.mat")]
        public Material sslrCombine;

        [Reload("Runtime/EnvironmentEffects/Decals/DecalRender.mat")]
        public Material decalRender;
    }

    [Serializable, ReloadGroup]
    public sealed class TextureResources
    {
        //[Reload(new[]
        //    {
        //        "Runtime/RenderPipelineResources/Texture/FilmGrain/Thin01.png",
        //        "Runtime/RenderPipelineResources/Texture/FilmGrain/Thin02.png",
        //        "Runtime/RenderPipelineResources/Texture/FilmGrain/Medium01.png",
        //        "Runtime/RenderPipelineResources/Texture/FilmGrain/Medium02.png",
        //        "Runtime/RenderPipelineResources/Texture/FilmGrain/Medium03.png",
        //        "Runtime/RenderPipelineResources/Texture/FilmGrain/Medium04.png",
        //        "Runtime/RenderPipelineResources/Texture/FilmGrain/Medium05.png",
        //        "Runtime/RenderPipelineResources/Texture/FilmGrain/Medium06.png",
        //        "Runtime/RenderPipelineResources/Texture/FilmGrain/Large01.png",
        //        "Runtime/RenderPipelineResources/Texture/FilmGrain/Large02.png"
        //    })]
        //public Texture2D[] filmGrainTex;
        [Reload("Runtime/Textures/Noise.png")]
        public Texture2D noiseTex;
    }

    [Serializable, ReloadGroup] 
    public sealed class AssetResources 
    {
        //[Reload("Runtime/RenderPipelineResources/defaultDiffusionProfile.asset")]
        //public DiffusionProfileSettings defaultDiffusionProfile;

        ////Area Light Emissive Meshes
        //[Reload("Runtime/RenderPipelineResources/Mesh/Cylinder.fbx")]
        //public Mesh emissiveCylinderMesh;
        //[Reload("Runtime/RenderPipelineResources/Mesh/Quad.fbx")]
        //public Mesh emissiveQuadMesh;
        //[Reload("Runtime/RenderPipelineResources/Mesh/Sphere.fbx")]
        //public Mesh sphereMesh;
    }

    public ShaderResources shaders;
    public MaterialResources materials;
    public TextureResources textures;
    //public ShaderGraphResources shaderGraphs;
    public AssetResources assets;
}