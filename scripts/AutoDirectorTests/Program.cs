using AutoDirector;
int passed = 0;
void Check(string n, bool ok) { if (!ok) { Console.WriteLine("FAIL: " + n); Environment.Exit(1); } passed++; }

Check("deterministic", Plan(42).Kind == Plan(42).Kind && Plan(42).Duration == Plan(42).Duration);
ShotPlan Plan(int s) => DirectorLogic.Plan(s, 6f, 12f);
var kinds = new HashSet<ShotKind>();
for (int i = 0; i < 400; i++)
{
    var p = DirectorLogic.Plan(i * 7919 - 1000, 6f, 12f);
    kinds.Add(p.Kind);
    Check("duration in range " + i, p.Duration >= 6f && p.Duration <= 12f);
    Check("zoom factors clamped " + i, p.ZoomStartFactor >= DirectorLogic.MinZoomFactor && p.ZoomStartFactor <= DirectorLogic.MaxZoomFactor && p.ZoomEndFactor >= DirectorLogic.MinZoomFactor && p.ZoomEndFactor <= DirectorLogic.MaxZoomFactor);
    Check("finite yaw rate " + i, float.IsFinite(p.YawRate) && Math.Abs(p.YawRate) <= 45f);
}
Check("all shot kinds appear", kinds.Count == 4);
Check("inverted range is repaired", DirectorLogic.Plan(1, 20f, 3f).Duration >= 20f);
Check("tiny min enforced", DirectorLogic.Plan(1, 0f, 0f).Duration >= 2f);
Check("ease endpoints", DirectorLogic.Ease(-1) == 0f && DirectorLogic.Ease(2) == 1f && Math.Abs(DirectorLogic.Ease(0.5f) - 0.5f) < 1e-6);
var push = new ShotPlan { Kind = ShotKind.PushIn, Duration = 10f, ZoomStartFactor = 1.2f, ZoomEndFactor = 0.3f };
Check("push-in starts wide ends close", DirectorLogic.ZoomAt(push, 100f, 0f) > DirectorLogic.ZoomAt(push, 100f, 10f));
Check("zoom never non-positive", DirectorLogic.ZoomAt(push, 100f, 5f) > 0f);
Check("yaw advances", DirectorLogic.YawAt(new ShotPlan { YawRate = 10f }, 90f, 3f) == 120f);
Console.WriteLine($"Auto Director: {passed} offline assertions passed.");
