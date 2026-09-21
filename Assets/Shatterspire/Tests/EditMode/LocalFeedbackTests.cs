using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;

namespace Shatterspire.Tests
{
    /// <summary>
    /// Was der Spieler an seinem Helden spuert, gehoert nur seinem Helden.
    ///
    /// Der Anlass: "Richturteil wird jetzt auch ohne Xiro dauerhaft benutzt. Da scheint ein dicker
    /// Fehler drin zu sein." Er war drin, seit dem Umbau auf echte Begleiter. Die zwei Begleiter
    /// haben seither ein eigenes Waffensystem - und alles, was darin fuer den Spieler gedacht war,
    /// lief fuer sie mit:
    ///
    /// - Das HUD bekam den Zustand ihres schweren Angriffs ueber ein Ereignis ohne Absender. Der
    ///   eigene Knopf lud, wurde golden und meldete "perfekt getroffen" im Takt ihrer Schlaege.
    /// - Die Trefferstarre setzt Time.timeScale fuer die ganze Welt herunter. Gemessen lief das Spiel
    ///   mit zwei Begleitern 13 bis 17 % der Zeit in Zeitlupe statt 4 bis 12 % - auch die eigene
    ///   Figur unter dem Daumen. Und weil danach fuer alle gesperrt ist, frassen sie die Starre der
    ///   eigenen Schlaege.
    /// - Kamerastoss, Ansagen ohne Ort und der Schmerzlaut kamen bei jedem ihrer Treffer.
    ///
    /// Zwei Arten von Pruefung: eine liest den Quelltext und haelt fest, dass es im Waffensystem
    /// genau eine Tuer zu jeder dieser Wirkungen gibt und dass sie den Besitzer prueft. Die andere
    /// treibt die echten Klassen und schaut, was ankommt.
    /// </summary>
    public sealed class LocalFeedbackTests
    {
        private static readonly MethodInfo RouterTick =
            typeof(PlayerInputRouter).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);

        private static readonly MethodInfo HeavyTick =
            typeof(WeaponSystem).GetMethod("UpdateHeavyAttack", BindingFlags.Instance | BindingFlags.NonPublic);

        private GameObject[] spawned = new GameObject[0];

        [TearDown]
        public void Clear()
        {
            MobileInput.Reset();
            Time.timeScale = 1f;
            foreach (var go in spawned) if (go) Object.DestroyImmediate(go);
            spawned = new GameObject[0];
            var stop = GameObject.Find("Hitstop");
            if (stop) Object.DestroyImmediate(stop);
        }

        private GameObject Spawn(string name)
        {
            var go = new GameObject(name);
            spawned = spawned.Append(go).ToArray();
            return go;
        }

        // ── Der Quelltext: eine Tuer je Wirkung ────────────────────────────

        private static string WeaponSource()
            => File.ReadAllText(Path.Combine(Application.dataPath, "Shatterspire/Scripts/Combat/WeaponSystem.cs"));

        [TestCase("Hitstop.Freeze(")]
        [TestCase("CameraController.Impulse(")]
        [TestCase("Sfx.Play2D(")]
        [TestCase("PrototypeVfx.SpawnExplosion(")]
        [TestCase("PrototypeVfx.SpawnJudgement(")]
        public void TheWeaponHasExactlyOneDoorAndItChecksTheOwner(string call)
        {
            var lines = WeaponSource().Split('\n').Where(l => l.Contains(call)).ToArray();
            Assert.AreEqual(1, lines.Length,
                $"'{call}' steht {lines.Length}-mal im Waffensystem. Jeder Aufruf muss ueber die eine "
                + "Stelle laufen, die prueft, ob der Held diesem Geraet gehoert - sonst spuert der "
                + "Spieler die Schlaege seiner Begleiter.");
            Assert.IsTrue(lines[0].Contains("isLocal"),
                $"Die eine Stelle fuer '{call}' prueft den Besitzer nicht: {lines[0].Trim()}");
        }

        [Test]
        public void TheHudHearsOnlyTheOwnHero()
        {
            var body = Regex.Match(WeaponSource(),
                @"private void PublishHeavyState\(\)\s*\{(?<body>[^}]*)\}").Groups["body"].Value;
            Assert.IsTrue(body.Contains("if (!isLocal) return;"),
                "Der schwere Angriff meldet sich ans HUD, ohne zu pruefen, wessen er ist. Das Ereignis "
                + "traegt keinen Absender - der eigene Knopf zeigt dann die Ladung der Begleiter.");
        }

        // ── Das Verhalten ──────────────────────────────────────────────────

        private WeaponSystem Hero(string name, bool local)
        {
            var hero = Spawn(name);
            var build = hero.AddComponent<PlayerBuild>();
            var health = hero.AddComponent<Health>();
            hero.AddComponent<PlayerInputRouter>();
            var weapon = hero.AddComponent<WeaponSystem>();
            foreach (var component in new MonoBehaviour[] { health, build, weapon })
                component.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(component, null);
            health.Configure(TeamId.Player, 160f);
            // Erst ohne Besitz einrichten, damit keine Ringe und Linien gebaut werden - die braucht
            // hier niemand. Den Besitz danach setzen: fuer die Rueckmeldung zaehlt nur er.
            weapon.SetLocal(false);
            weapon.ConfigureClass(HeroClassId.Paladin);
            weapon.SetLocal(local);
            typeof(WeaponSystem).GetField("heavyMeter", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(weapon, 100f);
            return weapon;
        }

        private static int CountHeavyEvents(WeaponSystem weapon)
        {
            var count = 0;
            void Listen(float meter, float charge, bool charging, HeavyTiming timing) => count++;
            GameEvents.HeavyAttackChanged += Listen;
            try
            {
                MobileInput.SetHeavy(true);
                RouterTick.Invoke(weapon.GetComponent<PlayerInputRouter>(), null);
                HeavyTick.Invoke(weapon, null);
            }
            finally
            {
                GameEvents.HeavyAttackChanged -= Listen;
                MobileInput.Reset();
            }
            return count;
        }

        [Test]
        public void ACompanionsChargeNeverReachesTheHud()
        {
            var bot = Hero("XIRO (Team)", local: false);
            Assert.AreEqual(0, CountHeavyEvents(bot),
                "Die Ladung eines Begleiters ist beim HUD angekommen. Genau so lud und loeste der "
                + "eigene Knopf aus, ohne dass jemand drueckte.");
            Assert.IsTrue(bot.ChargingHeavy, "Der Aufbau stimmt nicht: der Begleiter laedt gar nicht.");
        }

        [Test]
        public void TheOwnChargeStillReachesTheHud()
        {
            // Die Gegenrichtung, damit der Test oben nicht einfach deshalb gruen ist, weil nie
            // irgendetwas ankommt.
            var own = Hero("XIRO", local: true);
            Assert.Greater(CountHeavyEvents(own), 0, "Die eigene Ladung muss im HUD ankommen.");
        }

        [Test]
        public void OnlyTheOwnCritFreezesTheWorld()
        {
            var enemy = Spawn("Crawler").AddComponent<Health>();
            enemy.Configure(TeamId.Enemy, 10000f);

            var bot = Spawn("BRAX (Team)");
            bot.AddComponent<PartyMember>().Configure("BRAX", Color.white, HeroClassId.Guardian,
                PartySlot.Frontline, local: false);
            var player = Spawn("REX");
            player.AddComponent<PartyMember>().Configure("REX", Color.white, HeroClassId.Ranger,
                PartySlot.Flank, local: true);

            Time.timeScale = 1f;
            enemy.TakeDamage(new DamageInfo(10f, DamageType.Physical, bot, Vector3.zero, Vector3.zero, true));
            Assert.AreEqual(1f, Time.timeScale, 0.0001f,
                "Ein Krit des Begleiters hat die ganze Welt angehalten - auch die eigene Figur.");

            enemy.TakeDamage(new DamageInfo(10f, DamageType.Physical, player, Vector3.zero, Vector3.zero, true));
            Assert.Less(Time.timeScale, 1f, "Ein eigener Krit soll weiter kurz einfrieren.");
        }

        [Test]
        public void HeroesAreToldApartFromTheWorld()
        {
            var bot = Spawn("ORION (Team)");
            bot.AddComponent<PartyMember>().Configure("ORION", Color.white, HeroClassId.Arcanist,
                PartySlot.Flank, local: false);
            var player = Spawn("KORR");
            player.AddComponent<PartyMember>().Configure("KORR", Color.white, HeroClassId.Bomber,
                PartySlot.Flank, local: true);
            var enemy = Spawn("Marksman");
            // Ein Geschoss haengt am Helden, der es geworfen hat - die Frage geht nach oben.
            var arrow = Spawn("Arrow");
            arrow.transform.SetParent(bot.transform);

            Assert.IsTrue(PartyMember.IsOtherHero(bot));
            Assert.IsTrue(PartyMember.IsOtherHero(arrow));
            Assert.IsFalse(PartyMember.IsOtherHero(player));
            Assert.IsTrue(PartyMember.IsLocalHero(player));
            Assert.IsFalse(PartyMember.IsLocalHero(bot));

            // Die Welt ist weder das eine noch das andere: ihre Stoesse bleiben, wie sie waren.
            Assert.IsFalse(PartyMember.IsOtherHero(enemy));
            Assert.IsFalse(PartyMember.IsLocalHero(enemy));
            Assert.IsFalse(PartyMember.IsOtherHero(null));
        }
    }
}
