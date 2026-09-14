using NUnit.Framework;
using UnityEngine;

namespace Shatterspire.Tests
{
    /// <summary>
    /// Ein Schlag muss als Schlag zu sehen sein.
    ///
    /// Der Anlass: "der Char soll seine Waffe animiert schwingen, das sieht sonst super schlecht
    /// aus". Die Ursache war messbar. Jeder Kampfclip lief von 0 bis 0,92 - also einschliesslich
    /// des langen Ausholens - und wurde in die kurze Aktionsdauer gequetscht. Melee_2H_Attack_Chop
    /// ist 1,63 s lang und bekam 0,5 s: 2,8-faches Tempo, und weil 2,8 die Obergrenze ist, brach der
    /// Clip zusaetzlich bei 86 Prozent ab. Der Wirbel war bei 64 Prozent zu Ende.
    ///
    /// Niemand hat das gemeldet, weil nirgends eine Grenze stand. Diese Tests ziehen sie ein: mit
    /// den echten Clips aus dem Projekt nachgerechnet, was am Ende wirklich abgespielt wird.
    /// </summary>
    public sealed class SwingTimingTests
    {
        private const string Melee = "Art3D/KayKit/Animations/Rig_Medium_CombatMelee";
        private const string Ranged = "Art3D/KayKit/Animations/Rig_Medium_CombatRanged";

        /// <summary>
        /// Die jeweils erste Wahl aus ChampionAnimationDriver.ClipFor. Aendert sie sich dort,
        /// gehoert die Aenderung hierher.
        /// </summary>
        private static readonly (AttackMotion Motion, string Library, string Clip)[] Combat =
        {
            (AttackMotion.Swing, Melee, "Melee_2H_Attack_Slice"),
            (AttackMotion.Smash, Melee, "Melee_2H_Attack_Chop"),
            (AttackMotion.Spin, Melee, "Melee_2H_Attack_Spin"),
            (AttackMotion.Stab, Melee, "Melee_Block_Attack"),
            (AttackMotion.Leap, Melee, "Melee_1H_Attack_Jump_Chop"),
            (AttackMotion.Shot, Ranged, "Ranged_1H_Shoot"),
            (AttackMotion.Cast, Ranged, "Ranged_Magic_Shoot"),
            (AttackMotion.Release, Ranged, "Ranged_Bow_Release"),
            (AttackMotion.Draw, Ranged, "Ranged_Bow_Draw")
        };

        private static AnimationClip Load(string library, string name)
        {
            foreach (var clip in Resources.LoadAll<AnimationClip>(library))
                if (clip.name == name) return clip;
            return null;
        }

        /// <summary>
        /// Ab etwa dem Anderthalbfachen wird aus einem Schwung ein Zucken. Unter 0,6 wirkt er zaeh.
        /// Dazwischen liest er sich als das, was er ist.
        /// </summary>
        [Test]
        public void KampfclipsLaufenInLesbaremTempo()
        {
            foreach (var (motion, library, name) in Combat)
            {
                var clip = Load(library, name);
                if (!clip) { Assert.Ignore($"{name} nicht im Projekt."); return; }
                var window = ChampionAnimationDriver.WindowFor(motion);
                var span = clip.length * (window.To - window.From);
                var speed = span / ChampionAnimationDriver.DurationFor(motion, true);
                Assert.That(speed, Is.InRange(0.6f, 1.6f),
                    $"{motion} auf {name} laeuft mit {speed:0.00}-fachem Tempo.");
            }
        }

        /// <summary>Wird das Fenster jemals kuerzer als die Aktion, schneidet der Clip ab statt zu enden.</summary>
        [Test]
        public void KeinKampfclipWirdAbgeschnitten()
        {
            foreach (var (motion, library, name) in Combat)
            {
                var clip = Load(library, name);
                if (!clip) { Assert.Ignore($"{name} nicht im Projekt."); return; }
                var window = ChampionAnimationDriver.WindowFor(motion);
                var span = clip.length * (window.To - window.From);
                var speed = Mathf.Clamp(span / ChampionAnimationDriver.DurationFor(motion, true), 0.5f, 2.8f);
                var played = ChampionAnimationDriver.DurationFor(motion, true) * speed;
                Assert.That(played, Is.GreaterThanOrEqualTo(span - 0.01f),
                    $"{motion} spielt nur {played:0.00}s von {span:0.00}s des Fensters.");
            }
        }

        /// <summary>
        /// Der Schritt in den Schlag darf nie ueber das Ziel hinausfuehren. Ein falsches Vorzeichen
        /// oder eine fehlende Grenze ergaebe einen Helden, der bei jedem Hieb in den Gegner
        /// hineinlaeuft - oder quer durch den Raum springt.
        /// </summary>
        [Test]
        public void DerSchrittFuehrtNieZuNahHeran()
        {
            for (var distance = 0f; distance <= 20f; distance += 0.1f)
            {
                var step = MeleeApproach.StepDistance(distance);
                Assert.That(step, Is.GreaterThanOrEqualTo(0f), $"Rueckwaerts bei {distance:0.0}.");
                Assert.That(step, Is.LessThanOrEqualTo(MeleeApproach.MaxStep), $"Zu weit bei {distance:0.0}.");
                if (step > 0f)
                    Assert.That(distance - step, Is.GreaterThanOrEqualTo(MeleeApproach.IdealGap - 0.001f),
                        $"Aus {distance:0.0} wird {distance - step:0.00} - naeher als Schlagweite.");
            }
        }

        /// <summary>
        /// Der Schritt darf kein Dash werden. Genau das war zu sehen: bei manchen normalen Hieben
        /// schoss der Held auf sein Ziel zu. 1,7 Einheiten in den 0,13 s Ausholzeit eines Hiebs sind
        /// 13 Einheiten je Sekunde - doppeltes Lauftempo.
        /// </summary>
        [Test]
        public void DerSchrittBleibtEinSchrittUndWirdKeinSatz()
        {
            for (var seconds = 0.08f; seconds <= 0.40f; seconds += 0.02f)
            for (var distance = 0f; distance <= 12f; distance += 0.2f)
            {
                var step = MeleeApproach.StepDistance(distance, seconds);
                Assert.That(step / seconds, Is.LessThanOrEqualTo(MeleeApproach.MaxStepSpeed + 0.001f),
                    $"{distance:0.0} Einheiten in {seconds:0.00}s waeren {step / seconds:0.0}/s.");
                Assert.That(step, Is.LessThanOrEqualTo(MeleeApproach.StepDistance(distance) + 0.001f));
            }
        }

        /// <summary>
        /// Die Zielhilfe erfasst bis 15 Einheiten weit. So weit darf der Schritt nicht folgen - er
        /// wuerde nichts schliessen und nur aussehen wie ein Sprung.
        /// </summary>
        [Test]
        public void WeitEntfernteZieleWerdenNichtAngegangen()
        {
            Assert.That(MeleeApproach.StepDistance(MeleeApproach.MaxEngage + 0.1f), Is.EqualTo(0f));
            Assert.That(MeleeApproach.StepDistance(15f), Is.EqualTo(0f));
            Assert.That(MeleeApproach.StepDistance(MeleeApproach.MaxEngage), Is.GreaterThan(0f));
        }

        /// <summary>Wer schon auf Schlagweite steht, soll nicht noch geschoben werden.</summary>
        [Test]
        public void AufSchlagweiteWirdNichtNachgesetzt()
        {
            Assert.That(MeleeApproach.StepDistance(MeleeApproach.IdealGap), Is.EqualTo(0f));
            Assert.That(MeleeApproach.StepDistance(0.8f), Is.EqualTo(0f));
            // Am Rand der Reichweite ist der Schritt am groessten - darueber hinaus gar nicht mehr.
            Assert.That(MeleeApproach.StepDistance(MeleeApproach.MaxEngage), Is.EqualTo(MeleeApproach.MaxStep));
        }

        /// <summary>Der Treffer muss im gespielten Abschnitt liegen - sonst faellt Schaden ausserhalb des Schwungs.</summary>
        [Test]
        public void DerTrefferLiegtImFenster()
        {
            foreach (var (motion, _, _) in Combat)
            {
                var window = ChampionAnimationDriver.WindowFor(motion);
                Assert.That(window.Strike, Is.InRange(window.From, window.To),
                    $"{motion}: Treffer bei {window.Strike:0.000} liegt nicht in "
                    + $"{window.From:0.00}-{window.To:0.00}.");
            }
        }
    }
}
