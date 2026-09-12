using System.Collections.Generic;
using UnityEngine;

namespace Shatterspire
{
    /// <summary>Alle Klaenge des Prototyps. Namen beschreiben die Rolle, nicht die Technik.</summary>
    public enum Sound
    {
        Swing, Smash, Spin, Stab, Shot, Cast, Draw, Release,
        Dash, Footstep,
        HitLight, HitHeavy, HitCritical, Explosion, Shockwave,
        Death, EnemyArrival, Block, GuardBreak, Telegraph,
        HeavyReady, UltimateRise, CoreActivated, FloorCleared,
        UiClick, UiConfirm, PlayerHurt, Ambience
    }

    /// <summary>
    /// Erzeugt alle Klaenge zur Laufzeit aus Rechnung, so wie das Projekt auch seine Geometrie baut.
    /// Kein Audio-Asset im Repo, keine Lizenzfrage, und jeder Klang laesst sich an einer Zahl aendern
    /// statt in einem Editor.
    ///
    /// Aufbau: in einen Puffer werden Stimmen addiert - Toene mit Frequenzverlauf und gefiltertes
    /// Rauschen, jeweils mit eigener Huellkurve. Am Ende wird auf einen Zielpegel normiert, damit
    /// nichts uebersteuert und die Mischung ohne Regler stimmt.
    /// </summary>
    public static class ProceduralSound
    {
        public const int SampleRate = 44100;

        private enum Wave { Sine, Triangle, Square, Saw }

        private static readonly Dictionary<Sound, AudioClip> Cache = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache() => Cache.Clear();

        /// <summary>Erzeugt den Klang beim ersten Abruf und behaelt ihn danach.</summary>
        public static AudioClip For(Sound sound)
        {
            if (Cache.TryGetValue(sound, out var cached) && cached) return cached;
            var clip = Build(sound);
            Cache[sound] = clip;
            return clip;
        }

        /// <summary>Zielpegel je Klang. Leise Dauerklaenge duerfen die lauten Treffer nicht zudecken.</summary>
        public static float PeakFor(Sound sound) => sound switch
        {
            Sound.Footstep => 0.16f,
            Sound.Ambience => 0.1f,
            Sound.Draw => 0.3f,
            Sound.Swing or Sound.Stab => 0.36f,
            Sound.Smash or Sound.Spin => 0.46f,
            Sound.UiClick => 0.4f,
            Sound.UiConfirm => 0.5f,
            Sound.Telegraph => 0.42f,
            Sound.HitLight => 0.55f,
            Sound.Shockwave or Sound.HitHeavy => 0.8f,
            Sound.Explosion or Sound.GuardBreak or Sound.UltimateRise => 0.9f,
            _ => 0.65f
        };

        /// <summary>Wie stark die Tonhoehe bei jedem Abspielen streut. Ohne Streuung wird Wiederholung schnell laestig.</summary>
        public static float PitchSpreadFor(Sound sound) => sound switch
        {
            Sound.Ambience => 0f,
            Sound.CoreActivated or Sound.FloorCleared or Sound.UiConfirm or Sound.HeavyReady => 0.01f,
            Sound.Footstep or Sound.HitLight or Sound.Swing => 0.1f,
            _ => 0.055f
        };

        public static bool Loops(Sound sound) => sound == Sound.Ambience;

        private static AudioClip Build(Sound sound)
        {
            var length = LengthOf(sound);
            var buffer = new float[Mathf.Max(64, Mathf.RoundToInt(length * SampleRate))];
            var noise = new Noise((uint)(sound.GetHashCode() * 2654435761u + 12345u));
            Compose(sound, buffer, ref noise);
            Normalize(buffer, PeakFor(sound));
            if (Loops(sound)) CrossfadeEnds(buffer, 0.25f);
            else FadeTail(buffer, 0.01f);
            var clip = AudioClip.Create("SHATTERSPIRE " + sound, buffer.Length, 1, SampleRate, false);
            clip.SetData(buffer, 0);
            return clip;
        }

        private static float LengthOf(Sound sound) => sound switch
        {
            Sound.UiClick => 0.09f,
            Sound.Footstep => 0.12f,
            Sound.HitLight => 0.14f,
            Sound.Shot or Sound.Release => 0.2f,
            Sound.Stab => 0.22f,
            Sound.Swing => 0.26f,
            Sound.HitHeavy or Sound.Block => 0.3f,
            Sound.Dash or Sound.HitCritical => 0.32f,
            Sound.Cast or Sound.UiConfirm => 0.36f,
            Sound.Smash or Sound.Telegraph => 0.4f,
            Sound.Shockwave or Sound.EnemyArrival => 0.45f,
            Sound.Spin or Sound.PlayerHurt => 0.5f,
            Sound.Draw or Sound.HeavyReady => 0.55f,
            Sound.Explosion or Sound.Death or Sound.GuardBreak => 0.7f,
            Sound.CoreActivated => 0.85f,
            Sound.UltimateRise => 1.1f,
            Sound.FloorCleared => 1.3f,
            Sound.Ambience => 5f,
            _ => 0.3f
        };

        private static void Compose(Sound sound, float[] buffer, ref Noise noise)
        {
            switch (sound)
            {
                // ── Nahkampf ────────────────────────────────────────────
                // Ein Schlag ist Luft plus Einschlag: erst gefiltertes Rauschen, das aufzieht und
                // wieder abfaellt, dann ein tiefer Ton, der nach unten laeuft.
                case Sound.Swing:
                    AddNoise(buffer, ref noise, 0f, 0.24f, 1f, 0.09f, 9f, 2600f, 420f);
                    AddTone(buffer, 0.05f, 0.16f, 210f, 120f, Wave.Sine, 0.5f, 0.01f, 13f);
                    break;
                case Sound.Smash:
                    AddNoise(buffer, ref noise, 0f, 0.14f, 0.7f, 0.06f, 12f, 1900f, 300f);
                    AddTone(buffer, 0.1f, 0.3f, 150f, 62f, Wave.Sine, 1f, 0.004f, 9f);
                    AddTone(buffer, 0.1f, 0.12f, 320f, 180f, Wave.Triangle, 0.35f, 0.003f, 22f);
                    AddNoise(buffer, ref noise, 0.1f, 0.2f, 0.8f, 0.002f, 16f, 3400f, 700f);
                    break;
                case Sound.Spin:
                    // Wirbel: zwei Luftzuege hintereinander, dann der Einschlag der Runde.
                    AddNoise(buffer, ref noise, 0f, 0.2f, 0.7f, 0.08f, 10f, 2400f, 500f);
                    AddNoise(buffer, ref noise, 0.14f, 0.22f, 0.85f, 0.08f, 9f, 3000f, 500f);
                    AddTone(buffer, 0.3f, 0.2f, 140f, 58f, Wave.Sine, 1f, 0.004f, 11f);
                    AddNoise(buffer, ref noise, 0.3f, 0.18f, 0.7f, 0.002f, 18f, 4200f, 800f);
                    break;
                case Sound.Stab:
                    AddNoise(buffer, ref noise, 0f, 0.1f, 0.8f, 0.03f, 20f, 3800f, 900f);
                    AddTone(buffer, 0.02f, 0.16f, 420f, 190f, Wave.Triangle, 0.6f, 0.004f, 16f);
                    break;

                // ── Fernkampf ───────────────────────────────────────────
                case Sound.Shot:
                    AddNoise(buffer, ref noise, 0f, 0.07f, 1f, 0.002f, 42f, 6000f, 1200f);
                    AddTone(buffer, 0f, 0.14f, 760f, 240f, Wave.Square, 0.45f, 0.002f, 26f);
                    break;
                case Sound.Draw:
                    // Spannen: ein knarzender Aufzug, der sich langsam strafft.
                    AddNoise(buffer, ref noise, 0f, 0.5f, 0.55f, 0.16f, 2.4f, 1500f, 260f);
                    AddTone(buffer, 0.05f, 0.45f, 120f, 260f, Wave.Saw, 0.16f, 0.15f, 2f);
                    break;
                case Sound.Release:
                    AddNoise(buffer, ref noise, 0f, 0.06f, 1f, 0.001f, 48f, 7000f, 1600f);
                    AddTone(buffer, 0f, 0.12f, 540f, 150f, Wave.Triangle, 0.5f, 0.001f, 30f);
                    break;
                case Sound.Cast:
                    // Magie: zwei leicht verstimmte Stimmen, die zusammen aufsteigen - das Schweben
                    // entsteht durch die Schwebung zwischen beiden.
                    AddTone(buffer, 0f, 0.32f, 300f, 780f, Wave.Sine, 0.8f, 0.03f, 6f);
                    AddTone(buffer, 0f, 0.32f, 303f, 792f, Wave.Sine, 0.6f, 0.03f, 6f);
                    AddTone(buffer, 0.02f, 0.2f, 900f, 1500f, Wave.Triangle, 0.22f, 0.02f, 9f);
                    break;

                // ── Bewegung ────────────────────────────────────────────
                case Sound.Dash:
                    AddNoise(buffer, ref noise, 0f, 0.3f, 1f, 0.05f, 8f, 5200f, 600f);
                    AddTone(buffer, 0f, 0.2f, 180f, 420f, Wave.Sine, 0.35f, 0.02f, 8f);
                    break;
                case Sound.Footstep:
                    AddNoise(buffer, ref noise, 0f, 0.1f, 1f, 0.002f, 38f, 1300f, 90f);
                    AddTone(buffer, 0f, 0.06f, 110f, 70f, Wave.Sine, 0.5f, 0.002f, 34f);
                    break;

                // ── Treffer ─────────────────────────────────────────────
                case Sound.HitLight:
                    AddNoise(buffer, ref noise, 0f, 0.1f, 1f, 0.001f, 36f, 4200f, 500f);
                    AddTone(buffer, 0f, 0.1f, 260f, 150f, Wave.Triangle, 0.55f, 0.001f, 30f);
                    break;
                case Sound.HitHeavy:
                    AddNoise(buffer, ref noise, 0f, 0.16f, 1f, 0.002f, 22f, 3200f, 260f);
                    AddTone(buffer, 0f, 0.26f, 170f, 70f, Wave.Sine, 1f, 0.002f, 12f);
                    break;
                case Sound.HitCritical:
                    // Kritischer Treffer bekommt einen hellen Oberton, damit er sich vom Rest abhebt.
                    AddNoise(buffer, ref noise, 0f, 0.12f, 1f, 0.001f, 30f, 6500f, 900f);
                    AddTone(buffer, 0f, 0.2f, 300f, 140f, Wave.Triangle, 0.7f, 0.001f, 18f);
                    AddTone(buffer, 0.005f, 0.26f, 1580f, 1420f, Wave.Sine, 0.4f, 0.001f, 11f);
                    AddTone(buffer, 0.005f, 0.26f, 2370f, 2180f, Wave.Sine, 0.22f, 0.001f, 13f);
                    break;
                case Sound.Explosion:
                    AddNoise(buffer, ref noise, 0f, 0.6f, 1f, 0.004f, 7f, 2400f, 60f);
                    AddTone(buffer, 0f, 0.45f, 120f, 38f, Wave.Sine, 1f, 0.003f, 7f);
                    AddNoise(buffer, ref noise, 0f, 0.1f, 0.7f, 0.001f, 30f, 8000f, 1500f);
                    break;
                case Sound.Shockwave:
                    AddTone(buffer, 0f, 0.4f, 190f, 52f, Wave.Sine, 1f, 0.003f, 9f);
                    AddNoise(buffer, ref noise, 0f, 0.22f, 0.85f, 0.002f, 15f, 2600f, 200f);
                    AddTone(buffer, 0f, 0.1f, 380f, 200f, Wave.Triangle, 0.3f, 0.002f, 26f);
                    break;
                case Sound.PlayerHurt:
                    AddTone(buffer, 0f, 0.4f, 240f, 90f, Wave.Saw, 0.7f, 0.004f, 7f);
                    AddNoise(buffer, ref noise, 0f, 0.2f, 0.8f, 0.003f, 14f, 1800f, 120f);
                    break;

                // ── Schildtraeger ───────────────────────────────────────
                // Metall klingt unharmonisch. Drei Partiale ohne ganzzahliges Verhaeltnis geben das
                // "Tink", das ein einzelner Sinus nie hinbekommt.
                case Sound.Block:
                    AddTone(buffer, 0f, 0.28f, 1190f, 1150f, Wave.Sine, 1f, 0.001f, 14f);
                    AddTone(buffer, 0f, 0.24f, 1790f, 1730f, Wave.Sine, 0.6f, 0.001f, 18f);
                    AddTone(buffer, 0f, 0.2f, 2670f, 2590f, Wave.Sine, 0.35f, 0.001f, 22f);
                    AddNoise(buffer, ref noise, 0f, 0.06f, 0.5f, 0.001f, 46f, 9000f, 2200f);
                    break;
                case Sound.GuardBreak:
                    AddTone(buffer, 0f, 0.6f, 820f, 760f, Wave.Sine, 1f, 0.002f, 6f);
                    AddTone(buffer, 0f, 0.55f, 1310f, 1210f, Wave.Sine, 0.8f, 0.002f, 7f);
                    AddTone(buffer, 0f, 0.5f, 1970f, 1840f, Wave.Sine, 0.5f, 0.002f, 9f);
                    AddTone(buffer, 0f, 0.45f, 3050f, 2860f, Wave.Sine, 0.3f, 0.002f, 11f);
                    AddNoise(buffer, ref noise, 0f, 0.3f, 0.7f, 0.002f, 11f, 6000f, 700f);
                    AddTone(buffer, 0f, 0.3f, 150f, 60f, Wave.Sine, 0.6f, 0.003f, 10f);
                    break;

                // ── Ansagen ─────────────────────────────────────────────
                case Sound.Telegraph:
                    // Zwei kurze Stoesse: ein einzelner Ton wird im Kampflaerm ueberhoert.
                    AddTone(buffer, 0f, 0.12f, 680f, 680f, Wave.Square, 0.5f, 0.004f, 18f);
                    AddTone(buffer, 0.17f, 0.14f, 680f, 640f, Wave.Square, 0.6f, 0.004f, 16f);
                    break;
                case Sound.EnemyArrival:
                    AddTone(buffer, 0f, 0.42f, 90f, 46f, Wave.Saw, 1f, 0.06f, 5f);
                    AddNoise(buffer, ref noise, 0f, 0.4f, 0.45f, 0.08f, 6f, 900f, 60f);
                    break;
                case Sound.HeavyReady:
                    AddTone(buffer, 0f, 0.5f, 660f, 880f, Wave.Sine, 1f, 0.01f, 5f);
                    AddTone(buffer, 0.06f, 0.44f, 990f, 1320f, Wave.Sine, 0.45f, 0.01f, 6f);
                    break;
                case Sound.UltimateRise:
                    // Aufstieg mit Einschlag am Ende: das Warten wird hoerbar, der Ausloeser sitzt.
                    AddTone(buffer, 0f, 0.8f, 70f, 640f, Wave.Saw, 0.55f, 0.25f, 0.9f);
                    AddTone(buffer, 0f, 0.8f, 105f, 960f, Wave.Sine, 0.4f, 0.25f, 0.9f);
                    AddNoise(buffer, ref noise, 0f, 0.8f, 0.35f, 0.5f, 1.2f, 5000f, 200f);
                    AddTone(buffer, 0.78f, 0.32f, 200f, 55f, Wave.Sine, 1f, 0.002f, 10f);
                    AddNoise(buffer, ref noise, 0.78f, 0.3f, 0.9f, 0.002f, 12f, 7000f, 400f);
                    break;
                case Sound.CoreActivated:
                    AddTone(buffer, 0f, 0.3f, 440f, 440f, Wave.Sine, 0.8f, 0.01f, 7f);
                    AddTone(buffer, 0.14f, 0.3f, 587f, 587f, Wave.Sine, 0.85f, 0.01f, 7f);
                    AddTone(buffer, 0.28f, 0.5f, 880f, 880f, Wave.Sine, 1f, 0.01f, 4.5f);
                    AddTone(buffer, 0.28f, 0.5f, 1320f, 1320f, Wave.Sine, 0.4f, 0.01f, 5f);
                    break;
                case Sound.FloorCleared:
                    AddTone(buffer, 0f, 0.45f, 523f, 523f, Wave.Triangle, 0.8f, 0.01f, 4.5f);
                    AddTone(buffer, 0.12f, 0.5f, 659f, 659f, Wave.Triangle, 0.85f, 0.01f, 4.2f);
                    AddTone(buffer, 0.24f, 0.9f, 784f, 784f, Wave.Triangle, 1f, 0.01f, 2.8f);
                    AddTone(buffer, 0.24f, 0.9f, 1046f, 1046f, Wave.Sine, 0.55f, 0.01f, 2.8f);
                    AddTone(buffer, 0.24f, 0.9f, 261f, 261f, Wave.Sine, 0.6f, 0.01f, 2.6f);
                    break;
                case Sound.Death:
                    AddTone(buffer, 0f, 0.6f, 300f, 70f, Wave.Saw, 0.8f, 0.006f, 4.5f);
                    AddNoise(buffer, ref noise, 0f, 0.55f, 0.7f, 0.004f, 5.5f, 2200f, 90f);
                    AddNoise(buffer, ref noise, 0.3f, 0.35f, 0.5f, 0.03f, 7f, 5000f, 1200f);
                    break;

                // ── Menue ───────────────────────────────────────────────
                case Sound.UiClick:
                    AddTone(buffer, 0f, 0.07f, 880f, 760f, Wave.Square, 0.6f, 0.001f, 40f);
                    AddNoise(buffer, ref noise, 0f, 0.03f, 0.3f, 0.001f, 70f, 7000f, 2500f);
                    break;
                case Sound.UiConfirm:
                    AddTone(buffer, 0f, 0.16f, 660f, 660f, Wave.Triangle, 0.8f, 0.004f, 14f);
                    AddTone(buffer, 0.1f, 0.26f, 990f, 990f, Wave.Triangle, 1f, 0.004f, 9f);
                    break;

                // ── Hintergrund ─────────────────────────────────────────
                case Sound.Ambience:
                    // Ein Turm, der unter Spannung steht: tiefe Grundstimme, dazu zwei leicht
                    // verstimmte Partiale, deren Schwebung die Flaeche in Bewegung haelt.
                    AddDrone(buffer, 55f, 0.9f);
                    AddDrone(buffer, 82.6f, 0.45f);
                    AddDrone(buffer, 110.3f, 0.3f);
                    AddDrone(buffer, 164.5f, 0.12f);
                    AddNoise(buffer, ref noise, 0f, LengthOf(Sound.Ambience), 0.1f, 1.2f, 0.05f, 380f, 40f);
                    break;
            }
        }

        // ── Stimmen ─────────────────────────────────────────────────────

        /// <summary>
        /// Addiert einen Ton mit gleitender Frequenz. <paramref name="attack"/> ist die Anstiegszeit in
        /// Sekunden, <paramref name="decay"/> der Abfall in 1/Sekunden - grosse Werte klingen perkussiv.
        /// </summary>
        private static void AddTone(float[] buffer, float start, float duration, float fromHz, float toHz,
            Wave wave, float gain, float attack, float decay)
        {
            var first = Mathf.Max(0, Mathf.RoundToInt(start * SampleRate));
            var count = Mathf.RoundToInt(duration * SampleRate);
            var phase = 0f;
            for (var i = 0; i < count; i++)
            {
                var index = first + i;
                if (index >= buffer.Length) break;
                var t = i / (float)count;
                var hz = Mathf.Lerp(fromHz, toHz, t);
                phase += hz / SampleRate;
                if (phase > 1f) phase -= Mathf.Floor(phase);
                buffer[index] += Shape(wave, phase) * gain * Envelope(i / (float)SampleRate, attack, decay);
            }
        }

        /// <summary>
        /// Addiert gefiltertes Rauschen. Der Tiefpass ist vierpolig (vier Einpolstufen in Reihe, rund
        /// 24 dB je Oktave), der Hochpass zweipolig.
        ///
        /// Einpolig reichte nicht: weisses Rauschen hat gleich viel Energie je Hertz, und ein Abfall
        /// von nur 6 dB je Oktave laesst oberhalb der Grenze mehr Energie stehen als darunter liegt.
        /// Gemessen landete dadurch jeder Einschlag bei 5 bis 9 kHz - ein Zischen statt eines
        /// Schlages. Mit der Kaskade sitzt der Klang dort, wo die Grenzfrequenz es sagt.
        /// </summary>
        private static void AddNoise(float[] buffer, ref Noise noise, float start, float duration, float gain,
            float attack, float decay, float lowpassHz, float highpassHz)
        {
            var first = Mathf.Max(0, Mathf.RoundToInt(start * SampleRate));
            var count = Mathf.RoundToInt(duration * SampleRate);
            var lowpass = Coefficient(lowpassHz);
            var highpass = Coefficient(highpassHz);
            var a = 0f;
            var b = 0f;
            var c = 0f;
            var d = 0f;
            var e = 0f;
            var f = 0f;
            // Die Kaskade daempft auch im Durchlassbereich; das holt der Faktor wieder herein.
            const float makeUp = 3.6f;
            for (var i = 0; i < count; i++)
            {
                var index = first + i;
                if (index >= buffer.Length) break;
                a += (noise.Next() - a) * lowpass;
                b += (a - b) * lowpass;
                c += (b - c) * lowpass;
                d += (c - d) * lowpass;
                e += (d - e) * highpass;
                f += (e - f) * highpass;
                buffer[index] += (d - f) * gain * makeUp * Envelope(i / (float)SampleRate, attack, decay);
            }
        }

        /// <summary>Dauerstimme fuer die Hintergrundflaeche, ueber die ganze Laenge und ohne Abfall.</summary>
        private static void AddDrone(float[] buffer, float hz, float gain)
        {
            var phase = 0f;
            for (var i = 0; i < buffer.Length; i++)
            {
                phase += hz / SampleRate;
                if (phase > 1f) phase -= Mathf.Floor(phase);
                // Langsame Lautstaerkeatmung, je Stimme eine andere Geschwindigkeit.
                var breath = 0.82f + 0.18f * Mathf.Sin(i / (float)SampleRate * (0.21f + hz * 0.0013f) * Mathf.PI * 2f);
                buffer[i] += Mathf.Sin(phase * Mathf.PI * 2f) * gain * breath;
            }
        }

        private static float Shape(Wave wave, float phase) => wave switch
        {
            Wave.Sine => Mathf.Sin(phase * Mathf.PI * 2f),
            Wave.Triangle => 4f * Mathf.Abs(phase - 0.5f) - 1f,
            Wave.Square => phase < 0.5f ? 1f : -1f,
            _ => phase * 2f - 1f
        };

        private static float Envelope(float seconds, float attack, float decay)
        {
            var rise = attack <= 0f ? 1f : Mathf.Clamp01(seconds / attack);
            return rise * Mathf.Exp(-decay * Mathf.Max(0f, seconds - attack));
        }

        /// <summary>Koeffizient eines Einpol-Filters fuer die gegebene Grenzfrequenz.</summary>
        private static float Coefficient(float hz)
            => Mathf.Clamp01(1f - Mathf.Exp(-2f * Mathf.PI * hz / SampleRate));

        // ── Nachbearbeitung ─────────────────────────────────────────────

        private static void Normalize(float[] buffer, float peak)
        {
            var loudest = 0f;
            for (var i = 0; i < buffer.Length; i++) loudest = Mathf.Max(loudest, Mathf.Abs(buffer[i]));
            if (loudest < 0.0001f) return;
            var scale = peak / loudest;
            for (var i = 0; i < buffer.Length; i++) buffer[i] *= scale;
        }

        /// <summary>Letzte Millisekunden ausblenden, sonst knackt das Ende.</summary>
        private static void FadeTail(float[] buffer, float seconds)
        {
            var count = Mathf.Min(buffer.Length, Mathf.RoundToInt(seconds * SampleRate));
            for (var i = 0; i < count; i++)
                buffer[buffer.Length - 1 - i] *= i / (float)count;
        }

        /// <summary>Blendet das Ende ueber den Anfang, damit die Schleife keine hoerbare Naht hat.</summary>
        private static void CrossfadeEnds(float[] buffer, float seconds)
        {
            var count = Mathf.Min(buffer.Length / 3, Mathf.RoundToInt(seconds * SampleRate));
            for (var i = 0; i < count; i++)
            {
                var fade = i / (float)count;
                var tail = buffer[buffer.Length - count + i];
                buffer[i] = buffer[i] * fade + tail * (1f - fade);
            }
            for (var i = 0; i < count; i++) buffer[buffer.Length - count + i] *= 1f - i / (float)count;
        }

        /// <summary>
        /// Eigener Rauschgenerator (Xorshift) statt UnityEngine.Random: gleicher Klang bei jedem Start
        /// und unabhaengig davon, wer sonst am globalen Zufall dreht.
        /// </summary>
        private struct Noise
        {
            private uint state;
            public Noise(uint seed) => state = seed == 0u ? 0x9E3779B9u : seed;

            public float Next()
            {
                state ^= state << 13;
                state ^= state >> 17;
                state ^= state << 5;
                return state / 2147483648f - 1f;
            }
        }
    }
}
