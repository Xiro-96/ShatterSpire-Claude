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
    /// Kein Audio-Asset im Repo, keine Lizenzfrage, und jeder Klang laesst sich an einer Zahl aendern.
    ///
    /// Verfahren: <b>modale Synthese</b>. Ein echter Einschlag ist kein Ton, sondern ein kurzer Stoss,
    /// der einen Koerper zum Klingen bringt - Knochen, Stahl, Holz. Genau so entsteht er hier: ein
    /// Rauschstoss von wenigen Millisekunden regt Resonatoren an, deren Frequenzen absichtlich nicht
    /// im ganzzahligen Verhaeltnis stehen. Dazu ein Raum aus verzoegerten, gedaempften Kopien.
    ///
    /// Die erste Fassung arbeitete mit reinen Oszillatoren und klang deshalb nach Synthesizer:
    /// Rechteck- und Saegezaehne fuer Treffer, Dreiecks-Arpeggien fuer Melodien. Beides ist hier
    /// ersetzt - Treffer durch angeregte Koerper, Melodien durch Glockenpartiale.
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
            var room = RoomFor(sound);
            if (room > 0f) ApplyRoom(buffer, room);
            Normalize(buffer, PeakFor(sound));
            if (Loops(sound)) CrossfadeEnds(buffer, 0.25f);
            else FadeTail(buffer, 0.01f);
            var clip = AudioClip.Create("SHATTERSPIRE " + sound, buffer.Length, 1, SampleRate, false);
            clip.SetData(buffer, 0);
            return clip;
        }

        /// <summary>
        /// Anteil des Raums. Der Turm ist Stein: Weltklaenge bekommen eine Fahne, Menuetoene keine -
        /// die kommen aus der Oberflaeche und nicht aus dem Raum.
        /// </summary>
        private static float RoomFor(Sound sound) => sound switch
        {
            Sound.UiClick or Sound.UiConfirm or Sound.Ambience => 0f,
            Sound.Footstep => 0.2f,
            Sound.Explosion or Sound.GuardBreak or Sound.UltimateRise => 0.42f,
            Sound.Block or Sound.Shockwave or Sound.HitHeavy or Sound.Death => 0.32f,
            Sound.CoreActivated or Sound.FloorCleared or Sound.HeavyReady => 0.3f,
            _ => 0.24f
        };

        private static float LengthOf(Sound sound) => sound switch
        {
            Sound.UiClick => 0.1f,
            Sound.Footstep => 0.24f,
            Sound.HitLight => 0.28f,
            Sound.Shot or Sound.Release => 0.3f,
            Sound.Stab => 0.3f,
            Sound.Swing => 0.3f,
            Sound.HitHeavy => 0.5f,
            Sound.Block => 0.55f,
            Sound.Dash or Sound.HitCritical => 0.45f,
            Sound.Cast or Sound.UiConfirm => 0.45f,
            Sound.Smash or Sound.Telegraph => 0.55f,
            Sound.Shockwave or Sound.EnemyArrival => 0.6f,
            Sound.Spin or Sound.PlayerHurt => 0.6f,
            Sound.Draw or Sound.HeavyReady => 0.7f,
            Sound.Explosion or Sound.GuardBreak => 1f,
            Sound.Death => 0.9f,
            Sound.CoreActivated => 1.3f,
            Sound.UltimateRise => 1.3f,
            Sound.FloorCleared => 1.9f,
            Sound.Ambience => 5f,
            _ => 0.4f
        };

        private static void Compose(Sound sound, float[] buffer, ref Noise noise)
        {
            switch (sound)
            {
                // ── Nahkampf ────────────────────────────────────────────
                // Luftzug: Rauschen, dessen Durchlassbereich mit dem Schwung wandert. Ein fester
                // Filter klingt wie Zischen, ein wandernder wie bewegte Luft.
                case Sound.Swing:
                    AddSweptNoise(buffer, ref noise, 0f, 0.28f, 1f, 0.1f, 7f, 700f, 3200f, 260f);
                    AddResonator(buffer, ref noise, 0.07f, 170f, 0.1f, 0.3f, 0.004f);
                    break;
                case Sound.Smash:
                    AddSweptNoise(buffer, ref noise, 0f, 0.13f, 0.6f, 0.07f, 9f, 500f, 2200f, 200f);
                    // Der Einschlag: Stahl auf Stein. Tiefer Koerper, darueber zwei harte Moden.
                    Strike(buffer, ref noise, 0.12f, 0.004f, 3400f, 1f);
                    AddResonator(buffer, ref noise, 0.12f, 78f, 0.34f, 1f, 0.004f);
                    AddResonator(buffer, ref noise, 0.12f, 146f, 0.22f, 0.7f, 0.003f);
                    AddResonator(buffer, ref noise, 0.12f, 395f, 0.1f, 0.4f, 0.002f);
                    break;
                case Sound.Spin:
                    AddSweptNoise(buffer, ref noise, 0f, 0.22f, 0.65f, 0.09f, 8f, 600f, 2600f, 240f);
                    AddSweptNoise(buffer, ref noise, 0.16f, 0.24f, 0.8f, 0.09f, 7f, 800f, 3400f, 260f);
                    Strike(buffer, ref noise, 0.34f, 0.005f, 3000f, 1f);
                    AddResonator(buffer, ref noise, 0.34f, 72f, 0.34f, 1f, 0.005f);
                    AddResonator(buffer, ref noise, 0.34f, 132f, 0.24f, 0.65f, 0.004f);
                    break;
                case Sound.Stab:
                    AddSweptNoise(buffer, ref noise, 0f, 0.09f, 0.7f, 0.03f, 16f, 1400f, 4200f, 700f);
                    Strike(buffer, ref noise, 0.06f, 0.002f, 5200f, 0.8f);
                    AddResonator(buffer, ref noise, 0.06f, 430f, 0.12f, 0.7f, 0.002f);
                    AddResonator(buffer, ref noise, 0.06f, 690f, 0.09f, 0.4f, 0.002f);
                    break;

                // ── Fernkampf ───────────────────────────────────────────
                // Armbrust: harter Schnapper plus schwingende Sehne.
                case Sound.Shot:
                    Strike(buffer, ref noise, 0f, 0.0015f, 7000f, 1f);
                    AddResonator(buffer, ref noise, 0f, 240f, 0.07f, 0.8f, 0.0015f);
                    AddResonator(buffer, ref noise, 0f, 620f, 0.05f, 0.5f, 0.0015f);
                    AddResonator(buffer, ref noise, 0.002f, 1480f, 0.1f, 0.35f, 0.001f);
                    AddSweptNoise(buffer, ref noise, 0.004f, 0.12f, 0.35f, 0.01f, 22f, 2600f, 700f, 400f);
                    break;
                case Sound.Draw:
                    // Spannen: viele kleine Knackser, wie eine Sehne, die sich ueber Holz strafft.
                    for (var i = 0; i < 16; i++)
                    {
                        var at = 0.02f + i * 0.031f;
                        AddResonator(buffer, ref noise, at, 300f + i * 42f, 0.022f, 0.3f + i * 0.03f, 0.0015f);
                    }
                    AddSweptNoise(buffer, ref noise, 0f, 0.55f, 0.3f, 0.2f, 1.6f, 300f, 900f, 120f);
                    break;
                case Sound.Release:
                    Strike(buffer, ref noise, 0f, 0.001f, 9000f, 1f);
                    AddResonator(buffer, ref noise, 0f, 340f, 0.06f, 0.7f, 0.001f);
                    AddResonator(buffer, ref noise, 0f, 1180f, 0.09f, 0.45f, 0.001f);
                    AddSweptNoise(buffer, ref noise, 0.003f, 0.14f, 0.4f, 0.008f, 20f, 3400f, 900f, 500f);
                    break;
                case Sound.Cast:
                    // Magie darf synthetisch sein - das ist kein Gegenstand, der angeschlagen wird.
                    // Zwei leicht verstimmte Stimmen, deren Schwebung das Schweben macht.
                    AddTone(buffer, 0f, 0.4f, 300f, 780f, Wave.Sine, 0.8f, 0.04f, 5f);
                    AddTone(buffer, 0f, 0.4f, 303f, 792f, Wave.Sine, 0.6f, 0.04f, 5f);
                    AddSweptNoise(buffer, ref noise, 0f, 0.35f, 0.2f, 0.06f, 7f, 900f, 4000f, 600f);
                    break;

                // ── Bewegung ────────────────────────────────────────────
                case Sound.Dash:
                    AddSweptNoise(buffer, ref noise, 0f, 0.34f, 1f, 0.06f, 7f, 900f, 4600f, 300f);
                    AddResonator(buffer, ref noise, 0f, 120f, 0.12f, 0.3f, 0.004f);
                    break;
                case Sound.Footstep:
                    // Stiefel auf Stein: ein kurzer Schabgeraeusch-Anteil, darunter der dumpfe Koerper.
                    Strike(buffer, ref noise, 0f, 0.003f, 2600f, 0.5f);
                    AddResonator(buffer, ref noise, 0f, 86f, 0.09f, 1f, 0.004f);
                    AddResonator(buffer, ref noise, 0f, 148f, 0.06f, 0.55f, 0.003f);
                    AddSweptNoise(buffer, ref noise, 0.001f, 0.05f, 0.3f, 0.002f, 40f, 1800f, 500f, 260f);
                    break;

                // ── Treffer ─────────────────────────────────────────────
                // Waffe auf Knochen: harter Anschlag, drei Moden, kurzer Nachklang.
                case Sound.HitLight:
                    Strike(buffer, ref noise, 0f, 0.002f, 6000f, 1f);
                    AddResonator(buffer, ref noise, 0f, 318f, 0.09f, 0.9f, 0.002f);
                    AddResonator(buffer, ref noise, 0f, 547f, 0.07f, 0.6f, 0.002f);
                    AddResonator(buffer, ref noise, 0f, 892f, 0.05f, 0.35f, 0.002f);
                    AddResonator(buffer, ref noise, 0f, 132f, 0.11f, 0.55f, 0.003f);
                    break;
                case Sound.HitHeavy:
                    Strike(buffer, ref noise, 0f, 0.004f, 4200f, 1f);
                    AddResonator(buffer, ref noise, 0f, 62f, 0.4f, 1f, 0.005f);
                    AddResonator(buffer, ref noise, 0f, 118f, 0.3f, 0.8f, 0.004f);
                    AddResonator(buffer, ref noise, 0f, 231f, 0.16f, 0.5f, 0.003f);
                    AddResonator(buffer, ref noise, 0f, 402f, 0.1f, 0.3f, 0.002f);
                    break;
                case Sound.HitCritical:
                    Strike(buffer, ref noise, 0f, 0.002f, 9000f, 1f);
                    AddResonator(buffer, ref noise, 0f, 96f, 0.3f, 0.9f, 0.004f);
                    AddResonator(buffer, ref noise, 0f, 276f, 0.14f, 0.7f, 0.002f);
                    // Zwei helle Moden mehr als beim normalen Treffer: so hebt sich der Krit ab,
                    // ohne einfach lauter zu sein.
                    AddResonator(buffer, ref noise, 0.004f, 1560f, 0.3f, 0.5f, 0.0015f);
                    AddResonator(buffer, ref noise, 0.004f, 2330f, 0.26f, 0.3f, 0.0015f);
                    break;
                case Sound.Explosion:
                    // Detonation: Knall, dann ein Rauschen, dessen Filter zufaellt - so entsteht das
                    // Rollen, statt einfach leiser zu werden.
                    Strike(buffer, ref noise, 0f, 0.004f, 9000f, 1f);
                    AddSweptNoise(buffer, ref noise, 0f, 0.75f, 1f, 0.006f, 4.2f, 4200f, 160f, 40f);
                    AddResonator(buffer, ref noise, 0f, 44f, 0.55f, 1f, 0.008f);
                    AddResonator(buffer, ref noise, 0f, 79f, 0.4f, 0.6f, 0.006f);
                    break;
                case Sound.Shockwave:
                    Strike(buffer, ref noise, 0f, 0.003f, 5000f, 0.8f);
                    AddSweptNoise(buffer, ref noise, 0f, 0.4f, 0.7f, 0.004f, 8f, 2400f, 200f, 60f);
                    AddResonator(buffer, ref noise, 0f, 54f, 0.42f, 1f, 0.006f);
                    AddResonator(buffer, ref noise, 0f, 101f, 0.26f, 0.55f, 0.004f);
                    break;
                case Sound.PlayerHurt:
                    // Eigener Schaden: dumpfer Einschlag plus ein kurzer, tiefer Laut.
                    Strike(buffer, ref noise, 0f, 0.005f, 2600f, 0.8f);
                    AddResonator(buffer, ref noise, 0f, 74f, 0.3f, 1f, 0.006f);
                    AddTone(buffer, 0.01f, 0.32f, 190f, 96f, Wave.Sine, 0.45f, 0.02f, 7f);
                    AddSweptNoise(buffer, ref noise, 0f, 0.3f, 0.4f, 0.01f, 8f, 1400f, 300f, 120f);
                    break;

                // ── Schildtraeger ───────────────────────────────────────
                // Stahl klingt unharmonisch und lange. Vier Moden ohne ganzzahliges Verhaeltnis,
                // angeregt von einem Stoss von zwei Millisekunden - das ist ein echtes "Tink".
                case Sound.Block:
                    Strike(buffer, ref noise, 0f, 0.0015f, 11000f, 0.7f);
                    AddResonator(buffer, ref noise, 0f, 1187f, 0.42f, 1f, 0.0015f);
                    AddResonator(buffer, ref noise, 0f, 1793f, 0.36f, 0.7f, 0.0015f);
                    AddResonator(buffer, ref noise, 0f, 2671f, 0.3f, 0.45f, 0.0015f);
                    AddResonator(buffer, ref noise, 0f, 3907f, 0.22f, 0.25f, 0.0015f);
                    AddResonator(buffer, ref noise, 0f, 214f, 0.1f, 0.35f, 0.002f);
                    break;
                case Sound.GuardBreak:
                    Strike(buffer, ref noise, 0f, 0.003f, 9000f, 1f);
                    AddResonator(buffer, ref noise, 0f, 611f, 0.75f, 1f, 0.003f);
                    AddResonator(buffer, ref noise, 0f, 947f, 0.65f, 0.8f, 0.003f);
                    AddResonator(buffer, ref noise, 0f, 1523f, 0.55f, 0.6f, 0.002f);
                    AddResonator(buffer, ref noise, 0f, 2411f, 0.45f, 0.35f, 0.002f);
                    AddResonator(buffer, ref noise, 0f, 58f, 0.35f, 0.7f, 0.006f);
                    // Zweiter Anschlag kurz danach: der Schild gibt in zwei Stufen nach.
                    Strike(buffer, ref noise, 0.06f, 0.002f, 6000f, 0.6f);
                    AddResonator(buffer, ref noise, 0.06f, 1088f, 0.4f, 0.5f, 0.002f);
                    break;

                // ── Ansagen ─────────────────────────────────────────────
                case Sound.Telegraph:
                    // Statt Piepton: zwei angeschlagene Glocken. Traegt genauso weit, klingt aber
                    // nach Gegenstand und nicht nach Menuefehler.
                    AddBell(buffer, ref noise, 0f, 784f, 0.22f, 0.9f);
                    AddBell(buffer, ref noise, 0.2f, 784f, 0.3f, 1f);
                    break;
                case Sound.EnemyArrival:
                    AddSweptNoise(buffer, ref noise, 0f, 0.55f, 0.8f, 0.1f, 4f, 240f, 900f, 30f);
                    AddResonator(buffer, ref noise, 0f, 41f, 0.5f, 1f, 0.05f);
                    AddResonator(buffer, ref noise, 0.02f, 63f, 0.42f, 0.7f, 0.04f);
                    // Knochenklappern beim Auftauchen.
                    for (var i = 0; i < 7; i++)
                        AddResonator(buffer, ref noise, 0.12f + i * 0.045f, 420f + i * 130f, 0.05f, 0.22f, 0.0015f);
                    break;
                case Sound.HeavyReady:
                    AddBell(buffer, ref noise, 0f, 523f, 0.55f, 0.8f);
                    AddBell(buffer, ref noise, 0.07f, 784f, 0.5f, 0.7f);
                    break;
                case Sound.UltimateRise:
                    // Aufstieg mit Einschlag: erst Luft und Spannung, dann trifft es.
                    AddTone(buffer, 0f, 0.85f, 60f, 520f, Wave.Sine, 0.4f, 0.3f, 0.8f);
                    AddSweptNoise(buffer, ref noise, 0f, 0.85f, 0.45f, 0.55f, 1f, 400f, 5200f, 200f);
                    Strike(buffer, ref noise, 0.85f, 0.005f, 9000f, 1f);
                    AddResonator(buffer, ref noise, 0.85f, 48f, 0.45f, 1f, 0.006f);
                    AddResonator(buffer, ref noise, 0.85f, 92f, 0.35f, 0.6f, 0.005f);
                    AddSweptNoise(buffer, ref noise, 0.85f, 0.42f, 0.6f, 0.004f, 7f, 3600f, 200f, 60f);
                    break;
                case Sound.CoreActivated:
                    // Glockenpartiale statt Arpeggio: derselbe Aufstieg, aber wie angeschlagenes Metall.
                    AddBell(buffer, ref noise, 0f, 440f, 0.5f, 0.7f);
                    AddBell(buffer, ref noise, 0.16f, 587f, 0.55f, 0.8f);
                    AddBell(buffer, ref noise, 0.32f, 880f, 0.9f, 1f);
                    AddResonator(buffer, ref noise, 0.32f, 110f, 0.7f, 0.5f, 0.006f);
                    break;
                case Sound.FloorCleared:
                    AddBell(buffer, ref noise, 0f, 523f, 0.6f, 0.7f);
                    AddBell(buffer, ref noise, 0.16f, 659f, 0.7f, 0.8f);
                    AddBell(buffer, ref noise, 0.32f, 784f, 1.3f, 1f);
                    AddBell(buffer, ref noise, 0.34f, 1046f, 1.2f, 0.6f);
                    AddResonator(buffer, ref noise, 0.32f, 131f, 1f, 0.55f, 0.008f);
                    break;
                case Sound.Death:
                    // Skelett faellt: ein tiefer Aufprall, dann Knochen, die ueber Stein klappern.
                    Strike(buffer, ref noise, 0f, 0.004f, 3600f, 0.9f);
                    AddResonator(buffer, ref noise, 0f, 68f, 0.3f, 0.9f, 0.005f);
                    for (var i = 0; i < 13; i++)
                    {
                        var at = 0.05f + i * 0.055f + noise.Next() * 0.014f;
                        var hz = 380f + Mathf.Abs(noise.Next()) * 900f;
                        AddResonator(buffer, ref noise, at, hz, 0.05f, 0.34f - i * 0.02f, 0.0015f);
                        AddResonator(buffer, ref noise, at, hz * 1.72f, 0.035f, 0.2f - i * 0.012f, 0.0015f);
                    }
                    break;

                // ── Menue ───────────────────────────────────────────────
                case Sound.UiClick:
                    Strike(buffer, ref noise, 0f, 0.001f, 9000f, 0.6f);
                    AddResonator(buffer, ref noise, 0f, 2180f, 0.05f, 1f, 0.001f);
                    AddResonator(buffer, ref noise, 0f, 3410f, 0.035f, 0.4f, 0.001f);
                    break;
                case Sound.UiConfirm:
                    AddBell(buffer, ref noise, 0f, 660f, 0.2f, 0.7f);
                    AddBell(buffer, ref noise, 0.11f, 990f, 0.32f, 1f);
                    break;

                // ── Hintergrund ─────────────────────────────────────────
                case Sound.Ambience:
                    // Ein Turm unter Spannung: tiefe Grundstimme, verstimmte Partiale, dazu ein Zug
                    // von Luft, der die Flaeche atmen laesst.
                    AddDrone(buffer, 55f, 0.9f);
                    AddDrone(buffer, 82.6f, 0.45f);
                    AddDrone(buffer, 110.3f, 0.3f);
                    AddDrone(buffer, 164.5f, 0.12f);
                    AddSweptNoise(buffer, ref noise, 0f, LengthOf(Sound.Ambience), 0.16f, 1.5f, 0.04f,
                        200f, 420f, 30f);
                    break;
            }
        }

        // ── Bausteine ───────────────────────────────────────────────────

        /// <summary>
        /// Angeregter Resonator: ein Zweipol-Filter mit hoher Guete, angestossen von einem kurzen
        /// Rauschstoss. Das ist der Kern der modalen Synthese - <paramref name="decay"/> ist die Zeit
        /// bis zur Unhoerbarkeit, <paramref name="burst"/> die Dauer des Anstosses in Sekunden.
        ///
        /// Rekursion: y[n] = 2·r·cos(w)·y[n-1] − r²·y[n-2] + x[n], mit r aus der Abklingzeit.
        /// </summary>
        private static void AddResonator(float[] buffer, ref Noise noise, float start, float hz,
            float decay, float gain, float burst)
        {
            if (hz <= 0f || hz >= SampleRate * 0.45f) return;
            var first = Mathf.Max(0, Mathf.RoundToInt(start * SampleRate));
            var omega = 2f * Mathf.PI * hz / SampleRate;
            // Radius so, dass die Amplitude nach decay Sekunden auf etwa 1/1000 gefallen ist.
            var radius = Mathf.Exp(-6.9f / Mathf.Max(0.001f, decay) / SampleRate);
            var a1 = 2f * radius * Mathf.Cos(omega);
            var a2 = -radius * radius;
            var burstSamples = Mathf.Max(1, Mathf.RoundToInt(burst * SampleRate));
            var total = Mathf.Min(buffer.Length - first, Mathf.RoundToInt(decay * 1.1f * SampleRate) + burstSamples);
            if (total <= 0) return;
            var previous = 0f;
            var older = 0f;
            // Grundverstaerkung so, dass hohe Guete nicht automatisch laut wird.
            var scale = gain * (1f - radius) * 12f;
            for (var i = 0; i < total; i++)
            {
                var excitation = i < burstSamples ? noise.Next() : 0f;
                var value = a1 * previous + a2 * older + excitation;
                older = previous;
                previous = value;
                buffer[first + i] += value * scale;
            }
        }

        /// <summary>
        /// Glocke: eine Grundmode plus die typischen unharmonischen Partiale eines Roehrenglockenspiels
        /// (Verhaeltnisse um 2,76 / 5,40 / 8,93). Klingt nach angeschlagenem Metall statt nach Piepton.
        /// </summary>
        private static void AddBell(float[] buffer, ref Noise noise, float start, float hz,
            float decay, float gain)
        {
            AddResonator(buffer, ref noise, start, hz, decay, gain, 0.0015f);
            AddResonator(buffer, ref noise, start, hz * 2.76f, decay * 0.72f, gain * 0.5f, 0.0012f);
            AddResonator(buffer, ref noise, start, hz * 5.4f, decay * 0.45f, gain * 0.26f, 0.001f);
            AddResonator(buffer, ref noise, start, hz * 8.93f, decay * 0.3f, gain * 0.12f, 0.001f);
        }

        /// <summary>Der Anschlag selbst: sehr kurzer, sehr heller Stoss. Ohne ihn klingt jeder Treffer weich.</summary>
        private static void Strike(float[] buffer, ref Noise noise, float start, float duration,
            float brightnessHz, float gain)
            => AddSweptNoise(buffer, ref noise, start, Mathf.Max(0.001f, duration * 6f), gain, 0.0002f,
                1f / Mathf.Max(0.0005f, duration), brightnessHz, brightnessHz * 0.35f, 600f);

        /// <summary>
        /// Rauschen, dessen Durchlassbereich waehrend des Klangs wandert. Der wandernde Filter ist der
        /// Unterschied zwischen "Zischen" und "bewegter Luft" beziehungsweise "rollendem Donner".
        /// Tiefpass vierpolig, Hochpass zweipolig - einpolig laesst oberhalb der Grenze mehr Energie
        /// stehen als darunter liegt, weil weisses Rauschen gleich viel Energie je Hertz hat.
        /// </summary>
        private static void AddSweptNoise(float[] buffer, ref Noise noise, float start, float duration,
            float gain, float attack, float decay, float lowpassFromHz, float lowpassToHz, float highpassHz)
        {
            var first = Mathf.Max(0, Mathf.RoundToInt(start * SampleRate));
            var count = Mathf.RoundToInt(duration * SampleRate);
            if (count <= 0) return;
            var highpass = Coefficient(highpassHz);
            var a = 0f;
            var b = 0f;
            var c = 0f;
            var d = 0f;
            var e = 0f;
            var f = 0f;
            const float makeUp = 3.6f;
            for (var i = 0; i < count; i++)
            {
                var index = first + i;
                if (index >= buffer.Length) break;
                var t = i / (float)count;
                // Logarithmisch wandern: so liest das Ohr die Bewegung als gleichmaessig.
                var cutoff = Mathf.Exp(Mathf.Lerp(Mathf.Log(Mathf.Max(20f, lowpassFromHz)),
                    Mathf.Log(Mathf.Max(20f, lowpassToHz)), t));
                var lowpass = Coefficient(cutoff);
                a += (noise.Next() - a) * lowpass;
                b += (a - b) * lowpass;
                c += (b - c) * lowpass;
                d += (c - d) * lowpass;
                e += (d - e) * highpass;
                f += (e - f) * highpass;
                buffer[index] += (d - f) * gain * makeUp * Envelope(i / (float)SampleRate, attack, decay);
            }
        }

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

        /// <summary>Dauerstimme fuer die Hintergrundflaeche, ueber die ganze Laenge und ohne Abfall.</summary>
        private static void AddDrone(float[] buffer, float hz, float gain)
        {
            var phase = 0f;
            for (var i = 0; i < buffer.Length; i++)
            {
                phase += hz / SampleRate;
                if (phase > 1f) phase -= Mathf.Floor(phase);
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

        private static float Coefficient(float hz)
            => Mathf.Clamp01(1f - Mathf.Exp(-2f * Mathf.PI * hz / SampleRate));

        // ── Nachbearbeitung ─────────────────────────────────────────────

        /// <summary>
        /// Raum aus vier gegeneinander verstimmten Verzoegerungen mit Rueckfuehrung und Daempfung.
        /// Kein Hall-Prozessor, aber genau das, was einen Klang aus dem Kopfhoerer in einen Steinraum
        /// setzt - trockene Einschlaege klingen immer nach Labor.
        /// </summary>
        private static void ApplyRoom(float[] buffer, float mix)
        {
            int[] delays = { 1237, 1619, 2029, 2503 };
            float[] feedback = { 0.62f, 0.58f, 0.54f, 0.5f };
            var wet = new float[buffer.Length];
            for (var line = 0; line < delays.Length; line++)
            {
                var delay = delays[line];
                var gain = feedback[line];
                var damped = 0f;
                var lowpass = Coefficient(2600f);
                for (var i = delay; i < buffer.Length; i++)
                {
                    var fed = buffer[i - delay] + wet[i - delay] * gain;
                    damped += (fed - damped) * lowpass;
                    wet[i] += damped * 0.25f;
                }
            }
            for (var i = 0; i < buffer.Length; i++) buffer[i] += wet[i] * mix;
        }

        private static void Normalize(float[] buffer, float peak)
        {
            var loudest = 0f;
            for (var i = 0; i < buffer.Length; i++) loudest = Mathf.Max(loudest, Mathf.Abs(buffer[i]));
            if (loudest < 0.0001f) return;
            var scale = peak / loudest;
            for (var i = 0; i < buffer.Length; i++) buffer[i] *= scale;
        }

        private static void FadeTail(float[] buffer, float seconds)
        {
            var count = Mathf.Min(buffer.Length, Mathf.RoundToInt(seconds * SampleRate));
            for (var i = 0; i < count; i++)
                buffer[buffer.Length - 1 - i] *= i / (float)count;
        }

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
