using NUnit.Framework;
using UnityEngine;

namespace Shatterspire.Tests
{
    /// <summary>
    /// Selbstzielen muss vorhalten, sonst trifft es nichts, was laeuft.
    ///
    /// Ein Pfeil fliegt 22 Einheiten je Sekunde. Auf zehn Einheiten sind das knapp eine halbe
    /// Sekunde, in der ein Gegner mit 5 Einheiten je Sekunde zwei Einheiten weiter ist - bei einem
    /// Pfeil von 0,4 Einheiten Breite also ein klarer Fehlschuss. Genau daran waere ein
    /// Selbstzielen gescheitert, das nur auf die heutige Position zeigt.
    /// </summary>
    public sealed class InterceptTests
    {
        private const float ArrowSpeed = 22f;

        /// <summary>
        /// Die Probe aufs Exempel: der Pfeil muss zur selben Zeit am Treffpunkt sein wie das Ziel.
        /// </summary>
        private static void AssertMeets(Vector3 from, Vector3 target, Vector3 velocity, float tolerance)
        {
            var point = Targeting.PredictIntercept(from, target, velocity, ArrowSpeed);
            var flight = Vector3.Distance(from, point) / ArrowSpeed;
            var targetSpeed = velocity.magnitude;
            var chase = targetSpeed > 0.01f ? Vector3.Distance(target, point) / targetSpeed : flight;
            Assert.That(chase, Is.EqualTo(flight).Within(tolerance),
                $"Pfeil braucht {flight:0.000}s, das Ziel {chase:0.000}s bis {point}.");
        }

        [Test]
        public void EinQuerlaufendesZielWirdVorgehalten()
        {
            AssertMeets(Vector3.zero, new Vector3(0f, 0f, 10f), new Vector3(5f, 0f, 0f), 0.02f);
            AssertMeets(Vector3.zero, new Vector3(0f, 0f, 4f), new Vector3(7f, 0f, 0f), 0.02f);
            AssertMeets(Vector3.zero, new Vector3(6f, 0f, 6f), new Vector3(-3f, 0f, 4f), 0.02f);
        }

        [Test]
        public void EinStehendesZielWirdNichtVerschoben()
        {
            var target = new Vector3(0f, 0f, 9f);
            Assert.That(Targeting.PredictIntercept(Vector3.zero, target, Vector3.zero, ArrowSpeed),
                Is.EqualTo(target));
        }

        /// <summary>Ein Ziel, das wegläuft, darf die Vorhaltung nicht ins Nirgendwo ziehen.</summary>
        [Test]
        public void DieVorhaltungBleibtBegrenzt()
        {
            var target = new Vector3(0f, 0f, 18f);
            var point = Targeting.PredictIntercept(Vector3.zero, target, new Vector3(0f, 0f, 20f), ArrowSpeed);
            Assert.That(Vector3.Distance(target, point), Is.LessThanOrEqualTo(20f * 0.85f + 0.001f),
                "Hoechstens 0,85 s werden vorgehalten.");
        }

        /// <summary>Ohne Geschosstempo gibt es keine Flugzeit und damit nichts vorzuhalten.</summary>
        [Test]
        public void OhneGeschosstempoBleibtDerPunktStehen()
        {
            var target = new Vector3(3f, 0f, 3f);
            Assert.That(Targeting.PredictIntercept(Vector3.zero, target, new Vector3(9f, 0f, 0f), 0f),
                Is.EqualTo(target));
        }
    }
}
