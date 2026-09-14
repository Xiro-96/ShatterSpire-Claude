using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Shatterspire.Editor
{
    /// <summary>
    /// Vermisst die Kampfclips: wo im Clip liegt der Schlag?
    ///
    /// Ohne diese Zahlen ist jedes Zeitfenster geraten. Der Bericht faehrt jeden Clip auf dem
    /// Ritter-Rig ab, verfolgt die Waffenhand und meldet, wann sie am schnellsten ist - das ist der
    /// Moment des Treffers - und ueber welchen Abschnitt des Clips der Schlag ueberhaupt laeuft.
    /// Daraus ergeben sich sowohl das Abspielfenster als auch der Zeitpunkt, zu dem der Schaden
    /// fallen sollte.
    /// </summary>
    public static class ClipReport
    {
        private const string Rig = "Art3D/KayKit/Characters/Knight";

        private static readonly string[] Folders =
        {
            "Art3D/KayKit/Animations/Rig_Medium_CombatMelee",
            "Art3D/KayKit/Animations/Rig_Medium_CombatRanged",
            "Art3D/KayKit/Animations/Rig_Medium_General"
        };

        private static readonly string[] Wanted =
        {
            "Melee_2H_Attack_Slice", "Melee_2H_Attack_Chop", "Melee_2H_Attack_Spin",
            "Melee_2H_Attack_Spinning", "Melee_2H_Attack_Stab",
            "Melee_1H_Attack_Slice_Horizontal", "Melee_1H_Attack_Slice_Diagonal",
            "Melee_1H_Attack_Chop", "Melee_1H_Attack_Stab", "Melee_1H_Attack_Jump_Chop",
            "Melee_Block_Attack", "Ranged_Bow_Draw", "Ranged_Bow_Release",
            "Ranged_1H_Shoot", "Ranged_Magic_Shoot", "Throw", "Use_Item"
        };

        [MenuItem("SHATTERSPIRE/Kampfclips vermessen")]
        public static void Measure()
        {
            var source = Resources.Load<GameObject>(Rig);
            if (!source)
            {
                Debug.LogError($"SHATTERSPIRE ClipReport: Rig {Rig} nicht gefunden.");
                return;
            }

            var clips = new Dictionary<string, AnimationClip>();
            foreach (var folder in Folders)
                foreach (var clip in Resources.LoadAll<AnimationClip>(folder))
                    clips[clip.name] = clip;

            var rig = Object.Instantiate(source);
            rig.hideFlags = HideFlags.HideAndDontSave;
            var animator = rig.GetComponent<Animator>();
            var hand = animator && animator.isHuman
                ? animator.GetBoneTransform(HumanBodyBones.RightHand)
                : null;
            if (!hand)
            {
                Debug.LogError("SHATTERSPIRE ClipReport: keine rechte Hand am Rig.");
                Object.DestroyImmediate(rig);
                return;
            }

            var report = new StringBuilder("SHATTERSPIRE Kampfclips - Hand am schnellsten = Treffer\n");
            report.AppendLine("Clip                               Laenge  Treffer   Schwung      Spitze");
            foreach (var name in Wanted)
            {
                if (!clips.TryGetValue(name, out var clip)) { report.AppendLine($"{name,-34} fehlt"); continue; }
                report.AppendLine(Line(name, clip, rig, hand));
            }
            Object.DestroyImmediate(rig);
            Debug.Log(report.ToString());
        }

        /// <summary>Eine Zeile: Laenge, Trefferzeitpunkt, Schwungfenster, Spitzengeschwindigkeit.</summary>
        private static string Line(string name, AnimationClip clip, GameObject rig, Transform hand)
        {
            const int Samples = 120;
            var speeds = new float[Samples];
            var previous = Vector3.zero;
            for (var i = 0; i < Samples; i++)
            {
                var t = clip.length * i / (Samples - 1f);
                clip.SampleAnimation(rig, t);
                var point = rig.transform.InverseTransformPoint(hand.position);
                if (i > 0) speeds[i] = (point - previous).magnitude * (Samples - 1f) / clip.length;
                previous = point;
            }
            speeds[0] = speeds[1];

            var peak = 0f;
            var peakAt = 0;
            for (var i = 0; i < Samples; i++)
                if (speeds[i] > peak) { peak = speeds[i]; peakAt = i; }

            // Der Schwung ist der zusammenhaengende Abschnitt um die Spitze, in dem die Hand noch
            // mindestens ein Drittel so schnell ist. Davor ist Ausholen, danach Nachschwingen.
            var gate = peak * 0.34f;
            int from = peakAt, to = peakAt;
            while (from > 0 && speeds[from - 1] >= gate) from--;
            while (to < Samples - 1 && speeds[to + 1] >= gate) to++;

            var hit = peakAt / (Samples - 1f);
            return $"{name,-34} {clip.length,5:0.00}s  {hit * clip.length,5:0.00}s  "
                   + $"{from / (Samples - 1f),4:0.00}-{to / (Samples - 1f),4:0.00}  {peak,5:0.0}/s";
        }
    }
}
