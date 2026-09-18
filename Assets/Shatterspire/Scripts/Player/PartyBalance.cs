namespace Shatterspire
{
    /// <summary>
    /// Die Zahlen der Gruppe, an einer Stelle und ohne Unity nachrechenbar.
    ///
    /// Sie haengen alle an einer Entscheidung: die zwei Begleiter sind seit dem Umbau echte Helden
    /// mit allen Aktionen statt fest verdrahteter Treffer. Vorher trugen sie zusammen rund 19 % des
    /// Gruppenschadens. Liesse man sie voll zuschlagen, waeren es 67 % - das Spiel waere fast
    /// zweieinhalbmal so schnell im Toeten, und der Spieler waere der kleinste Teil seiner eigenen
    /// Gruppe.
    ///
    /// Gemessen (nur einfacher Angriff, die Werte aus WeaponSystem und HeroCatalog):
    ///   Spieler allein          44,6 Schaden/s
    ///   alte Begleiter zusammen 10,6  -> Gruppe 55,2
    ///   zwei Helden zu 30 %     27,5  -> Gruppe 72,1, also das 1,30-fache
    ///
    /// Deshalb steigt an zwei Stellen etwas mit: Gegner bekommen dasselbe 1,30-fache an Leben, damit
    /// ein Kampf nicht schneller vorbei ist als heute. Und sie schlagen haerter zu, weil sie sich
    /// jetzt auf drei Ziele verteilen - der Spieler faengt nur noch rund 40 % der Schlaege ab statt
    /// alle. Wer seine Gruppe verliert, steht in beidem allein da: das ist der Punkt.
    /// </summary>
    public static class PartyBalance
    {
        /// <summary>
        /// Anteil des Schadens, den ein Held austeilt, den kein Mensch steuert.
        ///
        /// Kein Wert aus der Fiktion, sondern eine Stellschraube: ein Bot kann einen Kampf nicht
        /// lesen. Er stellt sich hin und schlaegt zu. Wer ihm vollen Schaden gibt, bekommt drei
        /// Spieler zum Preis von einem.
        /// </summary>
        public const float AllyDamage = 0.3f;

        /// <summary>
        /// Wie schnell ein gefallenes Mitglied zurueckkommt: zu Beginn der naechsten Etage, mit so
        /// viel Leben. Eine Etage ohne Gruppe zu Ende zu bringen, ist die Strafe - nicht ein Aufstieg
        /// ohne Gruppe.
        /// </summary>
        public const float RejoinHealth = 0.6f;

        /// <summary>
        /// Anteil der Gegnerschlaege, der beim Spieler landet, wenn die Gruppe steht. Geschaetzt aus
        /// der Aufstellung: der Nahkaempfer geht vor und zieht am meisten, der Schuetze haelt sich
        /// heraus. Steht hier, weil die Gegnerzahlen daran haengen - und damit eine spaetere Messung
        /// eine Zahl zum Vergleichen hat.
        /// </summary>
        public const float PlayerThreatShare = 0.4f;
    }
}
