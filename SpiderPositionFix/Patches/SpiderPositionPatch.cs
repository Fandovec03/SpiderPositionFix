using HarmonyLib;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.AI;

namespace SpiderPositionFix.Patches
{
    class spiderPositionData
    {
        public int currentJumpMaskBit = 1;
        public float returningFromWallState = 0f;
        public bool startPatch = false;
        public bool applySpeedSlowdown = false;
        public float originalSpeed = 4.25f;
        public float offsetSpeed = 0f;
        public float reachedWallTimer = 0f;
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
            if (InitialScript.SpiderAssets != null)
            {
                try
                {
                    AnimatorOverrideController controller = InitialScript.SpiderAssets.LoadAsset<AnimatorOverrideController>("Assets/LethalCompany/CustomAnims/SandSpider/Spider Anim Override.overrideController");
                    __instance.creatureAnimator.runtimeAnimatorController = controller;
                }
                catch
                {
                    InitialScript.Logger.LogError("Failed to load OverrideController asset");
                }
            }
            if (!spiderData.ContainsKey(__instance))
            {
                spiderData.Add(__instance, new spiderPositionData());
            }
            spiderData[__instance].startPatch = true;
        }

        [HarmonyPatch("Update")]
        [HarmonyPrefix]
        static void UpdatePrefix(SandSpiderAI __instance)
        {
            spiderPositionData instanceData = spiderData[__instance];

            if (__instance.IsOwner)
            {
                if (__instance.reachedWallPosition && __instance.currentBehaviourStateIndex == 1 && __instance.agent.enabled == true)
                {
                    __instance.agent.enabled = false;
                    //__instance.agent.Warp(__instance.meshContainer.transform.position);
                }

                if (__instance.agent.enabled == false && (!__instance.onWall || __instance.waitOnWallTimer <= 0 || __instance.currentBehaviourStateIndex != 1))
                {
                    __instance.agent.enabled = true;
                    if (__instance.onWall)
                    {
                        //__instance.agent.Warp(__instance.floorPosition);
                    }
                }
            }
        }


        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        static void UpdatePostfix(SandSpiderAI __instance)
        {
            spiderPositionData instanceData = spiderData[__instance];
            if (!__instance.IsOwner) return;
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
                    instanceData.currentJumpMaskBit = 0;
                }
            }
            if (__instance.watchFromDistance == true)
            {
                if (debug) InitialScript.Logger.LogDebug("watchFromDistance true. Returning...");
                return;
            }
            if (!instanceData.applySpeedSlowdown)
            {
                if (__instance.currentBehaviourStateIndex == 1)
                {
                    instanceData.originalSpeed = __instance.spiderSpeed;
                }
                else instanceData.originalSpeed = __instance.agent.speed;
            }

            if (!__instance.onWall)
            {
                if (Vector3.Distance(__instance.meshContainer.position, __instance.transform.position) > 0.4f && !__instance.agent.isOnOffMeshLink)
                {
                    if (instanceData.applySpeedSlowdown == true)
                    {
                        instanceData.offsetSpeed = Mathf.Clamp(Vector3.Distance(__instance.meshContainer.position, __instance.transform.position), 0f, 2f) / 2;
                        __instance.agent.speed = instanceData.originalSpeed - Mathf.Min(1f, instanceData.offsetSpeed) - 0.15f;
                    }
                    else
                    {
                        instanceData.applySpeedSlowdown = true;
                        if (__instance.currentBehaviourStateIndex == 1)
                        {
                            instanceData.originalSpeed = __instance.spiderSpeed;
                        }
                        else __instance.agent.speed = instanceData.originalSpeed;
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
                    __instance.agent.speed = instanceData.originalSpeed / 1.15f;
                    if (debug) InitialScript.Logger.LogDebug("On offMeshLink. Cutting speed");
                }
            }
            else
            {
                if (instanceData.applySpeedSlowdown && __instance.onWall && !__instance.reachedWallPosition)
                {
                    instanceData.applySpeedSlowdown = false;
                    instanceData.offsetSpeed = 0;
                    __instance.agent.speed = instanceData.originalSpeed;
                    if (debug) InitialScript.Logger.LogDebug("/2/ Returning original speed");
                }
                if (__instance.onWall && (__instance.meshContainerTarget == __instance.floorPosition || __instance.meshContainerTarget == __instance.wallPosition))
                {
                    __instance.agent.speed = 0f;
                }
            }
        }
        [HarmonyPatch("LateUpdate")]
        [HarmonyPostfix]
        static void MeshContainerPositionFix(SandSpiderAI __instance)
        {
            spiderPositionData instanceData = spiderData[__instance];
            if (__instance.IsOwner)
            {
                if (instanceData.startPatch != true) return;
                if (!__instance.onWall)
                {
                    if (Vector3.Distance(__instance.meshContainer.position, __instance.transform.position) > 0.8f || Mathf.Abs(__instance.meshContainer.position.y - __instance.transform.position.y) > 0.25f)
                    {
                        string text = "null";
                        if (Vector3.Distance(__instance.meshContainer.position, __instance.transform.position) > 0.8f)
                        {
                            text = "Triggered by distance: " + Vector3.Distance(__instance.meshContainer.position, __instance.transform.position);
                        }
                        else if (Mathf.Abs(__instance.meshContainer.position.y - __instance.transform.position.y) > 0.25f)
                        {
                            text = "Triggered by height projection: " + Mathf.Abs(__instance.meshContainer.position.y - __instance.transform.position.y);
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
                        __instance.meshContainer.position = Vector3.Lerp(__instance.meshContainer.position, __instance.agent.nextPosition, Distance(Vector3.Distance(__instance.meshContainer.position, __instance.transform.position), 0.5f));
                        __instance.meshContainerPosition = __instance.meshContainer.position;

                        __instance.meshContainerTargetRotation = Quaternion.Lerp(__instance.meshContainer.rotation, Quaternion.LookRotation(__instance.agent.currentOffMeshLinkData.endPos - __instance.meshContainer.position, Vector3.up), 0.75f);
                    }
                    else if (Mathf.Abs(__instance.meshContainer.position.y - __instance.transform.position.y) > 0.25f)
                    {
                        __instance.meshContainerTargetRotation = Quaternion.LookRotation(__instance.agent.transform.position - __instance.meshContainer.position, Vector3.up);
                    }
                }

                if (!__instance.lookingForWallPosition && __instance.onWall && __instance.movingTowardsTargetPlayer)
                {
                    if (__instance.onWall && Vector3.Distance(__instance.meshContainer.position, __instance.transform.position) < 1f || instanceData.returningFromWallState > 6f)
                    {
                        instanceData.returningFromWallState = 0f;
                    }
                }
            }
            //NavMeshHit navHit = new NavMeshHit();
            //RoundManager.Instance.GetRandomNavMeshPositionInRadiusSpherical(__instance.floorPosition, 20, navHit);
        }

        [HarmonyPatch("DoAIInterval")]
        [HarmonyPostfix]
        static void DoAIIntervalPostfix(SandSpiderAI __instance)
        {
            /*if (__instance.lookingForWallPosition && __instance.gotWallPositionInLOS && !__instance.reachedWallPosition)
            {
                spiderData[__instance].reachedWallTimer += __instance.AIIntervalTime;
            }
            if (spiderData[__instance].reachedWallTimer > 10f)
            {
                InitialScript.Logger.LogWarning("Failed to reach wall in time. Recalculating...");
                __instance.gotWallPositionInLOS = __instance.GetWallPositionForSpiderMesh();
                spiderData[__instance].reachedWallTimer = 0f;
            }*/
        }

        [HarmonyPatch("CalculateMeshMovement")]
        [HarmonyPrefix]
        static bool MeshMovementPatch(SandSpiderAI __instance)
        {
            if (__instance.lookingForWallPosition && __instance.gotWallPositionInLOS && __instance.IsOwner)
            {
                if (!__instance.onWall)
                {
                    float distanceFromFloorPosition = Vector3.Distance(__instance.transform.position, __instance.floorPosition);
                    float distanceFromFloorPositionMesh = Vector3.Distance(__instance.meshContainer.transform.position, __instance.floorPosition);

                    InitialScript.Logger.LogInfo("distanceFromFloorPosition: " + distanceFromFloorPosition);
                    InitialScript.Logger.LogInfo("distanceFromFloorPositionMesh: " + distanceFromFloorPositionMesh);

                    __instance.SetDestinationToPosition(__instance.floorPosition);
                    __instance.CalculateSpiderPathToPosition();
                    __instance.navigateToPositionTarget = __instance.transform.position + Vector3.Normalize(__instance.agent.desiredVelocity) * 2f;
                    if (distanceFromFloorPosition < 1.7f && distanceFromFloorPositionMesh < 1.7f)
                    {
                        __instance.onWall = true;
                        return true;
                    }
                    return false;
                }
            }
            return true;
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

        [HarmonyPatch("TriggerChaseWithPlayer")]
        [HarmonyPrefix]
        static void TriggerChaseWithPlayerPrefix(SandSpiderAI __instance)
        {
            if(__instance.agent.enabled == false)
            {
                __instance.agent.enabled = true;
                if(__instance.onWall)
                {
                    __instance.agent.Warp(__instance.floorPosition);
                }
            }
        }
    }
}