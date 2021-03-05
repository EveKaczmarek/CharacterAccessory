using System;
using System.Collections.Generic;
#if DEBUG
using System.Reflection;
using System.Text;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;

using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

using KKAPI.Chara;
using KKAPI.Maker;

namespace CharacterAccessory
{
	[BepInPlugin(GUID, PluginName, Version)]
	[BepInDependency("marco.kkapi", "1.16")]
	[BepInDependency("com.deathweasel.bepinex.materialeditor", "2.5")]
	[BepInDependency("com.joan6694.illusionplugins.moreaccessories")]
	public partial class CharacterAccessory : BaseUnityPlugin
	{
		public const string GUID = "madevil.kk.ca";
		public const string PluginName = "Character Accessory";
		public const string Version = "1.0.0.0";

		internal static new ManualLogSource Logger;
		internal static CharacterAccessory Instance;

		internal static ConfigEntry<bool> CfgMakerMasterSwitch { get; set; }
		internal static ConfigEntry<bool> CfgDebugMode { get; set; }
#if DEBUG
		//internal static ConfigEntry<bool> CfgMEHookLoadData { get; set; }
#endif
		internal static ConfigEntry<bool> CfgMAHookUpdateStudioUI { get; set; }
		internal static Dictionary<string, Harmony> HooksInstance = new Dictionary<string, Harmony>();
		internal const int RefMax = 7;

		private void Awake()
		{
			Logger = base.Logger;
			Instance = this;

			CfgMakerMasterSwitch = Config.Bind("Maker", "Master Switch", true);
			CfgDebugMode = Config.Bind("Debug", "Debug Mode", false);
#if DEBUG
			//CfgMEHookLoadData = Config.Bind("Hook", "ME LoadData", true);
#endif
			CfgMAHookUpdateStudioUI = Config.Bind("Hook", "MoreAccessories UpdateStudioUI", true); // studio only

			if (Application.dataPath.EndsWith("CharaStudio_Data"))
				CharaStudio.Running = true;
		}

		private void Start()
		{
			CharacterApi.RegisterExtraBehaviour<CharacterAccessoryController>(GUID);
			HooksInstance["General"] = Harmony.CreateAndPatchAll(typeof(Hooks));

			MoreAccessoriesSupport.Init();
			HairAccessoryCustomizerSupport.Init();
			MaterialEditorSupport.Init();
			MaterialRouterSupport.Init();
			AccStateSyncSupport.Init();

			if (CharaStudio.Running)
			{
				HooksInstance["Studio"] = Harmony.CreateAndPatchAll(typeof(HooksStudio));

				SceneManager.sceneLoaded += (Scene scene, LoadSceneMode loadSceneMode) =>
				{
					if (!CharaStudio.Loaded && scene.name == "Studio")
					{
						CharaStudio.Loaded = true;
						CharaStudio.RegisterStudioControls();
					}
				};
			}
			else
			{
				MakerAPI.MakerBaseLoaded += (object sender, RegisterCustomControlsEvent ev) =>
				{
					//HooksInstance["Maker"] = Harmony.CreateAndPatchAll(typeof(HooksMaker));
				};

				MakerAPI.MakerExiting += (object sender, EventArgs ev) =>
				{
					/*
					HooksInstance["Maker"].UnpatchAll(HooksInstance["Maker"].Id);
					HooksInstance["Maker"] = null;
					*/
					SidebarToggleEnable = null;
				};

				MakerAPI.RegisterCustomSubCategories += RegisterCustomSubCategories;
			}
		}

		internal static void DebugMsg(LogLevel LogLevel, string LogMsg)
		{
			if (CfgDebugMode.Value)
				Logger.Log(LogLevel, LogMsg);
		}
#if DEBUG
		internal static string DisplayObjectInfo(object o)
		{
			StringBuilder sb = new StringBuilder();

			// Include the type of the object
			Type type = o.GetType();
			sb.Append("Type: " + type.Name);

			// Include information for each Field
			sb.Append("\r\n\r\nFields:");
			FieldInfo[] fi = type.GetFields();
			if (fi.Length > 0)
			{
				foreach (FieldInfo f in fi)
				{
					sb.Append("\r\n " + f.ToString() + " = " + f.GetValue(o));
				}
			}
			else
				sb.Append("\r\n None");

			// Include information for each Property
			sb.Append("\r\n\r\nProperties:");
			PropertyInfo[] pi = type.GetProperties();
			if (pi.Length > 0)
			{
				foreach (PropertyInfo p in pi)
				{
					sb.Append("\r\n " + p.ToString() + " = " + p.GetValue(o, null));
				}
			}
			else
				sb.Append("\r\n None");

			return sb.ToString();
		}
#endif
	}
}
