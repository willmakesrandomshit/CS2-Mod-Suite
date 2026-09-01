using System;
using Colossal.Logging;
using Game.Rendering;
using Game.Tools;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace CityPulse
{
    public static class CameraNavigator
    {
        public static ILog Log = LogManager.GetLogger(nameof(CityPulse)).SetShowsErrorsInUI(false);

        public static void JumpToCoordinates(CameraUpdateSystem cameraSystem, float3 position)
        {
            if (cameraSystem == null) return;
            try
            {
                if (cameraSystem.activeCameraController != null)
                {
                    cameraSystem.activeCameraController.pivot = position;
                }
            }
            catch (Exception ex)
            {
                Log.Warn(ex, "Failed to navigate camera pivot.");
            }
        }

        public static void SelectAndJumpToEntity(EntityManager entityManager, ToolSystem toolSystem, CameraUpdateSystem cameraSystem, int entityIndex)
        {
            if (toolSystem == null) return;
            try
            {
                var ent = new Entity { Index = entityIndex, Version = 1 };
                if (entityManager.Exists(ent))
                {
                    toolSystem.selected = ent;
                    if (entityManager.HasComponent<Game.Objects.Transform>(ent))
                    {
                        var trans = entityManager.GetComponentData<Game.Objects.Transform>(ent);
                        JumpToCoordinates(cameraSystem, trans.m_Position);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Warn(ex, "Failed to select and navigate to entity.");
            }
        }
    }
}
