using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace HorrorTools.MacLessBuilder
{
    // GitHub counts macOS runner minutes 10x (Windows 2x) against a monthly allowance quoted in Linux minutes,
    // and rounds every job up to a whole minute.
    internal static class UsageEstimator
    {
        public const string BillingUrl = "https://github.com/settings/billing";
        public const int DefaultBuildMinutes = 18;
        private const int FreePlanMinutes = 2000;
        private const int MacMultiplier = 10;
        private const int MedianSampleSize = 5;
        private const int ProPlanMinutes = 3000;
        private const int WindowsMultiplier = 2;

        public static float AverageBuildMinutes(IEnumerable<MacLessBuilderSettings.BuildRecord> history)
        {
            int[] recent = history
                .Where(r => r.Result == MacLessBuilderSettings.BuildRecord.Succeeded && r.RunMinutes > 0)
                .Take(MedianSampleSize)
                .Select(r => r.RunMinutes)
                .OrderBy(m => m)
                .ToArray();
            if (recent.Length == 0)
            {
                return DefaultBuildMinutes;
            }

            int middle = recent.Length / 2;
            return recent.Length % 2 == 1 ? recent[middle] : (recent[middle - 1] + recent[middle]) / 2f;
        }

        public static Estimate Calculate(int includedMinutes, float usedLinuxMinutes, float buildMinutes, float pricePerMacMinute)
        {
            float includedMac = includedMinutes / (float)MacMultiplier;
            float usedMac = usedLinuxMinutes / MacMultiplier;
            int remaining = Math.Max(0, (int)Math.Floor(includedMac - usedMac));
            int build = Math.Max(1, (int)Math.Ceiling(buildMinutes));
            int overage = Math.Max(0, build - remaining);
            // Rounded to 4 places first so float noise (62.0000001 cents) does not ceil up a whole cent.
            float cost = (float)(Math.Ceiling(Math.Round(overage * (double)pricePerMacMinute * 100, 4)) / 100);
            float usedFraction = includedMac > 0 ? usedMac / includedMac : 1;
            return new Estimate(remaining, build, remaining / build, overage, cost, usedFraction);
        }

        public static int IncludedMinutes(string planName)
        {
            return string.Equals(planName, "pro", StringComparison.OrdinalIgnoreCase) ? ProPlanMinutes : FreePlanMinutes;
        }

        // ponytail: History keeps the last 10 builds, which covers a month of free-plan builds; keep a
        // separate ledger if heavier users need the fallback to be exact.
        public static float LedgerLinuxMinutes(IEnumerable<MacLessBuilderSettings.BuildRecord> history, DateTime month)
        {
            return history
                .Where(r => DateTime.TryParseExact(r.Date, MacLessBuilderSettings.BuildRecord.DateFormat,
                                CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime date)
                            && date.Year == month.Year && date.Month == month.Month)
                .Sum(r => r.RunMinutes * MacMultiplier);
        }

        public static float LinuxEquivalentMinutes(IEnumerable<GitHubClient.UsageItem> items)
        {
            return items
                .Where(i => string.Equals(i.product, "actions", StringComparison.OrdinalIgnoreCase)
                            && string.Equals(i.unitType, "minutes", StringComparison.OrdinalIgnoreCase))
                .Sum(i => i.grossQuantity * Multiplier(i.sku));
        }

        private static int Multiplier(string sku)
        {
            string lower = sku?.ToLowerInvariant() ?? string.Empty;
            return lower.Contains("macos") ? MacMultiplier : lower.Contains("windows") ? WindowsMultiplier : 1;
        }

        internal readonly struct Estimate
        {
            public int BuildMinutes { get; }

            public float Cost { get; }

            public int FreeBuildsLeft { get; }

            public int OverageMinutes { get; }

            public int RemainingMacMinutes { get; }

            public float UsedFraction { get; }

            public Estimate(int remainingMacMinutes, int buildMinutes, int freeBuildsLeft, int overageMinutes, float cost, float usedFraction)
            {
                RemainingMacMinutes = remainingMacMinutes;
                BuildMinutes = buildMinutes;
                FreeBuildsLeft = freeBuildsLeft;
                OverageMinutes = overageMinutes;
                Cost = cost;
                UsedFraction = usedFraction;
            }
        }
    }
}
