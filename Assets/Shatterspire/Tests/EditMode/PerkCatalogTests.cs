using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Shatterspire.Tests
{
    /// <summary>
    /// Upgrades nach R.I.S.E.: jedes veraendert eine Aktion, jeder Held hat fuer jede Aktion eigene.
    /// Die Auswahl laeuft mit System.Random und ist dadurch ohne Unity pruefbar.
    /// </summary>
    public sealed class PerkCatalogTests
    {
        private static readonly HeroClassId[] AllHeroes = (HeroClassId[])Enum.GetValues(typeof(HeroClassId));
        private static readonly ActionSlot[] Actions = { ActionSlot.Light, ActionSlot.Heavy, ActionSlot.Skill, ActionSlot.Dash, ActionSlot.Ultimate };

        [Test]
        public void JedeIdHatGenauEinUpgrade()
        {
            foreach (PerkId id in Enum.GetValues(typeof(PerkId)))
                Assert.That(PerkCatalog.All.Count(perk => perk.Id == id), Is.EqualTo(1), $"{id}");
            Assert.That(PerkCatalog.All.Count, Is.EqualTo(Enum.GetValues(typeof(PerkId)).Length));
        }

        [Test]
        public void JederHeldHatFuerJedeAktionEigeneUpgrades()
        {
            foreach (var hero in AllHeroes)
            foreach (var slot in Actions)
            {
                var options = PerkCatalog.All.Where(perk => perk.Slot == slot && perk.AvailableFor(hero)).ToList();
                Assert.That(options.Count, Is.GreaterThanOrEqualTo(3), $"{hero} {slot}: zu wenig Auswahl");
                Assert.That(options.Count(perk => perk.Heroes.Length == 1), Is.GreaterThanOrEqualTo(1),
                    $"{hero} {slot}: kein Upgrade nur fuer diesen Helden");
            }
        }

        [Test]
        public void HeldenUpgradesGehoerenNurIhremHelden()
        {
            foreach (var perk in PerkCatalog.All)
            foreach (var hero in AllHeroes)
            {
                if (!perk.Id.ToString().StartsWith(hero.ToString(), StringComparison.Ordinal)) continue;
                Assert.That(perk.Heroes, Is.EqualTo(new[] { hero }), $"{perk.Id}");
                Assert.That(perk.Slot, Is.Not.EqualTo(ActionSlot.Passive), $"{perk.Id} muss eine Aktion veraendern");
            }
        }

        [Test]
        public void JederHeldHatEineEigeneUltimate()
        {
            var names = AllHeroes.Select(HeroCatalog.UltimateName).ToList();
            Assert.That(names.All(name => !string.IsNullOrWhiteSpace(name)), Is.True);
            Assert.That(names.Distinct().Count(), Is.EqualTo(AllHeroes.Length));
        }

        [Test]
        public void AuswahlLiefertDreiUpgradesFuerVerschiedeneAktionen()
        {
            foreach (var hero in AllHeroes)
            for (var seed = 0; seed < 200; seed++)
            {
                var roll = PerkCatalog.RollThree(hero, new HashSet<PerkId>(), new System.Random(seed));
                Assert.That(roll, Has.Count.EqualTo(3));
                Assert.That(roll.Select(perk => perk.Id).Distinct().Count(), Is.EqualTo(3));
                Assert.That(roll.All(perk => perk.AvailableFor(hero)), Is.True, $"{hero} Seed {seed}: fremdes Upgrade");
                Assert.That(roll.Select(perk => perk.Slot).Distinct().Count(), Is.EqualTo(3),
                    $"{hero} Seed {seed}: zwei Upgrades fuer dieselbe Aktion");
            }
        }

        [Test]
        public void AuswahlWiederholtNichtsSolangeNeueUpgradesUebrigSind()
        {
            var random = new System.Random(7);
            foreach (var hero in AllHeroes)
            {
                var owned = new HashSet<PerkId>();
                var available = PerkCatalog.All.Count(perk => perk.AvailableFor(hero));
                while (available - owned.Count >= 3)
                {
                    var roll = PerkCatalog.RollThree(hero, owned, random);
                    Assert.That(roll.Any(perk => owned.Contains(perk.Id)), Is.False, $"{hero}: Wiederholung bei {owned.Count}");
                    owned.Add(roll[0].Id);
                }
                var late = PerkCatalog.RollThree(hero, owned, random);
                Assert.That(late, Has.Count.EqualTo(3));
                Assert.That(late.Select(perk => perk.Id).Distinct().Count(), Is.EqualTo(3));
            }
        }

        [Test]
        public void HeldenUpgradesTauchenRegelmaessigAuf()
        {
            foreach (var hero in AllHeroes)
            {
                var random = new System.Random(11);
                var withHeroUpgrade = 0;
                for (var i = 0; i < 300; i++)
                    if (PerkCatalog.RollThree(hero, null, random).Any(perk => perk.Heroes.Length == 1)) withHeroUpgrade++;
                Assert.That(withHeroUpgrade, Is.GreaterThanOrEqualTo(90), $"{hero}: nur {withHeroUpgrade} von 300");
            }
        }

        [Test]
        public void FusionFlagsActivateFromTheirComponents()
        {
            var go = new GameObject("Fusion Test");
            go.AddComponent<Health>().Configure(TeamId.Player, 100f);
            var build = go.AddComponent<PlayerBuild>();
            build.Apply(PerkCatalog.Find(PerkId.FireBullet));
            build.Apply(PerkCatalog.Find(PerkId.ExplosiveShot));
            Assert.That(build.IsInferno, Is.True);
            Object.DestroyImmediate(go);
        }
    }
}
