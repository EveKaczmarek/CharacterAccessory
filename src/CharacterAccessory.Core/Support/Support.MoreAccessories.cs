using System;
using System.Collections.Generic;
using System.Reflection;
#if DEBUG
using System.Linq;
#endif
using MessagePack;

using BepInEx;
#if DEBUG
using BepInEx.Logging;
#endif
using HarmonyLib;

using KKAPI.Maker;
using JetPack;

namespace CharacterAccessory
{
	public partial class CharacterAccessory
	{
		internal static class MoreAccessoriesSupport
		{
			internal static BaseUnityPlugin _instance = null;
			internal static bool _installed = false;
			internal static bool BuggyBootleg = false;

			internal static void Init()
			{
				_installed = MoreAccessories.Installed;
				if (!_installed) return;

				_instance = MoreAccessories.Instance;
				Assembly _assembly = _instance.GetType().Assembly;
				BuggyBootleg = MoreAccessories.BuggyBootleg;

				if (BuggyBootleg) return;

				_hooksInstance["General"].Patch(_instance.GetType().Assembly.GetType("MoreAccessoriesKOI.ChaControl_UpdateVisible_Patches").GetMethod("Postfix", AccessTools.all, null, new[] { typeof(ChaControl) }, null), prefix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.ChaControl_UpdateVisible_Patches_Prefix)));

				if (CharaStudio.Running)
					_hooksInstance["General"].Patch(_instance.GetType().GetMethod("UpdateStudioUI", AccessTools.all, null, new Type[0], null), prefix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.MoreAccessories_UpdateStudioUI_Prefix)));
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
						DebugMsg(LogLevel.Warning, $"[ChaControl_UpdateVisible_Patches_Prefix][{__0.GetFullName()}] await loading");
#endif
						return false;
					}
					return true;
				}

				internal static bool MoreAccessories_UpdateStudioUI_Prefix(object __instance) // studio only
				{
					if (!_cfgMAHookUpdateStudioUI.Value) return true;

					bool flag = true;

					if (CharaStudio.CurOCIChar != null)
					{
						if (GetController(CharaStudio.CurOCIChar).DuringLoading)
							flag = false;
					}

					if (!flag)
					{
#if DEBUG
						DebugMsg(LogLevel.Warning, $"[MoreAccessories_UpdateStudioUI_Prefix][{CharaStudio.CurOCIChar.charInfo.GetFullName()}] await loading");
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
				return Accessory.GetPartsInfo(_chaCtrl, _coordinateIndex, _slotIndex);
			}

			internal static void SetPartsInfo(ChaControl _chaCtrl, int _coordinateIndex, int _slotIndex, ChaFileAccessory.PartsInfo _part)
			{
				byte[] _byte = MessagePackSerializer.Serialize(_part);
				Accessory.SetPartsInfo(_chaCtrl, _coordinateIndex, _slotIndex, MessagePackSerializer.Deserialize<ChaFileAccessory.PartsInfo>(_byte));
			}

			internal static List<ChaFileAccessory.PartsInfo> ListPartsInfo(ChaControl _chaCtrl, int _coordinateIndex)
			{
				return Accessory.ListPartsInfo(_chaCtrl, _coordinateIndex);
			}

			internal static void CheckAndPadPartInfo(ChaControl _chaCtrl, int _coordinateIndex, int _slotIndex)
			{
				MoreAccessories.CheckAndPadPartInfo(_chaCtrl, _coordinateIndex, _slotIndex);
			}

			internal static ChaAccessoryComponent GetChaAccessoryComponent(ChaControl _chaCtrl, int _slotIndex)
			{
				return Accessory.GetChaAccessoryComponent(_chaCtrl, _slotIndex);
			}

			internal static bool IsHairAccessory(ChaControl _chaCtrl, int _slotIndex)
			{
				return Accessory.IsHairAccessory(_chaCtrl, _slotIndex);
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
