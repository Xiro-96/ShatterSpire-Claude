using System.Collections.Generic;
using UnityEngine;

namespace Shatterspire
{
    /// <summary>
    /// Spielt die Klaenge aus <see cref="ProceduralSound"/> ab. Bewusst statisch wie
    /// <see cref="PrototypeVfx"/> und <see cref="CameraController.Impulse"/>: Ton ist Darstellung auf
    /// genau diesem Geraet und gehoert nicht zum Spielzustand, den sich im Co-op mehrere Clients
    /// teilen muessen.
    ///
    /// Aufbau: eine Handvoll AudioSources im Kreis. Weltklaenge laufen mit Abstand, Menue und Ansagen
    /// ohne. Derselbe Klang im selben Moment wird zusammengefasst, sonst summieren sich zehn Treffer
    /// einer Flaechenwirkung zu einem Knall.
    /// </summary>
    public static class Sfx
    {
        private const int Voices = 20;
        private const float MaxDistance = 34f;
        /// <summary>Mindestabstand zwischen zwei gleichen Klaengen. Verhindert das Aufsummieren.</summary>
        private const float RetriggerSeconds = 0.035f;

        private static AudioSource[] sources;
        private static int next;
        private static AudioSource ambience;
        private static readonly Dictionary<Sound, float> LastPlayed = new();
        private static float volume = 1f;

        /// <summary>Gesamtlautstaerke, 0 bis 1. Wirkt sofort auf alles.</summary>
        public static float Volume
        {
            get => volume;
            set
            {
                volume = Mathf.Clamp01(value);
                if (ambience) ambience.volume = volume * 0.55f;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Reset()
        {
            sources = null;
            ambience = null;
            next = 0;
            LastPlayed.Clear();
            volume = 1f;
        }

        /// <summary>Klang an einer Stelle in der Welt. Weit entfernte Kaempfe bleiben leise.</summary>
        public static void Play(Sound sound, Vector3 position, float gain = 1f)
        {
            var source = Take();
            if (!source || !Allow(sound)) return;
            source.transform.position = position;
            source.spatialBlend = 1f;
            Fire(source, sound, gain);
        }

        /// <summary>Klang ohne Ort: Menue, Ansagen, Aufstieg.</summary>
        public static void Play2D(Sound sound, float gain = 1f)
        {
            var source = Take();
            if (!source || !Allow(sound)) return;
            source.transform.localPosition = Vector3.zero;
            source.spatialBlend = 0f;
            Fire(source, sound, gain);
        }

        /// <summary>Startet die Hintergrundflaeche. Mehrfacher Aufruf laesst die laufende Schleife stehen.</summary>
        public static void StartAmbience()
        {
            EnsureRig();
            if (!ambience || ambience.isPlaying) return;
            ambience.clip = ProceduralSound.For(Sound.Ambience);
            ambience.loop = true;
            ambience.volume = volume * 0.55f;
            ambience.Play();
        }

        public static void StopAmbience()
        {
            if (ambience && ambience.isPlaying) ambience.Stop();
        }

        private static void Fire(AudioSource source, Sound sound, float gain)
        {
            source.clip = ProceduralSound.For(sound);
            source.loop = false;
            source.volume = Mathf.Clamp01(gain) * volume;
            var spread = ProceduralSound.PitchSpreadFor(sound);
            source.pitch = spread <= 0f ? 1f : 1f + Random.Range(-spread, spread);
            source.Play();
        }

        private static bool Allow(Sound sound)
        {
            if (LastPlayed.TryGetValue(sound, out var when) && Time.unscaledTime - when < RetriggerSeconds)
                return false;
            LastPlayed[sound] = Time.unscaledTime;
            return true;
        }

        private static AudioSource Take()
        {
            EnsureRig();
            if (sources == null) return null;
            // Im Kreis weiter und dabei die Quelle bevorzugen, die gerade nichts spielt.
            for (var attempt = 0; attempt < Voices; attempt++)
            {
                var candidate = sources[next];
                next = (next + 1) % Voices;
                if (candidate && !candidate.isPlaying) return candidate;
            }
            // Alles belegt: die aelteste Stimme wird ueberschrieben, statt den Klang zu verschlucken.
            var fallback = sources[next];
            next = (next + 1) % Voices;
            return fallback;
        }

        private static void EnsureRig()
        {
            if (sources != null && sources[0]) return;
            var root = new GameObject("SHATTERSPIRE Sound");
            Object.DontDestroyOnLoad(root);
            sources = new AudioSource[Voices];
            for (var i = 0; i < Voices; i++)
            {
                var voice = new GameObject("Voice " + i);
                voice.transform.SetParent(root.transform, false);
                sources[i] = Configure(voice.AddComponent<AudioSource>());
            }
            var bed = new GameObject("Ambience");
            bed.transform.SetParent(root.transform, false);
            ambience = Configure(bed.AddComponent<AudioSource>());
            ambience.spatialBlend = 0f;
        }

        private static AudioSource Configure(AudioSource source)
        {
            source.playOnAwake = false;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 4f;
            source.maxDistance = MaxDistance;
            source.dopplerLevel = 0f;
            // Klaenge laufen weiter, wenn der Hitstop die Zeit anhaelt - sonst wuerde jeder
            // kritische Treffer seinen eigenen Einschlag zerhacken.
            source.ignoreListenerPause = false;
            return source;
        }
    }
}
