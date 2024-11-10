using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using SpiderPositionFix.Patches;
using System.Collections.Generic;
using System.Reflection;
using System.IO;

namespace SpiderPositionFix
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    public class InitialScript : BaseUnityPlugin
    {
        public static InitialScript Instance { get; private set; } = null!;
        internal new static ManualLogSource Logger { get; private set; } = null!;
        internal static Harmony? Harmony { get; set; }
        internal static ConfigClass configSettings { get; set; } = null;
        internal static bool isStarlancePresent = BepInEx.Bootstrap.Chainloader.PluginInfos.ContainsKey("AudioKnight-StarlancerEnemyEscape");
        public static AssetBundle SpiderAssets;
        private void Awake()
        {
            Logger = base.Logger;
            Instance = this;
            configSettings = new ConfigClass(base.Config);

            Patch();

            string sAssemblyLocation = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            SpiderAssets = AssetBundle.LoadFromFile(Path.Combine(sAssemblyLocation, "spideranimationfixbundle"));
            if (SpiderAssets == null)
            {
                Logger.LogError("Failed to load Assets");
            }

            Logger.LogInfo($"{MyPluginInfo.PLUGIN_GUID} v{MyPluginInfo.PLUGIN_VERSION} has loaded!");
        }

        internal static void Patch()
        {
            Harmony ??= new Harmony(MyPluginInfo.PLUGIN_GUID);

            if (isStarlancePresent)
            {
                Logger.LogInfo("Found StarlanceEnemyEscape in Chainloader");
            }

            Logger.LogDebug("Patching spider position fix...");

            Harmony.PatchAll(typeof(SpiderPositionPatch));

            Logger.LogDebug("Finished patching!");
        }

        internal static void Unpatch()
        {
            Logger.LogDebug("Unpatching spider position fix...");

            Harmony?.UnpatchSelf();

            Logger.LogDebug("Finished unpatching!");
        }
    }

    class ConfigClass
    {
        public readonly ConfigEntry<bool> applyMask;
        //debug
        public readonly ConfigEntry<bool> debug;

        public ConfigClass(ConfigFile cfg)
        {
            cfg.SaveOnConfigSet = false;
            {
                applyMask = cfg.Bind("Settings", "Apply changes to agent areaMask", true, "Apply the changes made to the spider agent areaMask. This will affect the pathfinding over offMeshLinks");
                //debug
                debug = cfg.Bind("Debug", "Debug logs", false, "Enable debug logs");
            }
            ClearOrphanedEntries(cfg);
            cfg.Save();
            cfg.SaveOnConfigSet = true;
        }
        public void ClearOrphanedEntries(ConfigFile cfg)
        {
            PropertyInfo orphanedEnriesProp = AccessTools.Property(typeof(ConfigFile), "OrphanedEntries");
            var orphanedEntries = (Dictionary<ConfigDefinition, string>)orphanedEnriesProp.GetValue(cfg);
            orphanedEntries.Clear();
        }
    }
}
