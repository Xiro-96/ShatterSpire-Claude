using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Shatterspire.Tests
{
    /// <summary>
    /// Die Sprachtabelle ist ein Dictionary-Initialisierer. Ein doppelter Schluessel darin
    /// kompiliert sauber und wirft erst beim ersten Zugriff eine Ausnahme - das Spiel startet
    /// dann gar nicht. Genau das ist am 12.09. zweimal passiert, deshalb dieser Test.
    ///
    /// Der zweite Teil prueft, dass jeder Text, den der Spieler an Katalogen sieht, auch eine
    /// deutsche Fassung hat. Fehlt eine, bleibt Englisch stehen - kein Absturz, aber genau der
    /// Mischmasch, der beanstandet wurde.
    /// </summary>
    public sealed class LocalizationTests
    {
        private static void WithGerman(Action body)
        {
            var before = Loc.Language;
            try
            {
                Loc.Language = Language.German;
                body();
            }
            finally
            {
                Loc.Language = before;
            }
        }

        [Test]
        public void TabelleLaedtOhneDoppelteSchluessel()
        {
            // Der Zugriff loest den Initialisierer aus; ein doppelter Schluessel schlaegt hier zu.
            Assert.That(Loc.T("FLOOR"), Is.Not.Null);
        }

        [Test]
        public void DeutschIstDerStandard()
        {
            Assert.That(Loc.Language, Is.EqualTo(Language.German),
                "Ohne gespeicherte Einstellung muss Deutsch gelten.");
        }

        [Test]
        public void UnbekannterTextBleibtUnveraendert()
        {
            WithGerman(() =>
            {
                Assert.That(Loc.T("SOMETHING NOT IN THE TABLE"), Is.EqualTo("SOMETHING NOT IN THE TABLE"));
                Assert.That(Loc.T(null), Is.Null);
                Assert.That(Loc.T(string.Empty), Is.Empty);
            });
        }

        [Test]
        public void EnglischGibtDasOriginalZurueck()
        {
            var before = Loc.Language;
            try
            {
                Loc.Language = Language.English;
                Assert.That(Loc.T("FLOOR"), Is.EqualTo("FLOOR"));
            }
            finally
            {
                Loc.Language = before;
            }
        }

        [Test]
        public void JedesRelikIstUebersetzt()
        {
            WithGerman(() =>
            {
                var missing = new List<string>();
                foreach (RelicId relic in Enum.GetValues(typeof(RelicId)))
                {
                    var name = RelicCatalog.Name(relic);
                    var description = RelicCatalog.Description(relic);
                    if (Loc.T(name) == name) missing.Add($"{relic}: Name '{name}'");
                    if (Loc.T(description) == description) missing.Add($"{relic}: Text '{description}'");
                }
                Assert.That(missing, Is.Empty, "Ohne Eintrag bleibt Englisch stehen: " + string.Join(", ", missing));
            });
        }

        [Test]
        public void JedeVerbesserungUndWareIstUebersetzt()
        {
            WithGerman(() =>
            {
                var missing = new List<string>();
                foreach (var perk in PerkCatalog.All)
                {
                    if (Loc.T(perk.Name) == perk.Name) missing.Add($"Perk {perk.Id}: '{perk.Name}'");
                    if (Loc.T(perk.Description) == perk.Description) missing.Add($"Perk {perk.Id}: '{perk.Description}'");
                }
                foreach (var offer in ShopCatalog.All)
                {
                    if (Loc.T(offer.Name) == offer.Name) missing.Add($"Ware {offer.Id}: '{offer.Name}'");
                    if (Loc.T(offer.Description) == offer.Description) missing.Add($"Ware {offer.Id}: '{offer.Description}'");
                }
                Assert.That(missing, Is.Empty, "Nicht uebersetzt: " + string.Join(" | ", missing));
            });
        }

        [Test]
        public void JederHeldUndJedePfadangabeIstUebersetzt()
        {
            WithGerman(() =>
            {
                var missing = new List<string>();
                foreach (HeroClassId hero in Enum.GetValues(typeof(HeroClassId)))
                {
                    Check(missing, HeroCatalog.Role(hero), $"Rolle {hero}");
                    Check(missing, HeroCatalog.LightAttackName(hero), $"Light {hero}");
                    Check(missing, HeroCatalog.HeavyAttackName(hero), $"Heavy {hero}");
                    Check(missing, HeroCatalog.SkillName(hero), $"Skill {hero}");
                    Check(missing, HeroCatalog.UltimateName(hero), $"Ultimate {hero}");
                }
                foreach (RunMode mode in Enum.GetValues(typeof(RunMode)))
                    Check(missing, PathCatalog.Summary(mode), $"Pfad {mode}");
                foreach (RoomKind kind in Enum.GetValues(typeof(RoomKind)))
                    Check(missing, kind.ToString().ToUpperInvariant(), $"Raumart {kind}");
                Assert.That(missing, Is.Empty, "Nicht uebersetzt: " + string.Join(" | ", missing));
            });
        }

        /// <summary>Merkt sich alles, was unverändert zurueckkommt - mit Ausnahme der Eigennamen.</summary>
        private static void Check(List<string> missing, string text, string where)
        {
            // Eigennamen bleiben absichtlich gleich: HAMMER heisst auf Deutsch auch Hammer.
            if (Loc.T(text) != text) return;
            var identical = new[] { "HAMMER", "ELITE", "BOSS", "ULTIMATE", "BRAX", "REX", "ORION" };
            foreach (var allowed in identical)
                if (text == allowed) return;
            missing.Add($"{where}: '{text}'");
        }
    }
}
