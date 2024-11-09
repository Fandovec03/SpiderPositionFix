using HarmonyLib;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace SpiderPositionFix.Patches
{
    class spiderPositionData
    {
        public int currentJumpMaskBit = 1;
        public bool isInWallState = false;
        public float returningFromWallState = 0f;
        public bool startPatch = false;
        public bool applySpeedSlowdown = false;
        public float originalSpeed = 4.25f;
        public float offsetSpeed = 0f;
        public Vector3 climpPoint = Vector3.zero;
    }

    [HarmonyPatch(typeof(SandSpiderAI))]
    public class SpiderPositionPatch
    {
        static bool debug = InitialScript.configSettings.debug.Value;
        static Dictionary<SandSpiderAI, spiderPositionData> spiderData = [];

        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        static void StartPostfix(SandSpiderAI __instance)
        {
            if (!spiderData.ContainsKey(__instance))
            {
                spiderData.Add(__instance, new spiderPositionData());
            }
            spiderData[__instance].startPatch = true;
        }
        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        static void UpdatePostfix(SandSpiderAI __instance)
        {
            spiderPositionData instanceData = spiderData[__instance];

            if (InitialScript.configSettings.applyMask.Value == true)
            {
                if (__instance.isOutside && instanceData.currentJumpMaskBit != 1)
                {
                    ChangeJumpMask(__instance, ref instanceData.currentJumpMaskBit);
                    instanceData.currentJumpMaskBit = 1;
                }
                else if (!__instance.isOutside && instanceData.currentJumpMaskBit != 0)
                {
                    ChangeJumpMask(__instance, ref instanceData.currentJumpMaskBit);
                    instanceData.currentJumpMaskBit= 0;
                }
            }
            if (__instance.watchFromDistance == true)
            {
                InitialScript.Logger.LogDebug("watchFromDistance true. Returning...");
                return;
            }
            if (!instanceData.applySpeedSlowdown)
            {
                instanceData.originalSpeed = __instance.agent.speed;
            }

            if (!__instance.lookingForWallPosition && !__instance.gotWallPositionInLOS && !instanceData.isInWallState)
            {
                if (Vector3.Distance(__instance.meshContainerPosition, __instance.transform.position) > 0.3f && !__instance.onWall && !__instance.agent.isOnOffMeshLink)
                {
                    if (instanceData.applySpeedSlowdown == true)
                    {
                        instanceData.offsetSpeed += Time.deltaTime * (Mathf.Clamp(Vector3.Distance(__instance.meshContainerPosition, __instance.transform.position), 0f, 1f) / 0.4f);
                        __instance.agent.speed = instanceData.originalSpeed - Mathf.Min(0.75f, instanceData.offsetSpeed);
                    }
                    else
                    {
                        instanceData.applySpeedSlowdown = true;
                        __instance.agent.speed = instanceData.originalSpeed;
                    }
                    if (debug && instanceData.originalSpeed != 0) InitialScript.Logger.LogDebug("Applying slowdown. New speed: " + __instance.agent.speed);
                }
                else if (instanceData.applySpeedSlowdown && !__instance.agent.isOnOffMeshLink)
                {
                    instanceData.applySpeedSlowdown = false;
                    instanceData.offsetSpeed = 0;
                    __instance.agent.speed = instanceData.originalSpeed;
                    if (debug) InitialScript.Logger.LogDebug("Returning original speed");
                }
                if (__instance.agent.isOnOffMeshLink)
                {
                    instanceData.applySpeedSlowdown = true;
                    __instance.agent.speed = instanceData.originalSpeed / 1.25f;
                    if (debug) InitialScript.Logger.LogDebug("On offMeshLink. Cutting speed");
                }
            }
            else if (instanceData.applySpeedSlowdown && instanceData.isInWallState && !__instance.reachedWallPosition)
            {
                instanceData.applySpeedSlowdown = false;
                instanceData.offsetSpeed = 0;
                __instance.agent.speed = instanceData.originalSpeed;
                if (debug) InitialScript.Logger.LogDebug("/2/ Returning original speed");
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
                if (debug) InitialScript.Logger.LogInfo("Warping agent to nearest navMesh position to climbPoint: " + spiderData[__instance].climpPoint + ". Distance: " + Vector3.Distance(__instance.agent.transform.position, spiderData[__instance].climpPoint));
                __instance.agent.Warp(RoundManager.Instance.GetNavMeshPosition(spiderData[__instance].climpPoint, default, 2.5f));
            }

            if (!__instance.lookingForWallPosition && !__instance.gotWallPositionInLOS && !instanceData.isInWallState)
            {
                if ((Vector3.Distance(__instance.meshContainerPosition, __instance.transform.position) > 0.75f || Mathf.Abs(__instance.meshContainerPosition.y - __instance.transform.position.y) > 0.15f) && !__instance.onWall)
                {
                    string text = "null";
                    if (Vector3.Distance(__instance.meshContainerPosition, __instance.transform.position) > 0.75f)
                    {
                        text = "Triggered by distance: " + Vector3.Distance(__instance.meshContainerPosition, __instance.transform.position);
                    }
                    else if(Mathf.Abs(__instance.meshContainerPosition.y - __instance.transform.position.y) > 0.15f)
                    {
                        text = "Triggered by height projection: " + Mathf.Abs(__instance.meshContainerPosition.y - __instance.transform.position.y);
                    }
                    if (!__instance.onWall && !__instance.overrideSpiderLookRotation)
                    {
                        __instance.meshContainerTargetRotation = __instance.agent.transform.rotation;
                    }
                    if (debug) InitialScript.Logger.LogDebug(text);
                    __instance.meshContainerTarget = __instance.agent.transform.position;

                }
                if (__instance.agent.isOnOffMeshLink)
                {
                    __instance.meshContainer.position = Vector3.Lerp(__instance.meshContainerPosition, __instance.agent.nextPosition, Distance(Vector3.Distance(__instance.meshContainerPosition, __instance.transform.position), 0.5f));
                    __instance.meshContainerPosition = __instance.meshContainer.position;

                    __instance.meshContainerTargetRotation = Quaternion.Lerp(__instance.meshContainer.rotation, Quaternion.LookRotation(__instance.agent.currentOffMeshLinkData.endPos - __instance.meshContainer.position, Vector3.up), 0.75f);
                }
            }
            if (__instance.onWall && spiderData[__instance].climpPoint == Vector3.zero)
            {
                spiderData[__instance].climpPoint = __instance.meshContainer.position;
                if (debug) InitialScript.Logger.LogDebug("Set new climpPoint: " + spiderData[__instance].climpPoint);
            }
            if (__instance.lookingForWallPosition && __instance.gotWallPositionInLOS && !instanceData.isInWallState || __instance.onWall)
            {
                instanceData.isInWallState = true;
                if (debug) InitialScript.Logger.LogDebug("wallState2: " + instanceData.isInWallState);
            }
            if (!__instance.lookingForWallPosition && instanceData.isInWallState && __instance.movingTowardsTargetPlayer)
            {
                if (instanceData.isInWallState && Vector3.Distance(__instance.meshContainerPosition, __instance.transform.position) < 1f || instanceData.returningFromWallState > 6f)
                {
                    if (debug) InitialScript.Logger.LogDebug("returning from wall state and zeroing climp point: " + instanceData.climpPoint + ". Distance: " + Vector3.Distance(__instance.meshContainerPosition, __instance.transform.position));
                    instanceData.isInWallState = false;
                    instanceData.climpPoint = Vector3.zero;
                    instanceData.returningFromWallState = 0f;
                }
            }
        }

        static float Distance(float distance, float time)
        {
            float ratio = distance / time;
            return ratio;
        }

        static void ChangeJumpMask(SandSpiderAI __instance, ref int bit)
        {
            if (__instance != null)
            {
                __instance.agent.areaMask ^= (1 << NavMesh.GetAreaFromName("Jump"));

                if (bit == 0)
                {
                    bit = 1;
                }
                else if (bit == 1)
                {
                    bit = 0;
                }

                if (debug) InitialScript.Logger.LogDebug("Spider: Toggled mask bit to " + bit);
            }
        }
    }
}