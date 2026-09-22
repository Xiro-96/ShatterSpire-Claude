using System;
using UnityEngine;

namespace Shatterspire
{
    public enum DamageType { Physical, Fire, Ice, Lightning, Poison, Void, Holy, True }
    public enum TeamId { Player, Enemy }
    // Shieldbearer und Marksman kommen ans Ende, damit bestehende serialisierte
    // Werte ihre Zahl behalten.
    // Neue Arten kommen ans Ende, damit gespeicherte Zahlen ihre Bedeutung behalten.
    public enum EnemyKind
    {
        Crawler, Shooter, Brute, Elite, IronWarden, Shieldbearer, Marksman,
        // Die zwei Waechter vom 13.09.
        RiftTwin, ChoirWarden
    }

    public static class EnemyKinds
    {
        /// <summary>
        /// Ist diese Art ein Waechter einer Boss-Etage?
        ///
        /// Frueher stand an rund zwanzig Stellen "kind == IronWarden", und gemeint war meistens
        /// nicht dieser eine Gegner, sondern "ist ein Boss": Groesse, Kollider, keine Leine, eigene
        /// Bewegung, eigene Lebensleiste. Beim zweiten Waechter waeren alle zwanzig einzeln
        /// nachzutragen gewesen - und eine davon haette gefehlt.
        /// </summary>
        public static bool IsBoss(EnemyKind kind)
            => kind is EnemyKind.IronWarden or EnemyKind.RiftTwin or EnemyKind.ChoirWarden;

        /// <summary>Ab diesem Abstand beginnt ein Waechter ein Muster.</summary>
        public const float BossEngageReach = 9f;

        /// <summary>
        /// Ab welchem Abstand ein Gegner einen Angriff beginnt. Fuer die meisten die Reichweite ihres
        /// Schlags. Die Waechter haben Muster, die weiter reichen: der Warden schlaegt auf die Stelle
        /// des Ziels, zieht eine Linie von 10 Einheiten und wirft einen Ring aus Geschossen; der
        /// Zwilling springt; der Chorwaechter ruft seinen Chor.
        ///
        /// Vorher begannen sie trotzdem erst in Nahkampfreichweite (2,2 bis 3,2). Der Selbsttest
        /// zeigte es: REX besiegte den Warden zweimal in 17 und 24 Sekunden, ohne ein einziges Mal
        /// getroffen zu werden - wer auf Abstand blieb, bekam keines der Muster je zu sehen, und der
        /// Chorwaechter rief gegen eine Gruppe aus Fernkaempfern nie.
        /// </summary>
        public static float EngageReach(EnemyKind kind, float attackRange)
            => IsBoss(kind) ? Math.Max(attackRange, BossEngageReach) : attackRange;
    }
    public enum RoomKind { Combat, Elite, Treasure, Mystery, Boss }

    [Serializable]
    public readonly struct DamageInfo
    {
        public readonly float Amount;
        public readonly DamageType Type;
        public readonly GameObject Source;
        public readonly Vector3 HitPoint;
        public readonly Vector3 Force;
        public readonly bool IsCritical;

        public DamageInfo(float amount, DamageType type, GameObject source, Vector3 hitPoint,
            Vector3 force, bool isCritical = false)
        {
            Amount = amount;
            Type = type;
            Source = source;
            HitPoint = hitPoint;
            Force = force;
            IsCritical = isCritical;
        }
    }

    public interface IDamageable
    {
        bool IsAlive { get; }
        TeamId Team { get; }
        void TakeDamage(in DamageInfo damage);
    }

    public interface IExperienceReceiver
    {
        void AddExperience(int amount);
    }
}
