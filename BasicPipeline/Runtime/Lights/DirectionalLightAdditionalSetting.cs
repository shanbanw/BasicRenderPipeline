using UnityEngine;

public class DirectionalLightAdditionalSetting : MonoBehaviour
{
    private readonly static Vector3 s_RotationAxis = new Vector3(1.0f, 0.15f, 0.15f);

    [SerializeField]
    [Range(0f, 1f)]
    private float m_TimeOfDay = 0.78f;

    public float timeOfDay
    {
        get { return m_TimeOfDay; } 
        set
        {
            m_TimeOfDay = value;
            RotateSun();
        }
    }

    private void OnValidate()
    {
        RotateSun();
    }

    private void RotateSun()
    {
        transform.rotation = Quaternion.AngleAxis(m_TimeOfDay * 180f, s_RotationAxis);
    }

}