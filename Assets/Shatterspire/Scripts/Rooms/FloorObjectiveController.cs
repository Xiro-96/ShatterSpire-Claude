using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Shatterspire
{
    /// <summary>
    /// A short mobile-friendly tower floor: one readable objective, two enemy waves,
    /// one reward claim and a visible lift. Floors stay punchy while the full run can
    /// now contain many visually distinct stages without repeating a cell scavenger hunt.
    /// </summary>
    public sealed class FloorObjectiveController : MonoBehaviour
    {
        private enum FloorState { Seeking, Fighting, Claiming, ExitOpen, Finished }

        private readonly List<RiftCellNode> nodes = new();
        private Transform player;
        private EnemySpawner spawner;
        private AscensionGate gate;
        private Action completed;
        private RoomKind roomKind;
        private FloorState state;
        private int activated;
        private int floor;
        private float stateEnteredAt;
        private bool encounterRetryUsed;
        private Color routeAccent = new(0.08f, 0.76f, 0.92f);

        public IReadOnlyList<Vector3> CorePositions
        {
            get
            {
                var positions = new List<Vector3>(nodes.Count);
                foreach (var node in nodes) positions.Add(node.transform.position);
                return positions;
            }
        }

        public void Configure(Transform playerTransform, EnemySpawner enemySpawner, int floorIndex,
            RoomKind kind, Action onCompleted)
        {
            player = playerTransform;
            spawner = enemySpawner;
            floor = floorIndex;
            roomKind = kind;
            completed = onCompleted;
            state = FloorState.Seeking;
            stateEnteredAt = Time.time;
            spawner.EnemiesCleared += OnEncounterCleared;

            BuildTowerPath();
            var positions = new[]
            {
                new Vector3(0f, 0f, -2.2f)
            };
            for (var i = 0; i < positions.Length; i++)
            {
                var nodeObject = new GameObject("Power Cell " + (i + 1));
                nodeObject.transform.SetParent(transform, false);
                nodeObject.transform.position = positions[i];
                var node = nodeObject.AddComponent<RiftCellNode>();
                node.Configure(this, player, i + 1, kind == RoomKind.Elite ? 0.5f : 0.36f);
                nodes.Add(node);
            }
            nodes[0].SetCurrent();

            var gateObject = new GameObject("Tower Lift");
            gateObject.transform.SetParent(transform, false);
            gateObject.transform.position = new Vector3(0f, 0f, 13.2f);
            gate = gateObject.AddComponent<AscensionGate>();
            gate.Configure(player, CompleteFloor);
            GameEvents.RaiseObjectiveChanged(0, nodes.Count, "REACH POWER CELL 1");
            GameEvents.RaiseObjectiveTargetChanged(nodes[0].transform.position, "POWER CELL 1", true);
        }

        private void OnDestroy()
        {
            if (spawner) spawner.EnemiesCleared -= OnEncounterCleared;
        }

        private void Update()
        {
            if (!player || activated >= nodes.Count) return;
            if (state == FloorState.Fighting)
            {
                RepairStalledEncounter();
                return;
            }
            if (state != FloorState.Seeking) return;

            var distance = Vector3.Distance(Flat(player.position), Flat(nodes[activated].transform.position));
            var firstCellAutoStart = activated == 0 && Time.time - stateEnteredAt >= 0.35f;
            if (!firstCellAutoStart && distance > 5.1f) return;

            StartEncounter();
        }

        private void StartEncounter()
        {
            state = FloorState.Fighting;
            stateEnteredAt = Time.time;
            encounterRetryUsed = false;
            GameEvents.RaiseObjectiveChanged(activated, nodes.Count,
                "DEFEAT THE CELL GUARDIANS");
            GameEvents.RaiseObjectiveTargetChanged(nodes[activated].transform.position,
                "ACTIVE CELL " + (activated + 1), true);
            spawner.SpawnObjectiveEncounter(nodes[activated].transform.position, floor, activated, roomKind);
        }

        private void RepairStalledEncounter()
        {
            var elapsed = Time.time - stateEnteredAt;
            if (elapsed < 3f || spawner.LivingCount > 0) return;
            if (spawner.IsSpawning && elapsed < 6f) return;
            if (spawner.IsSpawning) spawner.ResetStalledObjectiveEncounter();
            if (encounterRetryUsed)
            {
                OnEncounterCleared();
                return;
            }

            encounterRetryUsed = true;
            stateEnteredAt = Time.time;
            spawner.SpawnObjectiveEncounter(nodes[activated].transform.position, floor, activated, roomKind);
        }

        private void OnEncounterCleared()
        {
            if (state != FloorState.Fighting || activated >= nodes.Count) return;
            state = FloorState.Claiming;
            stateEnteredAt = Time.time;
            nodes[activated].SetReady();
            GameEvents.RaiseObjectiveChanged(activated, nodes.Count, "POWER CELL SYNCING");
            GameEvents.RaiseObjectiveTargetChanged(nodes[activated].transform.position,
                "SYNCING CELL " + (activated + 1), true);
            StartCoroutine(AutoClaim(nodes[activated]));
        }

        private IEnumerator AutoClaim(RiftCellNode node)
        {
            yield return new WaitForSeconds(0.95f);
            if (state == FloorState.Claiming && activated < nodes.Count && node == nodes[activated])
                Activate(node);
        }

        public void Activate(RiftCellNode node)
        {
            if (state != FloorState.Claiming || activated >= nodes.Count || node != nodes[activated]) return;
            node.LockActivated();
            activated++;

            if (activated >= nodes.Count)
            {
                state = FloorState.ExitOpen;
                gate.Unlock();
                GameEvents.RaiseObjectiveChanged(activated, nodes.Count, "TOWER LIFT OPEN");
                GameEvents.RaiseObjectiveTargetChanged(gate.transform.position, "TOWER LIFT", true);
                return;
            }

            state = FloorState.Seeking;
            stateEnteredAt = Time.time;
            nodes[activated].SetCurrent();
            GameEvents.RaiseObjectiveChanged(activated, nodes.Count, "REACH POWER CELL " + (activated + 1));
            GameEvents.RaiseObjectiveTargetChanged(nodes[activated].transform.position,
                "POWER CELL " + (activated + 1), true);
        }

        private void CompleteFloor()
        {
            if (state is FloorState.Finished or FloorState.Fighting) return;
            state = FloorState.Finished;
            GameEvents.RaiseObjectiveTargetChanged(Vector3.zero, string.Empty, false);
            GameEvents.RaiseObjectiveChanged(nodes.Count, nodes.Count, "FLOOR SECURED");
            completed?.Invoke();
        }

        private void BuildTowerPath()
        {
            var theme = FloorCatalog.ThemeFor(floor);
            var colors = theme switch
            {
                FloorTheme.EmberFoundry => new[] { new Color(1f, 0.28f, 0.04f), new Color(1f, 0.56f, 0.06f), new Color(0.82f, 0.08f, 0.03f) },
                FloorTheme.AstralArchive => new[] { new Color(0.6f, 0.18f, 1f), new Color(0.12f, 0.82f, 1f), new Color(0.92f, 0.2f, 0.74f) },
                _ => new[] { new Color(0.2f, 0.88f, 0.74f), new Color(0.72f, 0.38f, 0.95f), new Color(1f, 0.61f, 0.18f) }
            };
            routeAccent = colors[0];
            BuildEncounterPlaza(new Vector3(0f, 0f, -2.2f), colors[0]);
            BuildGuidePath(new Vector3(0f, 0.08f, -11.8f), new Vector3(0f, 0.08f, -2.2f));
            BuildGuidePath(new Vector3(0f, 0.08f, -2.2f), new Vector3(0f, 0.08f, 13.2f));
        }

        private void BuildEncounterPlaza(Vector3 position, Color accent)
        {
            var border = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            border.name = "Cell Stone Plaza";
            border.transform.SetParent(transform, false);
            border.transform.position = position + Vector3.up * 0.065f;
            border.transform.localScale = new Vector3(3.7f, 0.045f, 3.7f);
            border.GetComponent<Renderer>().sharedMaterial = PrototypeFactory.CreateMaterial(
                new Color(0.035f, 0.06f, 0.14f), false, 0.06f);
            PrototypeFactory.RemoveCollider(border.GetComponent<Collider>());

            var inset = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            inset.name = "Cell Rift Inset";
            inset.transform.SetParent(transform, false);
            inset.transform.position = position + Vector3.up * 0.12f;
            inset.transform.localScale = new Vector3(3.08f, 0.022f, 3.08f);
            inset.GetComponent<Renderer>().sharedMaterial = PrototypeFactory.CreateMaterial(
                Color.Lerp(new Color(0.12f, 0.2f, 0.38f), accent, 0.22f), false, 0.04f);
            PrototypeFactory.RemoveCollider(inset.GetComponent<Collider>());

            var sigil = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            sigil.name = "Cell Sigil";
            sigil.transform.SetParent(transform, false);
            sigil.transform.position = position + Vector3.up * 0.155f;
            sigil.transform.localScale = new Vector3(0.88f, 0.016f, 0.88f);
            sigil.GetComponent<Renderer>().sharedMaterial = PrototypeFactory.CreateMaterial(accent, true, 0.3f, 0.04f);
            PrototypeFactory.RemoveCollider(sigil.GetComponent<Collider>());

            for (var i = 0; i < 4; i++)
            {
                var direction = Quaternion.Euler(0f, i * 90f, 0f) * Vector3.forward;
                var rune = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rune.name = "Cell Compass Rune";
                rune.transform.SetParent(transform, false);
                rune.transform.position = position + direction * 2.55f + Vector3.up * 0.16f;
                rune.transform.rotation = Quaternion.LookRotation(direction);
                rune.transform.localScale = new Vector3(0.18f, 0.025f, 0.72f);
                rune.GetComponent<Renderer>().sharedMaterial = PrototypeFactory.CreateMaterial(accent, true, 0.28f, 0.04f);
                PrototypeFactory.RemoveCollider(rune.GetComponent<Collider>());
            }
        }

        private void BuildGuidePath(Vector3 from, Vector3 to)
        {
            var direction = to - from;
            if (direction.sqrMagnitude < 0.01f) return;
            var length = direction.magnitude;
            var forward = direction / length;
            var midpoint = Vector3.Lerp(from, to, 0.5f);
            var route = GameObject.CreatePrimitive(PrimitiveType.Cube);
            route.name = "Recessed Rift Route";
            route.transform.SetParent(transform, false);
            route.transform.position = midpoint;
            route.transform.rotation = Quaternion.LookRotation(forward);
            route.transform.localScale = new Vector3(0.82f, 0.025f, length);
            route.GetComponent<Renderer>().sharedMaterial = PrototypeFactory.CreateMaterial(
                new Color(0.12f, 0.18f, 0.27f), false, 0.05f);
            PrototypeFactory.RemoveCollider(route.GetComponent<Collider>());

            var energy = GameObject.CreatePrimitive(PrimitiveType.Cube);
            energy.name = "Rift Route Energy";
            energy.transform.SetParent(transform, false);
            energy.transform.position = midpoint + Vector3.up * 0.025f;
            energy.transform.rotation = Quaternion.LookRotation(forward);
            energy.transform.localScale = new Vector3(0.07f, 0.012f, length * 0.96f);
            energy.GetComponent<Renderer>().sharedMaterial = PrototypeFactory.CreateMaterial(
                routeAccent, true, 0.18f);
            PrototypeFactory.RemoveCollider(energy.GetComponent<Collider>());
        }

        private static Vector3 Flat(Vector3 value) => new(value.x, 0f, value.z);
    }

    public sealed class RiftCellNode : MonoBehaviour
    {
        private static Mesh cellCrystalMesh;
        private FloorObjectiveController owner;
        private Transform player;
        private Transform core;
        private Transform progressRing;
        private Renderer beaconRenderer;
        private Renderer coreRenderer;
        private TextMesh cellLabel;
        private ObjectiveBeaconMotion beaconMotion;
        private float stabilizeDuration;
        private float progress;
        private bool ready;
        private bool activated;
        public bool Activated => activated;

        public void Configure(FloorObjectiveController controller, Transform playerTransform, int number, float seconds)
        {
            owner = controller;
            player = playerTransform;
            stabilizeDuration = seconds;
            BuildVisual(number);
        }

        public void SetCurrent()
        {
            if (beaconRenderer)
                beaconRenderer.sharedMaterial = PrototypeFactory.CreateMaterial(new Color(0.72f, 0.3f, 1f), true, 0.38f, 0.06f);
            if (cellLabel) cellLabel.gameObject.SetActive(true);
            beaconMotion?.SetMode(1);
        }

        public void SetReady()
        {
            ready = true;
            if (coreRenderer)
                coreRenderer.sharedMaterial = PrototypeFactory.CreateMaterial(new Color(1f, 0.76f, 0.12f), true, 0.58f, 0.08f);
            if (beaconRenderer)
                beaconRenderer.sharedMaterial = PrototypeFactory.CreateMaterial(new Color(1f, 0.68f, 0.12f), true, 0.45f, 0.05f);
            if (cellLabel)
            {
                cellLabel.text = "CLAIM";
                cellLabel.color = new Color(1f, 0.78f, 0.12f);
            }
            PrototypeVfx.SpawnExplosion(transform.position + Vector3.up * 0.8f, 1.65f, new Color(1f, 0.72f, 0.12f));
            beaconMotion?.SetMode(2);
        }

        private void Update()
        {
            if (activated || !player) return;
            if (!ready) return;

            var horizontalOffset = transform.position - player.position;
            horizontalOffset.y = 0f;
            var distance = horizontalOffset.magnitude;
            progress = distance <= 3.75f
                ? Mathf.Min(stabilizeDuration, progress + Time.deltaTime)
                : Mathf.Max(0f, progress - Time.deltaTime * 1.7f);
            if (progressRing)
            {
                var t = stabilizeDuration <= 0f ? 0f : progress / stabilizeDuration;
                progressRing.localScale = new Vector3(1.52f + t * 0.48f, 0.022f, 1.52f + t * 0.48f);
            }
            if (progress >= stabilizeDuration) owner.Activate(this);
        }

        public void LockActivated()
        {
            if (activated) return;
            activated = true;
            if (core) core.localScale *= 1.3f;
            if (coreRenderer)
                coreRenderer.sharedMaterial = PrototypeFactory.CreateMaterial(new Color(0.16f, 1f, 0.56f), true, 0.62f, 0.08f);
            if (beaconRenderer)
                beaconRenderer.sharedMaterial = PrototypeFactory.CreateMaterial(new Color(0.16f, 1f, 0.56f), true, 0.5f, 0.05f);
            if (cellLabel) cellLabel.gameObject.SetActive(false);
            beaconMotion?.SetMode(3);
            PrototypeVfx.SpawnExplosion(transform.position + Vector3.up * 0.8f, 2.25f, new Color(0.16f, 1f, 0.56f));
        }

        private void BuildVisual(int number)
        {
            SpawnProp("Art3D/Forge/Models/Prop_ItemHolder", transform, 0.95f);
            var baseDisc = CreatePart(PrimitiveType.Cylinder, "Power Cell Base", new Vector3(0f, 0.08f, 0f),
                new Vector3(1.28f, 0.08f, 1.28f), new Color(0.035f, 0.055f, 0.12f), false);
            baseDisc.transform.SetParent(transform, false);
            var ring = CreatePart(PrimitiveType.Cylinder, "Claim Ring", new Vector3(0f, 0.16f, 0f),
                new Vector3(1.52f, 0.022f, 1.52f), new Color(0.12f, 0.82f, 0.95f), true);
            ring.transform.SetParent(transform, false);
            progressRing = ring.transform;
            var coreObject = CreateCrystal("Floating Power Cell " + number, new Vector3(0f, 1.16f, 0f),
                new Vector3(0.78f, 1.18f, 0.78f), new Color(0.72f, 0.22f, 0.95f));
            coreObject.transform.SetParent(transform, false);
            coreObject.transform.localRotation = Quaternion.Euler(25f, 45f, 25f);
            core = coreObject.transform;
            coreRenderer = coreObject.GetComponent<Renderer>();
            var beacon = CreatePart(PrimitiveType.Cylinder, "Cell Beacon", new Vector3(0f, 2.35f, 0f),
                new Vector3(0.032f, 1.75f, 0.032f), new Color(0.18f, 0.72f, 0.92f), true);
            beacon.transform.SetParent(transform, false);
            beaconRenderer = beacon.GetComponent<Renderer>();

            var orbit = new GameObject("Power Cell Orbit").transform;
            orbit.SetParent(transform, false);
            orbit.localPosition = Vector3.up * 1.18f;
            for (var i = 0; i < 3; i++)
            {
                var angle = i * Mathf.PI * 2f / 3f;
                var shard = CreateCrystal("Power Cell Orbit Shard", new Vector3(Mathf.Cos(angle) * 1.02f,
                    (i - 1) * 0.12f, Mathf.Sin(angle) * 1.02f), new Vector3(0.17f, 0.42f, 0.17f),
                    i == 0 ? new Color(1f, 0.58f, 0.12f) : new Color(0.18f, 0.88f, 1f));
                shard.transform.SetParent(orbit, false);
                shard.transform.localRotation = Quaternion.Euler(18f, i * 120f, 42f);
            }

            cellLabel = new GameObject("Power Cell Label").AddComponent<TextMesh>();
            cellLabel.transform.SetParent(transform, false);
            cellLabel.transform.localPosition = Vector3.up * 3.15f;
            cellLabel.text = "POWER CELL " + number;
            cellLabel.fontSize = 34;
            cellLabel.characterSize = 0.045f;
            cellLabel.anchor = TextAnchor.MiddleCenter;
            cellLabel.alignment = TextAlignment.Center;
            cellLabel.color = new Color(0.82f, 0.5f, 1f);
            cellLabel.gameObject.AddComponent<WorldFacingLabel>();
            cellLabel.gameObject.SetActive(false);
            beaconMotion = gameObject.AddComponent<ObjectiveBeaconMotion>();
            beaconMotion.Configure(core, orbit);
        }

        private static GameObject CreatePart(PrimitiveType type, string name, Vector3 localPosition,
            Vector3 localScale, Color color, bool emissive)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = PrototypeFactory.CreateMaterial(color, emissive, 0.35f, 0.08f);
            PrototypeFactory.RemoveCollider(go.GetComponent<Collider>());
            return go;
        }

        private static GameObject CreateCrystal(string name, Vector3 localPosition, Vector3 localScale, Color color)
        {
            var go = new GameObject(name);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;
            go.AddComponent<MeshFilter>().sharedMesh = GetCellCrystalMesh();
            go.AddComponent<MeshRenderer>().sharedMaterial = PrototypeFactory.CreateMaterial(color, true, 0.22f, 0.03f);
            return go;
        }

        private static Mesh GetCellCrystalMesh()
        {
            if (cellCrystalMesh) return cellCrystalMesh;
            var vertices = new List<Vector3>(24);
            var triangles = new List<int>(24);
            var top = new Vector3(0f, 0.62f, 0f);
            var bottom = new Vector3(0f, -0.5f, 0f);
            var ring = new[]
            {
                new Vector3(-0.5f, -0.08f, -0.38f), new Vector3(0.5f, -0.08f, -0.38f),
                new Vector3(0.5f, -0.08f, 0.38f), new Vector3(-0.5f, -0.08f, 0.38f)
            };
            for (var i = 0; i < 4; i++)
            {
                var next = (i + 1) % 4;
                var start = vertices.Count;
                vertices.Add(top);
                vertices.Add(ring[next]);
                vertices.Add(ring[i]);
                triangles.Add(start);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
                start = vertices.Count;
                vertices.Add(bottom);
                vertices.Add(ring[i]);
                vertices.Add(ring[next]);
                triangles.Add(start);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
            }
            cellCrystalMesh = new Mesh { name = "SHATTERSPIRE Power Cell Crystal" };
            cellCrystalMesh.SetVertices(vertices);
            cellCrystalMesh.SetTriangles(triangles, 0);
            cellCrystalMesh.RecalculateNormals();
            cellCrystalMesh.RecalculateBounds();
            return cellCrystalMesh;
        }

        private static void SpawnProp(string path, Transform parent, float targetHeight)
        {
            var source = Resources.Load<GameObject>(path);
            if (!source) return;
            var instance = Instantiate(source, parent);
            instance.name = "Power Cell Pedestal";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;
            foreach (var collider in instance.GetComponentsInChildren<Collider>()) PrototypeFactory.RemoveCollider(collider);
            var renderers = instance.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return;
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            if (bounds.size.y > 0.001f) instance.transform.localScale *= targetHeight / bounds.size.y;
            foreach (var renderer in renderers)
                renderer.sharedMaterial = PrototypeFactory.CreateMaterial(new Color(0.13f, 0.19f, 0.28f), false, 0.16f, 0.05f);
        }
    }

    public sealed class ObjectiveBeaconMotion : MonoBehaviour
    {
        private Transform core;
        private Transform orbit;
        private Vector3 coreOrigin;
        private float rotationSpeed = 55f;
        private float bobAmount = 0.09f;

        public void Configure(Transform cellCore, Transform shardOrbit)
        {
            core = cellCore;
            orbit = shardOrbit;
            if (core) coreOrigin = core.localPosition;
        }

        public void SetMode(int mode)
        {
            rotationSpeed = mode == 2 ? 145f : mode == 3 ? 28f : 72f;
            bobAmount = mode == 2 ? 0.16f : mode == 3 ? 0.045f : 0.1f;
        }

        private void Update()
        {
            if (core)
            {
                core.Rotate(0f, rotationSpeed * Time.deltaTime, 0f, Space.Self);
                core.localPosition = coreOrigin + Vector3.up *
                    (Mathf.Sin(Time.time * (rotationSpeed > 100f ? 5.2f : 2.8f)) * bobAmount);
            }
            if (orbit) orbit.Rotate(0f, -rotationSpeed * 0.72f * Time.deltaTime, 0f, Space.Self);
        }
    }

    public sealed class AscensionGate : MonoBehaviour
    {
        private Transform player;
        private Action entered;
        private Renderer energyRenderer;
        private Renderer beaconRenderer;
        private Transform outerRing;
        private float channel;
        private bool unlocked;
        private bool used;

        public void Configure(Transform playerTransform, Action onEntered)
        {
            player = playerTransform;
            entered = onEntered;
            BuildVisual();
        }

        public void Unlock()
        {
            unlocked = true;
            if (energyRenderer)
                energyRenderer.sharedMaterial = PrototypeFactory.CreateMaterial(new Color(0.12f, 1f, 0.55f), true, 0.65f, 0.08f);
            if (beaconRenderer)
                beaconRenderer.sharedMaterial = PrototypeFactory.CreateMaterial(new Color(0.12f, 1f, 0.55f), true, 0.5f, 0.05f);
            PrototypeVfx.SpawnExplosion(transform.position + Vector3.up * 0.3f, 3.4f, new Color(0.12f, 1f, 0.55f));
        }

        private void Update()
        {
            if (outerRing) outerRing.Rotate(0f, (unlocked ? 65f : 18f) * Time.deltaTime, 0f);
            if (!unlocked || used || !player) return;
            var distance = Vector3.Distance(new Vector3(transform.position.x, 0f, transform.position.z),
                new Vector3(player.position.x, 0f, player.position.z));
            channel = distance <= 2.5f ? channel + Time.deltaTime : Mathf.Max(0f, channel - Time.deltaTime * 2f);
            if (channel < 0.85f) return;
            used = true;
            entered?.Invoke();
        }

        private void BuildVisual()
        {
            var source = Resources.Load<GameObject>("Art3D/Forge/Models/Platform_Round1");
            if (source)
            {
                var platform = Instantiate(source, transform);
                platform.name = "Tower Lift Platform";
                platform.transform.localPosition = Vector3.zero;
                platform.transform.localScale = Vector3.one * 2.2f;
                foreach (var collider in platform.GetComponentsInChildren<Collider>()) PrototypeFactory.RemoveCollider(collider);
            }
            var energy = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            energy.name = "Lift Energy";
            energy.transform.SetParent(transform, false);
            energy.transform.localPosition = Vector3.up * 0.13f;
            energy.transform.localScale = new Vector3(2.15f, 0.055f, 2.15f);
            energyRenderer = energy.GetComponent<Renderer>();
            energyRenderer.sharedMaterial = PrototypeFactory.CreateMaterial(new Color(0.12f, 0.2f, 0.28f), true, 0.4f, 0.2f);
            PrototypeFactory.RemoveCollider(energy.GetComponent<Collider>());
            var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            ring.name = "Lift Ring";
            ring.transform.SetParent(transform, false);
            ring.transform.localPosition = Vector3.up * 0.2f;
            ring.transform.localScale = new Vector3(2.75f, 0.035f, 2.75f);
            ring.GetComponent<Renderer>().sharedMaterial = PrototypeFactory.CreateMaterial(new Color(1f, 0.65f, 0.12f), true, 0.45f, 0.15f);
            PrototypeFactory.RemoveCollider(ring.GetComponent<Collider>());
            outerRing = ring.transform;
            var beacon = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beacon.name = "Lift Beacon";
            beacon.transform.SetParent(transform, false);
            beacon.transform.localPosition = Vector3.up * 4.2f;
            beacon.transform.localScale = new Vector3(0.1f, 3.3f, 0.1f);
            beaconRenderer = beacon.GetComponent<Renderer>();
            beaconRenderer.sharedMaterial = PrototypeFactory.CreateMaterial(new Color(0.14f, 0.18f, 0.22f), true, 0.25f, 0.05f);
            PrototypeFactory.RemoveCollider(beacon.GetComponent<Collider>());
        }
    }
}
