using GameNetcodeStuff;
using HarmonyLib;
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
        public bool applySpeedSlowdown = false;
        public float originalSpeed = 4.25f;
        public float offsetSpeed = 0f;
        public float reachedWallTimer = 0f;
        public float delayTimer = 0f;
        public int delayTimes = 0;
        public bool reachTheWallFail = false;
        public Dictionary<int, GameObject> debugObjects = [];
        public float time = 0.2f;
        public Vector3 originalWallPosition = Vector3.zero;
        public float invalidPositionTimer = 0f;
        public int faildetToGetPositionTimes = 0;
        public Transform altWallPosForMesh = new Transform();
        public bool randomWallPosTurn = false;
        public Vector3 previousMeshContainerPos = Vector3.zero;
    }

    [HarmonyPatch(typeof(SandSpiderAI))]
    public class SpiderPositionPatch
    {
        static bool debugLogs = InitialScript.configSettings.debugLogs.Value;
        static bool debugVisals = InitialScript.configSettings.debugVisuals.Value;
        internal static Dictionary<SandSpiderAI, spiderPositionData> spiderData = [];

#pragma warning disable CS8618 // Pole, které nemùže být null, musí pøi ukonèování konstruktoru obsahovat hodnotu, která není null. Zvažte pøidání modifikátoru required nebo deklaraci s možnou hodnotou null.
        static GameObject ballPrefab;
        static Material whiteBall;
        static Material redBall;
        static Material blueBall;
        static Material greenBall;
        static Material yellowBall;
#pragma warning restore CS8618 // Pole, které nemùže být null, musí pøi ukonèování konstruktoru obsahovat hodnotu, která není null. Zvažte pøidání modifikátoru required nebo deklaraci s možnou hodnotou null.

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
                    AnimatorOverrideController controller = InitialScript.SpiderAssets.LoadAsset<AnimatorOverrideController>("Assets/LethalCompany/CustomAnims/SandSpider/Spider Anim Override.overrideController");
                    // For visual objects
                    if (debugVisals)
                    {
                        try
                        {
                            ballPrefab = InitialScript.SpiderAssets.LoadAsset<GameObject>("Assets/LethalCompany/CustomAnims/SandSpider/WhiteBall.prefab");
                            whiteBall = InitialScript.SpiderAssets.LoadAsset<Material>("Assets/LethalCompany/CustomAnims/SandSpider/WhiteBallMat.mat");
                            redBall = InitialScript.SpiderAssets.LoadAsset<Material>("Assets/LethalCompany/CustomAnims/SandSpider/RedBallMat.mat");
                            blueBall = InitialScript.SpiderAssets.LoadAsset<Material>("Assets/LethalCompany/CustomAnims/SandSpider/BlueBallMat.mat");
                            greenBall = InitialScript.SpiderAssets.LoadAsset<Material>("Assets/LethalCompany/CustomAnims/SandSpider/GreenBallMat.mat");
                            yellowBall = InitialScript.SpiderAssets.LoadAsset<Material>("Assets/LethalCompany/CustomAnims/SandSpider/YellowBallMat.mat");
                        }
                        catch (Exception e)
                        {
                            InitialScript.Logger.LogWarning("Failed to load visual debug asset");
                            InitialScript.Logger.LogWarning(e);
                        }
                    }
                    //
                    __instance.creatureAnimator.runtimeAnimatorController = controller;
                }
                catch
                {
                    InitialScript.Logger.LogError("Failed to load OverrideController asset");
                }
            }
            GetSpiderData(__instance).startPatch = true;
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

                if (__instance.reachedWallPosition && __instance.agent.enabled == true && __instance.agent.avoidancePriority == 25)
                {
                    __instance.agent.avoidancePriority = 99;
                    __instance.agent.enabled = false;
                }

                if (__instance.agent.enabled == false && (!__instance.onWall || __instance.waitOnWallTimer <= 0) && __instance.agent.avoidancePriority == 99)
                {
                    __instance.agent.avoidancePriority = 25;
                    __instance.agent.enabled = true;
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
                    __instance.agent.speed = instanceData.originalSpeed / 1.15f;
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
                __instance.agent.speed = instanceData.originalSpeed / 3;
            }
        }

        /*[HarmonyPatch("CalculateSpiderPathToPosition")]
        [HarmonyPostfix]
        static void CalculateSpiderPathToPositionPostfix(SandSpiderAI __instance)
        {
            if (NavMesh.CalculatePath(__instance.meshContainer.position, __instance.navigateToPositionTarget, __instance.agent.areaMask, __instance.path1))
            {
                float desiredToActualRatio = 1 / (__instance.agent.desiredVelocity.magnitude / __instance.agent.velocity.magnitude);
                if (desiredToActualRatio > 0.90)
                {
                    if (Vector3.Distance(__instance.transform.position, __instance.meshContainer.position) < 2f) __instance.meshContainerTarget = __instance.transform.position + (__instance.agent.desiredVelocity * 1.15f * __instance.AIIntervalTime);
                    else __instance.meshContainerTarget = __instance.transform.position;
                }
            }
        }*/

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

            if (!__instance.onWall && !__instance.gotWallPositionInLOS && debugVisals)
            {
                /*foreach (GameObject i in instanceData.debugObjects.Values.ToList())
                {
                    GameObject.Destroy(i);
                }
                instanceData.debugObjects.Clear();*/

                GameObject spawningPrefab = ballPrefab;


                InstantiateVisalTool(__instance, spawningPrefab, yellowBall, $"meshContainerPosition", -5, __instance.meshContainerPosition);
                InstantiateVisalTool(__instance, spawningPrefab, greenBall, $"refVel", -4, __instance.meshContainer.position + __instance.refVel);
                InstantiateVisalTool(__instance, spawningPrefab, redBall, $"meshContainerTarget", -3, __instance.meshContainerTarget);
                InstantiateVisalTool(__instance, spawningPrefab, blueBall, $"meshContainerServerPosition", -2, __instance.meshContainerServerPosition);

                for (int i = 0; i < __instance.agent.path.corners.Length; i++)
                {
                    //InstantiateVisalTool(__instance, spawningPrefab, whiteBall, $"path corner #{i}", 100 + i, __instance.agent.path.corners[i]);
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

                if (__instance.agent.velocity.magnitude > 1f)
                {
                    //if (desiredToActualRatio > 0.9)
                    meshTargetPosition = __instance.transform.position + (__instance.agent.velocity * 1.15f * __instance.AIIntervalTime);
                    //else meshTargetPosition = __instance.meshContainer.position;
                }
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
                        direction = __instance.agent.desiredVelocity * 1.2f;
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
            List<int> failedRaycastList = new List<int>();


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

            foreach (GameObject i in instanceData.debugObjects.Values.ToList())
            {
                if (__instance.enabled) GameObject.Destroy(i);
            }
            instanceData.debugObjects.Clear();

            if (!debugVisals) return;
            //For debug visuals, otherwise the patch ends here
            Dictionary<int, List<Vector3>> wallVectors = new Dictionary<int, List<Vector3>>();
            LineRenderer renderedLine;
            RaycastHit rayHitCustom;
            GameObject spawningPrefab = ballPrefab;

            Vector3 projection = new Vector3(__instance.meshContainer.position.x, unmodifiedWallPosition.y, __instance.meshContainer.position.z);
            spawningPrefab.GetComponentInChildren<MeshRenderer>().SetMaterial(redBall);
            spawningPrefab.GetComponent<ScanNodeProperties>().headerText = $"projected WallPosition";
            float projectionDistance = Vector3.Distance(__instance.meshContainer.position, projection);
            int splitNum = (int)MathF.Round(projectionDistance * 2, 0);
            if (debugLogs) InitialScript.Logger.LogInfo($"Rounded up {projectionDistance} to {splitNum}");   
            renderedLine = spawningPrefab.GetComponentInChildren<LineRenderer>(); renderedLine.SetMaterial(redBall); renderedLine.useWorldSpace = true;

            if (debugLogs) InitialScript.Logger.LogInfo("projected wallPosition");
            instanceData.debugObjects.Add(-1,UnityEngine.Object.Instantiate(spawningPrefab, projection, Quaternion.identity));
            if (debugLogs) InitialScript.Logger.LogInfo($"instantiated projectedWallPosition {projection}");

            Vector3 projectedMeshPosition = Vector3.Project(__instance.meshContainer.position - unmodifiedWallPosition, normalProjection - unmodifiedWallPosition) + unmodifiedWallPosition;
            Vector3 CalculaterMeshPosition = new Vector3(projectedMeshPosition.x,__instance.meshContainer.position.y, projectedMeshPosition.z);


            wallVectors.Add(0,[__instance.meshContainer.position]);
            wallVectors.Add(1,[unmodifiedWallPosition, __instance.transform.position]);
            wallVectors.Add(2, [__instance.floorPosition, unmodifiedWallPosition]);
            wallVectors.Add(3, [normalProjection, unmodifiedWallPosition]);
            wallVectors.Add(4, [projectedMeshPosition, unmodifiedWallPosition]);

            for (int i = 1; i < splitNum; i++)
            {
                float t = (float)i / splitNum;

                Vector3 lerpedVector = Vector3.Lerp(CalculaterMeshPosition, projectedMeshPosition, t);
                Vector3 projectedWallPos = new Vector3(unmodifiedWallPosition.x, lerpedVector.y, unmodifiedWallPosition.z);

                spawningPrefab.GetComponentInChildren<MeshRenderer>().SetMaterial(whiteBall);
                Ray cRay = new Ray(lerpedVector, projectedWallPos - lerpedVector);
                if (!Physics.Raycast(cRay, out rayHitCustom, 7f, StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore))
                {
                    InitialScript.Logger.LogWarning("Raycast failed to hit anything within set distance.");
                    failedRaycastList.Add(i);
                }
                spawningPrefab.GetComponent<ScanNodeProperties>().headerText = $"Generated lerpedVector {i}";
                wallVectors.Add(4 + i, [cRay.GetPoint(rayHitCustom.distance - 0.2f), lerpedVector]);

                if (debugLogs) InitialScript.Logger.LogInfo($"set wallVector[{i}] to {wallVectors[4+i][0]}");
                if (wallVectors[4 + i][0] == Vector3.zero) InitialScript.Logger.LogWarning("Invalid raycast position detected");
            }

            if (debugLogs)
            {
                InitialScript.Logger.LogInfo($"final splitNum count = {splitNum}");
                InitialScript.Logger.LogInfo($"total count = {splitNum}");
            }


            for (int i = 0;i < 4 + splitNum;i++)
            {
                InitialScript.Logger.LogInfo($"Processing wallVector[{i}] |{i}|");
                try
                {
                    bool validWallVector = !Physics.Linecast(__instance.floorPosition, wallVectors[i][0], StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore) || !Physics.Linecast(normalProjection, wallVectors[i][0], StartOfRound.Instance.collidersAndRoomMaskAndDefault, QueryTriggerInteraction.Ignore);

                    if (i == 0) { spawningPrefab.GetComponentInChildren<MeshRenderer>().SetMaterial(greenBall); spawningPrefab.GetComponent<ScanNodeProperties>().headerText = "meshContainer position"; spawningPrefab.gameObject.name = $"meshContainer position {i}"; }
                    else if (i == 1) { spawningPrefab.GetComponentInChildren<MeshRenderer>().SetMaterial(blueBall); spawningPrefab.GetComponent<ScanNodeProperties>().headerText = "wall position"; spawningPrefab.gameObject.name = $"wall position {i}"; }
                    else if (i == 2) { spawningPrefab.GetComponentInChildren<MeshRenderer>().SetMaterial(yellowBall); spawningPrefab.GetComponent<ScanNodeProperties>().headerText = "floor position"; spawningPrefab.gameObject.name = $"floor position {i}"; }
                    else if (i == 3) { spawningPrefab.GetComponentInChildren<MeshRenderer>().SetMaterial(blueBall); spawningPrefab.GetComponent<ScanNodeProperties>().headerText = "projected normal position"; spawningPrefab.gameObject.name = $"projected normal position {i}"; }
                    else if (i == 4) { spawningPrefab.GetComponentInChildren<MeshRenderer>().SetMaterial(blueBall); spawningPrefab.GetComponent<ScanNodeProperties>().headerText = "projected meshContainer position on Normal"; spawningPrefab.gameObject.name = $"projected meshContainer position on Normal {i}"; }
                    else { spawningPrefab.GetComponentInChildren<MeshRenderer>().SetMaterial(whiteBall); spawningPrefab.GetComponent<ScanNodeProperties>().headerText = $"Generated position {i}"; spawningPrefab.gameObject.name = $"Generated position {i}"; }
                    
                    if (debugLogs) InitialScript.Logger.LogInfo($"Successfully spawned ball at {wallVectors[i][0]} |{i}|");

                    if (i > 4 && validWallVector)
                    {
                        spawningPrefab.GetComponentInChildren<MeshRenderer>().material = yellowBall;
                    }
                    if (i > 4 && failedRaycastList.Contains(i))
                    {
                        spawningPrefab.GetComponentInChildren<MeshRenderer>().material = redBall;
                    }
                    if (wallVectors[i].Count > 1)
                    {
                        if (debugLogs) InitialScript.Logger.LogInfo("Found multiple vectors");

                        try {
                            InitialScript.Logger.LogInfo($"Setting rendered line for {i}, 0: {wallVectors[i][0]}, 1: {wallVectors[i][1]}");
                            renderedLine = spawningPrefab.GetComponentInChildren<LineRenderer>(); renderedLine.material = spawningPrefab.GetComponentInChildren<MeshRenderer>().sharedMaterial; /* = spawningPrefab.GetComponentInChildren<MeshRenderer>().material*/ renderedLine.useWorldSpace = true;
                            renderedLine.gameObject.name = $"{spawningPrefab.gameObject.name} {i}";
                            InitialScript.Logger.LogInfo($"Set name for {i}, 0: {renderedLine.gameObject.name}, 1: {spawningPrefab.gameObject.name}");
                            SetRenderedLinePoints(wallVectors[i].ToArray(), renderedLine);
                        }
                        catch (Exception e) {
                            InitialScript.Logger.LogError($"failed to spawn a ray |{i}|");
                            InitialScript.Logger.LogError(e);
                        }
                    }

                    instanceData.debugObjects.Add(i, UnityEngine.Object.Instantiate(spawningPrefab, wallVectors[i][0], Quaternion.identity));
                }
                catch (Exception e)
                {
                    InitialScript.Logger.LogError($"failed to spawn a ball |{i}|");
                    InitialScript.Logger.LogError(e);
                }
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
        
        public static void SetRenderedLinePoints(Vector3[] positions, LineRenderer lr)
        {
            lr.positionCount = positions.Length;

            for (int i = 0; i < lr.positionCount; i++)
            {
                if (lr.positionCount < 2) break;

                lr.SetPosition(i, positions[i]);
            }
        }

        public static void InstantiateVisalTool(SandSpiderAI __instance, GameObject spawningPrefab, Material material, string headerText, int index, Vector3 position)
        {
            spiderPositionData instanceData = GetSpiderData(__instance);
            spawningPrefab.GetComponentInChildren<MeshRenderer>().material = material;
            spawningPrefab.GetComponent<ScanNodeProperties>().headerText = headerText;
            spawningPrefab.gameObject.name = headerText;
            if (instanceData.debugObjects.Keys.Contains(index))
            {
                UnityEngine.Object.Destroy(instanceData.debugObjects[index]);
                instanceData.debugObjects.Remove(index);
            }
            instanceData.debugObjects.Add(index, UnityEngine.Object.Instantiate(spawningPrefab, position, Quaternion.identity));
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
            }
        }
    }
}