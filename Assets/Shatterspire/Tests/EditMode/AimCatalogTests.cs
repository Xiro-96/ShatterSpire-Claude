using System;
using NUnit.Framework;
using UnityEngine;

namespace Shatterspire.Tests
{
    /// <summary>
    /// Jede Aktion muss eine Zielanzeige haben.
    ///
    /// Der Anlass: "man sollte den Schuss separat steuern koennen, so wie in Brawl Stars". Dort
    /// sieht man beim Zielen, wohin es geht. Eine vergessene Klasse waere hier eine Aktion ohne
    /// Anzeige - und das faellt beim Spielen kaum auf, weil nichts fehlt, sondern nur nichts da ist.
    /// </summary>
    public sealed class AimCatalogTests
    {
        private static readonly ActionSlot[] Actions =
            { ActionSlot.Light, ActionSlot.Heavy, ActionSlot.Skill, ActionSlot.Ultimate };

        private static AimDescription Describe(HeroClassId hero, ActionSlot slot, int pierces = 0,
            bool wideReach = false)
            => AimCatalog.Describe(hero, slot, pierces, wideReach, 15f, 13f);

        [Test]
        public void JederHeldHatFuerJedeAktionEineForm()
        {
            foreach (HeroClassId hero in Enum.GetValues(typeof(HeroClassId)))
            foreach (var slot in Actions)
            {
                var aim = Describe(hero, slot);
                Assert.That(aim.Shape, Is.Not.EqualTo(AimShape.None), $"{hero} {slot} hat keine Form.");
            }
        }

        /// <summary>Eine Bahn ohne Reichweite oder ein Kreis ohne Radius zeichnet nichts.</summary>
        [Test]
        public void JedeFormHatAusdehnung()
        {
            foreach (HeroClassId hero in Enum.GetValues(typeof(HeroClassId)))
            foreach (var slot in Actions)
            {
                var aim = Describe(hero, slot);
                Assert.That(aim.Width, Is.GreaterThan(0.1f), $"{hero} {slot} ist unendlich schmal.");
                if (aim.Shape is AimShape.Line or AimShape.Wedge or AimShape.Circle)
                    Assert.That(aim.Range, Is.GreaterThan(0.5f), $"{hero} {slot} reicht nicht bis vor die Fuesse.");
            }
        }

        /// <summary>
        /// Was den Helden selbst betrifft, darf keine Richtung vorgeben - und was eine Richtung hat,
        /// darf nicht als Kreis um den Helden gezeichnet werden.
        /// </summary>
        [Test]
        public void SelbstwirkendeAktionenZeigenKeineBahn()
        {
            Assert.That(Describe(HeroClassId.Paladin, ActionSlot.Ultimate).Shape,
                Is.EqualTo(AimShape.Around), "Die Vergeltung wirkt auf XIRO selbst.");
            Assert.That(Describe(HeroClassId.Ranger, ActionSlot.Ultimate).Shape,
                Is.EqualTo(AimShape.Around), "Der Jaegerblick wirkt auf Rex selbst.");
            foreach (HeroClassId hero in Enum.GetValues(typeof(HeroClassId)))
                Assert.That(Describe(hero, ActionSlot.Light).Shape, Is.Not.EqualTo(AimShape.Around),
                    $"{hero} greift nicht rundherum an.");
        }

        /// <summary>Upgrades, die die Reichweite aendern, muessen auch die Anzeige aendern.</summary>
        [Test]
        public void UpgradesVerschiebenDieAnzeigeMit()
        {
            var schmal = Describe(HeroClassId.Paladin, ActionSlot.Skill);
            var weit = Describe(HeroClassId.Paladin, ActionSlot.Skill, wideReach: true);
            Assert.That(weit.Range, Is.GreaterThan(schmal.Range), "WEITE BAHN verlaengert die Aschewelle.");

            var ohne = Describe(HeroClassId.Guardian, ActionSlot.Light);
            var mit = Describe(HeroClassId.Guardian, ActionSlot.Light, pierces: 3);
            Assert.That(mit.Width, Is.GreaterThan(ohne.Width), "Durchschlag verbreitert BRAX' Hieb.");
        }

        /// <summary>Die Aschewelle-Anzeige muss so breit sein wie die Welle selbst.</summary>
        [Test]
        public void DieAschewelleWirdSoBreitAngezeigtWieSieIst()
        {
            Assert.That(Describe(HeroClassId.Paladin, ActionSlot.Skill).Width,
                Is.EqualTo(AshWave.HalfWidth).Within(0.001f));
        }
    }
}
