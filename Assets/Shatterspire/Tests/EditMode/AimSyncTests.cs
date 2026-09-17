using NUnit.Framework;
using UnityEngine;

namespace Shatterspire.Tests
{
    /// <summary>
    /// Linie und Schuss muessen in dieselbe Richtung zeigen.
    ///
    /// Der Anlass: "die Linie und der Schuss sind nicht synchron". Die Zielanzeige haengt an der
    /// Figur, und ihre Drehung stand als lokaler Winkel da - sie wurde also um die Blickrichtung der
    /// Figur mitgedreht. Solange die Figur nach Norden sah, stimmte es; in jede andere Richtung zeigte
    /// die Linie daneben, bei Ost-auf-Ost sogar nach Sueden. Diese Tests drehen die Figur in viele
    /// Richtungen und pruefen die Linie in der Welt.
    /// </summary>
    public sealed class AimSyncTests
    {
        private GameObject owner;

        [SetUp]
        public void Build() => owner = new GameObject("Aim Owner");

        [TearDown]
        public void Clear()
        {
            if (owner) Object.DestroyImmediate(owner);
        }

        private static Vector3 Flat(Vector3 value) => new(value.x, 0f, value.z);

        [Test]
        public void DieLinieZeigtInSchussrichtungEgalWohinDieFigurSchaut()
        {
            var indicator = AimIndicator.Attach(owner.transform, Color.white);
            var line = new AimDescription(AimShape.Line, 10f, 0.4f);
            foreach (var facing in new[] { 0f, 37f, 90f, 180f, 270f })
            foreach (var aimYaw in new[] { 0f, 90f, 200f, 315f })
            {
                owner.transform.rotation = Quaternion.Euler(0f, facing, 0f);
                var direction = Quaternion.Euler(0f, aimYaw, 0f) * Vector3.forward;
                indicator.Show(line, direction, Vector3.zero);

                var band = indicator.transform.Find("Aim Band");
                var head = indicator.transform.Find("Aim Head");
                var label = $"Figur {facing} Grad, Ziel {aimYaw} Grad";
                // Die Laengsachse des Bandes ist das lokale Oben des flach gelegten Quads.
                Assert.That(Vector3.Angle(Flat(band.up), direction), Is.LessThan(0.5f), label + ": Band verdreht.");
                var centre = Flat(band.position - owner.transform.position);
                Assert.That(Vector3.Angle(centre, direction), Is.LessThan(0.5f), label + ": Band liegt daneben.");
                Assert.That(centre.magnitude, Is.EqualTo(5f).Within(0.01f), label + ": Band nicht auf halber Strecke.");
                var tip = Flat(head.position - owner.transform.position);
                Assert.That(Vector3.Angle(tip, direction), Is.LessThan(0.5f), label + ": Spitze liegt daneben.");
                Assert.That(tip.magnitude, Is.EqualTo(10f).Within(0.01f), label + ": Spitze nicht am Ende der Reichweite.");
            }
        }

        /// <summary>Eine vergroesserte Figur darf ihre Reichweite nicht mitvergroessern.</summary>
        [Test]
        public void DieReichweiteBleibtBeiSkalierterFigur()
        {
            owner.transform.localScale = Vector3.one * 2f;
            var indicator = AimIndicator.Attach(owner.transform, Color.white);
            indicator.Show(new AimDescription(AimShape.Line, 10f, 0.4f), Vector3.right, Vector3.zero);
            var band = indicator.transform.Find("Aim Band");
            Assert.That(band.lossyScale.y, Is.EqualTo(10f).Within(0.01f));
            var tip = Flat(indicator.transform.Find("Aim Head").position - owner.transform.position);
            Assert.That(tip.magnitude, Is.EqualTo(10f).Within(0.01f));
        }

        /// <summary>
        /// Beim Selbstzielen zaehlt die Blickrichtung kaum. Wer rueckwaerts vor einem Gegner flieht,
        /// soll ihn treffen - nicht einen weiter entfernten, der zufaellig in Laufrichtung steht.
        /// Mit dem alten Gewicht von 0,055 je Grad kostete der Gegner im Ruecken 9,9 Einheiten
        /// Aufschlag und verlor gegen jeden vor einem, der naeher als 14 Einheiten stand.
        /// </summary>
        [Test]
        public void BeimSelbstzielenGewinntDerVerfolgerImRuecken()
        {
            var behind = new GameObject("Behind").AddComponent<Health>();
            var ahead = new GameObject("Ahead").AddComponent<Health>();
            try
            {
                behind.transform.position = new Vector3(0f, 0f, -4f);
                ahead.transform.position = new Vector3(0f, 0f, 7f);
                const float Auto = 0.012f;
                var fleeing = Vector3.forward;
                var scoreBehind = Targeting.AutoAimScore(Vector3.zero, fleeing, behind, Auto);
                var scoreAhead = Targeting.AutoAimScore(Vector3.zero, fleeing, ahead, Auto);
                Assert.That(scoreBehind, Is.LessThan(scoreAhead), "Der Verfolger muss das bessere Ziel sein.");

                var oldBehind = Targeting.AutoAimScore(Vector3.zero, fleeing, behind);
                var oldAhead = Targeting.AutoAimScore(Vector3.zero, fleeing, ahead);
                Assert.That(oldBehind, Is.GreaterThan(oldAhead),
                    "Mit dem Gewicht der Zielhilfe verliert er - genau deshalb gibt es zwei Gewichte.");
            }
            finally
            {
                Object.DestroyImmediate(behind.gameObject);
                Object.DestroyImmediate(ahead.gameObject);
            }
        }
    }
}
