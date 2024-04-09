using UnityEditor;
using UnityEditor.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

class BasicRenderPipelineGlobalSettingsPanelProvider : RenderPipelineGlobalSettingsProvider<BasicPipeline, BasicRenderPipelineGlobalSettings>
{
    public BasicRenderPipelineGlobalSettingsPanelProvider()
        : base("Project/Graphics/Basic Pipeline Global Settings")
    { }

    [SettingsProvider]
    public static SettingsProvider CreateSettingsProvider() => new BasicRenderPipelineGlobalSettingsPanelProvider();

    protected override void Clone(RenderPipelineGlobalSettings src, bool activateAsset)
    {
        return;
    }

    protected override void Create(bool useProjectSettingsFolder, bool activateAsset)
    {
        return;
    }

    protected override void Ensure()
    {
        BasicRenderPipelineGlobalSettings.Ensure();
    }

}