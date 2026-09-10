using UnityEngine;

namespace Shatterspire
{
    public static class EnemyFactory
    {
        public static EnemyAgent Create(EnemyKind kind, Vector3 position, Transform player, int floor)
        {
            var root = new GameObject(kind.ToString());
            root.transform.position = position;
            var collider = root.AddComponent<CapsuleCollider>();
            collider.center = Vector3.up * 0.65f;
            collider.height = kind == EnemyKind.IronWarden ? 3.2f : kind == EnemyKind.Brute || kind == EnemyKind.Elite ? 2.2f : 1.3f;
            collider.radius = kind == EnemyKind.IronWarden ? 1.1f : kind == EnemyKind.Brute || kind == EnemyKind.Elite ? 0.7f : 0.42f;
            var body = root.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            root.AddComponent<Health>();
            root.AddComponent<StatusReceiver>();

            StylizedArt.BuildEnemy(root.transform, kind);

            var agent = root.AddComponent<EnemyAgent>();
            agent.Configure(kind, player, floor);
            StylizedArt.AddHealthBar(root, kind);
            return agent;
        }
    }
}
