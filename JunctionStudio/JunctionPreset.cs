using System;
using System.Collections.Generic;

namespace JunctionStudio
{
    [Serializable]
    public sealed class JunctionPresetLibrary
    {
        public List<JunctionPreset> Presets { get; set; } = new List<JunctionPreset>();
    }

    [Serializable]
    public sealed class JunctionPreset
    {
        public string Name { get; set; } = "Untitled";
        public int ApproachCount { get; set; }
        public float BaselineAngle { get; set; }
        public uint HiddenCategories { get; set; }
        public List<LineRecord> Lines { get; set; } = new List<LineRecord>();
        public List<SegmentRecord> Segments { get; set; } = new List<SegmentRecord>();
        public List<AreaRecord> Areas { get; set; } = new List<AreaRecord>();
        public List<VertexRecord> Vertices { get; set; } = new List<VertexRecord>();
    }

    [Serializable]
    public sealed class LineRecord
    {
        public int SourceApproach { get; set; }
        public int SourceGap { get; set; }
        public int TargetApproach { get; set; }
        public int TargetGap { get; set; }
        public int Style { get; set; }
        public float Curvature { get; set; }
    }

    [Serializable]
    public sealed class SegmentRecord
    {
        public int Line { get; set; }
        public float Start { get; set; }
        public float End { get; set; }
        public bool Visible { get; set; }
        public int Style { get; set; }
    }

    [Serializable]
    public sealed class AreaRecord
    {
        public int Style { get; set; }
        public bool Visible { get; set; }
        public int FirstVertex { get; set; }
        public int VertexCount { get; set; }
    }

    [Serializable]
    public sealed class VertexRecord
    {
        public byte Kind { get; set; }
        public int LegacyIndex { get; set; }
        public byte EdgeToNext { get; set; }
        public int ApproachA { get; set; } = -1;
        public int ApproachB { get; set; } = -1;
        public int Gap { get; set; }
        public float LocalX { get; set; }
        public float LocalY { get; set; }
        public float LocalZ { get; set; }
    }
}
