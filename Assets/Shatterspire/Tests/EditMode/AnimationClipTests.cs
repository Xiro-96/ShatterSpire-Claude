using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace Shatterspire.Tests
{
    /// <summary>
    /// Der Animations-Driver sucht Clips ueber ihren Namen. Findet er keinen, ist
    /// die betroffene Aktion stumm — ohne Fehler, ohne Warnung, ohne dass es beim
    /// Spielen als Fehler erkennbar waere. Genau so waren Angriff, Dash und
    /// Ultimate lange unbemerkt unanimiert, weil die gesuchten Namen
    /// (Melee_Hook, Sword_Dash, Shield_OneShot) im Projekt nie existiert haben.
    ///
    /// Dieser Test macht daraus einen roten Build statt einer stillen Luecke.
    /// </summary>
    public sealed class AnimationClipTests
    {
        private static readonly string[] Libraries =
        {
            "Art3D/KayKit/Animations/Rig_Medium_General",
            "Art3D/KayKit/Animations/Rig_Medium_MovementBasic",
            "Art3D/Animations/UAL2_Standard",
            "Art3D/KayKit/Animations/Rig_Medium_CombatMelee",
            "Art3D/KayKit/Animations/Rig_Medium_CombatRanged",
            "Art3D/KayKit/Animations/Rig_Medium_MovementAdvanced"
        };

        /// <summary>
        /// Muss mit den Kandidatenlisten in ChampionAnimationDriver.Configure
        /// uebereinstimmen. Aendert sich dort etwas, gehoert es hierher.
        /// </summary>
        private static readonly (string State, string[] Candidates)[] Required =
        {
            ("Idle", new[] { "Idle_A", "Idle_B", "Idle_No_Loop" }),
            ("Laufen", new[] { "Running_A", "Running_B", "Walking_A" }),
            ("Angriff", new[] { "Throw", "Use_Item", "Interact" }),
            // Kampfclips ohne Ersatz in der Liste: faellt der Import aus, soll der Test rot werden.
            ("Hammer quer", new[] { "Melee_2H_Attack_Slice", "Melee_1H_Attack_Slice_Horizontal" }),
            ("Hammer Wucht", new[] { "Melee_2H_Attack_Chop", "Melee_1H_Attack_Chop" }),
            ("Wirbel", new[] { "Melee_2H_Attack_Spin", "Melee_2H_Attack_Spinning" }),
            ("Schuss", new[] { "Ranged_1H_Shoot", "Ranged_2H_Shoot" }),
            ("Zauber", new[] { "Ranged_Magic_Shoot", "Ranged_Magic_Spellcasting" }),
            ("Ausweichen", new[] { "Dodge_Forward" }),
            ("Dash", new[] { "Jump_Start", "Jump_Full_Short", "Jump_Full_Long" }),
            ("Ultimate", new[] { "Spawn_Ground", "Spawn_Air", "Throw" }),
            ("Trefferreaktion", new[] { "Hit_A", "Hit_B", "Hit_Knockback" })
        };

        private static HashSet<string> LoadClipNames()
        {
            var names = new HashSet<string>();
            foreach (var library in Libraries)
            foreach (var clip in Resources.LoadAll<AnimationClip>(library))
            {
                if (!clip) continue;
                var name = clip.name;
                // Unity haengt bei FBX-Takes gelegentlich "Rig|Take" davor.
                var separator = name.LastIndexOf('|');
                names.Add(separator >= 0 ? name.Substring(separator + 1) : name);
            }
            return names;
        }

        [Test]
        public void AnimationsbibliothekenSindUeberhauptLadbar()
        {
            var names = LoadClipNames();
            Assert.That(names, Is.Not.Empty,
                "Aus Resources kam kein einziger AnimationClip. Entweder fehlen die FBX, " +
                "oder ihre Import-Einstellung hat keine Animation aktiviert.");
        }

        [Test]
        public void JederAnimationszustandFindetMindestensEinenClip()
        {
            var names = LoadClipNames();
            if (names.Count == 0) Assert.Ignore("Keine Clips ladbar, siehe vorheriger Test.");

            var missing = Required
                .Where(entry => !entry.Candidates.Any(names.Contains))
                .Select(entry => $"{entry.State} (gesucht: {string.Join(", ", entry.Candidates)})")
                .ToList();

            Assert.That(missing, Is.Empty,
                "Fuer diese Zustaende existiert kein einziger Clip, sie bleiben im Spiel " +
                "unanimiert:\n  " + string.Join("\n  ", missing) +
                "\n\nVorhandene Clips:\n  " + string.Join(", ", names.OrderBy(n => n)));
        }
    }
}
