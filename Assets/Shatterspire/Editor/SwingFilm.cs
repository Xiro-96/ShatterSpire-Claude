using System.IO;
using UnityEditor;
using UnityEngine;

namespace Shatterspire.Editor
{
    /// <summary>
    /// Rendert einen Schlag Bild fuer Bild heraus, ohne das Spiel zu starten.
    ///
    /// Der Anlass: zweimal aneinander vorbeigeredet. "Sieht schlecht aus" und "die Zahlen stimmen"
    /// sind keine Aussagen ueber dieselbe Sache. Hier steht die Figur mit ihrer Waffe in genau den
    /// Posen, die der abgespielte Ausschnitt durchlaeuft - was man hier nicht sieht, sieht man im
    /// Spiel auch nicht.
    /// </summary>
    public static class SwingFilm
    {
        private const string Rig = "Art3D/KayKit/Characters/Knight";
        private const string Melee = "Art3D/KayKit/Animations/Rig_Medium_CombatMelee";
        private const int Frames = 8;
        private const int Size = 420;

        [MenuItem("SHATTERSPIRE/Schlag herausrendern")]
        public static void Render()
        {
            var folder = Path.Combine(Directory.GetCurrentDirectory(), "Builds", "Swing");
            Directory.CreateDirectory(folder);

            var source = Resources.Load<GameObject>(Rig);
            if (!source) { Debug.LogError("SHATTERSPIRE SwingFilm: Rig fehlt."); return; }

            var rig = Object.Instantiate(source);
            rig.transform.position = Vector3.zero;
            rig.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            var animator = rig.GetComponentInChildren<Animator>();
            var grip = FindBone(rig.transform, "handslot.r");
            if (!grip && animator && animator.isHuman) grip = animator.GetBoneTransform(HumanBodyBones.RightHand);
            if (grip) AttachBlade(grip);

            // Eine frisch erzeugte Lichtquelle schaltet in den Projekteinstellungen die
            // Farbtemperatur ein. Das ist eine Aenderung am Spiel, nur weil ein Werkzeug lief -
            // deshalb wird der Wert gemerkt und am Ende zurueckgesetzt.
            var colorTemperature = UnityEngine.Rendering.GraphicsSettings.lightsUseColorTemperature;
            var lightObject = new GameObject("Key Light");
            var light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.5f;
            lightObject.transform.rotation = Quaternion.Euler(42f, 35f, 0f);

            var cameraObject = new GameObject("Film Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.backgroundColor = new Color(0.09f, 0.1f, 0.13f);
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.transform.position = new Vector3(3.6f, 2.1f, 3.9f);
            camera.transform.LookAt(new Vector3(0f, 1.15f, 0f));
            camera.fieldOfView = 42f;

            foreach (var (motion, clipName) in new[]
            {
                (AttackMotion.Swing, "Melee_2H_Attack_Slice"),
                (AttackMotion.Smash, "Melee_2H_Attack_Chop"),
                (AttackMotion.Spin, "Melee_2H_Attack_Spin")
            })
            {
                AnimationClip clip = null;
                foreach (var candidate in Resources.LoadAll<AnimationClip>(Melee))
                    if (candidate.name == clipName) clip = candidate;
                if (!clip) { Debug.LogWarning($"SwingFilm: {clipName} fehlt."); continue; }

                var window = ChampionAnimationDriver.WindowFor(motion);
                for (var i = 0; i < Frames; i++)
                {
                    var normalized = Mathf.Lerp(window.From, window.To, i / (Frames - 1f));
                    clip.SampleAnimation(rig, clip.length * normalized);
                    // SampleAnimation traegt die Wurzelbewegung in die Figur. Im Spiel ist Root
                    // Motion aus - ohne das Zuruecksetzen liefe die Figur aus dem Bild.
                    rig.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, 180f, 0f));
                    Shoot(camera, Path.Combine(folder, $"{motion}_{i}.png"));
                }
                Debug.Log($"SHATTERSPIRE SwingFilm: {motion} aus {clipName}, "
                          + $"Fenster {window.From:0.00}-{window.To:0.00}, {Frames} Bilder.");
            }

            Object.DestroyImmediate(cameraObject);
            Object.DestroyImmediate(lightObject);
            Object.DestroyImmediate(rig);
            UnityEngine.Rendering.GraphicsSettings.lightsUseColorTemperature = colorTemperature;
            Debug.Log($"SHATTERSPIRE SwingFilm: fertig in {folder}");
        }

        private static void Shoot(Camera camera, string path)
        {
            var target = new RenderTexture(Size, Size, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = target;
            camera.Render();
            var previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(Size, Size, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0f, 0f, Size, Size), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            camera.targetTexture = null;
            File.WriteAllBytes(path, image.EncodeToPNG());
            Object.DestroyImmediate(image);
            target.Release();
            Object.DestroyImmediate(target);
        }

        /// <summary>Ein schlichter Zweihaender am Griffpunkt - es geht um den Bogen, nicht um das Modell.</summary>
        private static void AttachBlade(Transform grip)
        {
            var scale = Mathf.Max(0.0001f, grip.lossyScale.x);
            var blade = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blade.name = "Film Blade";
            blade.transform.SetParent(grip, false);
            blade.transform.localPosition = Vector3.up * (0.98f / scale);
            blade.transform.localScale = new Vector3(0.19f, 1.72f, 0.05f) / scale;
            blade.GetComponent<Renderer>().sharedMaterial =
                PrototypeFactory.CreateMaterial(new Color(0.9f, 0.92f, 0.96f), false, 0.7f, 0.8f);
            Object.DestroyImmediate(blade.GetComponent<Collider>());
        }

        private static Transform FindBone(Transform root, string name)
        {
            foreach (var candidate in root.GetComponentsInChildren<Transform>(true))
                if (candidate.name.ToLowerInvariant() == name) return candidate;
            return null;
        }
    }
}
