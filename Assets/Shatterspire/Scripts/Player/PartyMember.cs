using System.Collections.Generic;
using UnityEngine;

namespace Shatterspire
{
    /// <summary>Wo einer in der Aufstellung steht - nicht, welche Klasse er spielt.</summary>
    public enum PartySlot { Frontline, Flank, Rear }

    /// <summary>
    /// Ein Mitglied der Gruppe. Sitzt auf jedem der drei Helden, auch auf dem des Spielers.
    ///
    /// Das ist der Kern des Umbaus: vorher war der Spieler ein Held und die zwei anderen waren
    /// etwas anderes - eine eigene, vereinfachte Klasse ohne Leben, ohne Klasse, ohne Upgrades.
    /// Jetzt sind alle drei dasselbe, und der einzige Unterschied ist, woher die Eingaben kommen:
    /// vom Bildschirm dieses Geraets, von einem Bot - oder spaeter aus dem Netz.
    ///
    /// Die Liste der Aktiven folgt demselben Muster wie <see cref="Health.Active"/>: Eintraege
    /// melden sich selbst an und ab, und beim Start einer Sitzung ist sie leer.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PartyMember : MonoBehaviour
    {
        private static readonly List<PartyMember> ActiveMembers = new();

        /// <summary>Radius des Koerpers eines Helden. Gegner halten mindestens diesen Abstand plus ihren eigenen.</summary>
        public const float BodyRadius = 0.45f;

        /// <summary>
        /// Die Physik-Ebene der Helden (ProjectSettings/TagManager, "Party"). Sie ignoriert sich selbst:
        /// Mitglieder einer Gruppe laufen durcheinander hindurch.
        ///
        /// Der Selbsttest fand den Helden in der Luecke zwischen zwei Kistenreihen, zwei Begleiter
        /// darin, und keiner kam vorbei - die Begleiter traten zur Seite, aber dort stand die Deckung.
        /// Wie in den meisten Co-op-Spielen blockieren sich Verbuendete nicht; Abstand halten die
        /// Bots weiter ueber ihre weiche Trennung, und Gegner bleiben fest.
        /// </summary>
        public const int Layer = 6;

        public const string LayerName = "Party";

        /// <summary>Stellt die Ebene auf einen Helden und alles an ihm, und laesst die Ebene sich selbst ignorieren.</summary>
        public static void PassThroughEachOther(GameObject hero)
        {
            Physics.IgnoreLayerCollision(Layer, Layer, true);
            if (!hero) return;
            foreach (var part in hero.GetComponentsInChildren<Transform>(true)) part.gameObject.layer = Layer;
        }

        /// <summary>Alle lebenden und gefallenen Mitglieder der Gruppe, in der Reihenfolge des Beitritts.</summary>
        public static IReadOnlyList<PartyMember> Active => ActiveMembers;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry() => ActiveMembers.Clear();

        private Health health;

        public string DisplayName { get; private set; } = string.Empty;
        public Color Accent { get; private set; } = Color.white;
        public HeroClassId HeroClass { get; private set; }
        public PartySlot Slot { get; private set; }

        /// <summary>Steuert dieses Geraet diesen Helden? Genau einer in der Gruppe sagt ja.</summary>
        public bool IsLocal { get; private set; }

        /// <summary>Was dieses Mitglied gerade tut - fuer die Team-Leiste im HUD.</summary>
        public string Status { get; internal set; } = "FOLLOWING";

        /// <summary>
        /// Das Leben dieses Helden. Wird bei Bedarf geholt, nicht nur in Awake: ausserhalb des
        /// laufenden Spiels - in den Tests - ruft Unity Awake gar nicht auf, und ein Mitglied ohne
        /// Leben ist kein Mitglied, sondern ein Absturz.
        /// </summary>
        public Health Health => health ? health : health = GetComponent<Health>();

        public bool IsAlive => Health && Health.IsAlive;

        private void Awake() => Register();

        private void OnEnable() => Register();

        private void OnDisable() => ActiveMembers.Remove(this);

        /// <summary>
        /// Meldet dieses Mitglied an und raeumt dabei auf. Zweimal anmelden geht nicht, und
        /// geloeschte Eintraege fliegen raus - ohne das waechst die Liste ueber eine Sitzung hinweg,
        /// weil OnDisable im Editor nicht laeuft.
        /// </summary>
        private void Register()
        {
            for (var i = ActiveMembers.Count - 1; i >= 0; i--)
                if (!ActiveMembers[i]) ActiveMembers.RemoveAt(i);
            if (!ActiveMembers.Contains(this)) ActiveMembers.Add(this);
        }

        public void Configure(string displayName, Color accent, HeroClassId heroClass, PartySlot slot, bool local)
        {
            Register();
            DisplayName = displayName;
            Accent = accent;
            HeroClass = heroClass;
            Slot = slot;
            IsLocal = local;
        }

        /// <summary>Der Held, den dieses Geraet steuert. Null, solange die Gruppe noch nicht steht.</summary>
        public static PartyMember Local
        {
            get
            {
                for (var i = 0; i < ActiveMembers.Count; i++)
                    if (ActiveMembers[i] && ActiveMembers[i].IsLocal) return ActiveMembers[i];
                return null;
            }
        }

        /// <summary>
        /// Naechstes lebendes Mitglied zu diesem Punkt. Gegner suchen sich darueber ihr Ziel - vorher
        /// hing an jedem Gegner fest der Spieler, und die Begleiter waren unantastbar.
        /// </summary>
        public static Health ClosestAlive(Vector3 point, Health preferred = null, float stickiness = 0f)
        {
            Health best = null;
            var bestDistance = float.MaxValue;
            for (var i = 0; i < ActiveMembers.Count; i++)
            {
                var member = ActiveMembers[i];
                if (!member || !member.IsAlive) continue;
                var offset = member.transform.position - point;
                offset.y = 0f;
                var distance = offset.magnitude;
                // Das bisherige Ziel behaelt einen Bonus. Ohne ihn wechselt ein Gegner die Seite,
                // sobald zwei Helden fast gleich weit weg stehen - und trifft dann keinen von beiden.
                if (member.Health == preferred) distance -= stickiness;
                if (distance >= bestDistance) continue;
                bestDistance = distance;
                best = member.Health;
            }
            return best;
        }

        /// <summary>
        /// Gehoert dieses Objekt zum Helden an diesem Geraet? Fuer Treffer ist das die Quelle
        /// (<see cref="DamageInfo.Source"/>) - Geschosse, Bomben und Wellen tragen dort den Helden,
        /// der sie geworfen hat.
        /// </summary>
        public static bool IsLocalHero(GameObject source)
        {
            if (!source) return false;
            var member = source.GetComponentInParent<PartyMember>();
            return member && member.IsLocal;
        }

        /// <summary>
        /// Gehoert dieses Objekt zu einem Helden, den dieses Geraet nicht steuert - einem Bot, und
        /// spaeter einem Mitspieler? Dann bekommt der Spieler hier keine Zeitlupe und keinen
        /// Kamerastoss davon. Alles ohne Helden - Gegner, Fallen, die Welt - bleibt, wie es war.
        /// </summary>
        public static bool IsOtherHero(GameObject source)
        {
            if (!source) return false;
            var member = source.GetComponentInParent<PartyMember>();
            return member && !member.IsLocal;
        }

        /// <summary>Lebt ueberhaupt noch jemand? Solange nicht, ist der Aufstieg vorbei.</summary>
        public static bool AnyAlive()
        {
            for (var i = 0; i < ActiveMembers.Count; i++)
                if (ActiveMembers[i] && ActiveMembers[i].IsAlive) return true;
            return false;
        }

        /// <summary>
        /// Wer die Gruppe begleitet, wenn der Spieler diesen Helden waehlt.
        ///
        /// Einer vorn, einer hinten: die Gruppe soll beide Entfernungen abdecken, sonst steht sie im
        /// Bosskampf entweder komplett im Nahkampf oder komplett am Rand. Der gewaehlte Held faellt
        /// heraus - zweimal derselbe waere kein Team, sondern ein Spiegel.
        /// </summary>
        public static (HeroClassId Hero, PartySlot Slot)[] OfflineTeamFor(HeroClassId selected)
        {
            var melee = FirstOther(selected, HeroClassId.Guardian, HeroClassId.Paladin);
            var ranged = FirstOther(selected, HeroClassId.Ranger, HeroClassId.Arcanist, HeroClassId.Bomber);
            // Der Spieler steht in der Mitte. Der Nahkaempfer geht vor, der Fernkaempfer haelt die
            // Flanke - und wenn der Spieler selbst der Nahkaempfer ist, rutscht der Begleiter nach
            // hinten, damit nicht beide im selben Handgemenge stehen.
            var meleeSlot = HeroCatalog.IsMelee(selected) ? PartySlot.Rear : PartySlot.Frontline;
            return new[] { (melee, meleeSlot), (ranged, PartySlot.Flank) };
        }

        private static HeroClassId FirstOther(HeroClassId selected, params HeroClassId[] candidates)
        {
            for (var i = 0; i < candidates.Length; i++)
                if (candidates[i] != selected) return candidates[i];
            return candidates[0];
        }
    }
}
