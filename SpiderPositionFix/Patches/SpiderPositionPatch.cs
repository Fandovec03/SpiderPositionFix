using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.ProBuilder.Shapes;

namespace SpiderPositionFix.Patches
{
    class spiderPositionData
    {
        public int currentJumpMaskBit = 1;
        public bool startPatch = false;
        public bool applySpeedSlowdown = false;
        public float originalSpeed = 4.25f;
        public float offsetSpeed = 0f;
        public float reachedWallTimer = 0f;
        public float delayTimer = 0f;
        public int delayTimes = 0;
        public bool reachTheWallFail = false;
        public Dictionary<int, GameObject> debugObjects = [];
    }

    [HarmonyPatch(typeof(SandSpiderAI))]
    public class SpiderPositionPatch
    {
        static bool debug = InitialScript.configSettings.debug.Value;
        static Dictionary<SandSpiderAI, spiderPositionData> spiderData = [];
        static GameObject ballPrefab;
        static Material whiteBall;
        static Material redBall;
        static Material blueBall;
        static Material greenBall;
        static Material yellowBall;

        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        static void StartPostfix(SandSpiderAI __instance)
        {
            if (InitialScript.SpiderAssets != null)
            {
                try
                {
                    AnimatorOverrideController controller = InitialScript.SpiderAssets.LoadAsset<AnimatorOverrideController>("Assets/LethalCompany/CustomAnims/SandSpider/Spider Anim Override.overrideController");
                    //ballPrefab = InitialScript.SpiderAssets.LoadAsset<GameObject>("Assets/LethalCompany/CustomAnims/SandSpider/WhiteBall.prefab");
                    //whiteBall = InitialScript.SpiderAssets.LoadAsset<Material>("Assets/LethalCompany/CustomAnims/SandSpider/WhiteBallMat.mat");
                    //redBall = InitialScript.SpiderAssets.LoadAsset<Material>("Assets/LethalCompany/CustomAnims/SandSpider/RedBallMat.mat");
                    //blueBall = InitialScript.SpiderAssets.LoadAsset<Material>("Assets/LethalCompany/CustomAnims/SandSpider/BlueBallMat.mat");
                    //greenBall = InitialScript.SpiderAssets.LoadAsset<Material>("Assets/LethalCompany/CustomAnims/SandSpider/GreenBallMat.mat");
                    //yellowBall = InitialScript.SpiderAssets.LoadAsset<Material>("Assets/LethalCompany/CustomAnims/SandSpider/YellowBallMat.mat");
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
                if (__instance.reachedWallPosition && __instance.agent.enabled == true)
                {
                    __instance.agent.avoidancePriority = 99;
                    __instance.agent.enabled = false;
                    //__instance.agent.Warp(__instance.meshContainer.transform.position);
                }

                if (__instance.agent.enabled == false && (!__instance.onWall || __instance.waitOnWallTimer <= 0))
                {
                    __instance.agent.avoidancePriority = 25;
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
                //if (debug) InitialScript.Logger.LogDebug("watchFromDistance true. Returning...");
                return;
            }

            if (!__instance.onWall)
            {
                __instance.spiderSpeed = __instance.agent.speed;
                /*if (Vector3.Distance(__instance.meshContainer.position, __instance.transform.position) > 0.4f && !__instance.agent.isOnOffMeshLink)
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
                }*/
                if (__instance.agent.isOnOffMeshLink)
                {
                    __instance.agent.speed = instanceData.originalSpeed / 1.15f;
                    if (debug) InitialScript.Logger.LogDebug("On offMeshLink. Cutting speed");
                }
                if (!__instance.agent.isOnOffMeshLink && !__instance.onWall)
                {
                    //__instance.meshContainerPosition = __instance.transform.position;
                    //__instance.meshContainer.position = __instance.transform.position;
                    __instance.meshContainerTarget = __instance.transform.position;
                }
            }
            else
            {
                /*if (instanceData.applySpeedSlowdown && __instance.onWall && !__instance.reachedWallPosition)
                {
                    instanceData.applySpeedSlowdown = false;
                    instanceData.offsetSpeed = 0;
                    __instance.agent.speed = instanceData.originalSpeed;
                    if (debug) InitialScript.Logger.LogDebug("/2/ Returning original speed");
                }*/
                __instance.spiderSpeed = 3.75f;
                if (__instance.onWall)
                {
                    __instance.agent.speed = 0f;
                }
            }
            if (__instance.reachedWallPosition)
            {
                spiderData[__instance].reachTheWallFail = false;
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

                /*if (__instance.agent.velocity.y > 0.6f || __instance.agent.velocity.y < -0.6f)
                {
                    if (debug) InitialScript.Logger.LogMessage($"Vertical velocity: {__instance.agent.velocity.y}");
                    __instance.meshContainerTargetRotation = Quaternion.LookRotation(__instance.agent.velocity, Vector3.up);
                }*/

                if (!__instance.onWall)
                {
                    /*if (/*Vector3.Distance(__instance.meshContainer.position, __instance.transform.position) > 0.8f || Mathf.Abs(__instance.meshContainer.position.y - __instance.transform.position.y) > 0.25f)
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

                    }*/
                    if (__instance.agent.isOnOffMeshLink)
                    {
                        __instance.meshContainer.position = Vector3.Lerp(__instance.meshContainer.position, __instance.agent.nextPosition, Distance(Vector3.Distance(__instance.meshContainer.position, __instance.transform.position), 0.5f));
                        __instance.meshContainerPosition = __instance.meshContainer.position;

                        __instance.meshContainerTargetRotation = Quaternion.Lerp(__instance.meshContainer.rotation, Quaternion.LookRotation(__instance.agent.currentOffMeshLinkData.endPos - __instance.meshContainer.position, Vector3.up), 0.75f);
                    }
                    else
                    {
                        if (Mathf.Abs(__instance.agent.velocity.y) > 0.6f) __instance.meshContainerTargetRotation = Quaternion.LookRotation(__instance.agent.velocity * Time.deltaTime, Vector3.up);
                        //__instance.meshContainerTargetRotation.SetLookRotation();
                    }
                    /*else if (Mathf.Abs(__instance.meshContainer.position.y - __instance.transform.position.y) > 0.25f)
                    {
                        __instance.meshContainerTargetRotation = Quaternion.LookRotation(__instance.agent.transform.position - __instance.meshContainer.position, Vector3.up);
                    }*/

                    if (Vector3.Distance(__instance.transform.position, __instance.meshContainer.position) > 0.25f && !__instance.onWall)
                    {
                        __instance.meshContainerTarget = __instance.transform.position;
                    }

                    //InitialScript.Logger.LogInfo($"original: {__instance.refVel.magnitude}, agent velocity: {__instance.agent.velocity.magnitude}, new: {(__instance.agent.velocity * Time.deltaTime).magnitude}");

                    //__instance.refVel = __instance.agent.velocity * Time.deltaTime;
                }

                /*if (!__instance.lookingForWallPosition && __instance.onWall && __instance.movingTowardsTargetPlayer)
                 {
                     if (__instance.onWall && Vector3.Distance(__instance.meshContainer.position, __instance.transform.position) < 1f || instanceData.returningFromWallState > 6f)
                     {
                         instanceData.returningFromWallState = 0f;
                     }
                 }*/
            }
            //NavMeshHit navHit = new NavMeshHit();
            //RoundManager.Instance.GetRandomNavMeshPositionInRadiusSpherical(__instance.floorPosition, 20, navHit);
            //InitialScript.Logger.LogDebug($"original: {__instance.refVel.magnitude}, agent velocity: {__instance.agent.velocity.magnitude}, agent velocity.sqrd: {__instance.agent.desiredVelocity.magnitude}, new: {(__instance.agent.velocity * Time.deltaTime).magnitude}");
        }

        [HarmonyPatch("CalculateMeshMovement")]
        [HarmonyPrefix]
        static bool MeshMovementPatch(SandSpiderAI __instance)
        {
            if (__instance.lookingForWallPosition && __instance.gotWallPositionInLOS)
            {
                if (!__instance.onWall)
                {
                    float distanceFromFloorPosition = Vector3.Distance(__instance.transform.position, __instance.floorPosition);
                    float distanceFromFloorPositionMesh = Vector3.Distance(__instance.meshContainer.transform.position, __instance.floorPosition);

                    if (spiderData[__instance].delayTimer > 0.4f)
                    {
                        if (debug)
                        {
                            InitialScript.Logger.LogInfo("distanceFromFloorPosition: " + distanceFromFloorPosition);
                            InitialScript.Logger.LogInfo("distanceFromFloorPositionMesh: " + distanceFromFloorPositionMesh);
                        }
                        spiderData[__instance].delayTimer = 0f;
                        spiderData[__instance].delayTimes++;

                        if (spiderData[__instance].delayTimes >= 40 && __instance.floorPosition != Vector3.zero)
                        {
                            InitialScript.Logger.LogWarning(__instance + ", ID " + __instance.NetworkObjectId + " failing to climb walls within set timer!");
                        }
                    }
                    else
                    {
                        spiderData[__instance].delayTimer += Time.deltaTime;
                    }
                    __instance.SetDestinationToPosition(__instance.floorPosition);
                    __instance.CalculateSpiderPathToPosition();
                    //__instance.navigateToPositionTarget = __instance.transform.position + Vector3.Normalize(__instance.agent.desiredVelocity) * 2f;
                    if (distanceFromFloorPosition < 0.7f && distanceFromFloorPositionMesh < 0.7f && __instance.floorPosition != Vector3.zero)
                    {
                        __instance.onWall = true;
                        //__instance.meshContainerTarget = __instance.wallPosition;
                        spiderData[__instance].delayTimes = 0;
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
        /*
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
        }*/

        [HarmonyPatch("GetWallPositionForSpiderMesh")]
        [HarmonyPostfix]
        static void GetWallPositionForSpiderMeshPatch(SandSpiderAI __instance, ref bool __result)
        {
            spiderPositionData instanceData = spiderData[__instance];

            if (__instance.floorPosition == Vector3.zero)
            {
                InitialScript.Logger.LogWarning($"failed to get position for floorPosition.");
                __result = false;
            }

            /*foreach (GameObject i in instanceData.debugObjects.Values.ToList())
            {
                GameObject.Destroy(i);
            }
            instanceData.debugObjects.Clear();

            Dictionary<int, Vector3> wallVectors = new Dictionary<int, Vector3>();

            RaycastHit rayHitCustom;
            GameObject spawningPrefab = ballPrefab;

            //if (__instance.floorPosition == Vector3.zero) __result = false;
            //{
            //if (__instance.wallPosition != null)
            //{
            Vector3 projection = new Vector3(__instance.meshContainer.position.x, __instance.wallPosition.y, __instance.meshContainer.position.z);
            spawningPrefab.GetComponent<MeshRenderer>().material = redBall;
            spawningPrefab.GetComponent<ScanNodeProperties>().headerText = $"projectedWallPosition {projection}";
            InitialScript.Logger.LogInfo("projected wallPosition");
            instanceData.debugObjects.Add(-1,UnityEngine.Object.Instantiate(spawningPrefab, projection, Quaternion.identity));
            InitialScript.Logger.LogInfo($"instantiated projectedWallPosition {projection}");

            wallVectors.Add(0,__instance.meshContainer.position);
            wallVectors.Add(5,__instance.wallPosition);
            wallVectors.Add(6, __instance.floorPosition);

            for (int i = 1; i < 5; i++)
            {
                float t = (float)i / 5;

                Vector3 lerpedVector = Vector3.Lerp(__instance.meshContainer.position, projection, t);
                //InitialScript.Logger.LogInfo($"calculated lerp {Vector3.Lerp(__instance.meshContainer.position, projection, t)}");
                //InitialScript.Logger.LogInfo($"projected lerpedVector {lerpedVector}, container position: {__instance.meshContainer.position}, projection: {projection}, t: {t}");
                Vector3 projectedWallPos = new Vector3(__instance.wallPosition.x, lerpedVector.y, __instance.wallPosition.z);

                spawningPrefab.GetComponent<MeshRenderer>().material = whiteBall;
                Physics.Raycast(lerpedVector, projectedWallPos - lerpedVector, out rayHitCustom, 7f, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore);
                spawningPrefab.GetComponent<ScanNodeProperties>().headerText = $"Generated lerpedVector {i}";
                //UnityEngine.Object.Instantiate(spawningPrefab, lerpedVector, Quaternion.identity);
                wallVectors.Add(i, rayHitCustom.point);

                InitialScript.Logger.LogInfo($"set wallVector[{i}] to {wallVectors[i]}");
            }

            for (int i = 0;i < 7;i++)
            {
                InitialScript.Logger.LogInfo($"Processing wallVector[{i}] |{i}|");
                try
                {
                    if (i == 0) { spawningPrefab.GetComponent<MeshRenderer>().material = greenBall; spawningPrefab.GetComponent<ScanNodeProperties>().headerText = "meshContainer position"; }
                    else if (i == 5) { spawningPrefab.GetComponent<MeshRenderer>().material = blueBall; spawningPrefab.GetComponent<ScanNodeProperties>().headerText = "wall position"; }
                    else if (i == 6) { spawningPrefab.GetComponent<MeshRenderer>().material = yellowBall; spawningPrefab.GetComponent<ScanNodeProperties>().headerText = "floor position"; }
                    else { spawningPrefab.GetComponent<MeshRenderer>().material = whiteBall; spawningPrefab.GetComponent<ScanNodeProperties>().headerText = $"Generated position {i}"; }

                    instanceData.debugObjects.Add(i,UnityEngine.Object.Instantiate(spawningPrefab, wallVectors[i], Quaternion.identity));
                    InitialScript.Logger.LogInfo($"Successfully spawned ball at {wallVectors[i]} |{i}|");
                }
                catch (Exception e)
                {
                    InitialScript.Logger.LogError($"failed to spawn a ball |{i}|");
                    InitialScript.Logger.LogError(e);
                }
            }*/
        }
    }
}