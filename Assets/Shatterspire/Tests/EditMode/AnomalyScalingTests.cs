using NUnit.Framework;
using UnityEngine;

namespace Shatterspire.Tests
{
    /// <summary>
    /// Die Anomalie skaliert Gegnerleben ueber <see cref="Health.IncreaseMaximum"/> mit einem
    /// negativen Betrag - dieselbe Form, in der die Etagenkurve darueber ihren Zuschlag gibt.
    ///
    /// Das ist die nicht offensichtliche Stelle: die Methode heisst "Increase" und wird hier zum
    /// Verkleinern benutzt. Wuerde sie das Maximum senken, ohne den Stand mitzuziehen, startete
    /// jeder Gegner im Glasbruch mit einem Lebensbalken ueber hundert Prozent.
    /// </summary>
    public sealed class AnomalyScalingTests
    {
        private static Health NewHealth(float maximum)
        {
            var health = new GameObject("Health Probe").AddComponent<Health>();
            health.Configure(TeamId.Enemy, maximum);
            return health;
        }

        [TearDown]
        public void Aufraeumen()
        {
            foreach (var probe in Object.FindObjectsByType<Health>(FindObjectsSortMode.None))
                if (probe && probe.name == "Health Probe")
                    Object.DestroyImmediate(probe.gameObject);
        }

        [Test]
        public void HalbesLebenSenktMaximumUndStandGemeinsam()
        {
            var health = NewHealth(100f);
            health.IncreaseMaximum(health.Maximum * (0.5f - 1f), true);
            Assert.That(health.Maximum, Is.EqualTo(50f).Within(0.001f));
            Assert.That(health.Current, Is.EqualTo(50f).Within(0.001f));
            Assert.That(health.Normalized, Is.EqualTo(1f).Within(0.001f),
                "Ein frisch gesetzter Gegner muss auch nach der Anomalie voll sein.");
        }

        [Test]
        public void MehrLebenHeiltDenZuschlagMit()
        {
            var health = NewHealth(100f);
            health.IncreaseMaximum(health.Maximum * (1.5f - 1f), true);
            Assert.That(health.Maximum, Is.EqualTo(150f).Within(0.001f));
            Assert.That(health.Normalized, Is.EqualTo(1f).Within(0.001f));
        }

        [Test]
        public void EtagenkurveUndAnomalieMultiplizierenSichNicht()
        {
            // Die Anomalie greift nach der Tiefenskalierung an, aber auf den dann geltenden Wert.
            // Angekuendigt ist "halbes Leben" - und halb muss es auf jeder Etage sein, nicht auf
            // Etage 2 halb und auf Etage 14 ein Achtel.
            var floorScale = 1f + 13f * EnemyBalance.HealthPerFloor;
            var health = NewHealth(100f);
            health.IncreaseMaximum(health.Maximum * (floorScale - 1f), true);
            var afterFloor = health.Maximum;
            health.IncreaseMaximum(health.Maximum * (0.5f - 1f), true);
            Assert.That(health.Maximum, Is.EqualTo(afterFloor * 0.5f).Within(0.001f));
        }

        [Test]
        public void RuhigeEtageAendertKeineZahl()
        {
            var calm = FloorModifierCatalog.For(FloorModifierId.None);
            Assert.That(calm.EnemyHealth, Is.EqualTo(1f));
            Assert.That(calm.EnemyDamage, Is.EqualTo(1f));
            Assert.That(calm.EnemySpeed, Is.EqualTo(1f));
            Assert.That(calm.EnemyCount, Is.EqualTo(1f));
            Assert.That(calm.Gold, Is.EqualTo(1f));
            Assert.That(calm.Shards, Is.EqualTo(1f));

            var health = NewHealth(100f);
            health.IncreaseMaximum(health.Maximum * (calm.EnemyHealth - 1f), true);
            Assert.That(health.Maximum, Is.EqualTo(100f).Within(0.001f));
        }

        [Test]
        public void DerSchwarmVerdoppeltEinLagerUndHalbiertEsNicht()
        {
            // Aufgerundet, damit aus einem Zweierlager ein Viererlager wird und nicht ein Dreier.
            var swarm = FloorModifierCatalog.For(FloorModifierId.Swarm);
            Assert.That(Mathf.CeilToInt(2 * swarm.EnemyCount), Is.EqualTo(4));
            Assert.That(Mathf.CeilToInt(8 * swarm.EnemyCount), Is.EqualTo(15));
            Assert.That(swarm.EnemyCount, Is.GreaterThan(1f));
        }
    }
}
