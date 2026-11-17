using System;

namespace AutoDirector
{
    // Pure shot-planning logic (no game types) so it can be tested outside the game.
    public enum ShotKind { Follow, SlowOrbit, FastOrbit, PushIn }

    public struct ShotPlan
    {
        public ShotKind Kind;
        public float Duration;     // seconds
        public float YawRate;      // degrees per second added to the starting yaw
        public float ZoomStartFactor; // multiplier on the player's starting zoom
        public float ZoomEndFactor;
    }

    public static class DirectorLogic
    {
        public const float MinZoomFactor = 0.25f;
        public const float MaxZoomFactor = 1.6f;

        // Deterministic from a seed so tests (and replays) are repeatable.
        public static ShotPlan Plan(int seed, float minSeconds, float maxSeconds)
        {
            uint h = (uint)seed * 2654435761u;
            h ^= h >> 15; h *= 2246822519u; h ^= h >> 13;
            float u1 = (h & 0xFFFF) / 65535f, u2 = ((h >> 16) & 0xFFFF) / 65535f;

            minSeconds = Math.Max(2f, minSeconds);
            maxSeconds = Math.Max(minSeconds, maxSeconds);
            var kind = (ShotKind)(int)(h % 4u);
            var plan = new ShotPlan { Kind = kind, Duration = minSeconds + (maxSeconds - minSeconds) * u1 };

            switch (kind)
            {
                case ShotKind.Follow: plan.YawRate = 0f; plan.ZoomStartFactor = plan.ZoomEndFactor = Clamp(0.5f + u2 * 0.5f); break;
                case ShotKind.SlowOrbit: plan.YawRate = 6f + u2 * 6f; plan.ZoomStartFactor = plan.ZoomEndFactor = Clamp(0.45f + u2 * 0.4f); break;
                case ShotKind.FastOrbit: plan.YawRate = 22f + u2 * 18f; plan.ZoomStartFactor = plan.ZoomEndFactor = Clamp(0.35f + u2 * 0.3f); break;
                default: plan.YawRate = 3f; plan.ZoomStartFactor = Clamp(1.2f); plan.ZoomEndFactor = Clamp(0.3f + u2 * 0.2f); break;
            }
            if ((h & 0x100000u) != 0) plan.YawRate = -plan.YawRate; // sometimes orbit the other way
            return plan;
        }

        public static float Clamp(float f) { return f < MinZoomFactor ? MinZoomFactor : f > MaxZoomFactor ? MaxZoomFactor : f; }

        // Smooth ease-in-out so cuts and pushes do not look mechanical.
        public static float Ease(float t)
        {
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            return t * t * (3f - 2f * t);
        }

        public static float ZoomAt(ShotPlan p, float baseZoom, float elapsed)
        {
            float t = p.Duration <= 0f ? 1f : elapsed / p.Duration;
            float factor = p.ZoomStartFactor + (p.ZoomEndFactor - p.ZoomStartFactor) * Ease(t);
            return baseZoom * Clamp(factor);
        }

        public static float YawAt(ShotPlan p, float baseYaw, float elapsed) { return baseYaw + p.YawRate * elapsed; }
    }
}
