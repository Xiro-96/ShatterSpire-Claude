using System;

namespace Shatterspire
{
    /// <summary>
    /// Ein Shift ist der saisonale Takt, in dem gewertet wird. Am Ende eines Shifts
    /// zaehlt der erreichte Rang, es gibt Tokens dafuer, und die Climb-Bestwerte
    /// werden zurueckgesetzt — der naechste Shift beginnt fuer alle wieder bei null.
    ///
    /// Der Index wird aus dem Kalender berechnet und nicht gespeichert. Damit laeuft
    /// die Saison auch offline korrekt weiter und kann nicht dadurch verlaengert
    /// werden, dass jemand das Spiel eine Woche nicht startet.
    /// </summary>
    public static class ShiftCalendar
    {
        public const int DaysPerShift = 7;

        /// <summary>Montag, 5. Januar 2026, 00:00 UTC. Shift 0 beginnt hier.</summary>
        private static readonly DateTime Epoch = new(2026, 1, 5, 0, 0, 0, DateTimeKind.Utc);

        public static int CurrentIndex => IndexFor(DateTime.UtcNow);

        public static int IndexFor(DateTime utc)
        {
            var days = (utc.Date - Epoch.Date).TotalDays;
            // Vor der Epoche gibt es keinen Shift. Negative Indizes wuerden die
            // Rollover-Logik verwirren, deshalb hart auf null geklemmt.
            if (days < 0d) return 0;
            return (int)(days / DaysPerShift);
        }

        public static DateTime StartOf(int shiftIndex)
            => Epoch.AddDays(Math.Max(0, shiftIndex) * (double)DaysPerShift);

        public static DateTime EndOf(int shiftIndex) => StartOf(shiftIndex + 1);

        public static TimeSpan RemainingIn(DateTime utc)
        {
            var remaining = EndOf(IndexFor(utc)) - utc;
            return remaining < TimeSpan.Zero ? TimeSpan.Zero : remaining;
        }

        public static TimeSpan Remaining => RemainingIn(DateTime.UtcNow);

        /// <summary>Kurzform fuer die Anzeige, etwa "4T 06H" oder "06H 12M".</summary>
        public static string Countdown(TimeSpan remaining)
        {
            if (remaining <= TimeSpan.Zero) return "ENDET";
            if (remaining.TotalDays >= 1d) return $"{remaining.Days}T {remaining.Hours:00}H";
            if (remaining.TotalHours >= 1d) return $"{remaining.Hours:00}H {remaining.Minutes:00}M";
            return $"{remaining.Minutes:00}M";
        }
    }
}
