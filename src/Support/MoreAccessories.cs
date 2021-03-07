using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using MessagePack;

using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

using KKAPI.Maker;

namespace CharacterAccessory
{
	public partial class CharacterAccessory
	{
		internal static class MoreAccessoriesSupport
		{
			internal static BaseUnityPlugin _instance = null;
			internal static bool _legacy = false;
			internal static object _accessoriesByChar;

			internal static void Init()
			{
				BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue("com.joan6694.illusionplugins.moreaccessories", out PluginInfo _pluginInfo);
				_instance = _pluginInfo.Instance;
				Assembly _assembly = _instance.GetType().Assembly;
				_legacy = _pluginInfo.Metadata.Version.CompareTo(new Version("1.1.0")) < 0;
#if DEBUG
				if (_legacy)
					Logger.LogWarning($"MoreAccessories version {_pluginInfo.Metadata.Version} found, running in legacy mode");
#endif
				_accessoriesByChar = Traverse.Create(_instance).Field("_accessoriesByChar").GetValue();

				HooksInstance["General"].Patch(_instance.GetType().Assembly.GetType("MoreAccessoriesKOI.ChaControl_UpdateVisible_Patches").GetMethod("Postfix", AccessTools.all, null, new[] { typeof(ChaControl) }, null), prefix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.ChaControl_UpdateVisible_Patches_Prefix)));

				if (CharaStudio.Running)
					HooksInstance["General"].Patch(_instance.GetType().GetMethod("UpdateStudioUI", AccessTools.all, null, new Type[0], null), prefix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.MoreAccessories_UpdateStudioUI_Prefix)));
			}

			internal static void UpdateStudioUI(ChaControl _chaCtrl)
			{
				if (CharaStudio.CurOCIChar == null) return;
				if (CharaStudio.CurOCIChar.charInfo != _chaCtrl) return;

				AccessTools.Method(_instance.GetType(), "UpdateUI").Invoke(_instance, null);
			}

			internal static class Hooks
			{
				internal static bool ChaControl_UpdateVisible_Patches_Prefix(ChaControl __0)
				{
					CharacterAccessoryController _pluginCtrl = GetController(__0);
					if (_pluginCtrl == null) return true;

					if (_pluginCtrl.DuringLoading)
					{
#if DEBUG
						DebugMsg(LogLevel.Warning, $"[ChaControl_UpdateVisible_Patches_Prefix][{__0.GetFullname()}] await loading");
#endif
						return false;
					}
					return true;
				}

				internal static bool MoreAccessories_UpdateStudioUI_Prefix(object __instance) // studio only
				{
					if (!CfgMAHookUpdateStudioUI.Value) return true;

					bool flag = true;

					if (CharaStudio.CurOCIChar != null)
					{
						if (GetController(CharaStudio.CurOCIChar).DuringLoading)
							flag = false;
					}

					if (!flag)
					{
#if DEBUG
						DebugMsg(LogLevel.Warning, $"[MoreAccessories_UpdateStudioUI_Prefix][{CharaStudio.CurOCIChar.charInfo.GetFullname()}] await loading");
#endif
						return false;
					}
					return flag;
				}
			}

			internal static int GetPartsCount(ChaControl chaCtrl, int CoordinateIndex) => (int) ListPartsInfo(chaCtrl, CoordinateIndex)?.Count + 20;

			internal static Dictionary<int, ChaFileAccessory.PartsInfo> ListUsedPartsInfo(ChaControl _chaCtrl, int _coordinateIndex)
			{
				Dictionary<int, ChaFileAccessory.PartsInfo> _parts = new Dictionary<int, ChaFileAccessory.PartsInfo>();
				int i = 0;
				foreach (ChaFileAccessory.PartsInfo _part in ListPartsInfo(_chaCtrl, _coordinateIndex))
				{
					if (_part.type > 120)
						_parts[i] = _part;
					i++;
				}
				return _parts;
			}

			internal static ChaFileAccessory.PartsInfo GetPartsInfo(ChaControl _chaCtrl, int _coordinateIndex, int _slotIndex)
			{
				if (_slotIndex < 20)
					return _chaCtrl.chaFile.coordinate[_coordinateIndex].accessory.parts[_slotIndex];

				return GetMorePartsInfo(_chaCtrl, _coordinateIndex, _slotIndex - 20);
			}

			internal static void SetPartsInfo(ChaControl _chaCtrl, int _coordinateIndex, int _slotIndex, ChaFileAccessory.PartsInfo _part)
			{
				byte[] _byte = MessagePackSerializer.Serialize(_part);
				if (_slotIndex < 20)
					_chaCtrl.chaFile.coordinate[_coordinateIndex].accessory.parts[_slotIndex] = MessagePackSerializer.Deserialize<ChaFileAccessory.PartsInfo>(_byte);
				else
				{
					CheckAndPadPartInfo(_chaCtrl, _coordinateIndex, _slotIndex - 20);
					ListMorePartsInfo(_chaCtrl, _coordinateIndex)[_slotIndex - 20] = MessagePackSerializer.Deserialize<ChaFileAccessory.PartsInfo>(_byte);
				}
			}

			internal static ChaFileAccessory.PartsInfo GetMorePartsInfo(ChaControl _chaCtrl, int _coordinateIndex, int _slotIndex)
			{
				return ListMorePartsInfo(_chaCtrl, _coordinateIndex).ElementAtOrDefault(_slotIndex);
			}

			internal static List<ChaFileAccessory.PartsInfo> ListPartsInfo(ChaControl _chaCtrl, int _coordinateIndex)
			{
				List<ChaFileAccessory.PartsInfo> _parts = _chaCtrl.chaFile.coordinate[_coordinateIndex].accessory.parts.ToList();
				_parts.AddRange(ListMorePartsInfo(_chaCtrl, _coordinateIndex) ?? new List<ChaFileAccessory.PartsInfo>());
				return _parts;
			}

			internal static List<ChaFileAccessory.PartsInfo> ListMorePartsInfo(ChaControl _chaCtrl, int _coordinateIndex)
			{
				List<ChaFileAccessory.PartsInfo> _parts = null; // ?? new List<ChaFileAccessory.PartsInfo>();

				object _charAdditionalData = _accessoriesByChar.RefTryGetValue(_chaCtrl.chaFile);
				if (_charAdditionalData == null) return _parts;
				object _rawAccessoriesInfos = Traverse.Create(_charAdditionalData).Field("rawAccessoriesInfos").GetValue();
				if (_rawAccessoriesInfos == null) return _parts;
				if (_legacy)
					(_rawAccessoriesInfos as Dictionary<ChaFileDefine.CoordinateType, List<ChaFileAccessory.PartsInfo>>).TryGetValue((ChaFileDefine.CoordinateType) _coordinateIndex, out _parts);
				else
					(_rawAccessoriesInfos as Dictionary<int, List<ChaFileAccessory.PartsInfo>>).TryGetValue(_coordinateIndex, out _parts);
				return _parts;
			}

			internal static void CheckAndPadPartInfo(ChaControl _chaCtrl, int _coordinateIndex, int _slotIndex)
			{
				List<ChaFileAccessory.PartsInfo> _parts = ListMorePartsInfo(_chaCtrl, _coordinateIndex);
				if (_parts == null) return;

				for (int i = _parts.Count; i < _slotIndex + 1; i++)
				{
					if (_parts.ElementAtOrDefault(i) == null)
						_parts.Add(new ChaFileAccessory.PartsInfo());
				}
			}

			internal static ChaAccessoryComponent GetChaAccessoryComponent(ChaControl _chaCtrl, int _slotIndex)
			{
				if (_slotIndex < 0) return null;
				return Traverse.Create(_instance).Method("GetChaAccessoryComponent", new object[] { _chaCtrl, _slotIndex }).GetValue<ChaAccessoryComponent>();
			}

			internal static bool IsHairAccessory(ChaControl _chaCtrl, int _slotIndex)
			{
				ChaAccessoryComponent accessory = GetChaAccessoryComponent(_chaCtrl, _slotIndex);
				if (accessory == null) return false;
				return accessory.gameObject.GetComponent<ChaCustomHairComponent>() != null;
			}

			internal static void CopyPartsInfo(ChaControl _chaCtrl, AccessoryCopyEventArgs ev)
			{
				foreach (int SlotIndex in ev.CopiedSlotIndexes)
				{
					ChaFileAccessory.PartsInfo _part = GetPartsInfo(_chaCtrl, (int) ev.CopySource, SlotIndex);
					SetPartsInfo(_chaCtrl, (int) ev.CopyDestination, SlotIndex, _part);
				}
			}

			internal static void TransferPartsInfo(ChaControl _chaCtrl, AccessoryTransferEventArgs ev)
			{
				int _coordinateIndex = _chaCtrl.fileStatus.coordinateType;

				ChaFileAccessory.PartsInfo _part = GetPartsInfo(_chaCtrl, _coordinateIndex, ev.SourceSlotIndex);
				SetPartsInfo(_chaCtrl, _coordinateIndex, ev.DestinationSlotIndex, _part);
			}

			internal static void RemovePartsInfo(ChaControl _chaCtrl, int _coordinateIndex, int _slotIndex)
			{
				SetPartsInfo(_chaCtrl, _coordinateIndex, _slotIndex, new ChaFileAccessory.PartsInfo());
			}
		}
	}
}
