using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;

namespace EventEngine
{
    public enum EventCategory
    {
        FootballMatch = 0,
        SportsEvent = 1,
        RockConcert = 2,
        Festival = 3,
        LiveShow = 4,
        Championship = 5,
        Convention = 6,
        Custom = 7
    }

    public enum EventPhase
    {
        Scheduled = 0,
        Arrivals = 1,
        Live = 2,
        Departing = 3,
        Completed = 4
    }

    [Serializable]
    public class VenueRecord
    {
        public Entity Entity;
        public int EntityIndex;
        public string Name = "Venue";
        public string PrefabName = "Stadium";
        public string TypeDescription = "Stadium & Sports Arena";
        public int Capacity = 15000;
        public int CurrentOccupants = 0;
        public float3 Position;
        public int TransitStopsNearby = 0;
        public int ParkingSpacesNearby = 0;
    }

    [Serializable]
    public class EventRecord
    {
        public int Id;
        public string Title = "City Championship";
        public EventCategory Category = EventCategory.FootballMatch;
        public Entity VenueEntity = Entity.Null;
        public int VenueEntityIndex = 0;
        public string VenueName = "Central Stadium";
        public int ExpectedAttendance = 15000;
        public int CurrentAttendance = 0;
        public int TravelingCount = 0;

        public int CarsCount = 0;
        public int TransitCount = 0;
        public int WalkingCount = 0;
        public int TaxiCount = 0;

        public int StartHour = 19;
        public int StartMinute = 30;
        public int DurationHours = 3;
        public int MinutesUntilEvent = 120;
        public long ScheduledStartTicks = 0;
        public EventPhase Phase = EventPhase.Scheduled;

        public float ArrivalIntensity = 1.0f;
        public float DepartureIntensity = 1.0f;
        public int CarUsagePercent = 45;
        public int TransitUsagePercent = 40;
        public int TaxiUsagePercent = 15;

        public string TrafficImpact = "HIGH";
        public bool IsQuickTest = false;

        public string GetCategoryLabel()
        {
            return Category switch
            {
                EventCategory.FootballMatch => "Football Match",
                EventCategory.SportsEvent => "Sports Event",
                EventCategory.RockConcert => "Rock Concert",
                EventCategory.Festival => "Festival & Fair",
                EventCategory.LiveShow => "Live Show",
                EventCategory.Championship => "Major Championship",
                EventCategory.Convention => "Convention & Expo",
                _ => "Custom Event"
            };
        }

        public string GetCategoryIcon()
        {
            return Category switch
            {
                EventCategory.FootballMatch => "⚽",
                EventCategory.SportsEvent => "🏟️",
                EventCategory.RockConcert => "🎵",
                EventCategory.Festival => "🎪",
                EventCategory.LiveShow => "🎤",
                EventCategory.Championship => "🏆",
                EventCategory.Convention => "👥",
                _ => "🎛️"
            };
        }
    }
}
