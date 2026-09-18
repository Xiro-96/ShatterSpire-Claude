using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Shatterspire.Tests
{
    /// <summary>
    /// Die Gruppe aus drei Helden.
    ///
    /// Der Anlass: die zwei Begleiter waren keine Helden. Sie hatten kein Leben, keine Klasse, keine
    /// Faehigkeit, und kein Gegner hat sie je angesehen - in <see cref="EnemyAgent"/> stand das Ziel
    /// ab dem Aufsetzen fest auf dem Spieler. Sie waren unverwundbar, weil sie niemand bemerkte.
    ///
    /// Geprueft wird deshalb dreierlei: wer mitkommt, wer angegriffen wird, und ob die Zahlen noch
    /// zusammenpassen - zwei echte Helden teilen deutlich mehr aus als zwei fest verdrahtete
    /// Begleiter, und die Gegner muessen mitwachsen.
    /// </summary>
    public sealed class PartyTests
    {
        private readonly List<GameObject> spawned = new();

        [TearDown]
        public void Clear()
        {
            foreach (var go in spawned) if (go) Object.DestroyImmediate(go);
            spawned.Clear();
        }

        private static IEnumerable<HeroClassId> AllHeroes
            => System.Enum.GetValues(typeof(HeroClassId)).Cast<HeroClassId>();

        // ── Wer mitkommt ────────────────────────────────────────────────────

        [Test]
        public void TeamNeverContainsTheChosenHero()
        {
            foreach (var hero in AllHeroes)
            {
                var team = PartyMember.OfflineTeamFor(hero);
                Assert.AreEqual(2, team.Length, $"{hero}: die Gruppe hat immer drei Mitglieder.");
                foreach (var mate in team)
                    Assert.AreNotEqual(hero, mate.Hero, $"{hero} klettert mit sich selbst.");
            }
        }

        [Test]
        public void TeamNeverContainsTheSameHeroTwice()
        {
            foreach (var hero in AllHeroes)
            {
                var team = PartyMember.OfflineTeamFor(hero);
                Assert.AreNotEqual(team[0].Hero, team[1].Hero, $"{hero}: zweimal derselbe Begleiter.");
            }
        }

        [Test]
        public void TeamCoversBothDistances()
        {
            foreach (var hero in AllHeroes)
            {
                var team = PartyMember.OfflineTeamFor(hero);
                Assert.IsTrue(team.Any(m => HeroCatalog.IsMelee(m.Hero)),
                    $"{hero}: niemand geht in den Nahkampf.");
                Assert.IsTrue(team.Any(m => !HeroCatalog.IsMelee(m.Hero)),
                    $"{hero}: niemand haelt Abstand.");
            }
        }

        [Test]
        public void MeleePlayerDoesNotGetASecondBrawlerInFront()
        {
            foreach (var hero in AllHeroes.Where(HeroCatalog.IsMelee))
            {
                var team = PartyMember.OfflineTeamFor(hero);
                Assert.IsFalse(team.Any(m => m.Slot == PartySlot.Frontline),
                    $"{hero} steht selbst vorn - der Begleiter soll nicht daneben stehen.");
            }
        }

        // ── Reichweiten ─────────────────────────────────────────────────────

        [Test]
        public void EngageRangeMatchesWhetherTheHeroIsMelee()
        {
            foreach (var hero in AllHeroes)
            {
                var range = HeroCatalog.EngageRange(hero);
                if (HeroCatalog.IsMelee(hero))
                    Assert.Less(range, 4f, $"{HeroCatalog.Name(hero)} kaempft im Nahkampf, "
                                           + "seine Reichweite darf keine Schussweite sein.");
                else
                    Assert.Greater(range, 4f, $"{HeroCatalog.Name(hero)} kaempft auf Abstand, "
                                              + "haelt aber Schlagweite.");
            }
        }

        // ── Wer angegriffen wird ────────────────────────────────────────────

        private PartyMember Member(Vector3 position, bool local, float health = 100f)
        {
            var go = new GameObject(local ? "Local" : "Mate");
            spawned.Add(go);
            go.transform.position = position;
            var body = go.AddComponent<Health>();
            body.Configure(TeamId.Player, health);
            var member = go.AddComponent<PartyMember>();
            member.Configure(local ? "YOU" : "MATE", Color.white, HeroClassId.Ranger,
                local ? PartySlot.Flank : PartySlot.Frontline, local);
            return member;
        }

        [Test]
        public void ClosestAliveFindsTheNearestMember()
        {
            var player = Member(Vector3.zero, local: true);
            var mate = Member(new Vector3(3f, 0f, 0f), local: false);

            Assert.AreSame(player.Health, PartyMember.ClosestAlive(new Vector3(-1f, 0f, 0f)),
                "Der Spieler steht naeher.");
            Assert.AreSame(mate.Health, PartyMember.ClosestAlive(new Vector3(5f, 0f, 0f)),
                "Ein Mitglied wird nicht angegriffen - genau das war der Fehler.");
        }

        [Test]
        public void ClosestAliveSkipsTheFallen()
        {
            var player = Member(Vector3.zero, local: true);
            var mate = Member(new Vector3(3f, 0f, 0f), local: false);
            mate.Health.TakeDamage(new DamageInfo(999f, DamageType.True, null, mate.transform.position, Vector3.zero, false));

            Assert.IsFalse(mate.IsAlive, "Der Aufbau des Tests stimmt nicht: das Mitglied lebt noch.");
            Assert.AreSame(player.Health, PartyMember.ClosestAlive(new Vector3(9f, 0f, 0f)),
                "Ein gefallenes Mitglied bindet keine Gegner mehr.");
        }

        [Test]
        public void StickinessKeepsAnEnemyOnItsTarget()
        {
            var player = Member(Vector3.zero, local: true);
            var mate = Member(new Vector3(4f, 0f, 0f), local: false);
            // Der Gegner steht genau dazwischen, einen halben Meter naeher am Mitglied.
            var point = new Vector3(2.25f, 0f, 0f);

            Assert.AreSame(mate.Health, PartyMember.ClosestAlive(point),
                "Ohne Vorgeschichte gewinnt der naechste.");
            Assert.AreSame(player.Health, PartyMember.ClosestAlive(point, player.Health, stickiness: 2.5f),
                "Mit Vorsprung bleibt der Gegner an seinem Ziel, statt bei jedem Schritt zu wechseln.");
        }

        [Test]
        public void AnyAliveIsFalseOnlyWhenTheWholePartyIsDown()
        {
            var player = Member(Vector3.zero, local: true);
            var mate = Member(new Vector3(3f, 0f, 0f), local: false);
            Assert.IsTrue(PartyMember.AnyAlive());

            player.Health.TakeDamage(new DamageInfo(999f, DamageType.True, null, Vector3.zero, Vector3.zero, false));
            Assert.IsTrue(PartyMember.AnyAlive(), "Das Mitglied lebt noch.");

            mate.Health.TakeDamage(new DamageInfo(999f, DamageType.True, null, Vector3.zero, Vector3.zero, false));
            Assert.IsFalse(PartyMember.AnyAlive());
        }

        // ── Die Zahlen dahinter ─────────────────────────────────────────────

        /// <summary>
        /// Schaden je Sekunde aus dem einfachen Angriff, nach denselben Werten, mit denen
        /// <see cref="WeaponSystem"/> rechnet. Traegt den Dauerschaden eines Helden.
        /// </summary>
        private static float LightAttackDps(HeroClassId hero)
        {
            var combo = hero switch
            {
                HeroClassId.Guardian => new[] { (1f, 0.38f), (1.4f, 0.46f), (1.75f, 0.64f) },
                HeroClassId.Paladin => new[] { (1f, 0.3f), (1.12f, 0.3f), (1.4f, 0.42f) },
                HeroClassId.Arcanist => new[] { (1f, 0.34f), (1.1f, 0.34f), (1.38f, 0.42f) },
                _ => new[] { (1f, 0.25f), (1.1f, 0.25f), (1.38f, 0.42f) }
            };
            var damage = combo.Sum(step => HeroCatalog.BaseDamage(hero) * step.Item1);
            return damage / combo.Sum(step => step.Item2);
        }

        [Test]
        public void AlliesCarryLessThanHalfOfTheParty()
        {
            var player = LightAttackDps(HeroClassId.Ranger);
            var ally = AllHeroes.Average(LightAttackDps) * PartyBalance.AllyDamage;
            var share = 2f * ally / (player + 2f * ally);

            // Bei vollem Schaden traegt das Paar zwei Drittel der Gruppe - der Spieler waere der
            // kleinste Teil seiner eigenen Gruppe. Die Grenze haelt das fest.
            Assert.Less(share, 0.45f, "Die zwei Mitglieder tragen mehr als der Spieler.");
            Assert.Greater(share, 0.25f, "Die zwei Mitglieder sind wieder Deko, die danebensteht.");
        }

        [Test]
        public void EnemyHealthGrewWithThePartyDamage()
        {
            // Vor dem Umbau: Spieler plus zwei fest verdrahtete Begleiter (9 Schaden je 1,7 s und
            // 4 je 0,75 s - die Zahlen standen so in CompanionBot).
            var player = LightAttackDps(HeroClassId.Ranger);
            var before = player + 9f / 1.7f + 4f / 0.75f;
            var after = player + 2f * AllHeroes.Average(LightAttackDps) * PartyBalance.AllyDamage;
            var growth = after / before;

            // Das Leben der Gegner ist von 1,35 auf 1,75 gestiegen, also um das 1,296-fache. Es soll
            // dem Zuwachs der Gruppe folgen, sonst ist ein Kampf schneller vorbei als vorher.
            var healthGrowth = EnemyBalance.BaseHealthScale / 1.35f;
            Assert.AreEqual(growth, healthGrowth, 0.12f,
                $"Die Gruppe teilt das {growth:0.00}-fache aus, die Gegner halten aber nur das "
                + $"{healthGrowth:0.00}-fache aus. Beides gehoert zusammen.");
        }

        [Test]
        public void EnemyDamageGrewBecauseThreatIsSharedNow()
        {
            // Frueher lief jeder Gegner auf den Spieler zu. Jetzt verteilt sich der Schaden auf drei
            // Ziele, und der Spieler faengt nur noch seinen Anteil ab. Ohne Ausgleich waere das
            // Spiel an genau der Stelle leichter geworden, an der es haerter werden sollte.
            var damageGrowth = EnemyBalance.BaseDamageScale / 1.2f;
            var felt = PartyBalance.PlayerThreatShare * damageGrowth;
            Assert.Less(felt, 1f, "Mit stehender Gruppe soll es nicht gefaehrlicher sein als frueher.");
            Assert.Greater(felt, 0.45f, "Mit stehender Gruppe ist es zu harmlos geworden.");
            Assert.Greater(damageGrowth, 1.2f,
                "Allein soll es deutlich haerter sein - sonst kostet der Verlust der Gruppe nichts.");
        }

        [Test]
        public void FallenMembersReturnHurt()
        {
            Assert.Greater(PartyBalance.RejoinHealth, 0f, "Sie kommen ueberhaupt zurueck.");
            Assert.Less(PartyBalance.RejoinHealth, 1f,
                "Wer zurueckkommt, kommt angeschlagen zurueck - sonst kostet das Fallen nichts.");
        }
    }
}
