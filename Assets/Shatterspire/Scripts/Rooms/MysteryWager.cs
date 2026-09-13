using System.Collections.Generic;
using UnityEngine;

namespace Shatterspire
{
    /// <summary>Wie hoch der Einsatz eines Altars ist. Das ist die Information, die offen liegt.</summary>
    public enum WagerStake { Small, Large }

    /// <summary>
    /// Woran eine Wette dreht. Bewusst getrennt von der Wette selbst: die Beschreibung wird aus
    /// Wirkung und Betrag gebaut, und der Aufbau des Helden liest ebenfalls nur diese zwei Felder.
    /// Damit kann kein Text eine Zahl nennen, die das Spiel nicht anwendet.
    /// </summary>
    public enum WagerEffect
    {
        Damage, AttackSpeed, MoveSpeed, UltimateCharge,
        MaxHealth, HealthShare, Gold, GoldShare
    }

    // Neue Wetten kommen ans Ende, damit gespeicherte Zahlen ihre Bedeutung behalten.
    public enum WagerId
    {
        None,
        Whetted, Breath, Quickened, Purse,
        Leaden, Thirst, Rusted,
        Honed, Lungs, Surge, Hoard,
        Shackled, Bloodletting, Blunted, Debt
    }

    public readonly struct Wager
    {
        public readonly WagerId Id;
        public readonly string Name;
        public readonly WagerStake Stake;
        public readonly WagerEffect Effect;

        /// <summary>
        /// Faktor bei den multiplikativen Wirkungen, glatter Betrag bei Leben und Gold, Anteil bei
        /// den Share-Wirkungen. Das Vorzeichen entscheidet, ob es ein Segen ist.
        /// </summary>
        public readonly float Amount;

        public Wager(WagerId id, string name, WagerStake stake, WagerEffect effect, float amount)
        {
            Id = id;
            Name = name;
            Stake = stake;
            Effect = effect;
            Amount = amount;
        }

        /// <summary>Multiplikative Wirkungen rechnen um 1, die uebrigen um 0.</summary>
        public bool IsMultiplier => Effect == WagerEffect.Damage || Effect == WagerEffect.AttackSpeed
                                    || Effect == WagerEffect.MoveSpeed || Effect == WagerEffect.UltimateCharge;

        /// <summary>
        /// Abgeleitet, nicht gespeichert: eine Wette, die als Segen gefuehrt wird und dem Spieler
        /// schadet, kann so nicht entstehen.
        /// </summary>
        public bool IsBoon => IsMultiplier ? Amount > 1f : Amount > 0f;

        public bool IsNothing => Id == WagerId.None;
    }

    /// <summary>
    /// Der Raetselraum als Wette.
    ///
    /// Vorher: "MYSTERY - unbekannte Begegnung" war ein Muenzwurf im Spawner, ob ueberhaupt
    /// Verteidiger kommen. Der Raum hielt nie ein Versprechen, weil es keins gab.
    ///
    /// Jetzt stehen zwei Altaere im Raum, einer mit kleinem und einer mit grossem Einsatz. Der
    /// Einsatz liegt offen, der Inhalt nicht. Man darf genau einen beruehren - oder beide stehen
    /// lassen und den Raum ohne Wette verlassen. Das ist die Entscheidung: wie weit will ich
    /// ausschlagen, und will ich ueberhaupt.
    ///
    /// Sechs von zehn Ziehungen sind ein Segen. Positiv im Erwartungswert, damit das Beruehren
    /// meistens richtig ist - und vier von zehn sind genug, dass es sich wie eine Wette anfuehlt.
    /// </summary>
    public static class WagerCatalog
    {
        /// <summary>Anteil der Segen an den Ziehungen.</summary>
        public const float BoonChance = 0.6f;

        private static readonly Wager[] Table =
        {
            new(WagerId.None, "EMPTY ALTAR", WagerStake.Small, WagerEffect.Damage, 1f),

            // ── Kleiner Einsatz ────────────────────────────────────────────
            new(WagerId.Whetted, "WHETTED", WagerStake.Small, WagerEffect.Damage, 1.12f),
            new(WagerId.Breath, "BREATH", WagerStake.Small, WagerEffect.MaxHealth, 20f),
            new(WagerId.Quickened, "QUICKENED", WagerStake.Small, WagerEffect.AttackSpeed, 1.08f),
            new(WagerId.Purse, "PURSE", WagerStake.Small, WagerEffect.Gold, 50f),
            new(WagerId.Leaden, "LEADEN", WagerStake.Small, WagerEffect.MoveSpeed, 0.94f),
            new(WagerId.Thirst, "THIRST", WagerStake.Small, WagerEffect.HealthShare, -0.15f),
            new(WagerId.Rusted, "RUSTED", WagerStake.Small, WagerEffect.AttackSpeed, 0.95f),

            // ── Grosser Einsatz ───────────────────────────────────────────
            new(WagerId.Honed, "HONED", WagerStake.Large, WagerEffect.Damage, 1.3f),
            new(WagerId.Lungs, "LUNGS", WagerStake.Large, WagerEffect.MaxHealth, 50f),
            new(WagerId.Surge, "SURGE", WagerStake.Large, WagerEffect.UltimateCharge, 1.35f),
            new(WagerId.Hoard, "HOARD", WagerStake.Large, WagerEffect.Gold, 150f),
            new(WagerId.Shackled, "SHACKLED", WagerStake.Large, WagerEffect.MoveSpeed, 0.85f),
            new(WagerId.Bloodletting, "BLOODLETTING", WagerStake.Large, WagerEffect.HealthShare, -0.35f),
            new(WagerId.Blunted, "BLUNTED", WagerStake.Large, WagerEffect.Damage, 0.88f),
            new(WagerId.Debt, "DEBT", WagerStake.Large, WagerEffect.GoldShare, -0.5f)
        };

        public static IReadOnlyList<Wager> All => Table;

        public static Wager For(WagerId id)
        {
            for (var i = 0; i < Table.Length; i++)
                if (Table[i].Id == id) return Table[i];
            return Table[0];
        }

        /// <summary>Alle Wetten eines Einsatzes, getrennt nach Segen und Fluch.</summary>
        public static List<Wager> Of(WagerStake stake, bool boons)
        {
            var list = new List<Wager>(4);
            foreach (var wager in Table)
            {
                if (wager.IsNothing || wager.Stake != stake || wager.IsBoon != boons) continue;
                list.Add(wager);
            }
            return list;
        }

        /// <summary>
        /// Was ein Altar traegt. Reine Funktion: im Co-op muessen die Altaere bei allen Spielern
        /// dasselbe enthalten, und ein Lauf-Seed muss eine Etage wiederherstellen koennen.
        ///
        /// Das <paramref name="salt"/> unterscheidet die Altaere desselben Raums.
        /// </summary>
        public static WagerId Draw(int runSeed, int floor, WagerStake stake, int salt)
        {
            var boon = RunRandom.Chance(runSeed, floor, salt * 31 + (int)stake, BoonChance);
            var pool = Of(stake, boon);
            if (pool.Count == 0) return WagerId.None;
            return pool[RunRandom.Index(runSeed, floor, salt * 7 + (int)stake + 11, pool.Count)].Id;
        }

        /// <summary>
        /// Die Wirkung in Worten, aus Wirkung und Betrag gebaut - derselbe Grundsatz wie bei den
        /// Anomalien: wer die Zahl aendert, aendert den Text mit.
        /// </summary>
        public static string Describe(in Wager wager)
        {
            if (wager.IsNothing) return Loc.T("NOTHING HAPPENS");
            var label = Loc.T(LabelOf(wager.Effect));
            if (wager.IsMultiplier) return $"{label} ×{Loc.Number(wager.Amount)}";
            if (wager.Effect == WagerEffect.HealthShare || wager.Effect == WagerEffect.GoldShare)
                return $"{label} {Signed(wager.Amount * 100f)} %";
            return $"{label} {Signed(wager.Amount)}";
        }

        /// <summary>Mit Vorzeichen, und mit dem echten Minuszeichen statt eines Bindestrichs.</summary>
        private static string Signed(float value)
            => value >= 0f ? "+" + Loc.Number(value) : "−" + Loc.Number(-value);

        private static string LabelOf(WagerEffect effect) => effect switch
        {
            WagerEffect.Damage => "DAMAGE",
            WagerEffect.AttackSpeed => "ATTACK SPEED",
            WagerEffect.MoveSpeed => "MOVE SPEED",
            WagerEffect.UltimateCharge => "ULTIMATE CHARGE",
            WagerEffect.MaxHealth => "MAXIMUM HEALTH",
            WagerEffect.HealthShare => "HEALTH",
            WagerEffect.GoldShare => "GOLD",
            _ => "GOLD"
        };

        public static string NameOf(WagerStake stake)
            => Loc.T(stake == WagerStake.Small ? "SMALL STAKE" : "HIGH STAKE");

        public static Color AccentOf(WagerStake stake)
            => stake == WagerStake.Small
                ? new Color(0.42f, 0.78f, 1f)
                : new Color(1f, 0.42f, 0.72f);
    }
}
