using System;

namespace EventEngine
{
    // Pure scheduling rules used by the simulation and the offline regression harness.
    internal static class EventSchedule
    {
        // CS2's calendar can have only 12/24 simulated days per year. Its public
        // DateTime jumps to the next calendar year at that boundary. Use a continuous
        // internal day count so an overnight event does not instantly expire then.
        internal static DateTime SimulationClock(int year, int daysPerYear, float date, float time)
        {
            if (year < 1 || daysPerYear < 1 || float.IsNaN(date) || float.IsNaN(time) ||
                date < 0f || date >= 1f || time < 0f || time >= 1f)
                throw new ArgumentOutOfRangeException("Invalid simulation clock values");
            double days = ((double)year - 1) * daysPerYear + Math.Floor(daysPerYear * (double)date) + time;
            return DateTime.MinValue.AddDays(days);
        }

        internal static DateTime NextStart(DateTime now, int hour, int minute)
        {
            var start = now.Date.AddHours(hour).AddMinutes(minute);
            return start <= now ? start.AddDays(1) : start;
        }

        internal static int MinutesUntil(DateTime now, DateTime start) =>
            Math.Max(0, (int)Math.Ceiling((start - now).TotalMinutes));

        internal static bool CanGenerateArrivals(EventPhase phase, int minutesUntil) =>
            (phase == EventPhase.Scheduled || phase == EventPhase.Arrivals) &&
            minutesUntil > 0 && minutesUntil <= 120;

        internal static int ArrivalTarget(int expected, int minutesUntil, float intensity)
        {
            if (float.IsNaN(intensity) || float.IsInfinity(intensity)) intensity = 1f;
            double progress = Math.Max(0, Math.Min(1, (120d - minutesUntil) / 120d));
            return (int)Math.Min(expected, Math.Floor(expected * progress * Math.Max(0, intensity)));
        }
    }
}
