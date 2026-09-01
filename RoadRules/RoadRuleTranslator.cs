using System;
using Game.Pathfind;

namespace RoadRules
{
    public enum RoadRuleEnforcementKind
    {
        Vanilla,
        HardClosure,
        PublicLaneGate,
        HeavyTrafficAvoidance,
        LocalAccessBias,
        Unsupported
    }

    /// <summary>
    /// A single, testable translation point between Road Rules UI state and the
    /// vehicle-eligibility mechanisms exposed by the current CS2 runtime.
    /// </summary>
    public struct RoadRuleTranslation
    {
        public RoadRuleEnforcementKind Kind;
        public VehicleAccessFlags EffectiveVehicles;
        public RuleFlags AddedNativeRules;
        public bool SetPublicOnly;
        public bool HardClosed;
        public string Description;

        public bool IsSupported => Kind != RoadRuleEnforcementKind.Unsupported;
    }

    public static class RoadRuleTranslator
    {
        public const VehicleAccessFlags PublicLaneUsers =
            VehicleAccessFlags.Buses |
            VehicleAccessFlags.Taxis |
            VehicleAccessFlags.Emergency |
            VehicleAccessFlags.Service;

        public const VehicleAccessFlags NoHeavyTraffic =
            VehicleAccessFlags.All & ~VehicleAccessFlags.Trucks;

        public static RoadRuleTranslation Translate(
            VehicleAccessFlags requestedVehicles,
            bool isClosed,
            bool localAccessOnly)
        {
            requestedVehicles &= VehicleAccessFlags.All;

            if (isClosed || requestedVehicles == VehicleAccessFlags.None)
            {
                return New(
                    RoadRuleEnforcementKind.HardClosure,
                    VehicleAccessFlags.None,
                    RuleFlags.HasBlockage,
                    false,
                    true,
                    "HARD BLOCK — all road vehicle path methods are removed");
            }

            if (localAccessOnly && requestedVehicles == VehicleAccessFlags.All)
            {
                return New(
                    RoadRuleEnforcementKind.LocalAccessBias,
                    VehicleAccessFlags.All,
                    RuleFlags.ForbidTransitTraffic,
                    false,
                    false,
                    "EXPERIMENTAL SOFT AVOIDANCE — through traffic is discouraged; destination access remains possible");
            }

            if (localAccessOnly)
            {
                return Unsupported("Local Access cannot be combined safely with a selective vehicle mask.");
            }

            if (requestedVehicles == VehicleAccessFlags.All)
            {
                return New(
                    RoadRuleEnforcementKind.Vanilla,
                    VehicleAccessFlags.All,
                    0,
                    false,
                    false,
                    "VANILLA — exact captured lane state is restored");
            }

            if (requestedVehicles == NoHeavyTraffic)
            {
                return New(
                    RoadRuleEnforcementKind.HeavyTrafficAvoidance,
                    NoHeavyTraffic,
                    RuleFlags.ForbidHeavyTraffic,
                    false,
                    false,
                    "SOFT AVOIDANCE — CS2's native heavy-traffic rule; not an absolute class gate");
            }

            if (requestedVehicles == PublicLaneUsers)
            {
                return New(
                    RoadRuleEnforcementKind.PublicLaneGate,
                    PublicLaneUsers,
                    RuleFlags.ForbidPrivateTraffic,
                    true,
                    false,
                    "NATIVE PUBLIC-LANE GATE — buses, taxis, emergency and supported service vehicles share this CS2 category");
            }

            return Unsupported(
                "The current CS2 pathfinder has no independent hard lane masks for Cars, Trucks, Buses, Taxis, Emergency and Services. " +
                "Use All Allowed, No Heavy Traffic, Bus/Taxi/Service Lane, Local Bias, or Close Lane.");
        }

        public static bool TryAccept(
            VehicleAccessFlags requestedVehicles,
            bool isClosed,
            bool localAccessOnly,
            out RoadRuleTranslation translation)
        {
            translation = Translate(requestedVehicles, isClosed, localAccessOnly);
            return translation.IsSupported;
        }

        public static string RunTranslationHarness()
        {
            Assert("ALL ALLOWED", Translate(VehicleAccessFlags.All, false, false),
                RoadRuleEnforcementKind.Vanilla, VehicleAccessFlags.All);
            Assert("NO TRUCKS", Translate(NoHeavyTraffic, false, false),
                RoadRuleEnforcementKind.HeavyTrafficAvoidance, NoHeavyTraffic);
            Assert("PUBLIC LANE", Translate(PublicLaneUsers, false, false),
                RoadRuleEnforcementKind.PublicLaneGate, PublicLaneUsers);
            Assert("CLOSE LANE", Translate(VehicleAccessFlags.None, true, false),
                RoadRuleEnforcementKind.HardClosure, VehicleAccessFlags.None);

            RoadRuleTranslation unsupported = Translate(
                VehicleAccessFlags.Buses | VehicleAccessFlags.Taxis,
                false,
                false);
            if (unsupported.Kind != RoadRuleEnforcementKind.Unsupported)
                throw new InvalidOperationException("BUS/TAXI-only mask must remain unsupported until CS2 exposes an exact hard category.");

            return "ALL ALLOWED=PASS; NO TRUCKS=PASS; PUBLIC LANE=PASS; CLOSE LANE=PASS; unsupported masks rejected=PASS";
        }

        private static RoadRuleTranslation New(
            RoadRuleEnforcementKind kind,
            VehicleAccessFlags effectiveVehicles,
            RuleFlags addedNativeRules,
            bool setPublicOnly,
            bool hardClosed,
            string description)
        {
            return new RoadRuleTranslation
            {
                Kind = kind,
                EffectiveVehicles = effectiveVehicles,
                AddedNativeRules = addedNativeRules,
                SetPublicOnly = setPublicOnly,
                HardClosed = hardClosed,
                Description = description
            };
        }

        private static RoadRuleTranslation Unsupported(string description)
        {
            return New(
                RoadRuleEnforcementKind.Unsupported,
                VehicleAccessFlags.All,
                0,
                false,
                false,
                description);
        }

        private static void Assert(
            string name,
            RoadRuleTranslation actual,
            RoadRuleEnforcementKind expectedKind,
            VehicleAccessFlags expectedVehicles)
        {
            if (actual.Kind != expectedKind || actual.EffectiveVehicles != expectedVehicles)
            {
                throw new InvalidOperationException(
                    $"Road Rules translation harness failed for {name}: " +
                    $"kind={actual.Kind}, vehicles={actual.EffectiveVehicles}");
            }
        }
    }
}
