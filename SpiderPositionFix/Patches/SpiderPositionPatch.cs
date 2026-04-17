using GameNetcodeStuff;
using HarmonyLib;
using SPF_debugTools.Patches;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using UnityEngine.AI;

namespace SpiderPositionFix.Patches
{
    class spiderPositionData
    {
        public int currentJumpMaskBit = 1;
        public bool startPatch = false;
        public bool isSlowedDown = false;
        public float originalSpeed = 4.25f;
        public float offsetSpeed = 0f;
        public float delayTimer = 0f;
        public int delayTimes = 0;
        public bool reachTheWallFail = false;
        public float time = 0.2f;
        public float invalidPositionTimer = 0f;
        public int faildetToGetPositionTimes = 0;
        public Transform altWallPosForMesh = new Transform();
        public Vector3 previousMeshContainerPos = Vector3.zero;
    }

    [HarmonyPatch(typeof(SandSpiderAI))]
    public class SpiderPositionPatch
    {
        static bool debugLogs = InitialScript.configSettings.debugLogs.Value;
        //static bool debugVisals = InitialScript.configSettings.debugVisuals.Value;
        internal static Dictionary<SandSpiderAI, spiderPositionData> spiderData = [];

        public static Transform getWallPosTransform(SandSpiderAI instance)
        {
            string valueName = "";
            spiderPositionData data = GetSpiderData(instance);
            if (data.faildetToGetPositionTimes > 10)
            {
                valueName = nameof(instance.homeNode) + " instance.homeNode";
                data.altWallPosForMesh = instance.homeNode;
            }
            else
            {
                valueName = nameof(instance.transform) + " instance.transform";
                data.altWallPosForMesh = instance.transform;
            }
            if (debugLogs) InitialScript.Logger.LogInfo($"Returning {valueName}");
            return data.altWallPosForMesh;
        }


        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        static void StartPostfix(SandSpiderAI __instance)
        {
            if (InitialScript.SpiderAssets != null)
            {
                try
                {
                    AnimatorOverrideController overrideController = new AnimatorOverrideController(__instance.creatureAnimator.runtimeAnimatorController);
                    overrideController["SpiderIdle"] = InitialScript.SpiderAssets.LoadAsset<AnimationClip>("Assets/LethalCompany/CustomAnims/SandSpider/SpiderIdleFixed.anim");
                    __instance.creatureAnimator.runtimeAnimatorController = overrideController;
                }
                catch
                {
                    InitialScript.Logger.LogError("Failed to load OverrideController asset");
                }
            }
            GetSpiderData(__instance).startPatch = true;
            if (InitialScript.debugTools && !InitialScript.debugToolsInit)
            {
                SPF_debugToolsClass.Init();
                InitialScript.debugToolsInit = true;
            }
            GetSpiderData(__instance).altWallPosForMesh = __instance.transform;
        }

        [HarmonyPatch("Update")]
        [HarmonyPrefix]
        static void UpdatePrefix(SandSpiderAI __instance)
        {
            spiderPositionData instanceData = GetSpiderData(__instance);

            if (__instance.IsOwner)
            {
                if (instanceData.time > 0f) instanceData.time -= Time.deltaTime;

                if (__instance.onWall && __instance.agent.isStopped == false)
                {
                    __instance.agent.avoidancePriority = 99;
                    __instance.agent.isStopped = true;
                }

                if (!__instance.onWall && __instance.agent.isStopped == true)
                {
                    __instance.agent.avoidancePriority = 25;
                    __instance.agent.isStopped = false;
                }
            }
            else
            {
                if (instanceData.time <= 0f) instanceData.time = 0f;
            }
        }


        [HarmonyPatch("Update")]
        [HarmonyPostfix]
        static void UpdatePostfix(SandSpiderAI __instance)
        {
            spiderPositionData instanceData = GetSpiderData(__instance);

            if (!__instance.IsOwner) return;

            if (instanceData.invalidPositionTimer > 0) instanceData.invalidPositionTimer -= Time.deltaTime;

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
                //if (debug) InitialScript.Logger.LogDebug("watchFromDistance true. Returning...");
                return;
            }

            if (!__instance.onWall)
            {
                if (__instance.currentBehaviourStateIndex != 1) __instance.agent.speed = __instance.spiderSpeed;

                if (__instance.agent.isOnOffMeshLink)
                {
                    __instance.agent.speed = __instance.spiderSpeed / 1.15f;
                    if (debugLogs) InitialScript.Logger.LogDebug("On offMeshLink. Cutting speed");
                }
            }
            if (__instance.currentBehaviourStateIndex == 1 && __instance.onWall)
            {
                __instance.spiderSpeed = 3.75f;
            }
            if (__instance.reachedWallPosition)
            {
                instanceData.reachTheWallFail = false;
            }

            if (Vector3.Distance(__instance.transform.position, __instance.meshContainer.position) > 2f && !__instance.onWall)
            {
                __instance.agent.speed = __instance.spiderSpeed / 3;
                instanceData.isSlowedDown = true;
            }
            else
            {
                instanceData.isSlowedDown = false;
            }
            if (InitialScript.debugTools)
            {
                SPF_debugToolsClass.SetDebugObjectsPosition(__instance);
            }
        }

        [HarmonyPatch("LateUpdate")]
        [HarmonyPostfix]
        static void MeshContainerPositionFix(SandSpiderAI __instance)
        {
            spiderPositionData instanceData = GetSpiderData(__instance);
            if (__instance.IsOwner)
            {
                if (instanceData.startPatch != true) return;

                //instanceData.MeshContainerVector = instanceData.nextMeshContainerPosition;
                //instanceData.nextMeshContainerPosition = Vector3.MoveTowards(instanceData.nextMeshContainerPosition, __instance.meshContainerTarget, __instance.spiderSpeed);
                //__instance.refVel = instanceData.MeshContainerVector - instanceData.nextMeshContainerPosition;

                if (Vector3.Distance(__instance.meshContainerServerPosition, __instance.meshContainer.position) > 1.5f || Vector3.SignedAngle(__instance.meshContainerServerRotation, __instance.meshContainer.eulerAngles, Vector3.up) > 30f)
                {
                    __instance.meshContainerServerPosition = __instance.meshContainer.position;
                    __instance.meshContainerServerRotation = __instance.meshContainer.rotation.eulerAngles;
                    if (__instance.IsServer)
                    {
                        __instance.SyncMeshContainerPositionClientRpc(__instance.meshContainerServerPosition, __instance.meshContainerServerRotation);
                    }
                }
            }
        }

        [HarmonyPatch("CalculateMeshMovement")]
        [HarmonyPrefix]
        static bool MeshMovementPatch(SandSpiderAI __instance)
        {
            spiderPositionData data = GetSpiderData(__instance);
            if (__instance.lookingForWallPosition && __instance.gotWallPositionInLOS)
            {
                if (!__instance.onWall)
                {
                    float distanceFromFloorPosition = Vector3.Distance(__instance.transform.position, __instance.floorPosition);
                    float distanceFromFloorPositionMesh = Vector3.Distance(__instance.meshContainer.position, __instance.floorPosition);

                    if (data.delayTimer > 1f)
                    {
                        if (debugLogs)
                        {
                            InitialScript.Logger.LogDebug("distanceFromFloorPosition: " + distanceFromFloorPosition);
                            InitialScript.Logger.LogDebug("distanceFromFloorPositionMesh: " + distanceFromFloorPositionMesh);
                        }
                        data.delayTimer = 0f;
                        data.delayTimes++;

                        if (data.delayTimes >= 20)
                        {
                            InitialScript.Logger.LogWarning(__instance + ", NWID " + __instance.NetworkObjectId + " failing to climb walls within set timer!");
                            data.delayTimes = 0;
                        }
                    }
                    else
                    {
                        data.delayTimer += Time.deltaTime;
                    }
                    __instance.SetDestinationToPosition(__instance.floorPosition);
                    //__instance.CalculateSpiderPathToPosition();
                    if (distanceFromFloorPosition < 1f && distanceFromFloorPositionMesh < 0.7f)
                    {
                        //__instance.onWall = true;
                        data.delayTimes = 0;
                        return true;
                    }
                    return false;
                }
            }
            return true;
        }

        [HarmonyPatch("CalculateSpiderPathToPosition")]
        [HarmonyPostfix]
        static void CalculateSpiderPathToPositionPostfix(SandSpiderAI __instance)
        {
            spiderPositionData instanceData = GetSpiderData(__instance);
            if (__instance.IsOwner) {

                Vector3 meshTargetPosition = __instance.meshContainer.position;

                if (__instance.agent.velocity.magnitude > 1.0f) meshTargetPosition = __instance.transform.position + (__instance.agent.velocity * 1.25f * __instance.AIIntervalTime);
                //else meshTargetPosition = __instance.meshContainer.position;
                if (instanceData.isSlowedDown) meshTargetPosition = __instance.transform.position;
                __instance.meshContainerTarget = meshTargetPosition;
            }
        }

        [HarmonyPatch("CalculateMeshMovement")]
        [HarmonyPostfix]
        static void MeshMovementPostfixPatch(SandSpiderAI __instance)
        {
            float desiredToActualRatio = 1 / (__instance.agent.desiredVelocity.magnitude / __instance.agent.velocity.magnitude);
            spiderPositionData data = GetSpiderData(__instance);
            if (!__instance.onWall)
            {
                if (__instance.agent.isOnOffMeshLink)
                {
                    __instance.meshContainer.position = Vector3.Lerp(__instance.meshContainer.position, __instance.agent.transform.position, Distance(Vector3.Distance(__instance.meshContainer.position, __instance.transform.position), 0.5f));
                    __instance.meshContainerPosition = __instance.meshContainer.position;

                    __instance.meshContainerTargetRotation = Quaternion.Lerp(__instance.meshContainer.rotation, Quaternion.LookRotation(__instance.agent.currentOffMeshLinkData.endPos - __instance.meshContainer.position, Vector3.up), 0.75f);
                }
                else
                {
                    if (data.time <= 0f && debugLogs) { InitialScript.Logger.LogDebug(__instance.agent.velocity.magnitude); data.time = 0.4f; }


                    if (!__instance.overrideSpiderLookRotation)
                    {
                        Vector3 direction = __instance.agent.desiredVelocity.normalized;
                        //if (desiredToActualRatio > 0.9)
                        direction = __instance.agent.velocity;
                        if (__instance.agent.path.corners.Length > 1) direction = __instance.agent.path.corners[1] - __instance.meshContainer.position;
                        Quaternion targetRotation = Quaternion.LookRotation(direction + (__instance.meshContainer.forward * 0.02f), Vector3.up);

                        //if (__instance.agent.path.corners.Length > 1)
                        //{
                        //    lookPosition = __instance.agent.path.corners[1];
                        //}
                        __instance.meshContainerTargetRotation = Quaternion.Lerp(__instance.meshContainer.rotation, targetRotation, 0.75f);
                    }
                }
                return;
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

                if (debugLogs) InitialScript.Logger.LogDebug("Spider: Toggled mask bit to " + bit);
            }
        }

        [HarmonyPatch("GetWallPositionForSpiderMesh")]
        [HarmonyPrefix]
        static void GetWallPositionForSpiderMeshPrefix(SandSpiderAI __instance)
        {
            __instance.floorPosition = Vector3.zero;
        }

        [HarmonyPatch("GetWallPositionForSpiderMesh")]
        [HarmonyPostfix]
        static void GetWallPositionForSpiderMeshPatch(SandSpiderAI __instance, ref bool __result)
        {
            spiderPositionData instanceData = GetSpiderData(__instance);
            NavMeshHit NMHit = new NavMeshHit();
            Vector3 normalPosition = __instance.wallPosition + __instance.wallNormal;
            Vector3 normalProjection = new Vector3(normalPosition.x, __instance.wallPosition.y, normalPosition.z);
            NavMeshPath pathCheck = new NavMeshPath();
            Vector3 unmodifiedWallPosition = __instance.rayHit.point;
            //List<int> failedRaycastList = new List<int>();


            if (debugLogs) InitialScript.Logger.LogInfo($"Test | WallPosition: {__instance.wallPosition}, unmodifiedWallPosition: {unmodifiedWallPosition}");

            if (__instance.floorPosition == Vector3.zero || RoundManager.Instance.GetNavMeshPosition(__instance.floorPosition, NMHit, 0.7f) == __instance.floorPosition || !__instance.agent.CalculatePath(__instance.floorPosition, pathCheck) || pathCheck.status == NavMeshPathStatus.PathPartial || pathCheck.status == NavMeshPathStatus.PathInvalid)
            {
                if (instanceData.invalidPositionTimer <= 0f)
                {
                    InitialScript.Logger.LogWarning($"failed to get valid position for floorPosition.");
                    instanceData.invalidPositionTimer = 5f;
                }
                
                Vector3 customWallPos = Vector3.zero;

                for (int i = 0; i < 4; i++)
                {
                    customWallPos = Vector3.Lerp(unmodifiedWallPosition, normalProjection, (float)(i + 1) / 4);
                    Vector3 newFloorPosition = Vector3.zero;
                    RaycastHit rcHit = new RaycastHit();
                    if (Physics.Raycast(customWallPos, Vector3.down, out rcHit, 20f, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
                    {
                        newFloorPosition = rcHit.point;
                    }

                    if (newFloorPosition == Vector3.zero || RoundManager.Instance.GetNavMeshPosition(newFloorPosition, NMHit, 0.7f) == newFloorPosition || !__instance.agent.CalculatePath(newFloorPosition, pathCheck) || pathCheck.status == NavMeshPathStatus.PathPartial || pathCheck.status == NavMeshPathStatus.PathInvalid)
                    {
                        __result = false;
                        instanceData.faildetToGetPositionTimes++;
                        continue;
                    }
                    __instance.floorPosition = newFloorPosition; 
                    instanceData.faildetToGetPositionTimes = 0;
                    __result = true;
                    instanceData.invalidPositionTimer = 0f;
                    InitialScript.Logger.LogMessage($"Assigned new floor position.");
                    break;
                }
            }
            pathCheck.ClearCorners();

            if (InitialScript.debugTools)
            {
                SPF_debugToolsClass.GetWallPositionForMesh(__instance, unmodifiedWallPosition, normalProjection);
            }

            [HarmonyTranspiler]
            [HarmonyPatch(nameof(SandSpiderAI.GetWallPositionForSpiderMesh))]
#pragma warning disable CS8321 // Lokální funkce je deklarovaná, ale vùbec se nepoužívá.
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
#pragma warning restore CS8321 // Lokální funkce je deklarovaná, ale vùbec se nepoužívá.
            {
                InitialScript.Logger.LogWarning("Fired Transpiller");
                CodeMatcher matcher = new CodeMatcher(instructions);

                matcher.
                    MatchForward(true,
                    new CodeMatch(OpCodes.Call, typeof(RoundManager).GetMethod("get_Instance")),
                    new CodeMatch(OpCodes.Ldarg_0),
                    new CodeMatch(OpCodes.Call, typeof(Component).GetMethod("get_Transform")))
                    .ThrowIfInvalid("Failed to find a match")
                    .Set(OpCodes.Call, AccessTools.Method(typeof(SpiderPositionPatch), nameof(SpiderPositionPatch.getWallPosTransform), [typeof(SandSpiderAI)]));    

                for (int i = 0; i < instructions.ToList().Count; i++)
                {
                    if (!debugLogs) break;
                    try
                    {
                        if (matcher.Instructions().ToList()[i].ToString() != instructions.ToList()[i].ToString())
                        {
                            InitialScript.Logger.LogError($"{matcher.Instructions().ToList()[i]} : {instructions.ToList()[i]}");
                            //offset--;
                        }
                        else InitialScript.Logger.LogInfo(instructions.ToList()[i]);
                    }
                    catch
                    {
                        InitialScript.Logger.LogError("Failed to read instructions");
                    }
                }
                InitialScript.Logger.LogWarning("Transpiller Finished");
                return matcher.Instructions();
            }
        }

        static spiderPositionData GetSpiderData(SandSpiderAI spider)
        {
            if (!spiderData.ContainsKey(spider)) spiderData.Add(spider, new spiderPositionData());
            return spiderData[spider];
        }
    }

    class EnemyAIPatch
    {
        [HarmonyPatch(typeof(EnemyAI), nameof(EnemyAI.OnDestroy))]
        [HarmonyPostfix]
        public static void OnDestroyPatch(EnemyAI aI)
        {
            if (aI is SandSpiderAI)
            {
                SpiderPositionPatch.spiderData.Remove((SandSpiderAI)aI);
                InitialScript.Logger.LogMessage($"Cleared {aI.enemyType.enemyName} #{aI.thisEnemyIndex}'s data");
                if (InitialScript.debugTools) SPF_debugToolsClass.DeleteObjects((SandSpiderAI)aI);
            }
        }
    }
}