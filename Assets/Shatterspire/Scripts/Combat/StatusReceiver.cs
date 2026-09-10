using System.Collections.Generic;
using UnityEngine;

namespace Shatterspire
{
    [RequireComponent(typeof(Health))]
    public sealed class StatusReceiver : MonoBehaviour
    {
        private Health health;
        private readonly List<Dot> dots = new();
        private float slowMultiplier = 1f;
        private float slowUntil;
        public float SpeedMultiplier => Time.time < slowUntil ? slowMultiplier : 1f;

        private struct Dot { public DamageType type; public float damage; public float nextTick; public float end; public GameObject source; }
        private void Awake() => health = GetComponent<Health>();

        public void ApplyBurn(float dps, float duration, GameObject source) => AddDot(DamageType.Fire, dps, duration, source);
        public void ApplyPoison(float dps, float duration, GameObject source) => AddDot(DamageType.Poison, dps, duration, source);
        public void ApplySlow(float multiplier, float duration)
        {
            slowMultiplier = Mathf.Min(slowMultiplier, Mathf.Clamp(multiplier, 0.15f, 1f));
            slowUntil = Mathf.Max(slowUntil, Time.time + duration);
        }

        private void AddDot(DamageType type, float dps, float duration, GameObject source)
            => dots.Add(new Dot { type = type, damage = dps * 0.5f, nextTick = Time.time + 0.5f, end = Time.time + duration, source = source });

        private void Update()
        {
            if (!health.IsAlive) { dots.Clear(); return; }
            for (var i = dots.Count - 1; i >= 0; i--)
            {
                var dot = dots[i];
                if (Time.time >= dot.end) { dots.RemoveAt(i); continue; }
                if (Time.time < dot.nextTick) continue;
                health.TakeDamage(new DamageInfo(dot.damage, dot.type, dot.source, transform.position + Vector3.up, Vector3.zero));
                dot.nextTick += 0.5f;
                dots[i] = dot;
            }
            if (Time.time >= slowUntil) slowMultiplier = 1f;
        }
    }
}
