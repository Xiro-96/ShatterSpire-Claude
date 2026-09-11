using System.Collections.Generic;
using NUnit.Framework;

namespace Shatterspire.Tests
{
    public sealed class RunFoundationTests
    {
        [Test]
        public void RunConfigurationCloneDoesNotShareRelicList()
        {
            var original = new RunConfig
            {
                Hero = HeroClassId.Guardian,
                Mode = RunMode.Legendary,
                Relics = new List<RelicId> { RelicId.WindstepSigil, RelicId.EmberLens }
            };

            var clone = original.Clone();
            clone.Relics.RemoveAt(0);

            Assert.That(clone.Hero, Is.EqualTo(HeroClassId.Guardian));
            Assert.That(clone.Mode, Is.EqualTo(RunMode.Legendary));
            Assert.That(original.Relics, Has.Count.EqualTo(2));
            Assert.That(clone.Relics, Has.Count.EqualTo(1));
        }

        [Test]
        public void FloorThemesCycleWithoutChangingFloorOne()
        {
            Assert.That(FloorCatalog.ThemeFor(1), Is.EqualTo(FloorTheme.ForgottenCourt));
            Assert.That(FloorCatalog.ThemeFor(2), Is.EqualTo(FloorTheme.EmberFoundry));
            Assert.That(FloorCatalog.ThemeFor(3), Is.EqualTo(FloorTheme.AstralArchive));
            Assert.That(FloorCatalog.ThemeFor(4), Is.EqualTo(FloorTheme.ForgottenCourt));
        }

        [Test]
        public void EveryHeroHasAUniqueCombatIdentity()
        {
            var names = new HashSet<string>();
            var kits = new HashSet<string>();
            foreach (HeroClassId hero in System.Enum.GetValues(typeof(HeroClassId)))
            {
                names.Add(HeroCatalog.Name(hero));
                kits.Add(HeroCatalog.Kit(hero));
            }

            Assert.That(names, Has.Count.EqualTo(3));
            Assert.That(kits, Has.Count.EqualTo(3));
        }
    }
}
