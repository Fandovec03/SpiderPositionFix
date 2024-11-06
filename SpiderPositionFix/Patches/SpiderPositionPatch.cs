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
        public bool applySpeedSlowdown = false;
        public float originalSpeed = 4.25f;
        public float offsetSpeed = 0f;
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
            spiderData.Add(__instance, new spiderPositionData());
            spiderData[__instance].startPatch = true;
        }
        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        static void UpdatePostfix(SandSpiderAI __instance)
        {
            spiderPositionData instanceData = spiderData[__instance];

            if (!instanceData.applySpeedSlowdown)
            {
                instanceData.originalSpeed = __instance.agent.speed;
            }

            if (!__instance.lookingForWallPosition && !__instance.gotWallPositionInLOS && !instanceData.isInWallState)
            {
                if (Vector3.Distance(__instance.meshContainerPosition, __instance.transform.position) > 0.4f && !__instance.onWall)
                {
                    if (instanceData.applySpeedSlowdown == true)
                    {
                        instanceData.offsetSpeed += Time.deltaTime;
                        __instance.agent.speed = instanceData.originalSpeed - Mathf.Min(0.5f, instanceData.offsetSpeed) - 0.3f;
                    }
                    else
                    {
                        instanceData.applySpeedSlowdown = true;
                        __instance.agent.speed = instanceData.originalSpeed - 0.3f;
                    }
                    InicialScript.Logger.LogDebug("Spider: Applying slowdown. New speed: " + __instance.agent.speed);
                }
                else if (instanceData.applySpeedSlowdown)
                {
                    instanceData.applySpeedSlowdown = false;
                    instanceData.offsetSpeed = 0;
                    __instance.agent.speed = instanceData.originalSpeed;
                    InicialScript.Logger.LogDebug("Spider: Returning original speed");
                }
            }
            else if (instanceData.applySpeedSlowdown && instanceData.isInWallState && !__instance.reachedWallPosition)
            {
                instanceData.applySpeedSlowdown = false;
                instanceData.offsetSpeed = 0;
                __instance.agent.speed = instanceData.originalSpeed;
                InicialScript.Logger.LogDebug("Spider/2/: Returning original speed");
            }
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
                if (Vector3.Distance(__instance.meshContainerPosition, __instance.transform.position) > 0.75f && !__instance.onWall)
                {
                    if (!__instance.onWall && !__instance.overrideSpiderLookRotation)
                    {
                        __instance.meshContainerTargetRotation = __instance.agent.transform.rotation;
                    }
                    InicialScript.Logger.LogDebug("Spider: distance: " + Vector3.Distance(__instance.meshContainerPosition, __instance.transform.position));
                    __instance.meshContainerTarget = __instance.agent.transform.position;

                }
                if (__instance.agent.isOnOffMeshLink)
                {
                    __instance.meshContainer.position = Vector3.Lerp(__instance.meshContainerPosition, __instance.agent.nextPosition, Distance(Vector3.Distance(__instance.meshContainerPosition, __instance.transform.position), 0.5f));
                    __instance.meshContainerPosition = __instance.meshContainer.position;

                    __instance.meshContainerTargetRotation = Quaternion.Lerp(__instance.meshContainer.rotation, Quaternion.LookRotation(__instance.agent.currentOffMeshLinkData.endPos - __instance.meshContainer.position, Vector3.up), 1f / 0.5f * Time.deltaTime);
                }
            }
            if (__instance.lookingForWallPosition && __instance.gotWallPositionInLOS && !instanceData.isInWallState || __instance.onWall)
            {
                instanceData.isInWallState = true;
                InicialScript.Logger.LogDebug("Spider: wallState2: " + instanceData.isInWallState);
            }
            if (!__instance.lookingForWallPosition && instanceData.isInWallState && __instance.movingTowardsTargetPlayer)
            {
                if (instanceData.isInWallState && Vector3.Distance(__instance.meshContainerPosition, __instance.transform.position) < 1f || instanceData.returningFromWallState > 6f)
                {
                    InicialScript.Logger.LogDebug("Spider: returning from wall state. Distance: "+Vector3.Distance(__instance.meshContainerPosition, __instance.transform.position));
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