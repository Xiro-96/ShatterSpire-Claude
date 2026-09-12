using System;
using System.Collections.Generic;
using UnityEngine;

namespace Shatterspire
{
    /// <summary>Was beim Haendler zu kaufen ist.</summary>
    public enum ShopOfferId { Whetstone, OiledGears, IronRation, FocusLens, Counterweight, ForgedBlade }

    public sealed class ShopOffer
    {
        public ShopOfferId Id;
        public string Name;
        public string Description;
        public int BasePrice;
        /// <summary>Aufschlag je bereits gekauftem Exemplar, als Anteil des Grundpreises.</summary>
        public float PriceStep;
        public Color Color;
        /// <summary>Nur einmal kaufbar: Waffen ersetzen sich nicht selbst.</summary>
        public bool Unique;
    }

    public static class ShopCatalog
    {
        private static readonly Color Common = new(0.72f, 0.77f, 0.82f);
        private static readonly Color Rare = new(0.18f, 0.65f, 1f);
        private static readonly Color Legendary = new(1f, 0.58f, 0.1f);

        public static readonly IReadOnlyList<ShopOffer> All = new[]
        {
            new ShopOffer
            {
                Id = ShopOfferId.Whetstone, Name = "WHETSTONE", BasePrice = 40, PriceStep = 0.7f,
                Description = "All damage +12%.", Color = Common
            },
            new ShopOffer
            {
                Id = ShopOfferId.OiledGears, Name = "OILED GEARS", BasePrice = 45, PriceStep = 0.8f,
                Description = "Attack speed +8%.", Color = Common
            },
            new ShopOffer
            {
                Id = ShopOfferId.IronRation, Name = "IRON RATION", BasePrice = 35, PriceStep = 0.5f,
                Description = "Maximum health +25, healed.", Color = Common
            },
            new ShopOffer
            {
                Id = ShopOfferId.FocusLens, Name = "FOCUS LENS", BasePrice = 55, PriceStep = 0.9f,
                Description = "Critical chance +6%.", Color = Rare
            },
            new ShopOffer
            {
                Id = ShopOfferId.Counterweight, Name = "COUNTERWEIGHT", BasePrice = 60, PriceStep = 0.8f,
                Description = "Heavy attack damage +18%.", Color = Rare
            },
            new ShopOffer
            {
                Id = ShopOfferId.ForgedBlade, Name = "FORGED WEAPON", BasePrice = 130, PriceStep = 0f,
                Description = "Damage +30%, attack speed -6%. Once per climb.", Color = Legendary,
                Unique = true
            }
        };

        public static ShopOffer Find(ShopOfferId id)
        {
            for (var i = 0; i < All.Count; i++)
                if (All[i].Id == id) return All[i];
            return null;
        }

        /// <summary>Preis steigt mit jedem Kauf derselben Ware - sonst kauft man nur noch eine Sache.</summary>
        public static int PriceOf(ShopOffer offer, int alreadyBought)
            => Mathf.RoundToInt(offer.BasePrice * (1f + offer.PriceStep * alreadyBought));
    }

    /// <summary>
    /// Gold eines Aufstiegs. Haengt am Spieler und nicht an einer statischen Ablage: im Co-op hat jeder
    /// sein eigenes Konto, und ein Aufstieg beginnt wieder bei null.
    /// </summary>
    public sealed class RunWallet : MonoBehaviour
    {
        private readonly Dictionary<ShopOfferId, int> bought = new();
        public int Gold { get; private set; }
        public event Action Changed;

        /// <summary>Gold fuer einen erledigten Gegner. Tiefere Etagen zahlen besser.</summary>
        public static int RewardFor(EnemyKind kind, int floor)
        {
            var basis = kind switch
            {
                EnemyKind.Crawler => 3,
                EnemyKind.Shooter => 4,
                EnemyKind.Marksman => 5,
                EnemyKind.Shieldbearer => 7,
                EnemyKind.Brute => 11,
                EnemyKind.Elite => 28,
                EnemyKind.IronWarden => 140,
                _ => 3
            };
            return Mathf.Max(1, Mathf.RoundToInt(basis * (1f + Mathf.Max(0, floor - 1) * 0.12f)));
        }

        public void Earn(int amount)
        {
            if (amount <= 0) return;
            Gold += amount;
            Changed?.Invoke();
        }

        public int TimesBought(ShopOfferId id) => bought.TryGetValue(id, out var count) ? count : 0;

        public int PriceOf(ShopOffer offer) => ShopCatalog.PriceOf(offer, TimesBought(offer.Id));

        public bool CanAfford(ShopOffer offer)
            => offer != null && !(offer.Unique && TimesBought(offer.Id) > 0) && Gold >= PriceOf(offer);

        /// <summary>Kauft eine Ware und wendet sie auf den Aufbau an. Gibt false zurueck, wenn es nicht reicht.</summary>
        public bool Buy(ShopOffer offer, PlayerBuild build)
        {
            if (!CanAfford(offer) || !build) return false;
            Gold -= PriceOf(offer);
            bought[offer.Id] = TimesBought(offer.Id) + 1;
            build.ApplyPurchase(offer.Id);
            Changed?.Invoke();
            return true;
        }
    }
}
