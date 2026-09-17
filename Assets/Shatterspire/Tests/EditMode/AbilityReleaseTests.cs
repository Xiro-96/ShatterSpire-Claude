using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Shatterspire.Tests
{
    /// <summary>
    /// Eine Faehigkeit geht dorthin, wo sie beim Loslassen hin sollte - nicht in Laufrichtung.
    ///
    /// Der Anlass: "manchmal schiesst er die Faehigkeit nicht nach vorne, sondern nach hinten".
    /// Faehigkeiten loesen beim Loslassen aus, und genau in diesem Bild war der Knopf nicht mehr
    /// gedrueckt: die Eingabe hielt den Spieler fuer "greift nicht an", schaltete das Selbstzielen
    /// ab und nahm die Laufrichtung. Wer beim Loslassen zurueckwich, schoss nach hinten. Eine
    /// gezogene Richtung wurde zudem schon beim Loslassen geloescht, bevor sie jemand las.
    ///
    /// Die Tests treiben den echten PlayerInputRouter Bild fuer Bild, mit einem Spieler, der nach
    /// rechts laeuft und die Faehigkeit loslaesst.
    /// </summary>
    public sealed class AbilityReleaseTests
    {
        private static readonly MethodInfo Tick =
            typeof(PlayerInputRouter).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);

        private GameObject owner;
        private PlayerInputRouter router;

        [SetUp]
        public void Build()
        {
            MobileInput.Reset();
            owner = new GameObject("Player");
            router = owner.AddComponent<PlayerInputRouter>();
        }

        [TearDown]
        public void Clear()
        {
            MobileInput.Reset();
            if (owner) Object.DestroyImmediate(owner);
        }

        private void Frame() => Tick.Invoke(router, null);

        private Vector3 AimDirection()
        {
            var aim = router.AimPoint - owner.transform.position;
            aim.y = 0f;
            return aim.normalized;
        }

        [Test]
        public void EinKurzerTippZieltSelbstAuchBeimZurueckweichen()
        {
            MobileInput.Move = Vector2.right;
            MobileInput.SetSkill(true);
            Frame();
            Assert.That(router.SkillHeld, Is.True);
            Assert.That(router.AutoAim, Is.True, "Waehrend des Haltens zielt die Faehigkeit selbst.");

            MobileInput.SetSkill(false);
            Frame();
            Assert.That(router.SkillReleased, Is.True);
            Assert.That(router.AutoAim, Is.True,
                "Im Bild des Loslassens muss das Selbstzielen noch gelten - sonst geht die Faehigkeit in Laufrichtung.");
        }

        [Test]
        public void EineGezogeneRichtungUeberlebtDasLoslassen()
        {
            MobileInput.Move = Vector2.right;
            MobileInput.SetSkill(true);
            MobileInput.ActionAim = Vector2.up;
            Frame();
            Assert.That(Vector3.Angle(AimDirection(), Vector3.forward), Is.LessThan(1f), "Beim Ziehen zeigt das Ziel nach oben.");

            MobileInput.ReleaseActionAim();
            MobileInput.SetSkill(false);
            Frame();
            Assert.That(router.SkillReleased, Is.True);
            Assert.That(router.ManualAim, Is.True);
            Assert.That(Vector3.Angle(AimDirection(), Vector3.forward), Is.LessThan(1f),
                "Beim Loslassen muss die gezogene Richtung gelten, nicht die Laufrichtung.");
        }

        [Test]
        public void DasSelbeGiltFuerDieUltimate()
        {
            MobileInput.Move = Vector2.left;
            MobileInput.SetUltimate(true);
            MobileInput.ActionAim = Vector2.down;
            Frame();
            MobileInput.ReleaseActionAim();
            MobileInput.SetUltimate(false);
            Frame();
            Assert.That(router.UltimateReleased, Is.True);
            Assert.That(Vector3.Angle(AimDirection(), Vector3.back), Is.LessThan(1f));
        }

        /// <summary>Ohne Faehigkeit bleibt alles wie gehabt: wer nur laeuft, schaut in Laufrichtung.</summary>
        [Test]
        public void WerNurLaeuftSchautInLaufrichtung()
        {
            MobileInput.Move = Vector2.right;
            Frame();
            Assert.That(router.AutoAim, Is.False);
            Assert.That(Vector3.Angle(AimDirection(), Vector3.right), Is.LessThan(1f));
        }
    }
}
