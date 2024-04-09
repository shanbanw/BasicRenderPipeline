using UnityEditor;
using UnityEditor.Rendering;

[CustomEditor(typeof(Tonemapping))]
sealed class TonemappingEditor : VolumeComponentEditor
{
    SerializedDataParameter m_Mode;
    SerializedDataParameter m_MiddleGrey;
    SerializedDataParameter m_White;
    SerializedDataParameter m_Adaptation;

    public override void OnEnable()
    {
        var o = new PropertyFetcher<Tonemapping>(serializedObject);

        m_Mode = Unpack(o.Find(x => x.mode));
        m_MiddleGrey = Unpack(o.Find(x => x.middleGrey));
        m_White = Unpack(o.Find(x => x.white));
        m_Adaptation = Unpack(o.Find(x => x.adaptation));
    }

    public override void OnInspectorGUI()
    {
        PropertyField(m_Mode);

        if (m_Mode.value.intValue == (int)TonemappingMode.Custom)
        {
            PropertyField(m_MiddleGrey); 
            PropertyField(m_White);
            PropertyField(m_Adaptation);
        }
    }
}
