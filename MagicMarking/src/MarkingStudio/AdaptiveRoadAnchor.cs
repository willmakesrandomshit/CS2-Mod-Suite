using System;
using Colossal.Serialization.Entities;
using Unity.Entities;
using Unity.Mathematics;

namespace MarkingStudio
{
    public enum RemapStatus
    {
        Exact,
        Remapped,
        Ambiguous,
        Orphaned
    }

    /// <summary>
    /// Precision adaptive road-aware anchor for Magic Marking 2.1.
    /// Stores geometric curve parameter t (0.0 to 1.0), lateral offset (meters),
    /// lane reference index, tangent, and normal vectors so markings can deterministically
    /// adapt when roads are widened, upgraded, moved, or regenerated.
    /// </summary>
    public struct AdaptiveRoadAnchor : IBufferElementData, ISerializable
    {
        public Entity targetEdge;
        public Entity targetNode;
        public float curveT;            // Normalized 0.0 -> 1.0 along road edge
        public float lateralOffset;     // Lateral distance in meters from road centerline
        public int laneIndex;          // Lane ordinal (e.g. 0 = leftmost)
        public float3 localNormal;      // Up / surface normal
        public float3 localTangent;     // Direction of travel
        public float3 cachedWorldPos;   // Reference position in world space
        public RemapStatus status;      // Current remapping classification
        public int markingId;          // Associative ID referencing line or area vertex

        private const int kVersion = 1;

        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        {
            writer.Write(kVersion);
            writer.Write(targetEdge);
            writer.Write(targetNode);
            writer.Write(curveT);
            writer.Write(lateralOffset);
            writer.Write(laneIndex);
            writer.Write(localNormal);
            writer.Write(localTangent);
            writer.Write(cachedWorldPos);
            writer.Write((int)status);
            writer.Write(markingId);
        }

        public void Deserialize<TReader>(TReader reader) where TReader : IReader
        {
            reader.Read(out int _);
            reader.Read(out targetEdge);
            reader.Read(out targetNode);
            reader.Read(out curveT);
            reader.Read(out lateralOffset);
            reader.Read(out laneIndex);
            reader.Read(out localNormal);
            reader.Read(out localTangent);
            reader.Read(out cachedWorldPos);
            reader.Read(out int statusInt);
            status = (RemapStatus)statusInt;
            reader.Read(out markingId);
        }
    }

    public struct RemapReportEntry
    {
        public int MarkingId;
        public Entity Node;
        public RemapStatus Status;
        public string Description;
        public float3 CurrentPos;
        public float3 ProposedPos;
        public float DeviationMeters;
    }
}
