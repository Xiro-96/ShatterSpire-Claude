using System;
using NUnit.Framework;
using UnityEngine;

namespace Shatterspire.Tests
{
    /// <summary>
    /// Klaenge entstehen hier aus Rechnung, und eine Rechnung kann man nicht anhoeren. Diese Tests
    /// sichern ab, was sich pruefen laesst: jeder Klang existiert, uebersteuert nicht, ist nicht
    /// still, und Einschlaege haben ihren lautesten Moment vorn - genau das unterscheidet einen
    /// Treffer von einem Summen.
    /// </summary>
    public sealed class SoundTests
    {
        private static Sound[] All => (Sound[])Enum.GetValues(typeof(Sound));

        private static float[] SamplesOf(Sound sound)
        {
            var clip = ProceduralSound.For(sound);
            Assert.That(clip, Is.Not.Null, $"{sound}: kein Clip.");
            var samples = new float[clip.samples];
            clip.GetData(samples, 0);
            return samples;
        }

        [Test]
        public void JederKlangHatEinenClipMitEinemKanal()
        {
            foreach (var sound in All)
            {
                var clip = ProceduralSound.For(sound);
                Assert.That(clip, Is.Not.Null, $"{sound}: kein Clip.");
                Assert.That(clip.channels, Is.EqualTo(1), $"{sound}: nicht mono.");
                Assert.That(clip.frequency, Is.EqualTo(ProceduralSound.SampleRate), $"{sound}: falsche Abtastrate.");
                Assert.That(clip.samples, Is.GreaterThan(1000), $"{sound}: zu kurz fuer einen hoerbaren Klang.");
            }
        }

        [Test]
        public void KeinKlangUebersteuertOderEnthaeltUngueltigeWerte()
        {
            foreach (var sound in All)
            foreach (var sample in SamplesOf(sound))
            {
                Assert.That(float.IsNaN(sample) || float.IsInfinity(sample), Is.False,
                    $"{sound}: ungueltiger Wert im Puffer.");
                Assert.That(Mathf.Abs(sample), Is.LessThanOrEqualTo(1f), $"{sound}: Wert ausserhalb von -1 bis 1.");
            }
        }

        [Test]
        public void JederKlangErreichtSeinenZielpegel()
        {
            foreach (var sound in All)
            {
                var peak = 0f;
                foreach (var sample in SamplesOf(sound)) peak = Mathf.Max(peak, Mathf.Abs(sample));
                Assert.That(peak, Is.EqualTo(ProceduralSound.PeakFor(sound)).Within(0.02f),
                    $"{sound}: Pegel {peak:0.000} statt {ProceduralSound.PeakFor(sound):0.000}. " +
                    "Entweder fehlt die Normierung oder der Klang ist leer.");
            }
        }

        [Test]
        public void KeinKlangIstStill()
        {
            foreach (var sound in All)
            {
                var samples = SamplesOf(sound);
                var energy = 0.0;
                foreach (var sample in samples) energy += sample * (double)sample;
                var rms = Mathf.Sqrt((float)(energy / samples.Length));
                Assert.That(rms, Is.GreaterThan(0.004f), $"{sound}: praktisch still (RMS {rms:0.00000}).");
            }
        }

        /// <summary>
        /// Ein Einschlag beginnt laut und faellt ab. Waere die Huellkurve verdreht, klaenge jeder
        /// Treffer wie ein Anschwellen - im Spiel fiele das erst spaet auf und waere schwer zu
        /// benennen. Hier ist es eine Zahl.
        /// </summary>
        [Test]
        public void EinschlaegeSindVornAmLautesten()
        {
            var percussive = new[]
            {
                Sound.HitLight, Sound.HitHeavy, Sound.HitCritical, Sound.Shockwave, Sound.Explosion,
                Sound.Footstep, Sound.UiClick, Sound.Block, Sound.GuardBreak, Sound.Shot, Sound.Release,
                Sound.Stab, Sound.Smash
            };
            foreach (var sound in percussive)
            {
                var samples = SamplesOf(sound);
                var peak = 0f;
                var peakIndex = 0;
                for (var i = 0; i < samples.Length; i++)
                {
                    var magnitude = Mathf.Abs(samples[i]);
                    if (magnitude <= peak) continue;
                    peak = magnitude;
                    peakIndex = i;
                }
                var position = peakIndex / (float)samples.Length;
                Assert.That(position, Is.LessThan(0.35f),
                    $"{sound}: lautester Moment erst bei {position:0.00} der Laenge - das schwillt an statt zuzuschlagen.");
            }
        }

        [Test]
        public void NurDieHintergrundflaecheLaeuftInSchleife()
        {
            foreach (var sound in All)
                Assert.That(ProceduralSound.Loops(sound), Is.EqualTo(sound == Sound.Ambience),
                    $"{sound}: falsche Schleifen-Einstellung.");
            Assert.That(ProceduralSound.For(Sound.Ambience).samples,
                Is.GreaterThan(ProceduralSound.SampleRate * 3),
                "Die Hintergrundflaeche ist zu kurz, die Schleife wuerde auffallen.");
        }

        [Test]
        public void TonhoehenStreuungGiltFuerWiederkehrendeKlaengeAberNichtFuerMelodien()
        {
            // Ein Treffer kommt hundertmal pro Kampf und braucht Streuung, damit er nicht nervt.
            Assert.That(ProceduralSound.PitchSpreadFor(Sound.HitLight), Is.GreaterThan(0.05f));
            Assert.That(ProceduralSound.PitchSpreadFor(Sound.Footstep), Is.GreaterThan(0.05f));
            // Eine Melodie verstimmt sich dagegen hoerbar.
            Assert.That(ProceduralSound.PitchSpreadFor(Sound.FloorCleared), Is.LessThan(0.02f));
            Assert.That(ProceduralSound.PitchSpreadFor(Sound.Ambience), Is.Zero);
        }
    }
}
