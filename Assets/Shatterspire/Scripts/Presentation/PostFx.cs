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

            // Tonemapping zuerst: ohne Kurve wirken gesaettigte Farben schnell
            // ausgebrannt, sobald Bloom dazukommt.
            var tonemapping = profile.Add<Tonemapping>();
            tonemapping.mode.overrideState = true;
            tonemapping.mode.value = TonemappingMode.ACES;

            // Bloom traegt den Stil: Kristalle, Projektile und Trefferblitze
            // bekommen dadurch ueberhaupt erst Leuchtkraft.
            var bloom = profile.Add<Bloom>();
            bloom.intensity.overrideState = true;
            bloom.intensity.value = 0.85f;
            bloom.threshold.overrideState = true;
            bloom.threshold.value = 0.95f;
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

            // Saettigung leicht hoch und Kontrast dazu: der Screenshot war ueber
            // die ganze Flaeche ein einziger, mittiger Farbwert.
            var grading = profile.Add<ColorAdjustments>();
            grading.postExposure.overrideState = true;
            grading.postExposure.value = 0.12f;
            grading.contrast.overrideState = true;
            grading.contrast.value = 14f;
            grading.saturation.overrideState = true;
            grading.saturation.value = 12f;
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
