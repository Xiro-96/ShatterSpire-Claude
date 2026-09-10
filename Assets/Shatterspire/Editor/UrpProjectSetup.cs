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

        // Der Pfad des Standard-Assets im URP-Paket. Ohne diese Daten rendert der
        // Renderer den Post-Processing-Stack ueberhaupt nicht.
        private const string DefaultPostProcessData =
            "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset";

        static UrpProjectSetup()
        {
            EditorApplication.delayCall += EnsureUrp;
            EditorApplication.delayCall += RepairPostProcessing;
        }

        [MenuItem("SHATTERSPIRE/Render-Pipeline reparieren", false, 40)]
        public static void RepairPostProcessing()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            var changed = false;

            // Der Renderer wurde per CreateInstance erzeugt und hatte daher nie
            // PostProcessData zugewiesen - im YAML stand postProcessData: {fileID: 0}.
            // Solange das so ist, bleibt jedes Volume wirkungslos.
            if (renderer && !renderer.postProcessData)
            {
                var data = AssetDatabase.LoadAssetAtPath<PostProcessData>(DefaultPostProcessData);
                if (data)
                {
                    renderer.postProcessData = data;
                    EditorUtility.SetDirty(renderer);
                    changed = true;
                    Debug.Log("SHATTERSPIRE: PostProcessData am Renderer ergaenzt.");
                }
                else
                {
                    Debug.LogWarning($"SHATTERSPIRE: {DefaultPostProcessData} nicht gefunden. " +
                                     "Post-Processing bleibt aus.");
                }
            }

            // Ohne HDR clippt Bloom an hellen Kanten, statt weich auszulaufen.
            if (pipeline && !pipeline.supportsHDR)
            {
                pipeline.supportsHDR = true;
                EditorUtility.SetDirty(pipeline);
                changed = true;
                Debug.Log("SHATTERSPIRE: HDR in der Pipeline aktiviert.");
            }

            if (changed) AssetDatabase.SaveAssets();
        }

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
