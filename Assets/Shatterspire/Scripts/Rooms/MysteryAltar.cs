using System.Collections.Generic;
using UnityEngine;

namespace Shatterspire
{
    /// <summary>
    /// Ein Altar im Raetselraum. Sein Einsatz steht angeschrieben, sein Inhalt nicht.
    ///
    /// Ausgeloest wird durch Stehenbleiben, nicht durch Beruehren: eine Wette, die man im Vorbeilaufen
    /// verliert, waere keine Entscheidung. Nach der ersten Wette schliessen alle Altaere des Raums -
    /// man darf genau einen.
    /// </summary>
    public sealed class MysteryAltar : MonoBehaviour
    {
        private const float Radius = 2.5f;
        private const float HoldSeconds = 0.85f;

        private Transform player;
        private PlayerBuild build;
        private RunWallet wallet;
        private Wager wager;
        private Transform ring;
        private Renderer crystalRenderer;
        private TextMesh label;
        private float held;
        private bool spent;
        private bool closed;

        /// <summary>Alle Altaere des laufenden Raums. Keine statische Ablage von Spielzustand -
        /// die Liste wird beim Aufbau des Raums gesetzt und beim Abbau geleert.</summary>
        private List<MysteryAltar> siblings;

        public Wager Offer => wager;
        public bool Spent => spent;

        public void Configure(Transform playerTransform, WagerStake stake, WagerId drawn,
            List<MysteryAltar> roomAltars)
        {
            player = playerTransform;
            build = player ? player.GetComponent<PlayerBuild>() : null;
            wallet = player ? player.GetComponent<RunWallet>() : null;
            wager = WagerCatalog.For(drawn);
            siblings = roomAltars;
            BuildVisual(stake);
        }

        private void Update()
        {
            if (spent || closed || !player) return;
            var offset = transform.position - player.position;
            offset.y = 0f;
            if (offset.sqrMagnitude > Radius * Radius)
            {
                held = Mathf.Max(0f, held - Time.deltaTime * 2f);
                ShowProgress();
                return;
            }
            held += Time.deltaTime;
            ShowProgress();
            if (held >= HoldSeconds) Resolve();
        }

        private void ShowProgress()
        {
            if (!ring) return;
            var t = Mathf.Clamp01(held / HoldSeconds);
            var size = 2.6f + t * 1.1f;
            ring.localScale = new Vector3(size, size, 1f);
        }

        private void Resolve()
        {
            spent = true;
            Apply();
            var accent = wager.IsBoon ? new Color(0.2f, 1f, 0.6f) : new Color(1f, 0.32f, 0.3f);
            PrototypeVfx.SpawnExplosion(transform.position + Vector3.up * 1.1f, 2.4f, accent);
            // Erst der Schlag - die Wette ist angenommen -, dann erst die Antwort.
            Sfx.Play2D(Sound.AltarToll);
            Sfx.Play2D(wager.IsBoon ? Sound.CoreActivated : Sound.Explosion, 0.8f);
            if (crystalRenderer)
                crystalRenderer.sharedMaterial = PrototypeFactory.CreateMaterial(accent, true, 0.6f, 0.05f);
            if (label)
            {
                label.text = $"{Loc.T(wager.Name)}\n{WagerCatalog.Describe(wager)}";
                label.color = accent;
            }
            GameEvents.RaiseNotice(
                $"{Loc.T(wager.IsBoon ? "BLESSING" : "CURSE")}\n{Loc.T(wager.Name)}"
                + $"\n{WagerCatalog.Describe(wager)}", 2.6f);
            Debug.Log($"SHATTERSPIRE Wette: {wager.Stake}, {wager.Id}, "
                      + $"{(wager.IsBoon ? "Segen" : "Fluch")}, {WagerCatalog.Describe(wager)}");
            CloseSiblings();
        }

        /// <summary>
        /// Wendet die Wette an. Laeuft ueber Wirkung und Betrag, nicht ueber die Kennung: damit
        /// wirkt jede Wette genau das, was ihr Text ankuendigt.
        /// </summary>
        private void Apply()
        {
            if (wager.IsNothing) return;
            switch (wager.Effect)
            {
                case WagerEffect.Gold:
                    if (wager.Amount >= 0f) wallet?.Earn(Mathf.RoundToInt(wager.Amount));
                    else wallet?.Spend(Mathf.RoundToInt(-wager.Amount));
                    break;
                case WagerEffect.GoldShare:
                    if (wallet) wallet.Spend(Mathf.RoundToInt(wallet.Gold * -wager.Amount));
                    break;
                case WagerEffect.HealthShare:
                    var health = player ? player.GetComponent<Health>() : null;
                    if (!health) break;
                    if (wager.Amount >= 0f) health.Heal(health.Maximum * wager.Amount);
                    else
                    {
                        // Ueber den Lebensbalken, nicht ueber Schaden: ein Fluch soll keine
                        // Vergeltungs-Relikte ausloesen und keinen Gegner als Quelle haben.
                        health.Drain(health.Maximum * -wager.Amount);
                    }
                    break;
                default:
                    build?.ApplyWager(wager);
                    break;
            }
        }

        private void CloseSiblings()
        {
            if (siblings == null) return;
            foreach (var altar in siblings)
                if (altar && altar != this) altar.Close();
        }

        /// <summary>Der nicht gewaehlte Altar sinkt ein und zeigt, was er gewesen waere.</summary>
        public void Close()
        {
            if (closed || spent) return;
            closed = true;
            if (ring) ring.gameObject.SetActive(false);
            if (crystalRenderer)
                crystalRenderer.sharedMaterial =
                    PrototypeFactory.CreateMaterial(new Color(0.24f, 0.26f, 0.3f), false, 0.2f, 0.1f);
            if (label)
            {
                // Aufdecken, was man nicht genommen hat. Ohne das bleibt jede Wette folgenlos
                // erzaehlt - man erfaehrt nie, ob die Entscheidung gut war.
                label.text = $"{Loc.T("PASSED UP")}\n{Loc.T(wager.Name)}";
                label.color = new Color(0.5f, 0.54f, 0.6f);
            }
        }

        /// <summary>
        /// Ein niedriger Steinsockel mit einem schwebenden Zeichen darueber.
        ///
        /// Die erste Fassung stellte ein Modell aus dem Baukasten hin - ein Bedienterminal mit
        /// Tastenfeld. Es war hoeher als der Held, verdeckte ihn und das Zeichen, und las sich als
        /// Technik statt als Altar. Aus Grundformen gebaut ist es niedriger als der Held, sein
        /// Zeichen steht frei, und die Groesse des Zeichens sagt den Einsatz.
        /// </summary>
        private void BuildVisual(WagerStake stake)
        {
            var accent = WagerCatalog.AccentOf(stake);
            var stone = new Color(0.13f, 0.13f, 0.17f);
            RoomProps.Part(transform, PrimitiveType.Cylinder, "Altar Base", new Vector3(0f, 0.11f, 0f),
                new Vector3(1.25f, 0.11f, 1.25f), stone, false);
            RoomProps.Part(transform, PrimitiveType.Cylinder, "Altar Column", new Vector3(0f, 0.4f, 0f),
                new Vector3(0.62f, 0.32f, 0.62f), Color.Lerp(stone, Color.white, 0.12f), false);
            RoomProps.Part(transform, PrimitiveType.Cube, "Altar Plate", new Vector3(0f, 0.76f, 0f),
                new Vector3(0.86f, 0.1f, 0.86f), Color.Lerp(stone, accent, 0.22f), false);
            ring = RoomProps.Ring(transform, "Altar Ring", 2.6f, accent).transform;
            var sign = RoomProps.Part(transform, PrimitiveType.Cube, "Altar Sign",
                new Vector3(0f, 1.35f, 0f),
                stake == WagerStake.Large ? new Vector3(0.46f, 0.68f, 0.46f) : new Vector3(0.28f, 0.42f, 0.28f),
                accent, true);
            sign.transform.localRotation = Quaternion.Euler(24f, 45f, 24f);
            crystalRenderer = sign.GetComponent<Renderer>();
            label = RoomProps.Label(transform, WagerCatalog.NameOf(stake), 2.15f, accent, 28);
            gameObject.AddComponent<AltarHover>().Configure(sign.transform);
        }
    }

    /// <summary>Das Zeichen ueber dem Altar schwebt und dreht sich - ein Hinweis, dass es wirkt.</summary>
    public sealed class AltarHover : MonoBehaviour
    {
        private Transform sign;
        private float baseHeight;

        public void Configure(Transform target)
        {
            sign = target;
            baseHeight = target ? target.localPosition.y : 0f;
        }

        private void Update()
        {
            if (!sign) return;
            sign.localPosition = new Vector3(0f, baseHeight + Mathf.Sin(Time.time * 1.7f) * 0.12f, 0f);
            sign.Rotate(0f, 42f * Time.deltaTime, 0f, Space.World);
        }
    }
}
