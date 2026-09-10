using UnityEngine;

namespace Shatterspire
{
    public sealed class ExperienceOrb : MonoBehaviour
    {
        private int value;
        private Transform target;
        private float bornAt;

        public static void Spawn(Vector3 position, int amount, Transform target)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "XP Orb";
            go.transform.position = position;
            go.transform.localScale = Vector3.one * 0.28f;
            go.GetComponent<Renderer>().material = PrototypeFactory.CreateMaterial(new Color(0.2f, 1f, 0.8f), true);
            go.GetComponent<Collider>().isTrigger = true;
            var orb = go.AddComponent<ExperienceOrb>();
            orb.value = amount;
            orb.target = target;
            orb.bornAt = Time.time;
        }

        private void Update()
        {
            if (!target) { Destroy(gameObject); return; }
            var distance = Vector3.Distance(transform.position, target.position);
            if (distance < 5f || Time.time - bornAt > 1f)
                transform.position = Vector3.MoveTowards(transform.position, target.position + Vector3.up * 0.5f, 11f * Time.deltaTime);
            if (distance < 0.7f)
            {
                target.GetComponent<IExperienceReceiver>()?.AddExperience(value);
                Destroy(gameObject);
            }
        }
    }
}
