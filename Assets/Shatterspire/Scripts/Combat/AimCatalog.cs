namespace Shatterspire
{
    /// <summary>
    /// Welche Form jede Aktion jedes Helden auf dem Boden hat, und wie weit sie reicht.
    ///
    /// Die Zahlen stehen hier und nicht in der Anzeige, damit die Anzeige nicht behaupten kann, was
    /// die Aktion nicht tut - und sie stehen ausserhalb von WeaponSystem, damit sie sich ohne
    /// laufendes Spiel nachrechnen lassen. AimCatalogTests prueft, dass jeder Held in jeder der vier
    /// Aktionen eine Form hat: eine vergessene Klasse waere sonst eine Aktion ohne Zielanzeige, und
    /// das faellt beim Spielen kaum auf.
    /// </summary>
    public static class AimCatalog
    {
        public static AimDescription Describe(HeroClassId hero, ActionSlot slot, int pierces,
            bool wideReach, float shotRange, float verdictRange) => slot switch
        {
            ActionSlot.Light => hero switch
            {
                // Die Laenge kommt aus der Norm, nicht aus einer Zahl daneben. Sie stand hier auf
                // 2,8 und 2,7, waehrend die Schlaege 3,25 und 2,60 weit reichten - der Keil am Boden
                // log in beide Richtungen. Jetzt gibt es eine Quelle fuer beides.
                HeroClassId.Guardian => new AimDescription(AimShape.Wedge,
                    ActionBalance.MeleeReach(hero), 1.35f + pierces * 0.28f),
                HeroClassId.Paladin => new AimDescription(AimShape.Wedge,
                    ActionBalance.MeleeReach(hero), 1.5f + pierces * 0.3f),
                HeroClassId.Bomber => new AimDescription(AimShape.Circle, 6.5f + pierces * 0.8f, 2.2f),
                _ => new AimDescription(AimShape.Line, shotRange, 0.4f)
            },
            ActionSlot.Heavy => hero switch
            {
                HeroClassId.Paladin => new AimDescription(AimShape.Circle, verdictRange, 2.6f),
                HeroClassId.Bomber => new AimDescription(AimShape.Circle, 7.5f, 2.6f),
                HeroClassId.Guardian => new AimDescription(AimShape.Circle, 1.4f, 3.15f),
                HeroClassId.Arcanist => new AimDescription(AimShape.Circle, 5f, 3f),
                _ => new AimDescription(AimShape.Line, shotRange, 0.5f)
            },
            ActionSlot.Skill => hero switch
            {
                HeroClassId.Guardian => new AimDescription(AimShape.Wedge, 4.6f, 1.55f),
                HeroClassId.Bomber => new AimDescription(AimShape.Wedge, 9.8f, 2.6f),
                HeroClassId.Paladin => new AimDescription(AimShape.Wedge, wideReach ? 16f : 11f, AshWave.HalfWidth),
                HeroClassId.Arcanist => new AimDescription(AimShape.Circle, 5.5f, 4.2f),
                _ => new AimDescription(AimShape.Wedge, 15f, 4f)
            },
            ActionSlot.Ultimate => hero switch
            {
                HeroClassId.Guardian => new AimDescription(AimShape.Circle, 7.5f, 4f),
                HeroClassId.Arcanist => new AimDescription(AimShape.Circle, 5.5f, 4f),
                HeroClassId.Bomber => new AimDescription(AimShape.Around, 0f, 6f),
                // Vergeltung und Jaegerblick wirken auf den Helden selbst: ein Kreis um ihn herum
                // sagt genau das, und eine Bahn nach vorn waere eine Luege.
                _ => new AimDescription(AimShape.Around, 0f, 2.5f)
            },
            _ => AimDescription.Nothing
        };
    }
}
