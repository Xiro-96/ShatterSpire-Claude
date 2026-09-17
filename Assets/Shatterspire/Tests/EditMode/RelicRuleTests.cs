using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shatterspire.Tests
{
    /// <summary>
    /// Die zehn Relikte mit eigenem Verb.
    ///
    /// Der Anlass: "mehr Relikte, mehr verschiedene Bonus-Dinger". Die ersten fuenfzehn waren fast
    /// alle ein Prozentwert auf eine Zahl. Die neuen tun etwas - und was etwas tut, kann auf eine
    /// Weise falsch sein, die ein Prozentwert nicht kann: zweimal ausloesen, nie ausloesen, am
    /// Anfang schon voll wirken. Diese Tests pruefen die Regeln dahinter.
    /// </summary>
    public sealed class RelicRuleTests
    {
        private readonly List<GameObject> created = new();

        [TearDown]
        public void Clear()
        {
            foreach (var go in created)
                if (go) Object.DestroyImmediate(go);
            created.Clear();
        }

        private PlayerBuild Build(params RelicId[] relics)
        {
            var go = new GameObject("Relic Test Player");
            created.Add(go);
            go.AddComponent<Health>().Configure(TeamId.Player, 100f);
            var build = go.AddComponent<PlayerBuild>();
            build.ConfigureRun(HeroClassId.Ranger, new RunConfig { Relics = new List<RelicId>(relics) }, null);
            return build;
        }

        // ── Letzter Atem ────────────────────────────────────────────────────

        [Test]
        public void LetzterAtemFaengtNurDenToedlichenTrefferAb()
        {
            Assert.That(RelicRules.LastBreathCatches(false, 50f, 40f), Is.True, "toedlich");
            Assert.That(RelicRules.LastBreathCatches(false, 40f, 40f), Is.True, "genau toedlich");
            Assert.That(RelicRules.LastBreathCatches(false, 39f, 40f), Is.False, "nicht toedlich");
            Assert.That(RelicRules.LastBreathCatches(true, 500f, 40f), Is.False, "schon verbraucht");
            Assert.That(RelicRules.LastBreathCatches(false, 10f, 0f), Is.False, "schon gefallen");
        }

        [Test]
        public void NachDemLetztenAtemBleibtGenugLeben()
        {
            Assert.That(RelicRules.LastBreathHealthAfter(5f, 100f), Is.EqualTo(30f).Within(0.001f));
            Assert.That(RelicRules.LastBreathHealthAfter(60f, 100f), Is.EqualTo(60f).Within(0.001f),
                "Wer mehr als 30 % hatte, verliert nichts.");
        }

        // ── Kriegsbanner ────────────────────────────────────────────────────

        [Test]
        public void DasKriegsbannerWaechstMitDemLaufUndHatEinenDeckel()
        {
            Assert.That(RelicRules.WarBannerBonus(1), Is.EqualTo(0f), "Auf Etage 1 nichts - es waechst mit dem Lauf.");
            var previous = 0f;
            for (var floor = 2; floor <= 30; floor++)
            {
                var bonus = RelicRules.WarBannerBonus(floor);
                Assert.That(bonus, Is.GreaterThanOrEqualTo(previous), $"Etage {floor}");
                Assert.That(bonus, Is.LessThanOrEqualTo(RelicRules.WarBannerMaximum + 0.0001f), $"Etage {floor}");
                previous = bonus;
            }
            Assert.That(RelicRules.WarBannerBonus(9), Is.EqualTo(RelicRules.WarBannerMaximum).Within(0.0001f));
        }

        [Test]
        public void DasKriegsbannerWirktAufDenSchaden()
        {
            var plain = Build();
            var banner = Build(RelicId.WarBanner);
            Assert.That(banner.DamageMultiplier, Is.EqualTo(plain.DamageMultiplier).Within(0.0001f), "Etage 1");
            plain.SetClimbFloor(9);
            banner.SetClimbFloor(9);
            Assert.That(banner.DamageMultiplier / plain.DamageMultiplier,
                Is.EqualTo(1f + RelicRules.WarBannerMaximum).Within(0.0001f), "Etage 9");
        }

        // ── Blutpakt ────────────────────────────────────────────────────────

        [Test]
        public void DerBlutpaktTauschtLebenGegenSchaden()
        {
            var plain = Build();
            var pact = Build(RelicId.BloodPact);
            var plainHealth = plain.GetComponent<Health>();
            var pactHealth = pact.GetComponent<Health>();
            Assert.That(pactHealth.Maximum, Is.EqualTo(plainHealth.Maximum * (1f - RelicRules.BloodPactHealthLoss)).Within(0.01f));
            Assert.That(pactHealth.Current, Is.EqualTo(pactHealth.Maximum).Within(0.01f), "Voll geheilt ins Spiel.");
            Assert.That(pact.DamageMultiplier / plain.DamageMultiplier,
                Is.EqualTo(RelicRules.BloodPactDamage).Within(0.0001f));
        }

        // ── Adrenalin ───────────────────────────────────────────────────────

        [Test]
        public void AdrenalinMachtKurzSchneller()
        {
            var build = Build(RelicId.Adrenaline);
            var before = build.MoveSpeedMultiplier;
            build.TriggerAdrenaline();
            Assert.That(build.MoveSpeedMultiplier / before, Is.EqualTo(RelicRules.AdrenalineSpeed).Within(0.0001f));
        }

        // ── Die Liste selbst ────────────────────────────────────────────────

        [Test]
        public void JedesVerbRelikWirdErkannt()
        {
            var build = Build(RelicId.SplinterBurst, RelicId.StormBell, RelicId.PhantomEdge);
            Assert.That(build.HasSplinterBurst && build.HasStormBell && build.HasPhantomEdge, Is.True);
            Assert.That(build.HasOverflow || build.HasSteadyHeart || build.HasSparkWard || build.HasAdrenaline, Is.False,
                "Nicht ausgeruestete Relikte wirken nicht.");
        }

        /// <summary>Die Relikt-Seite hat fuenf Spalten und fuenf Reihen. Mehr passen nicht, ohne in FERTIG zu laufen.</summary>
        [Test]
        public void AlleRelikteHabenAufDerSeitePlatz()
        {
            Assert.That(Enum.GetValues(typeof(RelicId)).Length, Is.LessThanOrEqualTo(25),
                "Mehr als 25 Relikte: MainMenuUI.ShowRelics braucht ein neues Raster.");
        }

        /// <summary>Ein Relikt, das so heisst wie ein Upgrade, ist beim Spielen nicht zu unterscheiden.</summary>
        [Test]
        public void KeinReliktHeisstWieEinUpgrade()
        {
            var perkNames = new HashSet<string>();
            foreach (var perk in PerkCatalog.All) perkNames.Add(perk.Name);
            foreach (RelicId relic in Enum.GetValues(typeof(RelicId)))
                Assert.That(perkNames.Contains(RelicCatalog.Name(relic)), Is.False,
                    $"{relic} heisst wie ein Upgrade: {RelicCatalog.Name(relic)}");
        }
    }
}
