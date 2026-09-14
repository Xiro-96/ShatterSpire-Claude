using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Shatterspire.Editor
{
    /// <summary>
    /// Vermisst die Kampfclips: wie gross ist der Schlag, und wann trifft er?
    ///
    /// Ohne diese Zahlen ist jedes Zeitfenster geraten. Der Bericht faehrt jeden Clip auf dem
    /// Ritter-Rig ab und verfolgt dabei nicht die Hand, sondern einen Punkt auf der Klinge - beim
    /// Zweihaender legt die Spitze ein Vielfaches der Hand zurueck, und sie ist das, was man beim
    /// Schlag sieht. Gemeldet werden der Moment des Treffers, der Abschnitt, ueber den der Schwung
    /// laeuft, und vor allem die Strecke, die die Klinge insgesamt macht: ein Clip, dessen Klinge
    /// kaum Weg zurueckleg,t bleibt unscheinbar, egal wie er abgespielt wird.
    /// </summary>
    public static class ClipReport
    {
        private const string Rig = "Art3D/KayKit/Characters/Knight";

        /// <summary>Wie weit der gemessene Punkt vom Griff entfernt auf der Klinge liegt.</summary>
        private const float BladeReach = 1.5f;

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
            "Melee_Dualwield_Attack_Slice", "Melee_Dualwield_Attack_Chop",
            "Melee_Block_Attack", "Melee_Unarmed_Attack_Kick",
            "Ranged_Bow_Draw", "Ranged_Bow_Release", "Ranged_1H_Shoot", "Ranged_Magic_Shoot",
            "Throw", "Use_Item"
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
            // Die Waffe haengt am Griffpunkt handslot.r und zeigt von dort nach oben.
            var grip = FindBone(rig.transform, "handslot.r");
            if (!grip && animator && animator.isHuman) grip = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (!grip)
            {
                Debug.LogError("SHATTERSPIRE ClipReport: kein Griffpunkt am Rig.");
                Object.DestroyImmediate(rig);
                return;
            }

            var report = new StringBuilder("SHATTERSPIRE Kampfclips - gemessen an der Klinge\n");
            report.AppendLine("Clip                               Laenge  Treffer   Schwung   Spitze    Weg");
            foreach (var name in Wanted)
            {
                if (!clips.TryGetValue(name, out var clip)) { report.AppendLine($"{name,-34} fehlt"); continue; }
                report.AppendLine(Line(name, clip, rig, grip));
            }
            Object.DestroyImmediate(rig);
            Debug.Log(report.ToString());
        }

        /// <summary>Sucht einen Knochen ueber seinen Namen, auch wenn er nicht zum Humanoid-Satz gehoert.</summary>
        private static Transform FindBone(Transform root, string name)
        {
            foreach (var candidate in root.GetComponentsInChildren<Transform>(true))
                if (candidate.name.ToLowerInvariant() == name) return candidate;
            return null;
        }

        private static string Line(string name, AnimationClip clip, GameObject rig, Transform grip)
        {
            const int Samples = 120;
            var speeds = new float[Samples];
            var previous = Vector3.zero;
            var travelled = 0f;
            for (var i = 0; i < Samples; i++)
            {
                var t = clip.length * i / (Samples - 1f);
                clip.SampleAnimation(rig, t);
                var world = grip.position + grip.rotation * Vector3.up * BladeReach;
                var point = rig.transform.InverseTransformPoint(world);
                if (i > 0)
                {
                    var step = (point - previous).magnitude;
                    travelled += step;
                    speeds[i] = step * (Samples - 1f) / clip.length;
                }
                previous = point;
            }
            speeds[0] = speeds[1];

            var peak = 0f;
            var peakAt = 0;
            for (var i = 0; i < Samples; i++)
                if (speeds[i] > peak) { peak = speeds[i]; peakAt = i; }

            // Der Schwung ist der zusammenhaengende Abschnitt um die Spitze, in dem die Klinge noch
            // mindestens ein Drittel so schnell ist. Davor ist Ausholen, danach Nachschwingen.
            var gate = peak * 0.34f;
            int from = peakAt, to = peakAt;
            while (from > 0 && speeds[from - 1] >= gate) from--;
            while (to < Samples - 1 && speeds[to + 1] >= gate) to++;

            var hit = peakAt / (Samples - 1f);
            return $"{name,-34} {clip.length,5:0.00}s  {hit * clip.length,5:0.00}s  "
                   + $"{from / (Samples - 1f),4:0.00}-{to / (Samples - 1f),4:0.00}  "
                   + $"{peak,5:0.0}/s  {travelled,6:0.0}";
        }
    }
}
