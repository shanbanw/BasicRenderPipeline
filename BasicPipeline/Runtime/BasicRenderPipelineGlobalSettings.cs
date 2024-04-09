using UnityEngine.Rendering;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
#if UNITY_EDITOR
using UnityEditorInternal;
using UnityEditor;
#endif
using System;

public class BasicRenderPipelineGlobalSettings : RenderPipelineGlobalSettings
{
    private static BasicRenderPipelineGlobalSettings cachedInstance = null;
    public static BasicRenderPipelineGlobalSettings instance
    {
        get
        {
#if !UNITY_EDITOR
            if (cachedInstance == null)
#endif
                cachedInstance = GraphicsSettings.GetSettingsForRenderPipeline<BasicPipeline>() as BasicRenderPipelineGlobalSettings;
            return cachedInstance;
        }
    }

    static internal void UpdateGraphicsSettings(BasicRenderPipelineGlobalSettings newSettings)
    {
        if (newSettings == cachedInstance)
            return;
        if (newSettings != null)
            GraphicsSettings.RegisterRenderPipelineSettings<BasicPipeline>(newSettings as RenderPipelineGlobalSettings);
        else
            GraphicsSettings.UnregisterRenderPipelineSettings<BasicPipeline>();
        cachedInstance = newSettings;
    }

#if UNITY_EDITOR

    static public BasicRenderPipelineGlobalSettings Ensure(bool canCreateNewAsset = true)
    {
        if (instance == null || instance.Equals(null))
        {
            //try load at default path
            BasicRenderPipelineGlobalSettings loaded = AssetDatabase.LoadAssetAtPath<BasicRenderPipelineGlobalSettings>($"Assets/BasicDefaultResources/BasicRenderPipelineGlobalSettings.asset");

            if (loaded == null)
            {
                //Use any available
                IEnumerator<BasicRenderPipelineGlobalSettings> enumerator = CoreUtils.LoadAllAssets<BasicRenderPipelineGlobalSettings>().GetEnumerator();
                if (enumerator.MoveNext())
                    loaded = enumerator.Current;
            }

            if (loaded != null)
                UpdateGraphicsSettings(loaded);

            // No migration available and no asset available? Create one if allowed
            if (canCreateNewAsset && instance == null)
            {
                var createdAsset = Create($"Assets/BasicDefaultResources/BasicRenderPipelineGlobalSettings.asset");
                UpdateGraphicsSettings(createdAsset);

                Debug.LogWarning("No HDRP Global Settings Asset is assigned. One has been created for you. If you want to modify it, go to Project Settings > Graphics > HDRP Global Settings.");
            }

            if (instance == null)
                Debug.LogError("Cannot find any HDRP Global Settings asset and Cannot create one from former used HDRP Asset.");

            Debug.Assert(instance, "Could not create HDRP's Global Settings - HDRP may not work correctly - Open the Graphics Window for additional help.");
        }

        return instance;
    }

    internal static BasicRenderPipelineGlobalSettings Create(string path, BasicRenderPipelineGlobalSettings dataSource = null)
    {
        BasicRenderPipelineGlobalSettings assetCreated = null;

        //ensure folder tree exist
        CoreUtils.EnsureFolderTreeInAssetFilePath(path);

        //prevent any path conflict
        path = AssetDatabase.GenerateUniqueAssetPath(path);

        //asset creation
        assetCreated = ScriptableObject.CreateInstance<BasicRenderPipelineGlobalSettings>();
        assetCreated.name = Path.GetFileName(path);
        AssetDatabase.CreateAsset(assetCreated, path);
        Debug.Assert(assetCreated);

        // copy data from provided source
        if (dataSource != null)
            EditorUtility.CopySerialized(dataSource, assetCreated);

        assetCreated.EnsureRuntimeResources(forceReload: true);

        return assetCreated;
    }

#endif // UNITY_EDITOR

    [SerializeField]
    BasicRenderPipelineRuntimeResources m_RenderPipelineResources;
    internal BasicRenderPipelineRuntimeResources renderPipelineResources
    {
        get
        {
#if UNITY_EDITOR
            EnsureRuntimeResources(false);
#endif
            return m_RenderPipelineResources;
        }
    }


#if UNITY_EDITOR
    // Yes it is stupid to retry right away but making it called in EditorApplication.delayCall
    // from EnsureResources create GC
    void DelayedNullReload<T>(string resourcePath)
        where T : BasicRenderPipelineResources
    {
        T resourcesDelayed = AssetDatabase.LoadAssetAtPath<T>(resourcePath);
        if (resourcesDelayed == null)
            EditorApplication.delayCall += () => DelayedNullReload<T>(resourcePath);
        else
            ResourceReloader.ReloadAllNullIn(resourcesDelayed, "Assets/BasicPipeline");
    }

    void EnsureResources<T>(bool forceReload, ref T resources, string resourcePath, Func<BasicRenderPipelineGlobalSettings, bool> checker)
        where T : BasicRenderPipelineResources
    {
        T resourceChecked = null;

        if (checker(this))
        {
            if (!EditorUtility.IsPersistent(resources)) // if not loaded from the Asset database
            {
                // try to load from AssetDatabase if it is ready
                resourceChecked = AssetDatabase.LoadAssetAtPath<T>(resourcePath);
                if (resourceChecked && !resourceChecked.Equals(null))
                    resources = resourceChecked;
            }
            if (forceReload)
                ResourceReloader.ReloadAllNullIn(resources, "Assets/BasicPipeline");
            return;
        }

        resourceChecked = AssetDatabase.LoadAssetAtPath<T>(resourcePath);
        if (resourceChecked != null && !resourceChecked.Equals(null))
        {
            resources = resourceChecked;
            if (forceReload)
                ResourceReloader.ReloadAllNullIn(resources, "Assets/BasicPipeline");
        }
        else
        {
            // Asset database may not be ready
            var objs = InternalEditorUtility.LoadSerializedFileAndForget(resourcePath);
            resources = (objs != null && objs.Length > 0) ? objs[0] as T : null;
            if (forceReload)
            {
                try
                {
                    if (ResourceReloader.ReloadAllNullIn(resources, "Assets/BasicPipeline"))
                    {
                        InternalEditorUtility.SaveToSerializedFileAndForget(
                            new UnityEngine.Object[] { resources },
                            resourcePath,
                            true);
                    }
                }
                catch (System.Exception e)
                {
                    // This can be called at a time where AssetDatabase is not available for loading.
                    // When this happens, the GUID can be get but the resource loaded will be null.
                    // Using the ResourceReloader mechanism in CoreRP, it checks this and add InvalidImport data when this occurs.
                    if (!(e.Data.Contains("InvalidImport") && e.Data["InvalidImport"] is int dii && dii == 1))
                        Debug.LogException(e);
                    else
                        DelayedNullReload<T>(resourcePath);
                }
            }
        }
        Debug.Assert(checker(this), $"Could not load {typeof(T).Name}.");
    }

#endif

#if UNITY_EDITOR
    // be sure to cach result for not using GC in a frame after first one.
    static readonly string runtimeResourcesPath = "Assets/BasicPipeline/Runtime/RenderPipelineResources/BasicRenderPipelineRuntimeResources.asset";

    internal void EnsureRuntimeResources(bool forceReload)
        => EnsureResources(forceReload, ref m_RenderPipelineResources, runtimeResourcesPath, AreRuntimeResourcesCreated_Internal);

    // Passing method in a Func argument create a functor that create GC
    // If it is static it is then only computed once but the Ensure is called after first frame which will make our GC check fail
    // So create it once and store it here.
    // Expected usage: HDRenderPipelineGlobalSettings.AreRuntimeResourcesCreated(anyHDRenderPipelineGlobalSettings) that will return a bool
    static Func<BasicRenderPipelineGlobalSettings, bool> AreRuntimeResourcesCreated_Internal = global
        => global.m_RenderPipelineResources != null && !global.m_RenderPipelineResources.Equals(null);
#endif
}
