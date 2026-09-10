using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Shatterspire.Editor
{
    [InitializeOnLoad]
    internal static class UrpProjectSetup
    {
        private const string SettingsDirectory = "Assets/Shatterspire/Settings";
        private const string RendererPath = SettingsDirectory + "/MobileForwardRenderer.asset";
        private const string PipelinePath = SettingsDirectory + "/MobileURP.asset";

        static UrpProjectSetup() => EditorApplication.delayCall += EnsureUrp;

        private static void EnsureUrp()
        {
            if (GraphicsSettings.defaultRenderPipeline is UniversalRenderPipelineAsset) return;
            if (!AssetDatabase.IsValidFolder(SettingsDirectory))
            {
                Directory.CreateDirectory(SettingsDirectory);
                AssetDatabase.Refresh();
            }

            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (!renderer)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
            }

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (!pipeline)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                pipeline.name = "Shatterspire Mobile URP";
                pipeline.supportsHDR = false;
                pipeline.supportsCameraDepthTexture = false;
                pipeline.supportsCameraOpaqueTexture = false;
                pipeline.msaaSampleCount = 2;
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
            }

            GraphicsSettings.defaultRenderPipeline = pipeline;
            QualitySettings.renderPipeline = pipeline;
            EditorUtility.SetDirty(pipeline);
            AssetDatabase.SaveAssets();
            Debug.Log("SHATTERSPIRE: Mobile URP configured automatically.");
        }
    }
}
