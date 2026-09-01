using Colossal.Serialization.Entities;
using Unity.Entities;

namespace MarkingStudio
{
    public enum MarkingDrawingMode
    {
        FollowRoad = 0,   // Authoritative road/lane geometry following (default)
        Straight = 1,     // Direct straight endpoint-to-endpoint chord
        CustomCurve = 2   // User-defined Bezier curve with manual pull factor
    }

    /// <summary>
    /// User-defined logical marking line at a road node. Per-node DynamicBuffer; one entry per
    /// line the user drew. Supports segmentation, road-following geometry, lateral offsets, and styles.
    /// </summary>
    [InternalBufferCapacity(0)]
    public struct MarkingLine : IBufferElementData, ISerializable
    {
        public Entity sourceEdge;
        public int    sourceGapIndex;
        public Entity targetEdge;
        public int    targetGapIndex;

        // Line style index (0 = Solid, 1 = Dashed, etc.)
        public int    style;

        // Bezier pull factor for this line's curve (-1.0 to 1.0).
        public float  curvature;

        // Drawing mode: 0 = FollowRoad (default), 1 = Straight, 2 = CustomCurve
        public int    drawingMode;

        // Lateral offset in meters (-2.0m to +2.0m)
        public float  lateralOffset;

        private const int kVersion = 5;

        public void Serialize<TWriter>(TWriter writer) where TWriter : IWriter
        {
            writer.Write(kVersion);
            writer.Write(sourceEdge);
            writer.Write(sourceGapIndex);
            writer.Write(targetEdge);
            writer.Write(targetGapIndex);
            writer.Write(style);
            writer.Write(curvature);
            writer.Write(drawingMode);
            writer.Write(lateralOffset);
        }

        public void Deserialize<TReader>(TReader reader) where TReader : IReader
        {
            reader.Read(out int version);
            reader.Read(out sourceEdge);
            reader.Read(out sourceGapIndex);
            reader.Read(out targetEdge);
            reader.Read(out targetGapIndex);
            reader.Read(out style);
            if (version >= 4) reader.Read(out curvature);
            else curvature = 0.4f;

            if (version >= 5)
            {
                reader.Read(out drawingMode);
                reader.Read(out lateralOffset);
            }
            else
            {
                drawingMode = 0; // FollowRoad default
                lateralOffset = 0f;
            }
        }
    }
}
