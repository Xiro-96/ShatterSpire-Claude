using UnityEngine;

namespace Shatterspire
{
    public static class EnemyFactory
    {
        public static EnemyAgent Create(EnemyKind kind, Vector3 position, Transform player, int floor)
            => Create(kind, position, player, floor, FloorModifierCatalog.For(FloorModifierId.None));

        public static EnemyAgent Create(EnemyKind kind, Vector3 position, Transform player, int floor,
            in FloorModifier modifier)
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
            agent.Configure(kind, player, floor, modifier);
            StylizedArt.AddHealthBar(root, kind);
            return agent;
        }
    }
}
