using UnityEngine;

namespace Shatterspire
{
    public static class EnemyFactory
    {
        public static EnemyAgent Create(EnemyKind kind, Vector3 position, Transform player, int floor,
            int runSeed, int salt)
            => Create(kind, position, player, floor, FloorModifierCatalog.For(FloorModifierId.None), runSeed, salt);

        /// <summary>
        /// Baut einen Gegner. runSeed und salt sagen, welcher Gegner das ist: aus ihnen zieht er
        /// seine Eigenschaften, damit im Co-op alle Spieler denselben Gegner vor sich haben.
        /// </summary>
        public static EnemyAgent Create(EnemyKind kind, Vector3 position, Transform player, int floor,
            in FloorModifier modifier, int runSeed, int salt)
        {
            var root = new GameObject(kind.ToString());
            root.transform.position = position;
            var stats = EnemyBalance.For(kind);
            var collider = root.AddComponent<CapsuleCollider>();
            collider.center = Vector3.up * 0.65f;
            collider.height = stats.ColliderHeight;
            collider.radius = stats.ColliderRadius;
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            root.AddComponent<Health>();
            root.AddComponent<StatusReceiver>();

            StylizedArt.BuildEnemy(root.transform, kind);

            var agent = root.AddComponent<EnemyAgent>();
            agent.Configure(kind, player, floor, modifier, runSeed, salt);
            StylizedArt.AddHealthBar(root, kind);
            return agent;
        }
    }
}
