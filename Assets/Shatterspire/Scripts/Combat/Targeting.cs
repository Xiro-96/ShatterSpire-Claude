using UnityEngine;

namespace Shatterspire
{
    public static class Targeting
    {
        public static Health FindClosest(Vector3 point, float radius, TeamId team, Health exclude = null)
        {
            Health best = null;
            var bestDistance = float.MaxValue;
            var active = Health.Active;
            for (var i = 0; i < active.Count; i++)
            {
                var candidate = active[i];
                if (!candidate || candidate == exclude || !candidate.IsAlive || candidate.Team != team) continue;
                var distance = (candidate.transform.position - point).sqrMagnitude;
                if (distance > radius * radius) continue;
                if (distance < bestDistance) { best = candidate; bestDistance = distance; }
            }
            return best;
        }

        /// <summary>
        /// Wohin man zielen muss, um ein laufendes Ziel zu treffen.
        ///
        /// Zwei Durchgaenge statt einer geschlossenen Loesung: der erste schaetzt die Flugzeit aus
        /// dem heutigen Abstand, der zweite aus dem geschaetzten Treffpunkt. Das reicht auf jede
        /// Entfernung, die hier vorkommt, und kommt ohne Wurzel einer quadratischen Gleichung aus,
        /// die bei zu schnellen Zielen gar keine Loesung haette.
        /// </summary>
        public static Vector3 PredictIntercept(Vector3 from, Vector3 targetPoint, Vector3 targetVelocity,
            float projectileSpeed)
        {
            if (projectileSpeed <= 0.01f) return targetPoint;
            var predicted = targetPoint;
            for (var pass = 0; pass < 2; pass++)
            {
                var flight = Vector3.Distance(from, predicted) / projectileSpeed;
                // Nicht beliebig weit vorhalten: auf sehr lange Sicht wird aus der Schaetzung Unsinn.
                predicted = targetPoint + targetVelocity * Mathf.Min(flight, 0.85f);
            }
            return predicted;
        }

        /// <summary>
        /// Wie gut sich ein Gegner als Ziel des Selbstzielens eignet - kleiner ist besser.
        ///
        /// Steht als eigene Funktion, weil zwei Stellen dieselbe Frage stellen: welches Ziel ist das
        /// beste, und ist ein neues deutlich besser als das bisherige. Zwei getrennte Rechnungen
        /// haetten frueher oder spaeter zwei verschiedene Antworten gegeben.
        /// </summary>
        /// <param name="facingWeight">
        /// Aufschlag je Grad Abweichung von <paramref name="forward"/>. Beim reinen Selbstzielen
        /// klein: wer rueckwaerts vor einem Gegner flieht, soll ihn treffen und nicht einen, der
        /// zufaellig in Laufrichtung steht.
        /// </param>
        public static float AutoAimScore(Vector3 point, Vector3 forward, Health candidate,
            float facingWeight = 0.055f)
        {
            var delta = candidate.transform.position - point;
            delta.y = 0f;
            var distance = delta.magnitude;
            forward.y = 0f;
            var facingPenalty = delta.sqrMagnitude > 0.01f && forward.sqrMagnitude > 0.01f
                ? Vector3.Angle(forward, delta) * facingWeight
                : 0f;
            var agent = candidate.GetComponent<EnemyAgent>();
            var priority = agent && EnemyKinds.IsBoss(agent.Kind) ? -7f
                : agent && agent.Kind == EnemyKind.Elite ? -3.5f
                // Armbruster zuerst: er ist das Ziel, das aus der Entfernung wehtut.
                : agent && agent.Kind == EnemyKind.Marksman ? -2.4f
                // Der Schildtraeger steht vorn und faengt sonst jede Zielhilfe ab, obwohl
                // Treffer auf seine Deckung fast nichts bringen.
                : agent && agent.Kind == EnemyKind.Shieldbearer ? 2.6f : 0f;
            return distance + facingPenalty + priority;
        }

        /// <summary>Ist dieser Gegner ueberhaupt ein Ziel? Tot, verbuendet oder noch im Auftauchen: nein.</summary>
        public static bool IsTargetable(Health candidate, TeamId team)
        {
            if (!candidate || !candidate.IsAlive || candidate.Team != team) return false;
            var agent = candidate.GetComponent<EnemyAgent>();
            // Wer noch auftaucht, ist kein Ziel - vorher waehlte das Selbstzielen ihn trotzdem, die
            // Zielhilfe der Waffe verwarf ihn, und der Schuss ging ins Leere.
            return !(agent && agent.IsArriving);
        }

        public static Health FindBestAutoAim(Vector3 point, Vector3 forward, float radius, TeamId team,
            float facingWeight = 0.055f)
        {
            Health best = null;
            var bestScore = float.MaxValue;
            var active = Health.Active;
            for (var i = 0; i < active.Count; i++)
            {
                var candidate = active[i];
                if (!IsTargetable(candidate, team)) continue;
                var delta = candidate.transform.position - point;
                delta.y = 0f;
                if (delta.magnitude > radius) continue;
                var score = AutoAimScore(point, forward, candidate, facingWeight);
                if (score >= bestScore) continue;
                bestScore = score;
                best = candidate;
            }
            return best;
        }

        /// <summary>
        /// Action-RPG target selection with a little stickiness. Attacks snap to a
        /// readable nearby enemy, but do not jump away from an already useful target.
        /// This is intentionally shared by touch, mouse, controller and later netcode.
        /// </summary>
        public static Health FindActionTarget(Vector3 point, Vector3 desiredDirection, float radius,
            TeamId team, Health current = null)
        {
            desiredDirection.y = 0f;
            if (desiredDirection.sqrMagnitude < 0.01f) desiredDirection = Vector3.forward;
            desiredDirection.Normalize();

            if (current && current.IsAlive && current.Team == team)
            {
                var stickyDelta = current.transform.position - point;
                stickyDelta.y = 0f;
                if (stickyDelta.sqrMagnitude <= radius * radius && Vector3.Angle(desiredDirection, stickyDelta) <= 72f)
                    return current;
            }

            Health best = null;
            var bestScore = float.MaxValue;
            var active = Health.Active;
            for (var i = 0; i < active.Count; i++)
            {
                var candidate = active[i];
                if (!candidate || !candidate.IsAlive || candidate.Team != team) continue;
                var delta = candidate.transform.position - point;
                delta.y = 0f;
                if (delta.sqrMagnitude < 0.01f) return candidate;

                var distance = delta.magnitude;
                if (distance > radius) continue;
                var angle = Vector3.Angle(desiredDirection, delta);
                var enemy = candidate.GetComponent<EnemyAgent>();
                var threatBonus = enemy && EnemyKinds.IsBoss(enemy.Kind) ? -2.2f
                    : enemy && enemy.Kind == EnemyKind.Elite ? -1.1f
                    : enemy && enemy.Kind == EnemyKind.Marksman ? -0.9f
                    : enemy && enemy.Kind == EnemyKind.Shieldbearer ? 1.4f : 0f;
                var score = distance + angle * 0.035f + threatBonus;
                if (score >= bestScore) continue;
                bestScore = score;
                best = candidate;
            }
            return best;
        }
        /// <summary>
        /// Zielhilfe fuer gezielte Angriffe. Beruecksichtigt nur Gegner in einem schmalen Kegel um
        /// die Zielrichtung und waehlt den, der der Richtung am naechsten liegt - nicht den
        /// naechsten Gegner ueberhaupt. Gegner, die gerade erst erscheinen, werden ignoriert.
        ///
        /// Ersetzt fuer den Spieler FindActionTarget. Dort kostete der Winkel fast nichts
        /// (0,035 je Grad) und es gab keine Winkelgrenze: ein frisch gespawnter Gegner hinter dem
        /// Spieler schlug die Mausrichtung, und jeder Schuss flog zu ihm.
        /// </summary>
        public static Health FindAimAssistTarget(Vector3 point, Vector3 aimDirection, float radius,
            float maxAngle, TeamId team)
        {
            aimDirection.y = 0f;
            if (aimDirection.sqrMagnitude < 0.01f) return null;
            aimDirection.Normalize();

            Health best = null;
            var bestAngle = maxAngle;
            var active = Health.Active;
            for (var i = 0; i < active.Count; i++)
            {
                var candidate = active[i];
                if (!candidate || !candidate.IsAlive || candidate.Team != team) continue;
                var agent = candidate.GetComponent<EnemyAgent>();
                if (agent && agent.IsArriving) continue;
                var delta = candidate.transform.position - point;
                delta.y = 0f;
                var distance = delta.magnitude;
                if (distance < 0.1f || distance > radius) continue;
                var angle = Vector3.Angle(aimDirection, delta);
                if (angle > bestAngle) continue;
                bestAngle = angle;
                best = candidate;
            }
            return best;
        }
    }

    public static class CombatUtility
    {
        /// <summary>
        /// Flaechenschaden. Gibt zurueck, wie viele getroffen wurden - eine Ladung, die ins Leere
        /// geht, soll nichts ausloesen, was ein Treffer ausloest.
        /// </summary>
        public static int Explode(Vector3 point, float radius, float damage, TeamId targetTeam, DamageType type, GameObject source)
        {
            PrototypeVfx.SpawnExplosion(point, radius, PrototypeVfx.ElementColor(type),
                !PartyMember.IsOtherHero(source));
            var hits = 0;
            var active = Health.Active;
            for (var i = active.Count - 1; i >= 0; i--)
            {
                var health = active[i];
                if (!health || !health.IsAlive || health.Team != targetTeam) continue;
                var offset = health.transform.position - point;
                offset.y = 0f;
                if (offset.sqrMagnitude > radius * radius) continue;
                var force = offset.sqrMagnitude > 0.001f ? offset.normalized * 4f : Vector3.zero;
                health.TakeDamage(new DamageInfo(damage, type, source, point, force));
                hits++;
                var status = health.GetComponent<StatusReceiver>();
                if (!status) continue;
                switch (type)
                {
                    case DamageType.Fire: status.ApplyBurn(damage * 0.16f, 2.6f, source); break;
                    case DamageType.Ice: status.ApplySlow(0.62f, 1.8f); break;
                    case DamageType.Poison: status.ApplyPoison(damage * 0.2f, 3.2f, source); break;
                }
            }
            return hits;
        }
    }
}
