using System;
using System.Collections.Generic;
using System.Text;
using SPF_debugTools.Patches;
using HarmonyLib;
using System.Numerics;
using UnityEngine;
using Vector3 = UnityEngine.Vector3;

namespace SpiderPositionFix.Patches
{
    public class SPF_debugToolsClass
    {
        public static void SetDebugObjectsPosition(SandSpiderAI instance)
        {
            SPF_debugToolsMethods.SetDebugObjectsPosition(instance);
        }

        public static void DeleteObjects(SandSpiderAI instance)
        {
            SPF_debugToolsMethods.DeleteObjects(instance);
        }

        public static void GetWallPositionForMesh(SandSpiderAI instance, Vector3 unmodifiedWallPosition, Vector3 normalProjection)
        {
            SPF_debugToolsMethods.GetWallPositionForMesh(instance, unmodifiedWallPosition, normalProjection);
        }

        public static void Init()
        {
            SPF_debugToolsMethods.InitDebugTools();
        }
    }
}
