using UnityEngine;

namespace Shatterspire
{
    /// <summary>
    /// Der Haendler als Ort im Aufzugsraum.
    ///
    /// Vorher stand er als Vollbild-Fenster zwischen zwei Etagen, jedes Mal, auch mit null Gold -
    /// eins von dreien hintereinander. Ein Stand, an dem man vorbeikommt, ist dieselbe Ware und
    /// dieselbe Entscheidung, aber sie unterbricht nichts: wer nichts kaufen will, laeuft weiter.
    ///
    /// Er oeffnet beim Stehenbleiben, nicht beim Beruehren, und danach erst wieder, wenn man
    /// weggegangen ist - sonst ginge das Fenster auf dem Weg zum Aufzug immer wieder auf.
    /// </summary>
    public sealed class TraderStall : MonoBehaviour
    {
        private const float Radius = 3.2f;
        private const float HoldSeconds = 0.4f;

        private Transform player;
        private PrototypeHUD hud;
        private RunWallet wallet;
        private TextMesh label;
        private Transform sign;
        private float held;
        private bool armed = true;

        /// <summary>Gold - dieselbe Farbe wie die Waehrung, mit der man hier bezahlt.</summary>
        private static readonly Color Accent = new(1f, 0.78f, 0.18f);

        public void Configure(Transform playerTransform)
        {
            player = playerTransform;
            wallet = player ? player.GetComponent<RunWallet>() : null;
            hud = FindFirstObjectByType<PrototypeHUD>();
            BuildVisual();
        }

        private void Update()
        {
            if (!player || !hud) return;
            var offset = transform.position - player.position;
            offset.y = 0f;
            var near = offset.sqrMagnitude <= Radius * Radius;
            if (!near)
            {
                held = 0f;
                armed = true;
                RefreshLabel(false);
                return;
            }
            RefreshLabel(true);
            if (!armed) return;
            held += Time.deltaTime;
            if (held < HoldSeconds) return;
            held = 0f;
            armed = false;
            Sfx.Play2D(Sound.UiClick);
            hud.OpenTrader(null, fromStall: true);
        }

        private void RefreshLabel(bool near)
        {
            if (!label) return;
            var gold = wallet ? wallet.Gold : 0;
            label.text = near
                ? $"{Loc.T("TRADER")}\n{gold} {Loc.T("GOLD")}"
                : Loc.T("TRADER");
            label.color = near ? Color.white : Accent;
        }

        private void LateUpdate()
        {
            // Die Muenze dreht sich um ihre senkrechte Achse - sie liegt flach, deshalb um Y im
            // eigenen Raum und nicht um die Weltachse.
            if (sign) sign.Rotate(0f, 90f * Time.deltaTime, 0f, Space.Self);
        }

        /// <summary>
        /// Ein Tisch mit Kisten dahinter und einer Muenze darueber.
        ///
        /// Die erste Fassung hatte ein Dach aus einer grellen tuerkisen Platte - das groesste Teil
        /// am Stand und das einzige, das man sah. Jetzt traegt nur die Muenze die Farbe, und die ist
        /// dieselbe wie die des Goldes im HUD: was hier passiert, ist an der Farbe zu erkennen.
        /// </summary>
        private void BuildVisual()
        {
            var wood = new Color(0.42f, 0.28f, 0.17f);
            var dark = new Color(0.24f, 0.16f, 0.1f);
            RoomProps.Prop("Art3D/KayKit/Dungeon/crates_stacked", transform, 1.2f);
            RoomProps.Part(transform, PrimitiveType.Cube, "Stall Counter", new Vector3(0f, 0.62f, -1.1f),
                new Vector3(2.3f, 0.16f, 0.85f), wood, false);
            RoomProps.Part(transform, PrimitiveType.Cube, "Stall Front", new Vector3(0f, 0.3f, -1.45f),
                new Vector3(2.3f, 0.6f, 0.14f), dark, false);
            for (var side = -1; side <= 1; side += 2)
                RoomProps.Part(transform, PrimitiveType.Cube, "Stall Leg", new Vector3(side * 1.0f, 0.3f, -0.78f),
                    new Vector3(0.16f, 0.6f, 0.16f), dark, false);
            RoomProps.Ring(transform, "Stall Ring", 4.4f, Accent);
            sign = RoomProps.Part(transform, PrimitiveType.Cylinder, "Stall Coin",
                new Vector3(0f, 1.75f, -1.1f), new Vector3(0.5f, 0.05f, 0.5f), Accent, true).transform;
            sign.localRotation = Quaternion.Euler(90f, 0f, 0f);
            label = RoomProps.Label(transform, Loc.T("TRADER"), 2.4f, Accent, 30);
        }
    }
}
