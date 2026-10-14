using TrafficStressTester;

int checks = 0;
void Equal<T>(T actual, T expected, string scenario)
{
    if (!EqualityComparer<T>.Default.Equals(actual, expected))
        throw new Exception($"{scenario}: expected {expected}, got {actual}");
    checks++;
}

// Presets translate to additional departures on one approved native route.
foreach (var (multiplier, extra) in new[] { (2, 1), (5, 4), (10, 9), (25, 24) })
    Equal(TrafficStressPolicy.ExtraDepartures(multiplier, 512), extra, $"{multiplier}x departure rate");
foreach (int unsupported in new[] { 0, 1, 3, 26, int.MaxValue })
    Equal(TrafficStressPolicy.ExtraDepartures(unsupported, 512), 0, "unsupported/vanilla rate");
Equal(TrafficStressPolicy.ExtraDepartures(25, 3), 3, "partial route respects remaining cap");
Equal(TrafficStressPolicy.ExtraDepartures(25, 0), 0, "exhausted cap");
Equal(TrafficStressPolicy.ExtraDepartures(25, -1), 0, "negative remaining cap");

foreach (int ramp in new[] { 0, 1, 2 })
{
    int previous = 0;
    for (int batch = 1; batch <= 8; batch++)
    {
        int budget = TrafficStressPolicy.BatchBudget(96, ramp, batch);
        Equal(budget >= previous && budget <= 96, true, "ramp is monotonic and bounded");
        previous = budget;
    }
    Equal(previous, 96, "ramp reaches configured cap");
}
Equal(TrafficStressPolicy.BatchBudget(96, 0, 1), 12, "Gentle starts at 1/8 cap");
Equal(TrafficStressPolicy.BatchBudget(96, 1, 3), 96, "Fast reaches full cap in three batches");
Equal(TrafficStressPolicy.BatchBudget(96, 2, 1), 96, "Extreme reaches full cap immediately");
Equal(TrafficStressPolicy.BatchBudget(int.MaxValue, 2, int.MaxValue), 512, "corrupt large settings remain bounded");
Equal(TrafficStressPolicy.BatchBudget(int.MinValue, 2, 1), 1, "corrupt negative settings remain bounded");

TrafficStressState.SetMultiplier(25);
TrafficStressState.AddGenerated(24);
TrafficStressState.PublishBatch(1, 96, true);
TrafficStressState.SetRoadVehicles(180);
TrafficStressState.Reset();
Equal(TrafficStressState.Multiplier, 1, "Stop returns to vanilla");
Equal(TrafficStressState.RequestsGenerated, 0, "Reset clears departure telemetry");
Equal(TrafficStressState.ReadyRoutes, 0, "Reset clears route telemetry");
Equal(TrafficStressState.Limited, false, "Reset clears cap status");
Equal(TrafficStressState.ConsumeStopRequest(), true, "Stop signals legacy cleanup");
Equal(TrafficStressState.ConsumeStopRequest(), false, "Stop signal consumed once");
TrafficStressState.SetMultiplier(25);
TrafficStressState.Reset();
TrafficStressState.SetMultiplier(25);
Equal(TrafficStressState.Multiplier, 25, "rapid Stop -> Start retains new setting");
Equal(TrafficStressState.ConsumeStopRequest(), true, "rapid restart still restarts ramp on next tick");
TrafficStressState.AddGenerated(24);
TrafficStressState.PrepareForNewCity(false);
Equal(TrafficStressState.Multiplier, 25, "optional city transition preserves selected rate");
Equal(TrafficStressState.RequestsGenerated, 0, "city transition clears departures");
Equal(TrafficStressState.RoadVehicles, 0, "city transition clears vehicle observations");
TrafficStressState.PrepareForNewCity(true);
Equal(TrafficStressState.Multiplier, 1, "default city transition stops stress");
Console.WriteLine($"Traffic Stress Lab: {checks} offline assertions passed.");
