using UnityEngine;

namespace Shatterspire
{
    /// <summary>
    /// Die Fluegel der Zornigen Vergeltung.
    ///
    /// Ein Buff ohne Bild ist eine Zahl, die niemand sieht - besonders im Koop, wo die anderen
    /// beiden erkennen sollen, dass gerade einer von ihnen brennt. Die Fluegel sind deshalb kein
    /// Schmuck, sondern die Anzeige: solange sie stehen, gelten die 25 Prozent.
    ///
    /// Sie entfalten sich beim Start, atmen leicht, und legen sich am Ende wieder an.
    /// </summary>
    public sealed class WrathWings : MonoBehaviour
    {
        private const int Feathers = 5;
        private const float UnfoldSeconds = 0.32f;
        private const float FoldSeconds = 0.45f;

        private readonly Transform[] feathers = new Transform[Feathers * 2];
        private readonly Quaternion[] spread = new Quaternion[Feathers * 2];
        private float bornAt;
        private float endsAt;

        /// <summary>Haengt ein Fluegelpaar an den Ruecken. <paramref name="seconds"/> ist die Dauer des Buffs.</summary>
        public static WrathWings Attach(Transform back, Color accent, float seconds)
        {
            if (!back) return null;
            var go = new GameObject("Wrath Wings");
            go.transform.SetParent(back, false);
            var scale = Mathf.Max(0.0001f, back.lossyScale.x);
            go.transform.localPosition = Vector3.zero;
            go.transform.localScale = Vector3.one / scale;

            var wings = go.AddComponent<WrathWings>();
            wings.bornAt = Time.time;
            wings.endsAt = Time.time + Mathf.Max(0.5f, seconds);

            var glow = Color.Lerp(accent, Color.white, 0.35f);
            for (var side = 0; side < 2; side++)
            for (var i = 0; i < Feathers; i++)
            {
                var sign = side == 0 ? -1f : 1f;
                // Nach hinten und aussen gefaechert, die oberen laenger als die unteren.
                var length = Mathf.Lerp(1.15f, 0.5f, i / (Feathers - 1f));
                var pivot = new GameObject($"Feather {side}{i}").transform;
                pivot.SetParent(go.transform, false);
                pivot.localPosition = new Vector3(sign * 0.12f, 0.1f - i * 0.05f, -0.16f);

                var feather = GameObject.CreatePrimitive(PrimitiveType.Cube);
                feather.name = "Wrath Feather";
                feather.transform.SetParent(pivot, false);
                feather.transform.localPosition = new Vector3(0f, length * 0.5f, 0f);
                feather.transform.localScale = new Vector3(0.1f, length, 0.03f);
                feather.GetComponent<Renderer>().sharedMaterial = PrototypeFactory.CreateMaterial(glow, true);
                PrototypeFactory.RemoveCollider(feather.GetComponent<Collider>());

                var index = side * Feathers + i;
                wings.feathers[index] = pivot;
                wings.spread[index] = Quaternion.Euler(
                    -22f - i * 4f,
                    sign * (28f + i * 13f),
                    sign * (46f + i * 9f));
                // Angelegt beginnen: die Entfaltung ist der Auftritt.
                pivot.localRotation = Quaternion.Euler(0f, sign * 6f, sign * 4f);
            }
            return wings;
        }

        private void LateUpdate()
        {
            var remaining = endsAt - Time.time;
            // Erst entfalten, dann halten, am Ende wieder anlegen.
            var unfold = Mathf.Clamp01((Time.time - bornAt) / UnfoldSeconds);
            var fold = Mathf.Clamp01(remaining / FoldSeconds);
            var open = Mathf.Min(Mathf.SmoothStep(0f, 1f, unfold), Mathf.SmoothStep(0f, 1f, fold));

            for (var i = 0; i < feathers.Length; i++)
            {
                var pivot = feathers[i];
                if (!pivot) continue;
                // Ein leichtes Atmen, damit sie nicht wie angeklebt wirken; jede Feder etwas versetzt.
                var breath = Mathf.Sin(Time.time * 3.1f + i * 0.4f) * 3.4f * open;
                var folded = Quaternion.Euler(0f, (i < Feathers ? -6f : 6f), (i < Feathers ? -4f : 4f));
                pivot.localRotation = Quaternion.Slerp(folded, spread[i], open)
                                      * Quaternion.Euler(breath, 0f, 0f);
            }
            if (remaining <= 0f) Destroy(gameObject);
        }
    }
}
