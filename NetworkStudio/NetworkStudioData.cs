using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

namespace NetworkStudio
{
    public enum FurnitureCategory
    {
        All = 0,
        RoadSigns = 1,
        StreetLights = 2,
        TrafficSignals = 3,
        Trees = 4,
        Barriers = 5
    }

    public enum SignPlacement
    {
        Default = 0,
        LeftVerge = 1,
        RightVerge = 2,
        CustomOffset = 3,
        Hidden = 4
    }

    public enum LightPlacement
    {
        Both = 0,
        LeftOnly = 1,
        RightOnly = 2,
        Alternating = 3,
        None = 4
    }

    [Serializable]
    public class FurnitureOverrideRecord
    {
        public int EdgeIndex;
        public int SubObjectIndex;
        public FurnitureCategory Category;
        public SignPlacement Placement;
        public float LateralOffset;
        public float LongitudinalOffset;
        public float HeightOffset;
        public float RotationDegrees;
        public bool IsHidden;
    }

    public struct FurnitureItemInfo
    {
        public Entity Entity;
        public int Index;
        public string Name;
        public FurnitureCategory Category;
        public float3 Position;
        public float LateralOffset;
        public bool IsOnAsphalt;
    }
}
