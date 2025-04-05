using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.ProBuilder;
using UnityEngine.ProBuilder.Shapes;
using static UnityEngine.MeshSubsetCombineUtility;

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
        public SandSpiderAI instance;
    }

    [HarmonyPatch(typeof(SandSpiderAI))]
    public class SpiderPositionPatch
    {
        static bool debugLogs = InitialScript.configSettings.debugLogs.Value;
        static bool debugVisals = InitialScript.configSettings.debugVisuals.Value;
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
                    if (debugVisals || true)
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
                        }
                    }
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
                spiderData[__instance].instance = __instance;
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
                if (instanceData.time > 0f) instanceData.time -= Time.deltaTime;

                if (__instance.reachedWallPosition && __instance.agent.enabled == true && __instance.agent.avoidancePriority == 25)
                {
                    __instance.agent.avoidancePriority = 99;
                    __instance.agent.enabled = false;
                    //__instance.agent.Warp(__instance.meshContainer.transform.position);
                }

                if (__instance.agent.enabled == false && (!__instance.onWall || __instance.waitOnWallTimer <= 0) && __instance.agent.avoidancePriority == 99)
                {
                    __instance.agent.avoidancePriority = 25;
                    __instance.agent.enabled = true;
                    if (__instance.onWall)
                    {
                        //__instance.agent.Warp(__instance.floorPosition);
                    }
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
            spiderPositionData instanceData = spiderData[__instance];

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
                __instance.spiderSpeed = __instance.agent.speed;

                if (__instance.agent.isOnOffMeshLink)
                {
                    __instance.agent.speed = instanceData.originalSpeed / 1.15f;
                    if (debugLogs) InitialScript.Logger.LogDebug("On offMeshLink. Cutting speed");
                }
            }
            else
            {
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

                if (!__instance.onWall && !__instance.agent.isOnOffMeshLink && Vector3.Distance(__instance.meshContainer.position, __instance.transform.position) > 0.35f && __instance.agent.velocity.magnitude > 3f && __instance.agent.speed > 0.5f)
                {
                    __instance.meshContainerTarget = __instance.transform.position + __instance.agent.velocity.normalized * 1.25f;
                }
            }

            if (!__instance.onWall && !__instance.gotWallPositionInLOS)
            {
                foreach (GameObject i in instanceData.debugObjects.Values.ToList())
                {
                    GameObject.Destroy(i);
                }
                instanceData.debugObjects.Clear();

                GameObject spawningPrefab = ballPrefab;


                InstantiateVisalTool(__instance, spawningPrefab, yellowBall, $"path1 corner #1", -5, __instance.meshContainer.position + (__instance.agent.velocity * Time.deltaTime *-1));
                InstantiateVisalTool(__instance, spawningPrefab, greenBall, $"refVel", -4, __instance.meshContainer.position + __instance.refVel);
                InstantiateVisalTool(__instance, spawningPrefab, redBall, $"meshContainerTarget", -3, __instance.meshContainerTarget);
                InstantiateVisalTool(__instance, spawningPrefab, blueBall, $"meshContainerServerPosition", -2, __instance.meshContainerServerPosition);
            }
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
                    float distanceFromFloorPositionMesh = Vector3.Distance(__instance.meshContainer.position, __instance.floorPosition);

                    if (spiderData[__instance].delayTimer > 1f)
                    {
                        if (debugLogs)
                        {
                            InitialScript.Logger.LogDebug("distanceFromFloorPosition: " + distanceFromFloorPosition);
                            InitialScript.Logger.LogDebug("distanceFromFloorPositionMesh: " + distanceFromFloorPositionMesh);
                        }
                        spiderData[__instance].delayTimer = 0f;
                        spiderData[__instance].delayTimes++;

                        if (spiderData[__instance].delayTimes >= 20)
                        {
                            InitialScript.Logger.LogWarning(__instance + ", NWID " + __instance.NetworkObjectId + " failing to climb walls within set timer!");
                            spiderData[__instance].delayTimes = 0;
                        }
                    }
                    else
                    {
                        spiderData[__instance].delayTimer += Time.deltaTime;
                    }
                    __instance.SetDestinationToPosition(__instance.floorPosition);
                    __instance.CalculateSpiderPathToPosition();
                    if (distanceFromFloorPosition < 0.7f && distanceFromFloorPositionMesh < 0.7f)
                    {
                        __instance.onWall = true;
                        spiderData[__instance].delayTimes = 0;
                        return true;
                    }
                    return false;
                }
            }
            return true;
        }

        [HarmonyPatch("CalculateMeshMovement")]
        [HarmonyPostfix]
        static void MeshMovementPostfixPatch(SandSpiderAI __instance)
        {
            if (!__instance.onWall)
            {
                if (__instance.agent.isOnOffMeshLink)
                {
                    __instance.meshContainer.position = Vector3.Lerp(__instance.meshContainer.position, __instance.agent.nextPosition, Distance(Vector3.Distance(__instance.meshContainer.position, __instance.transform.position), 0.5f));
                    __instance.meshContainerPosition = __instance.meshContainer.position;

                    __instance.meshContainerTargetRotation = Quaternion.Lerp(__instance.meshContainer.rotation, Quaternion.LookRotation(__instance.agent.currentOffMeshLinkData.endPos - __instance.meshContainer.position, Vector3.up), 0.75f);
                }
                else
                {
                    if (spiderData[__instance].time <= 0f && debugLogs) { InitialScript.Logger.LogDebug(__instance.agent.velocity.magnitude); spiderData[__instance].time = 0.4f; }

                    if (__instance.agent.velocity.magnitude > 3f && !__instance.overrideSpiderLookRotation)
                    {
                        __instance.meshContainerTargetRotation = Quaternion.LookRotation(__instance.agent.velocity.normalized * Time.deltaTime, Vector3.up);
                    }

                    if (__instance.agent.velocity.magnitude > 3f && __instance.agent.speed > 0.5f)
                    {
                        __instance.navigateToPositionTarget = __instance.transform.position + __instance.agent.velocity.normalized * Time.deltaTime * 1.25f;
                    }

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
            spiderPositionData instanceData = spiderData[__instance];
            NavMeshHit NMHit = new NavMeshHit();
            Vector3 normalPosition = __instance.wallPosition + __instance.wallNormal;
            Vector3 normalProjection = new Vector3(normalPosition.x, __instance.wallPosition.y, normalPosition.z);
            NavMeshPath pathCheck = new NavMeshPath();
            Vector3 unmodifiedWallPosition = __instance.rayHit.point;
            List<int> failedRaycastList = new List<int>();


            InitialScript.Logger.LogInfo($"Test | WallPosition: {__instance.wallPosition}, unmodifiedWallPosition: {unmodifiedWallPosition}");

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
                        spiderData[__instance].faildetToGetPositionTimes++;
                        continue;
                    }
                    __instance.floorPosition = newFloorPosition;
                    spiderData[__instance].faildetToGetPositionTimes = 0;
                    __result = true;
                    instanceData.invalidPositionTimer = 0f;
                    InitialScript.Logger.LogMessage($"Assigned new floor position.");
                    break;
                }
            }
            pathCheck.ClearCorners();

            foreach (GameObject i in instanceData.debugObjects.Values.ToList())
            {
                GameObject.Destroy(i);
            }
            instanceData.debugObjects.Clear();

            if (!debugVisals) return;

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
            static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
            {
                if (true)
                {
                    InitialScript.Logger.LogWarning("Fired Transpiller");
                    CodeMatcher matcher = new CodeMatcher(instructions);

                    matcher.MatchForward(false,
                        new CodeMatch(OpCodes.Call, RoundManager.Instance),
                        new CodeMatch(OpCodes.Ldarg_0),
                        new CodeMatch(OpCodes.Call, typeof(UnityEngine.Component).GetMethod(nameof(Component.transform.position), BindingFlags.Instance | BindingFlags.Public)))
                        .ThrowIfInvalid("Failed to find a match").Set(OpCodes.Ldfld, AccessTools.Field(typeof(SandSpiderAI), nameof(SandSpiderAI.homeNode)));
                    InitialScript.Logger.LogWarning("Transpiller Finished");
                    return matcher.Instructions();
                }
                return instructions;
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
            spiderPositionData instanceData = spiderData[__instance];
            spawningPrefab.GetComponentInChildren<MeshRenderer>().material = material;
            spawningPrefab.GetComponent<ScanNodeProperties>().headerText = headerText;
            instanceData.debugObjects.Add(index, UnityEngine.Object.Instantiate(spawningPrefab, position, Quaternion.identity));
        }

        private bool GetWallPositionForSpiderMesh(SandSpiderAI __instance)
        {
            float num = 6f;
            Vector3 check = __instance.transform.position;

            float num2 = RoundManager.Instance.YRotationThatFacesTheNearestFromPosition(__instance.transform.position + Vector3.up * num, 10f);
            float num3 = RoundManager.Instance.YRotationThatFacesTheNearestFromPosition(__instance.homeNode.position + Vector3.up * num, 10f);

            return false;
        }
    }
}