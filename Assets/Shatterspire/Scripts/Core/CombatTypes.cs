using System;
using UnityEngine;

namespace Shatterspire
{
    public enum DamageType { Physical, Fire, Ice, Lightning, Poison, Void, Holy, True }
    public enum TeamId { Player, Enemy }
    public enum EnemyKind { Crawler, Shooter, Brute, Elite, IronWarden }
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
