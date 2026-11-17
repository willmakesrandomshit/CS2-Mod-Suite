using BuildGuard;

int passed = 0;
void Check(string n, bool ok) { if (!ok) { Console.WriteLine("FAIL: " + n); Environment.Exit(1); } passed++; }
PreviewItem P(ChangeKind c, ObjectKind o, int cost = 0) => new PreviewItem { Change = c, Object = o, Cost = cost };

var empty = Impact.Summarize(new PreviewItem[0]);
Check("empty", empty.Empty && !empty.NeedsAttention && Impact.Describe(empty) == "Nothing to preview yet.");

var upgrade = Impact.Summarize(new[]
{
    P(ChangeKind.Replace, ObjectKind.RoadSegment, 1200), P(ChangeKind.Replace, ObjectKind.RoadSegment, 900),
    P(ChangeKind.Delete, ObjectKind.Building, -300), P(ChangeKind.Modify, ObjectKind.Intersection, 50),
    P(ChangeKind.Create, ObjectKind.RoadSegment, 400), P(ChangeKind.Delete, ObjectKind.Other),
});
Check("segments changed", upgrade.SegmentsChanged == 2 && upgrade.SegmentsCreated == 1);
Check("building removal flags attention", upgrade.BuildingsDeleted == 1 && upgrade.NeedsAttention);
Check("intersection counted", upgrade.IntersectionsChanged == 1);
Check("other counted", upgrade.OtherItems == 1);
Check("net cost sums, refunds subtract", upgrade.NetCost == 1200 + 900 - 300 + 50 + 400);
string d = Impact.Describe(upgrade);
Check("description wording", d.Contains("1 building will be removed.") && d.Contains("2 road segments will be changed.") && d.EndsWith("Net cost: 2250."));
Check("singular/plural", Impact.Describe(Impact.Summarize(new[] { P(ChangeKind.Delete, ObjectKind.Building), P(ChangeKind.Delete, ObjectKind.Building) })).Contains("2 buildings will be removed."));
Check("does not claim safety", !d.ToLower().Contains("safe") && !d.ToLower().Contains("no other"));
Check("replaced building also flags attention", Impact.Summarize(new[] { P(ChangeKind.Replace, ObjectKind.Building) }).NeedsAttention);
Check("created building is not an alarm", !Impact.Summarize(new[] { P(ChangeKind.Create, ObjectKind.Building) }).NeedsAttention);
Console.WriteLine($"BuildGuard: {passed} offline assertions passed.");
