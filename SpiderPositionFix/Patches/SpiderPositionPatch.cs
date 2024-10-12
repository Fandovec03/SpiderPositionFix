using HarmonyLib;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.AI;

namespace SpiderPositionFix.Patches
{
    [HarmonyPatch(typeof(SandSpiderAI))]
    public class SpiderPositionPatch
    {
        private static bool isInWallState = false;
        private static float returningFromWallState = 0f;
        private static bool startPatch = false;

        [HarmonyPatch("Start")]
        [HarmonyPostfix]
        static void StartPostfix(SandSpiderAI __instance)
        {
            __instance.agent.areaMask &= ~(1 << NavMesh.GetAreaFromName("Jump"));
            startPatch = true;
        }

        [HarmonyPatch("LateUpdate")]
        [HarmonyPostfix]
        static void MeshContainerPositionFix(SandSpiderAI __instance)
        {       

            InicialScript.Logger.LogInfo(Vector3.Distance(__instance.meshContainerPosition, __instance.transform.position));

            if (startPatch != true) return;

            if (!__instance.lookingForWallPosition && !__instance.gotWallPositionInLOS && !isInWallState)
            {
                if (Vector3.Distance(__instance.meshContainerPosition, __instance.transform.position) > 1f)
                {
                    __instance.meshContainer.LookAt(__instance.transform.position, __instance.transform.up);
                    __instance.meshContainerPosition = Vector3.Lerp(__instance.meshContainerPosition, __instance.transform.position, Distance(Vector3.Distance(__instance.meshContainerPosition, __instance.transform.position), 2f) * Time.deltaTime);
                }
            }
            if (__instance.lookingForWallPosition && __instance.gotWallPositionInLOS && !isInWallState)
            {
                isInWallState = true;
            }
            if (__instance.lookingForWallPosition && __instance.gotWallPositionInLOS && isInWallState)
            {
                returningFromWallState += Time.deltaTime;

                if (isInWallState && Vector3.Distance(__instance.meshContainerPosition, __instance.transform.position - __instance.meshContainerPosition) < 1f || returningFromWallState > 10f)
                {
                    isInWallState = false;
                    returningFromWallState = 0f;
                }
            }
        }

        static float Distance(float distance, float speed)
        {
            float ratio = distance / speed;
            return ratio;
        }
    }
}