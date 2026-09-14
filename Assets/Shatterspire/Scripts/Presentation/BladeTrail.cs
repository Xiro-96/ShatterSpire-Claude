using System.Collections;
using UnityEngine;

namespace Shatterspire
{
    /// <summary>
    /// Die Spur an der Klinge: der Bogen, den die Waffe beim Schlag zieht.
    ///
    /// Der Anlass war eine Messung. Die beiden Clips fuer die normalen Hiebe - Melee_2H_Attack_Slice
    /// und Melee_2H_Attack_Chop - bewegen die Klinge mit 14 Einheiten je Sekunde. Die 1H-Clips im
    /// selben Paket kommen auf 30 bis 50. Die Zweihandschlaege sind also von Haus aus die
    /// ruhigsten, und genau sie tragen den Grossteil des Kampfes.
    ///
    /// Eine schnellere Wiedergabe wuerde das nicht loesen, sondern nur hetzen. Was einen Schwung
    /// lesbar macht, ist nicht sein Tempo, sondern dass man seinen Weg sieht - in jedem Spiel
    /// dieses Genres zieht die Waffe deshalb eine Spur hinter sich her. Die Spur haengt am
    /// Griffpunkt, folgt also genau der Animation und erfindet nichts dazu.
    /// </summary>
    public sealed class BladeTrail : MonoBehaviour
    {
        /// <summary>Wie lange ein Stueck Spur stehen bleibt. Laenger wird daraus ein Band, kuerzer sieht man nichts.</summary>
        private const float Persistence = 0.22f;

        private TrailRenderer trail;
        private Coroutine sweep;

        /// <summary>
        /// Haengt eine Spur an die Waffenhand. <paramref name="reach"/> ist der Abstand vom Griff,
        /// an dem sie sitzt - weiter aussen zieht sie einen groesseren Bogen.
        /// </summary>
        public static BladeTrail Attach(Transform grip, Color accent, float reach)
        {
            if (!grip) return null;
            var go = new GameObject("Blade Trail");
            go.transform.SetParent(grip, false);
            // Der Griff kann beliebig skaliert sein; die Spur soll trotzdem dort sitzen, wo die
            // Klinge wirklich ist.
            var scale = Mathf.Max(0.0001f, grip.lossyScale.x);
            go.transform.localPosition = Vector3.up * (reach / scale);
            go.transform.localScale = Vector3.one / scale;

            var blade = go.AddComponent<BladeTrail>();
            var renderer = go.AddComponent<TrailRenderer>();
            blade.trail = renderer;
            renderer.time = Persistence;
            renderer.minVertexDistance = 0.03f;
            renderer.alignment = LineAlignment.View;
            renderer.numCapVertices = 2;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.widthCurve = new AnimationCurve(
                new Keyframe(0f, 0.9f), new Keyframe(0.4f, 0.55f), new Keyframe(1f, 0f));
            var head = Color.Lerp(accent, Color.white, 0.6f);
            var tail = accent;
            head.a = 0.85f;
            tail.a = 0f;
            renderer.colorGradient = new Gradient
            {
                colorKeys = new[] { new GradientColorKey(head, 0f), new GradientColorKey(tail, 1f) },
                alphaKeys = new[] { new GradientAlphaKey(0.85f, 0f), new GradientAlphaKey(0f, 1f) }
            };
            renderer.sharedMaterial = PrototypeFactory.CreateTrailMaterial(accent);
            renderer.emitting = false;
            return blade;
        }

        /// <summary>
        /// Zieht die Spur fuer die Dauer eines Schlags. Danach laeuft sie von selbst aus - deshalb
        /// wird nur das Aufzeichnen abgeschaltet und nicht der ganze Renderer.
        /// </summary>
        public void Sweep(float seconds)
        {
            if (!trail || seconds <= 0f) return;
            if (sweep != null) StopCoroutine(sweep);
            sweep = StartCoroutine(SweepRoutine(seconds));
        }

        private IEnumerator SweepRoutine(float seconds)
        {
            // Vorher leeren: sonst spannt sich beim naechsten Schlag ein Band von der letzten
            // Position zur neuen, quer durch die Figur.
            trail.Clear();
            trail.emitting = true;
            yield return new WaitForSeconds(seconds);
            trail.emitting = false;
            sweep = null;
        }

        private void OnDisable()
        {
            if (trail) trail.emitting = false;
            sweep = null;
        }
    }
}
