using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using UnityEngine.UI;
using Studio;
using ChaCustom;
using TMPro;

using BepInEx;
using HarmonyLib;

using KKAPI.Chara;

namespace CharacterAccessory
{
	public partial class CharacterAccessory
	{
		internal static class MoreOutfitsSupport
		{
			private static BaseUnityPlugin _instance = null;
			private static bool _installed = false;

			internal static TMP_Dropdown _makerDropdownRef;
			internal static Dropdown _studioDropdownRef;

			internal static void Init()
			{
				BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue("com.deathweasel.bepinex.moreoutfits", out PluginInfo _pluginInfo);
				_instance = _pluginInfo?.Instance;
				if (_instance != null)
					_installed = true;
			}

			internal static CharaCustomFunctionController GetController(ChaControl _chaCtrl) => Traverse.Create(_instance).Method("GetController", new object[] { _chaCtrl }).GetValue<CharaCustomFunctionController>();

			internal static Dictionary<int, string> CoordinateNames(CharaCustomFunctionController _pluginCtrl)
			{
				return Traverse.Create(_pluginCtrl).Field("CoordinateNames").GetValue<Dictionary<int, string>>();
			}

			internal static string GetCoodinateName(ChaControl _chaCtrl, int _coordinateIndex)
			{
				return JetPack.MoreOutfits.GetCoodinateName(_chaCtrl, _coordinateIndex);
			}

			internal static string GetCoodinateName(CharaCustomFunctionController _pluginCtrl, int _coordinateIndex)
			{
				return Traverse.Create(_pluginCtrl).Method("GetCoodinateName", new object[] { _coordinateIndex }).GetValue<string>();
			}

			internal static void MakerInit()
			{
				if (!_installed) return;

				_makerDropdownRef = null;
				_hooksInstance["Maker"].Patch(_instance.GetType().Assembly.GetType("KK_Plugins.MoreOutfits.MakerUI").GetMethod("UpdateMakerUI", AccessTools.all), postfix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.UpdateMakerUI_Postfix)));
			}

			internal static void StudioInit()
			{
				if (!_installed) return;

				_hooksInstance["Studio"].Patch(_instance.GetType().Assembly.GetType("KK_Plugins.MoreOutfits.StudioUI").GetMethod("InitializeStudioUI", AccessTools.all), postfix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.InitializeStudioUI_Postfix)));
			}

			internal static class Hooks
			{
				internal static void UpdateMakerUI_Postfix()
				{
					BuildMakerDropdownRef();
				}

				internal static void InitializeStudioUI_Postfix(MPCharCtrl __0)
				{
					BuildStudioDropdownRef();
				}
			}

			internal static void BuildMakerDropdownRef()
			{
				ChaControl _chaCtrl = CustomBase.Instance.chaCtrl;
				int _referral = (int) _chaCtrl?.gameObject?.GetComponent<CharacterAccessoryController>()?.GetReferralIndex();

				if (!_installed)
				{
					_makerDropdownReferral.SetValue(_referral);
					return;
				}

				if (_makerDropdownRef == null)
					_makerDropdownRef = GameObject.Find("tglCharaAcc")?.GetComponentInChildren<TMP_Dropdown>(true);
				if (_makerDropdownRef == null)
				{
					_logger.LogError($"[BuildDropdownRef] failed to get dropdown component");
					return;
				}

				List<string> _coordinateList = _cordNames.ToList();
				for (int i = _coordinateList.Count; i < _chaCtrl.chaFile.coordinate.Length; i++)
					_coordinateList.Add(GetCoodinateName(_chaCtrl, i));
				_coordinateList.Add("CharaAcc");

				_makerDropdownRef.ClearOptions();
				_makerDropdownRef.options.AddRange(_coordinateList.Select(x => new TMP_Dropdown.OptionData(x)));
				_makerDropdownRef.value = _referral;
				_makerDropdownRef.RefreshShownValue();
			}

			internal static void BuildStudioDropdownRef()
			{
				if (!_installed) return;

				if (_studioDropdownRef == null)
					_studioDropdownRef = GameObject.Find("StudioScene/Canvas Main Menu/02_Manipulate/00_Chara/01_State/Viewport/Content/CharaAcc_Items_SAPI/CustomDropdown Referral/Dropdown")?.GetComponent<Dropdown>();
				if (_studioDropdownRef == null)
				{
					_logger.LogError($"[BuildDropdownRef] failed to get dropdown component");
					return;
				}

				List<Dropdown.OptionData> _options = _studioDropdownRef.options;

				ChaControl _chaCtrl = JetPack.CharaStudio.CurOCIChar?.charInfo;

				if (_chaCtrl == null)
				{
					_options.RemoveRange(_cordNames.Count, _options.Count - _cordNames.Count);
					_options.Add(new Dropdown.OptionData("CharaAcc"));
					return;
				}

				_options.RemoveRange(_cordNames.Count, _options.Count - _cordNames.Count);
				for (int i = _cordNames.Count; i < _chaCtrl.chaFile.coordinate.Length; i++)
				{
					if (i < _cordNames.Count)
						_options.Add(new Dropdown.OptionData(_cordNames[i]));
					else
						_options.Add(new Dropdown.OptionData(GetCoodinateName(_chaCtrl, i)));
				}
				_options.Add(new Dropdown.OptionData("CharaAcc"));
			}
		}
	}
}
