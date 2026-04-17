using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using SpiderPositionFix.Patches;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace SpiderPositionFix
{
    [BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
    [BepInDependency("SPF_debugTools", BepInDependency.DependencyFlags.SoftDependency)]
    public class InitialScript : BaseUnityPlugin
    {
        public static InitialScript Instance { get; private set; } = null!;
        internal new static ManualLogSource Logger { get; private set; } = null!;
        internal static Harmony? Harmony { get; set; }
        internal static ConfigClass configSettings { get; set; } = null;
        public static AssetBundle SpiderAssets;
        internal static bool debugTools = false;
        internal static bool debugToolsInit = false;
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

            Logger.LogDebug("Patching spider position fix...");

            Harmony.PatchAll(typeof(SpiderPositionPatch));
            if (Chainloader.PluginInfos.ContainsKey("SPF_debugTools"))
            {
                //Harmony.PatchAll(typeof(SPF_debugToolsClass));
                debugTools = true;
            }
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
        public readonly ConfigEntry<bool> debugLogs;
        //public readonly ConfigEntry<bool> debugVisuals;

        public ConfigClass(ConfigFile cfg)
        {
            cfg.SaveOnConfigSet = false;
            {
                applyMask = cfg.Bind("Settings", "Apply changes to agent areaMask", true, "Apply the changes made to the spider agent areaMask. This will affect the pathfinding over offMeshLinks");
                //debug
                debugLogs = cfg.Bind("Debug", "Debug logs", false, "Enable debug logs");
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
