using UnityEngine;
using UnityEngine.Rendering;

public class RendererUtils
{
    public static Vector3 ColorToVector3(Color color)
    {
        return new Vector3(color.r, color.g, color.b);
    }

    //https://www.khronos.org/opengl/wiki/Cubemap_Texture
    public static Matrix4x4 GetCubemapViewMatrix(CubemapFace face)
    {
        Vector3 right = Vector3.right;
        Vector3 up = Vector3.up;
        switch(face)
        {
            case CubemapFace.PositiveX:
                right = new Vector3(0.0f, 0.0f, -1.0f);
                up = new Vector3(0.0f, -1.0f, 0.0f);
                break;
            case CubemapFace.NegativeX:
                right = new Vector3(0.0f, 0.0f, 1.0f);
                up = new Vector3(0.0f, -1.0f, 0.0f);
                break;
            case CubemapFace.PositiveY:
                right = new Vector3(1.0f, 0.0f, 0.0f);
                up = new Vector3(0.0f, 0.0f, 1.0f);
                break;
            case CubemapFace.NegativeY:
                right = new Vector3(1.0f, 0.0f, 0.0f);
                up = new Vector3(0.0f, 0.0f, -1.0f);
                break;
            case CubemapFace.PositiveZ:
                right = new Vector3(1.0f, 0.0f, 0.0f);
                up = new Vector3(0.0f, -1.0f, 0.0f);
                break;
            case CubemapFace.NegativeZ:
                right = new Vector3(-1.0f, 0.0f, 0.0f);
                up = new Vector3(0.0f, -1.0f, 0.0f);
                break;
        }

        // For cubemap capture(reflection probe) view space is still left handed(cubemap convention) and the determinant is positive.
        Vector3 front = Vector3.Cross(right, up);

        Matrix4x4 matrix = Matrix4x4.identity;
        matrix.SetRow(0, right);
        matrix.SetRow(1, up);
        matrix.SetRow(2, front);
        return matrix;
    }

    //https://forum.unity.com/threads/how-to-control-point-light-shadow-cull-mode.921668
    public static Matrix4x4 GetCubemapViewMatrixCorrected(CubemapFace face)
    {
        var lookAt = Matrix4x4.LookAt(Vector3.zero, CoreUtils.lookAtList[(int)face], CoreUtils.upVectorList[(int)face]);
        return Matrix4x4.Scale(new Vector3(1.0f, 1.0f, -1.0f)) * lookAt.transpose;
    }


}
