using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Shatterspire.Tests
{
    public sealed class PerkCatalogTests
    {
        [Test]
        public void VerticalSliceContainsTwentyUniquePerks()
        {
            Assert.That(PerkCatalog.All, Has.Count.EqualTo(20));
            Assert.That(PerkCatalog.All.Select(p => p.Id).Distinct().Count(), Is.EqualTo(20));
        }

        [Test]
        public void PerkRollAlwaysReturnsThreeDifferentChoices()
        {
            var result = PerkCatalog.RollThree(new HashSet<PerkId>());
            Assert.That(result, Has.Count.EqualTo(3));
            Assert.That(result.Select(p => p.Id).Distinct().Count(), Is.EqualTo(3));
        }

        [Test]
        public void FusionFlagsActivateFromTheirComponents()
        {
            var go = new GameObject("Fusion Test");
            go.AddComponent<Health>().Configure(TeamId.Player, 100f);
            var build = go.AddComponent<PlayerBuild>();
            build.Apply(PerkCatalog.All.First(p => p.Id == PerkId.FireBullet));
            build.Apply(PerkCatalog.All.First(p => p.Id == PerkId.ExplosiveShot));
            Assert.That(build.IsInferno, Is.True);
            Object.DestroyImmediate(go);
        }
    }
}
