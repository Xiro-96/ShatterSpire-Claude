using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Shatterspire
{
    /// <summary>
    /// Baut das globale Post-Processing zur Laufzeit auf, passend zum restlichen
    /// Projekt, in dem auch alles andere aus Code entsteht.
    ///
    /// Bis hierher lief das Spiel voellig ohne Post-Processing: der Renderer hatte
    /// keine PostProcessData zugewiesen und es existierte kein Volume. Genau daher
    /// kommt der flache, unfertige Eindruck — nicht von den Modellen.
    /// </summary>
    public static class PostFx
    {
        private static VolumeProfile profile;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetProfile() => profile = null;

        public static void Build()
        {
            if (Object.FindAnyObjectByType<Volume>()) return;

            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Shatterspire Post FX";

            // Neutral statt ACES. ACES ist eine filmische Kurve und entsaettigt sehr
            // helle Werte gezielt Richtung Weiss. Im ersten Spielbild mit dieser
            // Einstellung wurde der orangegoldene Lift-Ring dadurch zu einer
            // flachen weissen Scheibe - fuer einen gesaettigten Stil die falsche Wahl.
            var tonemapping = profile.Add<Tonemapping>();
            tonemapping.mode.overrideState = true;
            tonemapping.mode.value = TonemappingMode.Neutral;

            // Jedes emissive Material im Projekt leuchtet mit color * 1,35. Solange
            // HDR aus war, wurde das bei 1 abgeschnitten. Mit HDR liegen diese Werte
            // darueber - eine Schwelle knapp unter 1 liess deshalb jede leuchtende
            // Flaeche bluehen. Erst oberhalb von 1,35 bluehen nur noch echte
            // Spitzen wie Treffer und Projektilkerne.
            var bloom = profile.Add<Bloom>();
            bloom.intensity.overrideState = true;
            bloom.intensity.value = 0.42f;
            bloom.threshold.overrideState = true;
            bloom.threshold.value = 1.4f;
            bloom.scatter.overrideState = true;
            bloom.scatter.value = 0.62f;
            bloom.tint.overrideState = true;
            bloom.tint.value = new Color(0.86f, 0.93f, 1f);
            // Ein Hauch Streulicht, damit helle Kanten nicht wie aufgeklebt wirken.
            bloom.dirtIntensity.overrideState = true;
            bloom.dirtIntensity.value = 0f;

            // Leichte Vignette zieht den Blick zur Mitte. Auf einem Telefon ist das
            // der billigste Weg, die Aufmerksamkeit dort zu halten, wo gespielt wird.
            var vignette = profile.Add<Vignette>();
            vignette.intensity.overrideState = true;
            vignette.intensity.value = 0.26f;
            vignette.smoothness.overrideState = true;
            vignette.smoothness.value = 0.45f;
            vignette.color.overrideState = true;
            vignette.color.value = new Color(0.02f, 0.02f, 0.05f);

            // Kontrast hoch, Belichtung neutral. Die erste Einstellung hatte +0,12
            // Belichtung - zusammen mit HDR und dem Kantenlicht hat das den Boden
            // ausgewaschen. Neutral-Tonemapping haelt die Saettigung ohnehin besser
            // als ACES, deshalb reicht hier ein kleiner Zuschlag.
            var grading = profile.Add<ColorAdjustments>();
            grading.postExposure.overrideState = true;
            grading.postExposure.value = 0f;
            grading.contrast.overrideState = true;
            grading.contrast.value = 16f;
            grading.saturation.overrideState = true;
            grading.saturation.value = 6f;
            grading.colorFilter.overrideState = true;
            grading.colorFilter.value = new Color(1f, 0.98f, 0.95f);

            var host = new GameObject("Post FX Volume");
            var volume = host.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;
            volume.weight = 1f;
            volume.sharedProfile = profile;
        }

        /// <summary>
        /// Ohne dieses Flag rendert die Kamera den Stack nicht, egal wie das Volume
        /// eingestellt ist.
        /// </summary>
        public static void EnableOn(Camera camera)
        {
            if (!camera) return;
            var data = camera.GetUniversalAdditionalCameraData();
            if (!data) return;
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
        }
    }
}
