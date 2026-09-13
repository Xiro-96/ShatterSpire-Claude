using UnityEngine;

namespace Shatterspire
{
    /// <summary>
    /// Die drei Bausteine, aus denen die Einrichtungsstuecke eines Raums bestehen: ein Koerper aus
    /// einer Grundform, ein geladenes Modell aus den Resources, und eine Schrift, die zur Kamera
    /// schaut. Herausgezogen, weil Schatzkammer und Raetselraum dieselben drei brauchen.
    /// </summary>
    public static class RoomProps
    {
        /// <summary>Ein Koerper ohne Collider - nichts davon soll den Helden aufhalten.</summary>
        public static GameObject Part(Transform parent, PrimitiveType type, string name,
            Vector3 localPosition, Vector3 localScale, Color color, bool emissive)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial =
                PrototypeFactory.CreateMaterial(color, emissive, 0.35f, 0.08f);
            PrototypeFactory.RemoveCollider(go.GetComponent<Collider>());
            return go;
        }

        /// <summary>
        /// Laedt ein Modell und stellt es auf die gewuenschte Hoehe. Gibt null zurueck, wenn das
        /// Modell fehlt - der Raum bleibt dann spielbar, nur karger.
        /// </summary>
        public static GameObject Prop(string resourcePath, Transform parent, float targetHeight)
        {
            var source = Resources.Load<GameObject>(resourcePath);
            if (!source) return null;
            var instance = Object.Instantiate(source, parent);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            foreach (var collider in instance.GetComponentsInChildren<Collider>())
                PrototypeFactory.RemoveCollider(collider);
            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return instance;
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            if (bounds.size.y > 0.001f) instance.transform.localScale *= targetHeight / bounds.size.y;
            return instance;
        }

        /// <summary>
        /// Ein Ring auf dem Boden - ein Reifen, keine Scheibe.
        ///
        /// Eine flachgedrueckte Zylinder-Grundform ist gefuellt und legt sich als farbige Pfuetze
        /// ueber den Boden. Derselbe Fehler war schon einmal im Krater der Schmiedesturz-Ultimate.
        /// Der Ring kommt deshalb aus einer Textur mit Loch in der Mitte.
        /// </summary>
        public static GameObject Ring(Transform parent, string name, float diameter, Color color,
            float height = 0.03f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, height, 0f);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(diameter, diameter, 1f);
            go.GetComponent<Renderer>().sharedMaterial = PrototypeFactory.CreateRadialDecal(color, 0.82f);
            PrototypeFactory.RemoveCollider(go.GetComponent<Collider>());
            return go;
        }

        public static TextMesh Label(Transform parent, string text, float height, Color color, int fontSize = 32)
        {
            var label = new GameObject("Room Label").AddComponent<TextMesh>();
            label.transform.SetParent(parent, false);
            label.transform.localPosition = Vector3.up * height;
            label.text = text;
            label.fontSize = fontSize;
            label.characterSize = 0.045f;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.color = color;
            label.gameObject.AddComponent<WorldFacingLabel>();
            return label;
        }
    }
}
