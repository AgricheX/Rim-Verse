namespace Rimverse
{
    public static class RimverseUnits
    {
        public const int TicksPerHour = 2500;
        public const int TicksPerDay = 60000;
        public const int DaysPerQuadrum = 15;
        public const int DaysPerYear = 60;
        public const int TicksPerQuadrum = TicksPerDay * DaysPerQuadrum;
        public const int TicksPerYear = TicksPerDay * DaysPerYear;

        public const int SizeClassMin = 1;
        public const int SizeClassMax = 100;

        public const float LightYearsPerParsec = 3.26f;

        public static float DaysFromTicks(int ticks) => ticks / (float)TicksPerDay;

        public static int TicksFromDays(float days) => (int)(days * TicksPerDay);

        public static int TicksFromHours(float hours) => (int)(hours * TicksPerHour);

        public static int ClampSizeClass(int value)
        {
            if (value < SizeClassMin) return SizeClassMin;
            if (value > SizeClassMax) return SizeClassMax;
            return value;
        }
    }
}
