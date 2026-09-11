using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Shatterspire
{
    [DisallowMultipleComponent]
    public sealed class Health : MonoBehaviour, IDamageable
    {
        private static readonly List<Health> ActiveInstances = new();

        [SerializeField] private TeamId team;
        [SerializeField, Min(1f)] private float maximum = 100f;
        private float current;
        private float invulnerableUntil;
        private Renderer[] renderers;
        private Color[] baseColors;

        public event Action<DamageInfo> Damaged;
        public event Action Died;
        public bool IsAlive => current > 0f;
        public TeamId Team => team;
        public float Current => current;
        public float Maximum => maximum;
        public float Normalized => maximum <= 0f ? 0f : current / maximum;
        public static IReadOnlyList<Health> Active => ActiveInstances;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => ActiveInstances.Clear();

        private void OnEnable()
        {
            if (!ActiveInstances.Contains(this)) ActiveInstances.Add(this);
        }

        private void OnDisable() => ActiveInstances.Remove(this);

        public void Configure(TeamId value, float maxHealth)
        {
            team = value;
            maximum = Mathf.Max(1f, maxHealth);
            current = maximum;
            CacheRenderers();
            GameEvents.RaiseHealthChanged(this);
        }

        private void Awake()
        {
            current = maximum;
            CacheRenderers();
        }

        private void CacheRenderers()
        {
            renderers = GetComponentsInChildren<Renderer>();
            baseColors = new Color[renderers.Length];
            for (var i = 0; i < renderers.Length; i++)
                baseColors[i] = renderers[i].material.color;
        }

        public void SetInvulnerable(float seconds) => invulnerableUntil = Mathf.Max(invulnerableUntil, Time.time + seconds);

        public void TakeDamage(in DamageInfo damage)
        {
            if (!IsAlive || Time.time < invulnerableUntil || damage.Amount <= 0f) return;
            current = Mathf.Max(0f, current - damage.Amount);
            Damaged?.Invoke(damage);
            GetComponent<StylizedCharacterMotion>()?.PulseHit();
            GameEvents.RaiseHealthChanged(this);
            DamageNumber.Spawn(damage.HitPoint, damage.Amount, damage.IsCritical, damage.Type);
            if (damage.Force.sqrMagnitude > 0.01f || damage.IsCritical)
                PrototypeVfx.SpawnHit(damage.HitPoint, damage.Force, damage.Type, damage.IsCritical);
            // Nur bei kritischen Treffern auf Gegner. Ein Stop bei jedem Schaden
            // waere Dauerzeitlupe, und Treffer am Spieler sollen nicht belohnen.
            if (damage.IsCritical && team == TeamId.Enemy) Hitstop.Freeze(0.045f, 0.08f);
            if (isActiveAndEnabled) StartCoroutine(Flash());
            if (current <= 0f)
            {
                Died?.Invoke();
                GameEvents.RaiseEntityDied(this);
            }
        }

        public void Heal(float amount)
        {
            if (!IsAlive || amount <= 0f) return;
            current = Mathf.Min(maximum, current + amount);
            GameEvents.RaiseHealthChanged(this);
        }

        public void Revive(float normalizedHealth)
        {
            if (IsAlive) return;
            current = Mathf.Clamp(maximum * normalizedHealth, 1f, maximum);
            SetInvulnerable(1.75f);
            GameEvents.RaiseHealthChanged(this);
        }

        public void IncreaseMaximum(float amount, bool healDifference)
        {
            maximum = Mathf.Max(1f, maximum + amount);
            if (healDifference) current = Mathf.Min(maximum, current + amount);
            GameEvents.RaiseHealthChanged(this);
        }

        private IEnumerator Flash()
        {
            for (var i = 0; i < renderers.Length; i++)
                if (renderers[i]) renderers[i].material.color = Color.white;
            yield return new WaitForSecondsRealtime(0.055f);
            for (var i = 0; i < renderers.Length; i++)
                if (renderers[i]) renderers[i].material.color = baseColors[i];
        }
    }
}
