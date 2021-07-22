using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

using KKAPI.Chara;
using KKAPI.Maker;
using KKAPI.Utilities;

namespace CharacterAccessory
{
	[BepInPlugin(GUID, Name, Version)]
	[BepInDependency("madevil.JetPack", JetPack.Core.Version)]
	[BepInDependency("marco.kkapi", "1.17")]
	[BepInDependency("com.deathweasel.bepinex.materialeditor", "3.0")]
	[BepInDependency("com.joan6694.illusionplugins.moreaccessories", "1.1.0")]
	public partial class CharacterAccessory : BaseUnityPlugin
	{
		public const string GUID = "madevil.kk.ca";
#if DEBUG
		public const string Name = "Character Accessory (Debug Build)";
#else
		public const string Name = "Character Accessory";
#endif
		public const string Version = "1.4.0.0";

		internal static new ManualLogSource Logger;
		internal static CharacterAccessory Instance;
		internal static Dictionary<string, Harmony> HooksInstance = new Dictionary<string, Harmony>();

		internal static ConfigEntry<bool> CfgMakerMasterSwitch { get; set; }
		internal static ConfigEntry<bool> CfgDebugMode { get; set; }
		internal static ConfigEntry<bool> CfgStudioFallbackReload { get; set; }
		internal static ConfigEntry<bool> CfgMAHookUpdateStudioUI { get; set; }

		internal const int PluginDataVersion = 3;
		internal static List<string> SupportList = new List<string>();
		internal static List<string> CordNames = new List<string>();

		private void Awake()
		{
			Logger = base.Logger;
			Instance = this;

			CfgMakerMasterSwitch = Config.Bind("Maker", "Master Switch", true, new ConfigDescription("A quick switch on the sidebar that templary disable the function", null, new ConfigurationManagerAttributes { IsAdvanced = true }));
			CfgStudioFallbackReload = Config.Bind("Studio", "Fallback Reload Mode", false, new ConfigDescription("Enable this if some plugins are having visual problem", null, new ConfigurationManagerAttributes { IsAdvanced = true }));
			CfgDebugMode = Config.Bind("Debug", "Debug Mode", false, new ConfigDescription("Showing debug messages in LogWarning level", null, new ConfigurationManagerAttributes { IsAdvanced = true }));
			CfgMAHookUpdateStudioUI = Config.Bind("Hook", "MoreAccessories UpdateStudioUI", true, new ConfigDescription("Performance tweak, disable it if having issue on studio chara state panel update", null, new ConfigurationManagerAttributes { IsAdvanced = true }));

			if (Application.dataPath.EndsWith("CharaStudio_Data"))
				CharaStudio.Running = true;
		}

		private void Start()
		{
			CordNames = Enum.GetNames(typeof(ChaFileDefine.CoordinateType)).ToList();
			CharacterApi.RegisterExtraBehaviour<CharacterAccessoryController>(GUID);
			HooksInstance["General"] = Harmony.CreateAndPatchAll(typeof(Hooks));

			MoreAccessoriesSupport.Init();
#if DEBUG
			MoreOutfitsSupport.Init();
#endif
			HairAccessoryCustomizerSupport.Init();
			MaterialEditorSupport.Init();
			MaterialRouterSupport.Init();
			AccStateSyncSupport.Init();
			DynamicBoneEditorSupport.Init();
			AAAPKSupport.Init();

			CumOnOverSupport.Init();
			BonerStateSync.Init();

			if (CharaStudio.Running)
			{
				HooksInstance["Studio"] = Harmony.CreateAndPatchAll(typeof(HooksStudio));
				SceneManager.sceneLoaded += CharaStudio.StartupCheck;
			}
			else
			{
				MakerAPI.MakerBaseLoaded += (object sender, RegisterCustomControlsEvent ev) =>
				{
					HooksInstance["Maker"] = Harmony.CreateAndPatchAll(typeof(HooksMaker));
#if DEBUG
					MoreOutfitsSupport.MakerInit();
#endif
					{
						BaseUnityPlugin _instance = JetPack.Toolbox.GetPluginInstance("ClothingStateMenu");
						if (_instance != null)
							HooksInstance["Maker"].Patch(_instance.GetType().GetMethod("OnGUI", AccessTools.all), prefix: new HarmonyMethod(typeof(HooksMaker), nameof(HooksMaker.DuringLoading_Prefix)));
					}
				};

				MakerAPI.MakerExiting += (object sender, EventArgs ev) =>
				{
					HooksInstance["Maker"].UnpatchAll(HooksInstance["Maker"].Id);
					HooksInstance["Maker"] = null;

					MakerDropdownRef = null;
					MakerToggleEnable = null;
					MakerToggleAutoCopyToBlank = null;
					SidebarToggleEnable = null;
				};

				MakerAPI.RegisterCustomSubCategories += RegisterCustomSubCategories;
			}
		}

		internal static void DebugMsg(LogLevel LogLevel, string LogMsg)
		{
			if (CfgDebugMode.Value)
				Logger.Log(LogLevel, LogMsg);
			else
				Logger.Log(LogLevel.Debug, LogMsg);
		}
#if DEBUG
		internal static string DisplayObjectInfo(object o)
		{
			return JetPack.Toolbox.DisplayObjectInfo(o);
		}
#endif
	}
}
