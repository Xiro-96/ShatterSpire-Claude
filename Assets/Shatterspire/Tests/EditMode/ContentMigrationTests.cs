using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Shatterspire.Tests
{
    /// <summary>
    /// Beweist, dass der Umzug der Kataloge in Assets die Balance nicht verändert
    /// hat. Solange diese Tests grün sind, spielt sich das Spiel mit den Assets
    /// genauso wie mit den alten switch-Ketten.
    ///
    /// Vor dem ersten Migrationslauf werden die Tests übersprungen statt rot —
    /// ein frisch geklontes Repository soll nicht wegen fehlender Assets scheitern.
    ///
    /// Hinweis: Unitys mitgeliefertes NUnit kennt kein Assert.Multiple, deshalb
    /// stehen die Prüfungen hier einzeln.
    /// </summary>
    public sealed class ContentMigrationTests
    {
        private const float Tolerance = 0.0001f;

        private static GameDatabase Database()
        {
            var database = Resources.Load<GameDatabase>(GameDatabase.ResourcePath);
            if (!database)
                Assert.Ignore("Migrationslauf noch nicht ausgeführt. " +
                              "Im Editor: SHATTERSPIRE > Kataloge zu Assets migrieren.");
            return database;
        }

        [Test]
        public void DatenbankEnthaeltJedenEnumWertGenauEinmal()
        {
            var problems = Database().Validate();
            Assert.That(problems, Is.Empty, "Lücken in der Datenbank:\n" + string.Join("\n", problems));
        }

        [Test]
        public void HeldenwerteStimmenMitDemCodeUeberein()
        {
            var database = Database();
            foreach (HeroClassId hero in Enum.GetValues(typeof(HeroClassId)))
            {
                var asset = database.Hero(hero);
                Assert.That(asset, Is.Not.Null, $"Kein Asset für Held {hero}.");
                Assert.That(asset.DisplayName, Is.EqualTo(HeroCatalog.Name(hero)), $"{hero}: Name");
                Assert.That(asset.Role, Is.EqualTo(HeroCatalog.Role(hero)), $"{hero}: Rolle");
                Assert.That(asset.KitSummary, Is.EqualTo(HeroCatalog.Kit(hero)), $"{hero}: Kit");
                Assert.That(asset.BaseHealth, Is.EqualTo(HeroCatalog.BaseHealth(hero)).Within(Tolerance), $"{hero}: Leben");
                Assert.That(asset.BaseSpeed, Is.EqualTo(HeroCatalog.BaseSpeed(hero)).Within(Tolerance), $"{hero}: Bewegung");
                Assert.That(asset.BaseDamage, Is.EqualTo(HeroCatalog.BaseDamage(hero)).Within(Tolerance), $"{hero}: Schaden");
                Assert.That(asset.SkillCooldown, Is.EqualTo(HeroCatalog.SkillCooldown(hero)).Within(Tolerance), $"{hero}: Skill-Cooldown");
                Assert.That(asset.LightAttackName, Is.EqualTo(HeroCatalog.LightAttackName(hero)), $"{hero}: Light-Name");
                Assert.That(asset.HeavyAttackName, Is.EqualTo(HeroCatalog.HeavyAttackName(hero)), $"{hero}: Heavy-Name");
                Assert.That(asset.SkillName, Is.EqualTo(HeroCatalog.SkillName(hero)), $"{hero}: Skill-Name");
            }
        }

        [Test]
        public void GegnerwerteStimmenMitDemCodeUeberein()
        {
            var database = Database();
            foreach (EnemyKind kind in Enum.GetValues(typeof(EnemyKind)))
            {
                var asset = database.Enemy(kind);
                var stats = EnemyBalance.For(kind);
                Assert.That(asset, Is.Not.Null, $"Kein Asset für Gegner {kind}.");
                Assert.That(asset.Health, Is.EqualTo(stats.Health).Within(Tolerance), $"{kind}: Leben");
                Assert.That(asset.Speed, Is.EqualTo(stats.Speed).Within(Tolerance), $"{kind}: Bewegung");
                Assert.That(asset.AttackRange, Is.EqualTo(stats.AttackRange).Within(Tolerance), $"{kind}: Reichweite");
                Assert.That(asset.AttackDamage, Is.EqualTo(stats.AttackDamage).Within(Tolerance), $"{kind}: Schaden");
                Assert.That(asset.KnockbackResistance, Is.EqualTo(stats.KnockbackResistance).Within(Tolerance), $"{kind}: Rückstoß");
                Assert.That(asset.StaggerSeconds, Is.EqualTo(stats.StaggerSeconds).Within(Tolerance), $"{kind}: Stagger");
                Assert.That(asset.SeparationSpacing, Is.EqualTo(stats.SeparationSpacing).Within(Tolerance), $"{kind}: Abstand");
                Assert.That(asset.TelegraphSeconds, Is.EqualTo(stats.TelegraphSeconds).Within(Tolerance), $"{kind}: Vorwarnzeit");
                Assert.That(asset.AttackCooldown, Is.EqualTo(stats.AttackCooldown).Within(Tolerance), $"{kind}: Abklingzeit");
                Assert.That(asset.ColliderHeight, Is.EqualTo(stats.ColliderHeight).Within(Tolerance), $"{kind}: Collider-Höhe");
                Assert.That(asset.ColliderRadius, Is.EqualTo(stats.ColliderRadius).Within(Tolerance), $"{kind}: Collider-Radius");
            }
        }

        /// <summary>
        /// Die Rückstoß-Staffelung ist bewusst austariert: leichte Gegner fliegen,
        /// der Boss steht. Ein versehentliches Gleichziehen dieser Werte würde das
        /// Trefferfeedback zerstören, ohne einen anderen Test rot zu machen.
        /// </summary>
        [Test]
        public void RueckstossStaffelungBleibtNachGewichtGeordnet()
        {
            var database = Database();
            var order = new[] { EnemyKind.Crawler, EnemyKind.Brute, EnemyKind.Elite, EnemyKind.IronWarden };
            for (var i = 1; i < order.Length; i++)
            {
                var lighter = database.Enemy(order[i - 1]);
                var heavier = database.Enemy(order[i]);
                Assert.That(heavier.KnockbackResistance, Is.LessThan(lighter.KnockbackResistance),
                    $"{order[i]} muss schwerer stehen als {order[i - 1]}.");
                Assert.That(heavier.StaggerSeconds, Is.LessThan(lighter.StaggerSeconds),
                    $"{order[i]} darf nicht länger staggern als {order[i - 1]}.");
            }
        }

        [Test]
        public void PerkAssetsSpiegelnDenKatalogVollstaendig()
        {
            var database = Database();
            Assert.That(database.Perks, Has.Count.EqualTo(PerkCatalog.All.Count));

            foreach (var expected in PerkCatalog.All)
            {
                var asset = database.Perk(expected.Id);
                Assert.That(asset, Is.Not.Null, $"Kein Asset für Perk {expected.Id}.");
                Assert.That(asset.DisplayName, Is.EqualTo(expected.Name), $"{expected.Id}: Name");
                Assert.That(asset.Description, Is.EqualTo(expected.Description), $"{expected.Id}: Text");
                Assert.That(asset.Rarity, Is.EqualTo(expected.Rarity), $"{expected.Id}: Seltenheit");
            }
        }

        [Test]
        public void PerkBrueckeLiefertIdentischeLaufzeitdaten()
        {
            var database = Database();
            foreach (var expected in PerkCatalog.All)
            {
                var runtime = database.Perk(expected.Id).ToRuntime();
                Assert.That(runtime.Id, Is.EqualTo(expected.Id));
                Assert.That(runtime.Name, Is.EqualTo(expected.Name), $"{expected.Id}: Name");
                Assert.That(runtime.Description, Is.EqualTo(expected.Description), $"{expected.Id}: Text");
                Assert.That(runtime.Rarity, Is.EqualTo(expected.Rarity), $"{expected.Id}: Seltenheit");
                Assert.That(runtime.Color, Is.EqualTo(expected.Color), $"{expected.Id}: Farbe");
            }
        }

        [Test]
        public void RelicNamenUndTexteStimmenMitDemKatalogUeberein()
        {
            var database = Database();
            foreach (RelicId relic in Enum.GetValues(typeof(RelicId)))
            {
                var asset = database.Relic(relic);
                Assert.That(asset, Is.Not.Null, $"Kein Asset für Relic {relic}.");
                Assert.That(asset.DisplayName, Is.EqualTo(RelicCatalog.Name(relic)), $"{relic}: Name");
                Assert.That(asset.Description, Is.EqualTo(RelicCatalog.Description(relic)), $"{relic}: Text");
            }
        }

        /// <summary>
        /// Jedes Relic muss genau den Effekt tragen, den PlayerBuild.ConfigureRun
        /// und RunDirector heute anwenden — und keinen zweiten dazu.
        /// </summary>
        [Test]
        public void RelicEffekteEntsprechenDemBisherigenVerhalten()
        {
            var database = Database();
            var expected = new Dictionary<RelicId, RelicEffect>
            {
                [RelicId.WindstepSigil] = new RelicEffect(1, 0f, 1f, 0f, 1f, false),
                [RelicId.HuntersMark] = new RelicEffect(0, 0.1f, 1f, 0f, 1f, false),
                [RelicId.DawnSeed] = new RelicEffect(0, 0f, 1f, 10f, 1f, false),
                [RelicId.EmberLens] = new RelicEffect(0, 0f, 1f, 0f, 1f, true),
                [RelicId.ArcBattery] = new RelicEffect(0, 0f, 1.25f, 0f, 1f, false),
                [RelicId.FortunePrism] = new RelicEffect(0, 0f, 1f, 0f, 1.25f, false)
            };

            Assert.That(expected.Keys, Is.EquivalentTo(Enum.GetValues(typeof(RelicId)).Cast<RelicId>()),
                "Neues Relic im Enum, aber keine erwartete Wirkung im Test hinterlegt.");

            foreach (var entry in expected)
            {
                var relic = entry.Key;
                var want = entry.Value;
                var asset = database.Relic(relic);
                Assert.That(asset.ExtraDashCharges, Is.EqualTo(want.DashCharges), $"{relic}: Dash-Ladungen");
                Assert.That(asset.CritChanceBonus, Is.EqualTo(want.Crit).Within(Tolerance), $"{relic}: Crit");
                Assert.That(asset.HeavyChargeMultiplier, Is.EqualTo(want.Charge).Within(Tolerance), $"{relic}: Heavy-Ladung");
                Assert.That(asset.FloorHealBonus, Is.EqualTo(want.Heal).Within(Tolerance), $"{relic}: Etagen-Heilung");
                Assert.That(asset.ShardMultiplier, Is.EqualTo(want.Shards).Within(Tolerance), $"{relic}: Shard-Faktor");
                Assert.That(asset.HeavyErupts, Is.EqualTo(want.Erupts), $"{relic}: Heavy detoniert");
            }
        }

        private readonly struct RelicEffect
        {
            public readonly int DashCharges;
            public readonly float Crit;
            public readonly float Charge;
            public readonly float Heal;
            public readonly float Shards;
            public readonly bool Erupts;

            public RelicEffect(int dashCharges, float crit, float charge, float heal, float shards, bool erupts)
            {
                DashCharges = dashCharges;
                Crit = crit;
                Charge = charge;
                Heal = heal;
                Shards = shards;
                Erupts = erupts;
            }
        }
    }
}
