using System;
using System.Collections.Generic;
using System.Linq;

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

using ExtensibleSaveFormat;

using KKAPI;
using KKAPI.Chara;
using KKAPI.Maker;
using KKAPI.Utilities;

namespace CharacterAccessory
{
	[BepInPlugin(GUID, Name, Version)]
	[BepInDependency("madevil.JetPack", JetPack.Core.Version)]
	[BepInDependency(KoikatuAPI.GUID, KoikatuAPI.VersionConst)]
	[BepInDependency(ExtendedSave.GUID, ExtendedSave.Version)]
#if KK
	[BepInDependency("com.joan6694.illusionplugins.moreaccessories", "1.1.0")]
	[BepInDependency("com.deathweasel.bepinex.materialeditor", "3.1.1")]
#else
	[BepInDependency("com.deathweasel.bepinex.materialeditor", "3.1.2")]
#endif
	[BepInIncompatibility("KK_ClothesLoadOption")]
	public partial class CharacterAccessory : BaseUnityPlugin
	{
		public const string GUID = "madevil.kk.ca";
#if DEBUG
		public const string Name = "Character Accessory (Debug Build)";
#else
		public const string Name = "Character Accessory";
#endif
		public const string Version = "1.9.2.1";

		internal static ManualLogSource _logger;
		internal static CharacterAccessory _instance;
		internal static Dictionary<string, Harmony> _hooksInstance = new Dictionary<string, Harmony>();

		internal static ConfigEntry<bool> _cfgMakerMasterSwitch { get; set; }
		internal static ConfigEntry<bool> _cfgDebugMode { get; set; }
		internal static ConfigEntry<bool> _cfgStudioFallbackReload { get; set; }
		internal static ConfigEntry<bool> _cfgMAHookUpdateStudioUI { get; set; }
		//internal static ConfigEntry<bool> _cfgForceEnable { get; set; }

		internal const int PluginDataVersion = 3;
		internal static List<string> _supportList = new List<string>();
		internal static List<string> _cordNames = new List<string>();

		private void Awake()
		{
			_logger = base.Logger;
			_instance = this;

			_cfgMakerMasterSwitch = Config.Bind("Maker", "Master Switch", true, new ConfigDescription("A quick switch on the sidebar that templary disable the function", null, new ConfigurationManagerAttributes { IsAdvanced = true }));
			_cfgStudioFallbackReload = Config.Bind("Studio", "Fallback Reload Mode", false, new ConfigDescription("Enable this if some plugins are having visual problem", null, new ConfigurationManagerAttributes { IsAdvanced = true, Browsable = JetPack.CharaStudio.Running }));
			_cfgDebugMode = Config.Bind("Debug", "Debug Mode", false, new ConfigDescription("Showing debug messages in LogWarning level", null, new ConfigurationManagerAttributes { IsAdvanced = true }));
			//_cfgForceEnable = Config.Bind("Debug", "Force Enable Mode", false, new ConfigDescription("", null, new ConfigurationManagerAttributes { IsAdvanced = true }));
			_cfgMAHookUpdateStudioUI = Config.Bind("Debug", "MoreAccessories UpdateStudioUI", true, new ConfigDescription("Performance tweak, disable it if having issue on studio chara state panel update", null, new ConfigurationManagerAttributes { IsAdvanced = true, Browsable = JetPack.CharaStudio.Running }));
		}

		private void Start()
		{
#if KK
			if (JetPack.MoreAccessories.BuggyBootleg)
			{
#if DEBUG
				if (!JetPack.MoreAccessories.Installed)
				{
					_logger.LogError($"Backward compatibility in BuggyBootleg MoreAccessories is disabled");
					return;
				}
#else
				_logger.LogError($"Could not load {Name} {Version} because it is incompatible with MoreAccessories experimental build");
				return;
#endif
			}

			if (!JetPack.CoordinateLoadOption.Safe)
			{
				_logger.LogError($"Could not load {Name} {Version} because it is incompatible with outdated CoordinateLoadOption");
				return;
			}
#endif
			_cordNames = Enum.GetNames(typeof(ChaFileDefine.CoordinateType)).ToList();
			CharacterApi.RegisterExtraBehaviour<CharacterAccessoryController>(GUID);
			_hooksInstance["General"] = Harmony.CreateAndPatchAll(typeof(Hooks));

			MoreAccessoriesSupport.Init();

			MoreOutfitsSupport.Init();
			HairAccessoryCustomizerSupport.Init();
			MaterialEditorSupport.Init();
			MaterialRouterSupport.Init();
			AccStateSyncSupport.Init();
			DynamicBoneEditorSupport.Init();
			AAAPKSupport.Init();
			BendUrAccSupport.Init();

			CumOnOverSupport.Init();
			BonerStateSync.Init();

			if (JetPack.CharaStudio.Running)
			{
				JetPack.CharaStudio.OnStudioLoaded += (_sender, _args) => RegisterStudioControls();
			}
			else
			{
				MakerAPI.MakerBaseLoaded += (_sender, _args) =>
				{
					_hooksInstance["Maker"] = Harmony.CreateAndPatchAll(typeof(HooksMaker));
					MoreOutfitsSupport.MakerInit();

					{
						BaseUnityPlugin _instance = JetPack.Toolbox.GetPluginInstance("ClothingStateMenu");
						if (_instance != null)
							_hooksInstance["Maker"].Patch(_instance.GetType().GetMethod("OnGUI", AccessTools.all), prefix: new HarmonyMethod(typeof(HooksMaker), nameof(HooksMaker.DuringLoading_Prefix)));
					}
				};

				MakerAPI.MakerExiting += (_sender, _args) =>
				{
					_hooksInstance["Maker"].UnpatchAll(_hooksInstance["Maker"].Id);
					_hooksInstance["Maker"] = null;

					_makerDropdownReferral = null;
					_makerToggleEnable = null;
					_makerToggleAutoCopyToBlank = null;
					_sidebarToggleEnable = null;
				};

				MakerAPI.RegisterCustomSubCategories += RegisterCustomSubCategories;
			}

			JetPack.Chara.OnChangeCoordinateType += (_sender, _args) =>
			{
				if (!_args.CoordinateChanged) return;

				CharacterAccessoryController _pluginCtrl = GetController(_args.ChaControl);
				if (_pluginCtrl == null) return;

				if (_args.State == "Prefix")
					_pluginCtrl.TaskUnlock();
				else if (_args.State == "Postfix")
					_pluginCtrl.AutoCopyCheck();
			};
#if KKS
			ExtendedSave.CardBeingImported += (_importedExtData, _coordinateMapping) =>
			{
				int ReferralIndex = -1;
				if (_importedExtData.TryGetValue(GUID, out PluginData _pluginData))
				{
					if (_pluginData.data.TryGetValue("ReferralIndex", out object _loadedReferralIndex) && _loadedReferralIndex != null)
					{
						ReferralIndex = (int) _loadedReferralIndex;
						if (ReferralIndex > -1)
						{
							if (ReferralIndex < _coordinateMapping.Count)
								ReferralIndex = (int) _coordinateMapping[ReferralIndex];
							else
								ReferralIndex = -1;
						}
					}

					_importedExtData.Remove(GUID);

					_pluginData.version = PluginDataVersion;
					_pluginData.data["ReferralIndex"] = ReferralIndex;
					_importedExtData[GUID] = _pluginData;
				}
			};
#endif
		}

		internal static void DebugMsg(LogLevel LogLevel, string LogMsg)
		{
			if (_cfgDebugMode.Value)
				_logger.Log(LogLevel, LogMsg);
			else
				_logger.Log(LogLevel.Debug, LogMsg);
		}
#if DEBUG
		internal static string DisplayObjectInfo(object o)
		{
			return JetPack.Toolbox.DisplayObjectInfo(o);
		}
#endif
	}
}
