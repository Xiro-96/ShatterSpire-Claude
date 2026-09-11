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

            if (renderer && EnsureAmbientOcclusion(renderer)) changed = true;

            if (changed) AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Ambient Occlusion dunkelt Ecken, Kanten und Kontaktstellen ab. Genau das fehlte den
        /// Aufnahmen vom 11.09.: Figuren, Mauern und Kisten standen ohne Kontaktschatten auf dem
        /// Boden, die Szene wirkte flach. Hinzugefuegt so, wie URPs eigener Renderer-Editor es
        /// tut - als Sub-Asset plus Eintrag in m_RendererFeatures und m_RendererFeatureMap.
        /// </summary>
        private static bool EnsureAmbientOcclusion(UniversalRendererData renderer)
        {
            foreach (var feature in renderer.rendererFeatures)
                if (feature is ScreenSpaceAmbientOcclusion) return false;

            // Listen zuerst pruefen, bevor ein Sub-Asset entsteht - sonst bliebe bei einem
            // geaenderten URP-Format ein verwaistes Objekt im Renderer-Asset zurueck.
            var rendererObject = new SerializedObject(renderer);
            var features = rendererObject.FindProperty("m_RendererFeatures");
            var map = rendererObject.FindProperty("m_RendererFeatureMap");
            if (features == null || map == null)
            {
                Debug.LogWarning("SHATTERSPIRE: Renderer-Feature-Listen nicht gefunden, SSAO nicht hinzugefuegt.");
                return false;
            }

            var ssao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
            ssao.name = nameof(ScreenSpaceAmbientOcclusion);
            AssetDatabase.AddObjectToAsset(ssao, renderer);
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ssao, out _, out long localId);

            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = ssao;
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            rendererObject.ApplyModifiedPropertiesWithoutUndo();

            // Fuer Mobile: halbe Aufloesung und wenige Samples. Der Standardradius von 0,035 ist
            // bei einer Kamera elf Einheiten ueber dem Boden praktisch unsichtbar.
            var settings = new SerializedObject(ssao);
            // Die Felder sind in URP internal und nur ueber den serialisierten Pfad erreichbar.
            // Fehlt ein Pfad nach einem URP-Update, soll das Setup warnen statt abzubrechen.
            void Set(string path, System.Action<SerializedProperty> apply)
            {
                var property = settings.FindProperty(path);
                if (property != null) apply(property);
                else Debug.LogWarning($"SHATTERSPIRE: SSAO-Einstellung {path} nicht gefunden.");
            }
            Set("m_Settings.Intensity", p => p.floatValue = 2f);
            Set("m_Settings.Radius", p => p.floatValue = 0.25f);
            Set("m_Settings.DirectLightingStrength", p => p.floatValue = 0.25f);
            Set("m_Settings.Downsample", p => p.boolValue = true);
            Set("m_Settings.Samples", p => p.enumValueIndex = 1); // Medium - Low rauschte bei bewegter Kamera sichtbar
            settings.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(ssao);
            EditorUtility.SetDirty(renderer);
            Debug.Log("SHATTERSPIRE: Ambient Occlusion am Renderer ergaenzt.");
            return true;
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
