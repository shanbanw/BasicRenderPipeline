using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.GlobalIllumination;

public class CascadedMatrixSet
{
    private bool m_AntiFlickerOn;
    public bool antiFlickerOn
    {
        get { return m_AntiFlickerOn; }
        set { m_AntiFlickerOn = value; }
    }
    private int m_ShadowMapSize;
    private float m_CascadeTotalRange;
    private float[] m_CascadeRanges = new float[s_MaxCascades + 1];
    private Vector3 m_ShadowBoundCenter;
    private float m_ShadowBoundRadius;

    private const int s_MaxCascades = 4;
    private int m_TotalCascades = 3;
    public int totalCascades
    {
        get { return m_TotalCascades; }
        set
        {
            m_TotalCascades = Mathf.Clamp(value, 1, s_MaxCascades);
        }
    }
    private Vector3[] m_CascadeBoundCenter = new Vector3[s_MaxCascades];
    private float[] m_CascadeBoundRadius = new float[s_MaxCascades];

    private Matrix4x4 m_WorldToShadowSpace;
    public Matrix4x4 worldToShadowSpace { get { return m_WorldToShadowSpace; } }

    private Matrix4x4[] m_WorldToCascadeProj = new Matrix4x4[s_MaxCascades];
    public Matrix4x4[] worldToCascadeProj { get { return m_WorldToCascadeProj; } }

    private Vector4 m_ToCascadeOffsetX;
    public Vector4 toCascadeOffsetX { get { return m_ToCascadeOffsetX; } }

    private Vector4 m_ToCascadeOffsetY;
    public Vector4 toCascadeOffsetY { get { return m_ToCascadeOffsetY; } }

    private Vector4 m_ToCascadeScale;
    public Vector4 toCascadeScale { get {  return m_ToCascadeScale; } }

    private static Dictionary<int, CascadedMatrixSet> s_CascadedMatrixSetnstance = new Dictionary<int, CascadedMatrixSet>();
    public static CascadedMatrixSet GetOrCreateCascadedMatrixSet(Camera camera)
    {
        if (camera == null)
            return null;

        CascadedMatrixSet cascade;
        int id = camera.GetInstanceID();
        if (!s_CascadedMatrixSetnstance.TryGetValue(id, out cascade))
        {
            cascade = new CascadedMatrixSet();
            s_CascadedMatrixSetnstance.Add(id, cascade);
        }

        return cascade;
    }

    private CascadedMatrixSet()
    {
        m_AntiFlickerOn = true;
        m_CascadeTotalRange = 50f;
    }

    private Matrix4x4 m_CameraObjectMatrix;
    private Vector2 tanFov;
    public void Init(Camera camera, int shadowMapSize, int cascadeCount, Vector4 cascadeRanges )
    {
        m_ShadowMapSize = shadowMapSize;

        totalCascades = cascadeCount;

        m_CascadeRanges[0] = camera.nearClipPlane;

        for ( int i = 1; i < cascadeCount + 1; i++ )
        {
            m_CascadeRanges[i] = cascadeRanges[i - 1];
        }
        m_CascadeTotalRange = cascadeRanges[cascadeCount-1];

        m_CameraObjectMatrix = camera.transform.localToWorldMatrix;
        
        tanFov.y = Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        tanFov.x = camera.aspect * tanFov.y;
    }

    private void ExtractFrustumPoints(float near, float far, Vector3[] frustumCorners)
    { 
        Vector3 camRight = m_CameraObjectMatrix.GetColumn(0);
        Vector3 camUp = m_CameraObjectMatrix.GetColumn(1);
        Vector3 camForward = m_CameraObjectMatrix.GetColumn(2);
        Vector3 camPos = m_CameraObjectMatrix.GetColumn(3);

        frustumCorners[0] = camPos + (-camRight * tanFov.x + camUp * tanFov.y + camForward) * near;
        frustumCorners[1] = camPos + (-camRight * tanFov.x - camUp * tanFov.y + camForward) * near;
        frustumCorners[2] = camPos + (camRight * tanFov.x + camUp * tanFov.y + camForward) * near;
        frustumCorners[3] = camPos + (camRight * tanFov.x - camUp * tanFov.y + camForward) * near;

        frustumCorners[4] = camPos + (-camRight * tanFov.x + camUp * tanFov.y + camForward) * far;
        frustumCorners[5] = camPos + (-camRight * tanFov.x - camUp * tanFov.y + camForward) * far;
        frustumCorners[6] = camPos + (camRight * tanFov.x + camUp * tanFov.y + camForward) * far;
        frustumCorners[7] = camPos + (camRight * tanFov.x - camUp * tanFov.y + camForward) * far;
    }

    private void ExtractFrustumBoundSphere(float near, float far, out Vector3 boundCenter, out float boundRadius)
    {
        Vector3 camRight = m_CameraObjectMatrix.GetColumn(0);
        Vector3 camUp = m_CameraObjectMatrix.GetColumn(1);
        Vector3 camForward = m_CameraObjectMatrix.GetColumn(2);
        Vector3 camPos = m_CameraObjectMatrix.GetColumn(3);

        boundCenter = camPos + camForward * ( 0.5f * (near + far));
        Vector3 boundSpan = camPos + (camRight * tanFov.x + camUp * tanFov.y + camForward) * far - boundCenter;
        boundRadius = boundSpan.magnitude;
    }

    private bool CascadeNeedUpdate(Matrix4x4 shadowView, int cascadeIndex, Vector3 newCenter, out Vector3 offset)
    {
        Vector3 oldCenterInCascade = shadowView.MultiplyPoint3x4(m_CascadeBoundCenter[cascadeIndex]);
        Vector3 newCenterInCascade = shadowView.MultiplyPoint3x4(newCenter);
        Vector3 centerDiff = newCenterInCascade - oldCenterInCascade;

        float pixelSize = m_ShadowMapSize / (2 * m_CascadeBoundRadius[cascadeIndex]);

        float pixelOffX = centerDiff.x * pixelSize;
        float pixelOffY = centerDiff.y * pixelSize;

        bool needUpdate = Mathf.Abs(pixelOffX) > 0.5f || Mathf.Abs(pixelOffY) > 0.5f;
        offset = Vector3.zero;
        if (needUpdate)
        {
            offset.x = Mathf.Floor(0.5f + pixelOffX) / pixelSize;
            offset.y = Mathf.Floor(0.5f + pixelOffY) / pixelSize;
            offset.z = centerDiff.z;
        }

        return needUpdate;
    }

    private Vector3[] m_FrustumCorners = new Vector3[8];
    public void Update(Vector3 directionalDir)
    {
        Vector3 worldCenter = m_CameraObjectMatrix.GetColumn(3) + m_CameraObjectMatrix.GetColumn(2) * (m_CascadeTotalRange + m_CascadeRanges[0]) * 0.5f;
        Vector3 lookAt = worldCenter + directionalDir;
        Vector3 up = Vector3.Cross(directionalDir, Vector3.right);
        Matrix4x4 shadowObject = Matrix4x4.LookAt(worldCenter, lookAt, up);
        Matrix4x4 shadowView = shadowObject.inverse;
        Matrix4x4 gupShadowView = Matrix4x4.Scale(new Vector3(1.0f, 1.0f, -1.0f)) * shadowView;

        ExtractFrustumBoundSphere(m_CascadeRanges[0], m_CascadeTotalRange, out m_ShadowBoundCenter, out float boundRadius);
        m_ShadowBoundRadius = Mathf.Max(boundRadius, m_ShadowBoundRadius); // Expend the radius to compensate for numerical errors
        Matrix4x4 projection = Matrix4x4.Ortho(-m_ShadowBoundRadius, m_ShadowBoundRadius, -m_ShadowBoundRadius, m_ShadowBoundRadius, -m_ShadowBoundRadius, m_ShadowBoundRadius);
        Matrix4x4 gpuProjection = GL.GetGPUProjectionMatrix(projection, true);
        Matrix4x4 gpuNonFlippedProjection = GL.GetGPUProjectionMatrix(projection, false);
        float epsilon = LightManager.kEpsilon;
        if (SystemInfo.usesReversedZBuffer)
        {
            epsilon = -LightManager.kEpsilon;
        }
        gpuProjection[2, 3] += epsilon;
        m_WorldToShadowSpace = gpuProjection * gupShadowView;

        for (int cascadeIndex = 0; cascadeIndex < m_TotalCascades; cascadeIndex++)
        {
            Matrix4x4 cascadeTrans, cascadeScale;
            if (m_AntiFlickerOn)
            {
                ExtractFrustumBoundSphere(m_CascadeRanges[cascadeIndex], m_CascadeRanges[cascadeIndex + 1], out var newCenter, out var radius);
                m_CascadeBoundRadius[cascadeIndex] = Mathf.Max(m_CascadeBoundRadius[cascadeIndex], radius);

                if (CascadeNeedUpdate(shadowView, cascadeIndex, newCenter, out Vector3 offset))
                {
                    Vector3 offsetWorld = shadowObject.MultiplyVector(offset);
                    m_CascadeBoundCenter[cascadeIndex] += offsetWorld;
                }

                Vector3 cascadeCenterShadowSpace = m_WorldToShadowSpace.MultiplyPoint3x4(m_CascadeBoundCenter[cascadeIndex]);
                m_ToCascadeOffsetX[cascadeIndex] = -cascadeCenterShadowSpace.x;
                m_ToCascadeOffsetY[cascadeIndex] = -cascadeCenterShadowSpace.y;
                cascadeTrans = Matrix4x4.Translate(new Vector3(m_ToCascadeOffsetX[cascadeIndex], m_ToCascadeOffsetY[cascadeIndex], 0.0f));

                m_ToCascadeScale[cascadeIndex] = m_ShadowBoundRadius / m_CascadeBoundRadius[cascadeIndex];
                cascadeScale = Matrix4x4.Scale(new Vector3(m_ToCascadeScale[cascadeIndex], m_ToCascadeScale[cascadeIndex], 1.0f));
            }
            else
            {
                ExtractFrustumPoints(m_CascadeRanges[cascadeIndex], m_CascadeRanges[cascadeIndex], m_FrustumCorners);

                Vector3 vMin = new (Mathf.Infinity, Mathf.Infinity, Mathf.Infinity);
                Vector3 vMax = new (Mathf.NegativeInfinity, Mathf.NegativeInfinity, Mathf.NegativeInfinity);
                for(int i = 0; i < 8; i++)
                {
                    Vector3 pointShadowSpace = m_WorldToShadowSpace.MultiplyPoint3x4(m_FrustumCorners[i]);
                    for (int j = 0; j < 3; j++)
                    {
                        if (vMin[j] > pointShadowSpace[j])
                            vMin[j] = pointShadowSpace[j];
                        if (vMax[j] < pointShadowSpace[j])
                            vMax[j] = pointShadowSpace[j];
                    }
                }
                Vector3 cascadeCenterShadowSpace = 0.5f * (vMin + vMax);
                m_ToCascadeOffsetX[cascadeIndex] = -cascadeCenterShadowSpace.x;
                m_ToCascadeOffsetY[cascadeIndex] = -cascadeCenterShadowSpace.y;
                cascadeTrans = Matrix4x4.Translate(new(m_ToCascadeOffsetX[cascadeIndex], m_ToCascadeOffsetY[cascadeIndex], 0.0f));
                m_ToCascadeScale[cascadeIndex] = 2.0f / Mathf.Max(vMax.x - vMin.x, vMax.y - vMin.y);
                cascadeScale = Matrix4x4.Scale(new(m_ToCascadeScale[cascadeIndex], m_ToCascadeScale[cascadeIndex], 1.0f));
            }
            m_WorldToCascadeProj[cascadeIndex] = cascadeScale * cascadeTrans * m_WorldToShadowSpace;
        }
        for(int i = m_TotalCascades; i < 4; i++)
        {
            m_ToCascadeOffsetX[i] = 250f;
            m_ToCascadeOffsetY[i] = 250f;
            m_ToCascadeScale[i] = 0.1f;
        }
    }
}