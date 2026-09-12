using System;
using UnityEngine;

namespace Shatterspire
{
    /// <summary>
    /// Die Abschussserie eines Aufstiegs und der Punktemultiplikator, der daran haengt.
    ///
    /// Zweck: Punkte gab es bisher nur am Ende und nur fuer Etagen, Bosse, Gegner und Splitter. Damit
    /// war jeder Abschuss gleich viel wert, egal ob man sauber durch eine Gruppe geht oder einzeln
    /// abarbeitet. Die Serie macht die Art, wie man kaempft, zum ersten Mal sichtbar - und weil der
    /// Multiplikator in die Endpunktzahl laeuft, zaehlt sie auch fuer den Rang.
    ///
    /// Haengt am Spieler und nicht in einer statischen Ablage: im Co-op fuehrt jeder seine eigene
    /// Serie, siehe die Regel gegen statischen Spielzustand.
    /// </summary>
    public sealed class KillStreak : MonoBehaviour
    {
        /// <summary>So lange bleibt die Serie nach einem Abschuss stehen.</summary>
        public const float WindowSeconds = 3.5f;
        /// <summary>Die Stufen, an denen der Multiplikator springt.</summary>
        private static readonly int[] Steps = { 5, 10, 20, 30, 45 };
        private static readonly float[] Multipliers = { 1.2f, 1.5f, 2f, 2.5f, 3f };

        private float expiresAt;

        /// <summary>Laufende Serie. 0, wenn keine laeuft.</summary>
        public int Count { get; private set; }

        /// <summary>Hoechste Serie dieses Aufstiegs, fuer den Endbildschirm.</summary>
        public int Best { get; private set; }

        /// <summary>Punkte aus der Serie, die zur Endpunktzahl dazukommen.</summary>
        public int Bonus { get; private set; }

        /// <summary>Anteil der verbleibenden Zeit, 1 direkt nach einem Abschuss.</summary>
        public float Remaining => Count <= 0 ? 0f : Mathf.Clamp01((expiresAt - Time.time) / WindowSeconds);

        /// <summary>Aktueller Multiplikator auf die Punkte je Abschuss.</summary>
        public float Multiplier => MultiplierFor(Count);

        /// <summary>Wird ausgeloest, wenn die Serie eine neue Stufe erreicht - fuer Bild und Ton.</summary>
        public event Action<int, float> StepReached;

        public static float MultiplierFor(int count)
        {
            var multiplier = 1f;
            for (var i = 0; i < Steps.Length; i++)
                if (count >= Steps[i]) multiplier = Multipliers[i];
            return multiplier;
        }

        /// <summary>Ein Gegner ist gefallen.</summary>
        public void Register()
        {
            var before = Multiplier;
            Count++;
            Best = Mathf.Max(Best, Count);
            expiresAt = Time.time + WindowSeconds;
            // Punkte je Abschuss wie in der Endabrechnung, aber mit dem Multiplikator der Serie.
            // Nur der Zuschlag wird gesammelt; die Grundpunkte rechnet ClimbScore weiterhin selbst.
            Bonus += Mathf.RoundToInt(ClimbScore.PerEnemy * (Multiplier - 1f));
            if (Multiplier > before) StepReached?.Invoke(Count, Multiplier);
        }

        private void Update()
        {
            if (Count <= 0 || Time.time < expiresAt) return;
            Count = 0;
        }
    }
}
