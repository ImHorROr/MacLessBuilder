using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace HorrorTools.MacLessBuilder.Tests
{
    public class UsageEstimatorTests
    {
        private const float Price = 0.062f;

        [Test]
        public void AverageBuildMinutes_NoHistory_UsesDefault()
        {
            Assert.AreEqual(UsageEstimator.DefaultBuildMinutes, UsageEstimator.AverageBuildMinutes(new List<MacLessBuilderSettings.BuildRecord>()));
        }

        [Test]
        public void AverageBuildMinutes_TakesMedianOfLatestFiveSuccessfulRuns()
        {
            var history = new List<MacLessBuilderSettings.BuildRecord>
            {
                Record(20),
                Record(14),
                Record(99, "Failed at Building"),
                Record(16),
                Record(0),
                Record(15),
                Record(40),
                Record(1),
            };

            // Latest five successful runs with minutes: 20, 14, 16, 15, 40 -> median 16. The 1 is older than the window.
            Assert.AreEqual(16f, UsageEstimator.AverageBuildMinutes(history));
        }

        [Test]
        public void Calculate_FreePlanWithMinutesLeft_CostsNothing()
        {
            // 580 Linux minutes used = 58 macOS minutes of the 200 included.
            UsageEstimator.Estimate estimate = UsageEstimator.Calculate(2000, 580, 16, Price);

            Assert.AreEqual(142, estimate.RemainingMacMinutes);
            Assert.AreEqual(8, estimate.FreeBuildsLeft);
            Assert.AreEqual(0, estimate.OverageMinutes);
            Assert.AreEqual(0f, estimate.Cost);
        }

        [Test]
        public void Calculate_AllowanceNearlyGone_EstimatesOverageCost()
        {
            UsageEstimator.Estimate estimate = UsageEstimator.Calculate(2000, 1950, 16, Price);

            Assert.AreEqual(5, estimate.RemainingMacMinutes);
            Assert.AreEqual(0, estimate.FreeBuildsLeft);
            Assert.AreEqual(11, estimate.OverageMinutes);
            Assert.AreEqual(0.69f, estimate.Cost, 0.0001f);
        }

        [Test]
        public void Calculate_RoundsBuildUpAndCostToWholeCents()
        {
            // 17.2 minutes bills as 18; 18 x 0.062 = 1.116 rounds up to 1.12, and an exact 10 x 0.062 stays 0.62.
            Assert.AreEqual(1.12f, UsageEstimator.Calculate(2000, 3000, 17.2f, Price).Cost, 0.0001f);
            Assert.AreEqual(0.62f, UsageEstimator.Calculate(2000, 2000, 10, Price).Cost, 0.0001f);
        }

        [Test]
        public void IncludedMinutes_ByPlan()
        {
            Assert.AreEqual(2000, UsageEstimator.IncludedMinutes("free"));
            Assert.AreEqual(3000, UsageEstimator.IncludedMinutes("Pro"));
            Assert.AreEqual(2000, UsageEstimator.IncludedMinutes(null));
        }

        [Test]
        public void LedgerLinuxMinutes_CountsOnlyThisMonthAtMacRate()
        {
            var history = new List<MacLessBuilderSettings.BuildRecord>
            {
                new MacLessBuilderSettings.BuildRecord { Date = "2026-09-27 08:10", RunMinutes = 12 },
                new MacLessBuilderSettings.BuildRecord { Date = "2026-09-02 10:00", RunMinutes = 5 },
                new MacLessBuilderSettings.BuildRecord { Date = "2026-08-31 23:59", RunMinutes = 30 },
                new MacLessBuilderSettings.BuildRecord { Date = "not a date", RunMinutes = 30 },
            };

            Assert.AreEqual(170f, UsageEstimator.LedgerLinuxMinutes(history, new DateTime(2026, 9, 15)));
        }

        [Test]
        public void LinuxEquivalentMinutes_AppliesRunnerMultipliersAndSkipsOtherUnits()
        {
            var items = new[]
            {
                Usage("actions", "actions_linux", "Minutes", 100),
                Usage("Actions", "Actions macOS 3-core", "minutes", 12),
                Usage("actions", "actions_windows", "Minutes", 10),
                Usage("actions", "actions_storage", "GigabyteHours", 500),
                Usage("packages", "packages_storage", "Minutes", 999),
            };

            Assert.AreEqual(100 + 120 + 20, UsageEstimator.LinuxEquivalentMinutes(items));
        }

        private static MacLessBuilderSettings.BuildRecord Record(int runMinutes, string result = MacLessBuilderSettings.BuildRecord.Succeeded)
        {
            return new MacLessBuilderSettings.BuildRecord { Result = result, RunMinutes = runMinutes };
        }

        private static GitHubClient.UsageItem Usage(string product, string sku, string unitType, float quantity)
        {
            return new GitHubClient.UsageItem { product = product, sku = sku, unitType = unitType, grossQuantity = quantity };
        }
    }
}
