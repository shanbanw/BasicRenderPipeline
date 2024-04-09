using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class BackCamera
{
    private static Dictionary<Camera, BackCamera> s_BackCameraInstance = new Dictionary<Camera, BackCamera>();
    public static BackCamera GetOrCreateBackCamera(Camera camera)
    {
        if (camera == null)
            return null;

        BackCamera BackCamera;
        if (!s_BackCameraInstance.TryGetValue(camera, out BackCamera))
        {
            BackCamera = new BackCamera(camera);
            s_BackCameraInstance.Add(camera, BackCamera);
        }

        return BackCamera;
    }

    private static readonly int shaderVariablesGlobalId = Shader.PropertyToID("ShaderVariablesGlobal");

    public Camera camera;
    public Vector2Int finalViewport; //Dynamic resolution
    public string name { get; private set; }
    internal bool isMainGameView { get { return camera.cameraType == CameraType.Game && camera.targetTexture == null; } }
    public VolumeStack volumeStack { get; private set; }
    private BackCamera(Camera cam)
    {
        camera = cam;
        name = cam.name;
        finalViewport = new Vector2Int(cam.pixelWidth, cam.pixelHeight);

        volumeStack = VolumeManager.instance.CreateStack();
    }

    public Vector4 perspectiveValues;

    public void Setup(ScriptableRenderContext context, CommandBuffer cmd)
    {
        ShaderVariablesGlobal globalVariables = new ShaderVariablesGlobal();
        globalVariables._ViewMatrix = camera.worldToCameraMatrix;
        var projMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, !isMainGameView);
        globalVariables._ProjMatrix = projMatrix;
        globalVariables._InvViewMatrix = camera.cameraToWorldMatrix;
        globalVariables._ViewProjMatrix = globalVariables._ProjMatrix * camera.worldToCameraMatrix;
        globalVariables._InvViewProjMatrix = globalVariables._ViewProjMatrix.inverse;
        globalVariables._InvViewProjMatrix = globalVariables._ViewProjMatrix.inverse;
        globalVariables._WorldSpaceCameraPos = camera.cameraToWorldMatrix.GetColumn(3);

        globalVariables._PerspectiveValues.x = 1.0f / projMatrix[0, 0];
        globalVariables._PerspectiveValues.y = 1.0f / projMatrix[1, 1];
        globalVariables._PerspectiveValues.z = projMatrix[2, 3];
        globalVariables._PerspectiveValues.w = projMatrix[2, 2];

        perspectiveValues = globalVariables._PerspectiveValues;

        globalVariables._RTHandleScale = RTHandles.rtHandleProperties.rtHandleScale;
        globalVariables._ViewportSize = new Vector4(RTHandles.rtHandleProperties.currentViewportSize.x, RTHandles.rtHandleProperties.currentViewportSize.y, 0f, 0f);
        globalVariables._ViewportSize.z = 1.0f / globalVariables._ViewportSize.x;
        globalVariables._ViewportSize.w = 1.0f / globalVariables._ViewportSize.y;

        ConstantBuffer.PushGlobal<ShaderVariablesGlobal>(cmd, globalVariables, shaderVariablesGlobalId);
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();
    }

    public void DeferredSetup(ScriptableRenderContext context, CommandBuffer cmd)
    {
        ShaderVariablesGlobal globalVariables = new ShaderVariablesGlobal();
        globalVariables._ViewMatrix = camera.worldToCameraMatrix;
        var projMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true);
        globalVariables._ProjMatrix = projMatrix;
        globalVariables._InvViewMatrix = camera.cameraToWorldMatrix;
        globalVariables._ViewProjMatrix = globalVariables._ProjMatrix * camera.worldToCameraMatrix;
        globalVariables._InvViewProjMatrix = globalVariables._ViewProjMatrix.inverse;
        globalVariables._InvViewProjMatrix = globalVariables._ViewProjMatrix.inverse;
        globalVariables._WorldSpaceCameraPos = camera.cameraToWorldMatrix.GetColumn(3);

        globalVariables._PerspectiveValues.x = 1.0f / projMatrix[0, 0];
        globalVariables._PerspectiveValues.y = 1.0f / projMatrix[1, 1];
        globalVariables._PerspectiveValues.z = projMatrix[2, 3];
        globalVariables._PerspectiveValues.w = projMatrix[2, 2];

        perspectiveValues = globalVariables._PerspectiveValues;

        globalVariables._RTHandleScale = RTHandles.rtHandleProperties.rtHandleScale;
        globalVariables._ViewportSize = new Vector4(RTHandles.rtHandleProperties.currentViewportSize.x, RTHandles.rtHandleProperties.currentViewportSize.y, 0f, 0f);
        globalVariables._ViewportSize.z = 1.0f / globalVariables._ViewportSize.x;
        globalVariables._ViewportSize.w = 1.0f / globalVariables._ViewportSize.y;
        ConstantBuffer.PushGlobal<ShaderVariablesGlobal>(cmd, globalVariables, shaderVariablesGlobalId);
        context.ExecuteCommandBuffer(cmd);
        cmd.Clear();
    }

    void Dispose()
    {
        VolumeManager.instance.DestroyStack(volumeStack);
    }

    internal static void ClearAll()
    {
        foreach(var cam in s_BackCameraInstance.Values)
        {
            cam.Dispose();
        }

        s_BackCameraInstance.Clear();
    }

    

}
