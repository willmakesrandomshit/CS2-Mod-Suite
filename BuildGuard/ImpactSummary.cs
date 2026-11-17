using System.Collections.Generic;
using System.Text;

namespace BuildGuard
{
    // Pure logic (no game types) so it can be tested outside the game.
    // Input is what the game's own edit preview already contains: temporary entities that
    // say "this original object will be created / deleted / replaced / modified".
    public enum ChangeKind { Create, Delete, Replace, Modify, Upgrade }
    public enum ObjectKind { Building, RoadSegment, Intersection, Other }

    public struct PreviewItem
    {
        public ChangeKind Change;
        public ObjectKind Object;
        public int Cost; // game currency; may be negative for refunds
    }

    public struct ImpactSummary
    {
        public int BuildingsDeleted;
        public int BuildingsReplaced;
        public int SegmentsCreated;
        public int SegmentsDeleted;
        public int SegmentsChanged;     // replace / modify / upgrade
        public int IntersectionsChanged;
        public int OtherItems;
        public long NetCost;
        public bool Empty => BuildingsDeleted + BuildingsReplaced + SegmentsCreated + SegmentsDeleted + SegmentsChanged + IntersectionsChanged + OtherItems == 0;
        public bool NeedsAttention => BuildingsDeleted > 0 || BuildingsReplaced > 0;
    }

    public static class Impact
    {
        public static ImpactSummary Summarize(IEnumerable<PreviewItem> items)
        {
            var s = new ImpactSummary();
            foreach (var it in items)
            {
                s.NetCost += it.Cost;
                switch (it.Object)
                {
                    case ObjectKind.Building:
                        if (it.Change == ChangeKind.Delete) s.BuildingsDeleted++;
                        else if (it.Change == ChangeKind.Replace) s.BuildingsReplaced++;
                        else s.OtherItems++;
                        break;
                    case ObjectKind.RoadSegment:
                        if (it.Change == ChangeKind.Create) s.SegmentsCreated++;
                        else if (it.Change == ChangeKind.Delete) s.SegmentsDeleted++;
                        else s.SegmentsChanged++;
                        break;
                    case ObjectKind.Intersection:
                        if (it.Change != ChangeKind.Create) s.IntersectionsChanged++;
                        else s.OtherItems++;
                        break;
                    default:
                        s.OtherItems++;
                        break;
                }
            }
            return s;
        }

        // Plain statements about what the preview contains. It never claims what is NOT
        // affected: zoning, pedestrian paths and mod-owned data are not examined by this version.
        public static string Describe(ImpactSummary s)
        {
            if (s.Empty) return "Nothing to preview yet.";
            var sb = new StringBuilder();
            if (s.BuildingsDeleted > 0) sb.Append(s.BuildingsDeleted).Append(s.BuildingsDeleted == 1 ? " building will be removed. " : " buildings will be removed. ");
            if (s.BuildingsReplaced > 0) sb.Append(s.BuildingsReplaced).Append(s.BuildingsReplaced == 1 ? " building will be replaced. " : " buildings will be replaced. ");
            if (s.SegmentsDeleted > 0) sb.Append(s.SegmentsDeleted).Append(s.SegmentsDeleted == 1 ? " road segment will be removed. " : " road segments will be removed. ");
            if (s.SegmentsCreated > 0) sb.Append(s.SegmentsCreated).Append(s.SegmentsCreated == 1 ? " road segment will be created. " : " road segments will be created. ");
            if (s.SegmentsChanged > 0) sb.Append(s.SegmentsChanged).Append(s.SegmentsChanged == 1 ? " road segment will be changed. " : " road segments will be changed. ");
            if (s.IntersectionsChanged > 0) sb.Append(s.IntersectionsChanged).Append(s.IntersectionsChanged == 1 ? " intersection will be changed. " : " intersections will be changed. ");
            if (s.OtherItems > 0) sb.Append(s.OtherItems).Append(" other objects are part of this edit. ");
            sb.Append("Net cost: ").Append(s.NetCost).Append('.');
            return sb.ToString();
        }
    }
}
