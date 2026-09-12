namespace Shatterspire
{
    /// <summary>
    /// Streuwerte aus festen Eingaben statt aus <see cref="UnityEngine.Random"/>.
    ///
    /// Ueberall, wo ein Ergebnis reproduzierbar sein muss, gehoert der Zufall hierher: im Co-op
    /// muessen alle drei Spieler dieselben Angebote und dieselbe Etage sehen, ohne dass einer sie
    /// dem anderen schickt, und ein Lauf-Seed muss eine ganze Etagenfolge wiederherstellen koennen -
    /// fuer die Fehlersuche und spaeter fuer den Abgleich zwischen Client und Host.
    ///
    /// Nichts daran ist kryptografisch. Es muss nur gleichmaessig streuen und auf jeder Maschine
    /// dasselbe Ergebnis liefern, deshalb ausschliesslich Ganzzahl-Arithmetik ohne Gleitkomma.
    /// </summary>
    public static class RunRandom
    {
        /// <summary>Knuths Multiplikator, und drei Primzahlen zum Auseinanderziehen der Achsen.</summary>
        private const uint Golden = 2654435761u;

        public static uint Hash(int runSeed, int floor, int salt)
        {
            var value = unchecked((uint)runSeed * Golden + (uint)floor * 40503u + (uint)salt * 2246822519u);
            // Ohne Nachlauf streuen die unteren Bits zu schlecht - und genau die werden per Modulo
            // abgefragt. Zwei Xorshift-Runden mischen die oberen nach unten.
            value ^= value >> 13;
            value = unchecked(value * Golden);
            value ^= value >> 16;
            return value;
        }

        /// <summary>Ein Index in [0, count). Bei count &lt;= 0 immer 0.</summary>
        public static int Index(int runSeed, int floor, int salt, int count)
            => count <= 0 ? 0 : (int)(Hash(runSeed, floor, salt) % (uint)count);

        /// <summary>Eine Entscheidung mit der angegebenen Wahrscheinlichkeit.</summary>
        public static bool Chance(int runSeed, int floor, int salt, float probability)
        {
            if (probability <= 0f) return false;
            if (probability >= 1f) return true;
            // Auf zehntausend Stufen aufgeloest: feiner als jede Wahrscheinlichkeit, die das Spiel
            // benutzt, und die Umrechnung bleibt in Ganzzahlen vergleichbar.
            return Hash(runSeed, floor, salt) % 10000u < (uint)(probability * 10000f);
        }
    }
}
