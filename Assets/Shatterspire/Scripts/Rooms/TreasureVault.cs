using System.Collections.Generic;
using UnityEngine;

namespace Shatterspire
{
    /// <summary>
    /// Die Schatzkammer.
    ///
    /// Vorher war "TREASURE" die schwaechste Route im Spiel: keine Verteidiger, ein bisschen Heilung,
    /// einmal durchlaufen. Man wachte eine Beschriftung.
    ///
    /// Jetzt liegen Horte im Raum, und sie haben ein eigenes Verb: hingehen und aufbrechen. Der erste
    /// geoeffnete Hort loest den Alarm aus, danach versiegelt die Kammer - was man in der Zeit nicht
    /// erreicht, sinkt ein. Die Kammer bleibt damit die ruhige Route, aber die Herausforderung ist
    /// Gier und Weg, nicht Ueberleben. Das ist absichtlich: die Wahl am Aufzug braucht eine Route,
    /// die keinen Kampf verlangt, sonst gibt es keine Verschnaufpause im Turm.
    /// </summary>
    public sealed class TreasureVault : MonoBehaviour
    {
        /// <summary>
        /// Wie lange die Kammer nach dem ersten Hort offen bleibt.
        ///
        /// Aus der Messung an 200 erzeugten Etagen, nicht geschaetzt: drei bis vier Horte je Etage,
        /// mittlerer Gesamtweg 125 Einheiten, laengste Einzelstrecke 99. Der langsamste Held laeuft
        /// 5,25 Einheiten je Sekunde, mit Umwegen um Deckung rund 4,2. Bei 28 Sekunden ist auf jeder
        /// Etage mindestens die Haelfte der Horte zu schaffen und auf den meisten alle - wer schlecht
        /// laeuft, verliert einen. Genau das soll die Kammer sein.
        ///
        /// Die erste Fassung stand auf 22 Sekunden. Der Test hat gezeigt, dass damit auf den langen
        /// Etagen nicht einmal der zweite Hort zu erreichen gewesen waere.
        /// </summary>
        public const float SealSeconds = 28f;

        /// <summary>Gold im ersten Hort der ersten Etage. Steigt mit der Tiefe wie jede Beute.</summary>
        public const int BaseGold = 45;

        private readonly List<TreasureCache> caches = new();
        private float sealsAt;
        private bool running;
        private bool sealed_;
        private int opened;

        public int Opened => opened;
        public int Total => caches.Count;
        public bool Sealed => sealed_;

        /// <summary>Gold eines Hortes auf einer Etage. Dieselbe Kurve wie bei Gegnerbeute.</summary>
        public static int GoldFor(int floor)
            => Mathf.Max(1, Mathf.RoundToInt(BaseGold * (1f + Mathf.Max(0, floor - 1) * 0.12f)));

        public void Configure(Transform player, FloorLayout layout, int floor)
        {
            var spots = Spots(layout);
            for (var i = 0; i < spots.Count; i++)
            {
                var holder = new GameObject("Treasure Cache " + (i + 1));
                holder.transform.SetParent(transform, false);
                holder.transform.position = spots[i];
                var cache = holder.AddComponent<TreasureCache>();
                cache.Configure(this, player, GoldFor(floor));
                caches.Add(cache);
            }
            GameEvents.RaiseVaultChanged(0, caches.Count, SealSeconds, caches.Count > 0);
            Debug.Log($"SHATTERSPIRE Schatzkammer: {caches.Count} Horte auf Etage {floor}, "
                      + $"je {GoldFor(floor)} Gold, {SealSeconds:0} s nach dem ersten.");
        }

        /// <summary>Radius, den ein Hort frei braucht - Koerper plus Griffweite des Spielers.</summary>
        public const float Clearance = 1.2f;

        /// <summary>
        /// Groesster Abstand zum vorherigen Hort. Daraus folgt, dass jeder gelegte Hort in der
        /// Alarmzeit auch zu erreichen ist: 60 Einheiten sind beim langsamsten Helden rund 14
        /// Sekunden, also zwei Strecken je Alarm.
        ///
        /// Ohne diese Grenze lag auf gestreckten Etagen ein Hort 135 Einheiten vom naechsten
        /// entfernt - sichtbar, angezeigt, und in der Alarmzeit nicht zu schaffen. Ein Hort, den man
        /// sieht und nicht holen kann, ist schlechter als gar keiner.
        /// </summary>
        public const float MaximumLeg = 60f;

        /// <summary>
        /// Wo die Horte liegen: einer je Raum, am Rand statt in der Mitte. In der Mitte steht der
        /// Power Core, und ein Hort direkt daneben waere kein Weg, sondern ein Mitnehmen.
        ///
        /// Reine Rechnung auf dem Grundriss, ohne Wegenetz: der Grundriss kennt seine Deckungen
        /// selbst, und so ist die Platzierung ohne Unity nachpruefbar. Es werden mehrere Winkel
        /// versucht, weil ein Hort in einer Deckung nicht zu erreichen waere - ein Hort, den man
        /// sieht und nicht holen kann, ist schlimmer als keiner.
        /// </summary>
        public static List<Vector3> Spots(FloorLayout layout)
        {
            var spots = new List<Vector3>();
            if (layout == null) return spots;
            var last = layout.SpawnPoint;
            foreach (var room in layout.Rooms)
            {
                if (room.Role == RoomRole.Start) continue;
                if (!TrySpot(layout.Seed, room, out var spot)) continue;
                // Der erste Hort darf beliebig weit weg liegen: bis dahin laeuft die Uhr nicht.
                if (spots.Count > 0 && (spot - last).magnitude > MaximumLeg) continue;
                spots.Add(spot);
                last = spot;
            }
            return spots;
        }

        /// <summary>Acht Winkel ab einem aus dem Seed gezogenen Startwinkel, der erste freie gewinnt.</summary>
        public static bool TrySpot(int seed, LayoutRoom room, out Vector3 spot)
        {
            spot = room.Bounds.Center;
            if (room == null) return false;
            var reach = Mathf.Min(room.Bounds.Width, room.Bounds.Depth) * 0.32f;
            var start = RunRandom.Index(seed, room.Index, 53, 360);
            for (var step = 0; step < 8; step++)
            {
                var angle = start + step * 45f;
                var offset = Quaternion.Euler(0f, angle, 0f) * Vector3.forward * reach;
                var candidate = room.Bounds.Clamp(room.Bounds.Center + offset, 2.2f);
                if (InCover(room, candidate)) continue;
                spot = candidate;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Liegt der Punkt zu nah an einer Deckung?
        ///
        /// Das Vorzeichen ist der Kern: <see cref="Area.Contains"/> mit positivem Rand verkleinert
        /// die Flaeche, mit negativem vergroessert es sie. Gefragt ist "zu nah dran", also der
        /// negative Rand - genau so prueft auch <see cref="FloorNavigation"/> die Begehbarkeit.
        ///
        /// Die erste Fassung stand auf plus und fragte damit "steckt der Hort tief in der Deckung".
        /// Die Antwort war fast immer nein, und der Held blieb in der Aufnahme 2,3 Einheiten vor
        /// einem Hort stehen, an den er nie herankam.
        /// </summary>
        private static bool InCover(LayoutRoom room, Vector3 point)
        {
            foreach (var cover in room.Cover)
                if (cover.Contains(point, -Clearance)) return true;
            return false;
        }

        /// <summary>Ein Hort ist offen. Der erste startet den Alarm.</summary>
        public void ReportOpened(TreasureCache cache, int gold)
        {
            opened++;
            if (!running)
            {
                running = true;
                sealsAt = Time.time + SealSeconds;
                Sfx.Play2D(Sound.AltarToll, 0.8f);
                GameEvents.RaiseNotice($"{Loc.T("VAULT ALARM")}\n{Loc.T("GRAB WHAT YOU CAN")}", 2.4f);
                Debug.Log("SHATTERSPIRE Schatzkammer: Alarm ausgeloest.");
            }
            GameEvents.RaiseVaultChanged(opened, caches.Count, Mathf.Max(0f, sealsAt - Time.time), !sealed_);
            if (opened >= caches.Count) SealNow(true);
        }

        private void Update()
        {
            if (sealed_ || !running) return;
            var remaining = sealsAt - Time.time;
            GameEvents.RaiseVaultChanged(opened, caches.Count, Mathf.Max(0f, remaining), true);
            if (remaining <= 0f) SealNow(false);
        }

        private void SealNow(bool emptied)
        {
            if (sealed_) return;
            sealed_ = true;
            var lost = 0;
            foreach (var cache in caches)
            {
                if (!cache || cache.Opened) continue;
                cache.Sink();
                lost++;
            }
            GameEvents.RaiseVaultChanged(opened, caches.Count, 0f, false);
            GameEvents.RaiseNotice(emptied
                ? $"{Loc.T("VAULT EMPTIED")}\n{opened}/{caches.Count}"
                : $"{Loc.T("VAULT SEALED")}\n{opened}/{caches.Count}", 2.4f);
            Debug.Log($"SHATTERSPIRE Schatzkammer versiegelt: {opened} von {caches.Count} geholt, "
                      + $"{lost} eingesunken.");
        }
    }

    /// <summary>
    /// Ein Hort. Geht auf, wenn jemand hingeht - kein Angriff noetig.
    ///
    /// Das ist bewusst so: mit einem Daumen am Stick ist Hingehen die verlaessliche Eingabe, und die
    /// Kammer soll ueber den Weg entschieden werden, nicht ueber Zielgenauigkeit. Ausserdem duerfte
    /// ein Hort mit Lebensbalken nicht in die Zielerfassung geraten, sonst schlaegt die Gruppe auf
    /// Truhen statt auf Gegner.
    /// </summary>
    public sealed class TreasureCache : MonoBehaviour
    {
        private const float Reach = 1.9f;

        private TreasureVault vault;
        private Transform player;
        private RunWallet wallet;
        private PlayerBuild build;
        private int gold;
        private bool opened;
        private GameObject glow;
        private GameObject beacon;
        private TextMesh label;

        public bool Opened => opened;

        public void Configure(TreasureVault owner, Transform playerTransform, int goldAmount)
        {
            vault = owner;
            player = playerTransform;
            wallet = player ? player.GetComponent<RunWallet>() : null;
            build = player ? player.GetComponent<PlayerBuild>() : null;
            gold = goldAmount;
            BuildVisual();
        }

        private void Update()
        {
            if (opened || !player) return;
            var offset = transform.position - player.position;
            offset.y = 0f;
            if (offset.sqrMagnitude > Reach * Reach) return;
            Open();
        }

        private void Open()
        {
            opened = true;
            var accent = new Color(1f, 0.78f, 0.16f);
            var payout = build ? Mathf.RoundToInt(gold * build.GoldMultiplier) : gold;
            wallet?.Earn(payout);
            PrototypeVfx.SpawnExplosion(transform.position + Vector3.up * 0.6f, 1.9f, accent);
            Sfx.Play(Sound.VaultOpen, transform.position);
            // Der geleerte Hort bleibt stehen, verliert aber sein Leuchten: so sieht man von weitem,
            // welche Truhen noch offen sind. Die erste Fassung drehte das ganze Modell um 105 Grad,
            // weil sie den Deckel zu haben glaubte - die Truhe lag dann auf der Seite wie ein Fass.
            if (glow) Destroy(glow);
            if (beacon) Destroy(beacon);
            if (label)
            {
                label.text = "+" + payout;
                label.color = accent;
                label.fontSize = 44;
                label.transform.localPosition = Vector3.up * 1.9f;
                label.gameObject.AddComponent<RisingLabel>().Configure(1.6f);
            }
            vault?.ReportOpened(this, payout);
        }

        /// <summary>Was der Alarm nicht mehr hergibt, sinkt in den Boden.</summary>
        public void Sink()
        {
            if (opened) return;
            opened = true;
            if (label) label.gameObject.SetActive(false);
            gameObject.AddComponent<SinkingProp>().Configure(1.1f);
        }

        private void BuildVisual()
        {
            var accent = new Color(1f, 0.78f, 0.16f);
            var chest = RoomProps.Prop("Art3D/KayKit/Dungeon/chest_gold", transform, 0.85f)
                        ?? RoomProps.Prop("Art3D/Forge/Models/Prop_Chest", transform, 0.85f);
            if (chest == null)
                RoomProps.Part(transform, PrimitiveType.Cube, "Cache Body", new Vector3(0f, 0.35f, 0f),
                    new Vector3(0.8f, 0.6f, 0.6f), new Color(0.32f, 0.22f, 0.12f), false);
            glow = RoomProps.Ring(transform, "Cache Glow", 2.2f, accent);
            beacon = RoomProps.Part(transform, PrimitiveType.Cylinder, "Cache Beacon",
                new Vector3(0f, 1.5f, 0f), new Vector3(0.03f, 1.1f, 0.03f), accent, true);
            label = RoomProps.Label(transform, Loc.T("HOARD"), 2.5f, accent, 30);
        }
    }

    /// <summary>Steigt auf und verblasst. Fuer die Zahl, die man gerade eingesammelt hat.</summary>
    public sealed class RisingLabel : MonoBehaviour
    {
        private float seconds;
        private float elapsed;
        private TextMesh text;
        private Vector3 start;

        public void Configure(float duration)
        {
            seconds = Mathf.Max(0.1f, duration);
            text = GetComponent<TextMesh>();
            start = transform.localPosition;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / seconds);
            transform.localPosition = start + Vector3.up * (t * 1.1f);
            if (text)
            {
                var color = text.color;
                color.a = 1f - t;
                text.color = color;
            }
            if (t >= 1f) Destroy(gameObject);
        }
    }

    /// <summary>Sinkt in den Boden und verschwindet. Fuer alles, was seine Gelegenheit verpasst hat.</summary>
    public sealed class SinkingProp : MonoBehaviour
    {
        private float seconds;
        private float elapsed;
        private Vector3 start;

        public void Configure(float duration)
        {
            seconds = Mathf.Max(0.1f, duration);
            start = transform.position;
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / seconds);
            transform.position = start + Vector3.down * (t * 2.2f);
            if (t >= 1f) Destroy(gameObject);
        }
    }
}
