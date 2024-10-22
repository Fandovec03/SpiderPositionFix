using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UIElements.Experimental;

namespace SpiderPositionFix.Patches
{
    class spiderPositionData
    {
        public bool isInWallState = false;
        public float returningFromWallState = 0f;
        public bool startPatch = false;
    }
    [HarmonyPatch(typeof(SandSpiderAI))]
    public class SpiderPositionPatch
    {
        static Dictionary<SandSpiderAI, spiderPositionData> spiderData = [];

        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        static void StartPostfix(SandSpiderAI __instance)
        {
            __instance.agent.areaMask &= ~(1 << NavMesh.GetAreaFromName("Jump"));
            spiderData.Add(__instance,new spiderPositionData());
            spiderData[__instance].startPatch = true;
        }

        [HarmonyPatch("LateUpdate")]
        [HarmonyPostfix]
        static void MeshContainerPositionFix(SandSpiderAI __instance)
        {
            spiderPositionData instanceData = spiderData[__instance];
            if (instanceData.startPatch != true) return;
            if (!__instance.lookingForWallPosition && __instance.moveTowardsDestination && spiderData[__instance].isInWallState)
            {
                __instance.agent.transform.position = RoundManager.Instance.GetNavMeshPosition(__instance.meshContainer.transform.position);
            }
            if (!__instance.lookingForWallPosition && !__instance.gotWallPositionInLOS && !instanceData.isInWallState)
            {
                //InicialScript.Logger.LogDebug("Spider: wallState: " + instanceData.isInWallState);
                if (Vector3.Distance(__instance.meshContainerPosition, __instance.transform.position) > 0.75f && !__instance.onWall)
                {
                    if (!__instance.onWall)
                    {
                        __instance.meshContainerTargetRotation = __instance.agent.transform.rotation;
                    }
                    __instance.meshContainerTarget = Vector3.Lerp(__instance.meshContainerPosition, __instance.agent.nextPosition, Distance(Vector3.Distance(__instance.meshContainerPosition, __instance.transform.position), 0.5f) * Time.deltaTime);
                    //InicialScript.Logger.LogDebug("Spider: SLERP distance: " + Vector3.Distance(__instance.meshContainerPosition, __instance.transform.position));
                }
                if (__instance.agent.isOnOffMeshLink)
                    {
                        __instance.meshContainer.position = Vector3.Lerp(__instance.meshContainerPosition, __instance.agent.nextPosition, Distance(Vector3.Distance(__instance.meshContainerPosition, __instance.transform.position), 0.5f));
                        __instance.meshContainerPosition = __instance.meshContainer.position;
                        __instance.meshContainer.LookAt(__instance.agent.currentOffMeshLinkData.endPos, Vector3.up);
                    }
            }
            if (__instance.lookingForWallPosition && __instance.gotWallPositionInLOS && !instanceData.isInWallState || __instance.onWall)
            {
                instanceData.isInWallState = true;
                //InicialScript.Logger.LogDebug("Spider: wallState2: " + instanceData.isInWallState);
            }
            if (!__instance.lookingForWallPosition && __instance.moveTowardsDestination && instanceData.isInWallState)
            {
                if (instanceData.isInWallState && Vector3.Distance(__instance.meshContainerPosition, __instance.transform.position) < 1f || instanceData.returningFromWallState > 6f)
                {
                    //InicialScript.Logger.LogDebug("Spider: returning from wall state. Distance: "+Vector3.Distance(__instance.meshContainerPosition, __instance.transform.position));
                    instanceData.isInWallState = false;
                    instanceData.returningFromWallState = 0f;
                }
            }
        }

        static float Distance(float distance, float time)
        {
            float ratio = distance / time;
            return ratio;
        }
    }
}