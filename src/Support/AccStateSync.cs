using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using ParadoxNotion.Serialization;

using BepInEx;
using HarmonyLib;

using KKAPI.Chara;
using KKAPI.Maker;

namespace CharacterAccessory
{
	public partial class CharacterAccessory
	{
		internal static class AccStateSyncSupport
		{
			internal static BaseUnityPlugin _instance = null;
			internal static bool _installed = false;
			internal static bool _legacy = false;
			internal static Dictionary<string, Type> _types = new Dictionary<string, Type>();

			internal static void Init()
			{
				BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue("madevil.kk.ass", out PluginInfo _pluginInfo);
				_instance = _pluginInfo?.Instance;

				if (_instance != null)
				{
					_legacy = _pluginInfo.Metadata.Version.CompareTo(new Version("3.1.2")) < 0;
					if (_legacy)
					{
						Logger.LogError($"AccStateSync version {_pluginInfo.Metadata.Version} found, minimun version 3.1.2 is reqired");
						return;
					}

					_installed = true;

					_types["AccStateSyncController"] = _instance.GetType().Assembly.GetType("AccStateSync.AccStateSync+AccStateSyncController");
					_types["AccTriggerInfo"] = _instance.GetType().Assembly.GetType("AccStateSync.AccStateSync+AccTriggerInfo");

					HooksInstance["General"].Patch(_types["AccStateSyncController"].GetMethod("SetAccessoryStateAll", AccessTools.all, null, new[] { typeof(bool) }, null), prefix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.DuringLoading_Prefix)));
					HooksInstance["General"].Patch(_types["AccStateSyncController"].GetMethod("SyncAllAccToggle", AccessTools.all, null, new Type[0], null), prefix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.DuringLoading_Prefix)));
					HooksInstance["General"].Patch(_types["AccStateSyncController"].GetMethod("AccSlotChangedHandler", AccessTools.all, null, new[] { typeof(int) }, null), prefix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.DuringLoading_Prefix)));
					HooksInstance["General"].Patch(_types["AccStateSyncController"].GetMethod("ToggleByClothesState", AccessTools.all, null, new[] { typeof(int), typeof(int) }, null), prefix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.DuringLoading_Prefix)));
					HooksInstance["General"].Patch(_types["AccStateSyncController"].GetMethod("ToggleByShoesType", AccessTools.all, null, new[] { typeof(int), typeof(int) }, null), prefix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.DuringLoading_Prefix)));
#if DEBUG
					Logger.LogWarning($"[SetAccessoryStateAll][{_types["AccStateSyncController"].GetMethod("SetAccessoryStateAll", AccessTools.all, null, new[] { typeof(bool) }, null) == null}]");
					Logger.LogWarning($"[SyncAllAccToggle][{_types["AccStateSyncController"].GetMethod("SyncAllAccToggle", AccessTools.all, null, new Type[0], null) == null}]");
					Logger.LogWarning($"[AccSlotChangedHandler][{_types["AccStateSyncController"].GetMethod("AccSlotChangedHandler", AccessTools.all, null, new[] { typeof(int) }, null) == null}]");
					Logger.LogWarning($"[ToggleByClothesState][{_types["AccStateSyncController"].GetMethod("ToggleByClothesState", AccessTools.all, null, new[] { typeof(int), typeof(int) }, null) == null}]");
					Logger.LogWarning($"[ToggleByShoesType][{_types["AccStateSyncController"].GetMethod("ToggleByShoesType", AccessTools.all, null, new[] { typeof(int), typeof(int) }, null) == null}]");
#endif
				}
			}

			internal static CharaCustomFunctionController GetController(ChaControl _chaCtrl)
			{
				if (!_installed) return null;
				return Traverse.Create(_instance).Method("GetController", new object[] { _chaCtrl }).GetValue<CharaCustomFunctionController>();
			}

			internal class UrineBag
			{
				private readonly ChaControl _chaCtrl;
				private readonly CharaCustomFunctionController _pluginCtrl;
				private Dictionary<int, object> _charaAccData = new Dictionary<int, object>();

				internal UrineBag(ChaControl ChaControl)
				{
					if (!_installed) return;
					_chaCtrl = ChaControl;
					_pluginCtrl = GetController(_chaCtrl);
				}

				internal void Reset()
				{
					if (!_installed) return;
					_charaAccData.Clear();
				}

				internal Dictionary<int, string> Save()
				{
					if (!_installed) return null;
					Dictionary<int, string> ContainerJson = new Dictionary<int, string>();
					foreach (KeyValuePair<int, object> x in _charaAccData)
						ContainerJson[x.Key] = JSONSerializer.Serialize(_types["AccTriggerInfo"], x.Value);
					return ContainerJson;
				}

				internal void Load(Dictionary<int, string> _json)
				{
					if (!_installed) return;
					_charaAccData.Clear();
					if (_json == null) return;

					foreach (KeyValuePair<int, string> x in _json)
						_charaAccData[x.Key] = JSONSerializer.Deserialize(_types["AccTriggerInfo"], x.Value);
				}

				internal void Backup()
				{
					if (!_installed) return;
					_charaAccData.Clear();

					CharacterAccessoryController _controller = CharacterAccessory.GetController(_chaCtrl);
					int _coordinateIndex = _chaCtrl.fileStatus.coordinateType;
					List<int> _slots = _controller.PartsInfo.Keys.ToList();
					Logger.LogWarning($"[AccStateSync][Backup][slots: {string.Join(",", _slots.Select(x => x.ToString()).ToArray())}]");

					object OutfitTriggerInfo = Traverse.Create(_pluginCtrl).Field("CharaTriggerInfo").GetValue().RefElementAt(_coordinateIndex);
					if (OutfitTriggerInfo == null) return;

					object Parts = Traverse.Create(OutfitTriggerInfo).Property("Parts").GetValue();
					List<int> _keys = Traverse.Create(Parts).Property("Keys").GetValue<ICollection<int>>().ToList();
					Logger.LogWarning($"[AccStateSync][Backup][keys: {string.Join(",", _keys.Select(x => x.ToString()).ToArray())}]");
					foreach (int _slotIndex in _keys)
					{
						object x = Parts.RefTryGetValue(_slotIndex);
						if (x == null) continue;
						/*
						int Kind = Traverse.Create(x).Property("Kind").GetValue<int>();
						if (Kind >= 9)
						*/
							_charaAccData[_slotIndex] = x.JsonClone();
					}
				}

				internal void Restore()
				{
					if (!_installed) return;
					int _coordinateIndex = _chaCtrl.fileStatus.coordinateType;

					object CharaTriggerInfo = Traverse.Create(_pluginCtrl).Field("CharaTriggerInfo").GetValue();
					object OutfitTriggerInfo = CharaTriggerInfo.RefElementAt(_coordinateIndex);
					if (OutfitTriggerInfo == null) return;

					foreach (KeyValuePair<int, object> x in _charaAccData)
					{
						Traverse _traverse = Traverse.Create(OutfitTriggerInfo).Property("Parts");
						if (_traverse.GetValue().RefTryGetValue(x.Key) != null)
							_traverse.Method("Remove", new object[] { x.Key }).GetValue();
						_traverse.Method("Add", new object[] { x.Key, x.Value.JsonClone() }).GetValue();
					}
				}

				internal void CopyPartsInfo(AccessoryCopyEventArgs ev)
				{
					if (!_installed) return;

					object CharaTriggerInfo = Traverse.Create(_pluginCtrl).Field("CharaTriggerInfo").GetValue();
					object _src = CharaTriggerInfo.RefElementAt((int) ev.CopySource);
					object _dst = CharaTriggerInfo.RefElementAt((int) ev.CopyDestination);
					if (_src == null || _dst == null) return;

					object srcOutfitTriggerInfo = Traverse.Create(_src).Property("Parts").GetValue();
					Traverse _traverseDst = Traverse.Create(_dst).Property("Parts");
					foreach (int _slotIndex in ev.CopiedSlotIndexes)
					{
						if (_traverseDst.GetValue().RefTryGetValue(_slotIndex) != null)
							_traverseDst.Method("Remove", new object[] { _slotIndex }).GetValue();

						object _copy = srcOutfitTriggerInfo.RefTryGetValue(_slotIndex);
						if (_copy == null) continue;

						_traverseDst.Method("Add", new object[] { _slotIndex, _copy.JsonClone() }).GetValue();
					}
				}

				internal void TransferPartsInfo(AccessoryTransferEventArgs ev)
				{
					if (!_installed) return;
					RemovePartsInfo(ev.DestinationSlotIndex);

					int _coordinateIndex = _chaCtrl.fileStatus.coordinateType;
					object OutfitTriggerInfo = Traverse.Create(_pluginCtrl).Field("CharaTriggerInfo").GetValue().RefElementAt(_coordinateIndex);
					if (OutfitTriggerInfo == null) return;

					object Parts = Traverse.Create(OutfitTriggerInfo).Property("Parts").GetValue();
					object x = Parts.RefTryGetValue(ev.SourceSlotIndex);
					if (x == null) return;
					object _copy = x.JsonClone();
					Traverse.Create(_copy).Property("Slot").SetValue(ev.DestinationSlotIndex);
					Traverse.Create(Parts).Method("Add", new object[] { ev.DestinationSlotIndex, _copy }).GetValue();
				}

				internal void RemovePartsInfo(int _slotIndex)
				{
					if (!_installed) return;
					Traverse.Create(_pluginCtrl).Field("CurOutfitTriggerInfo").Property("Parts").Method("Remove", new object[] { _slotIndex }).GetValue();
				}

				internal void InitCurOutfitTriggerInfo(string _caller)
				{
					if (!_installed) return;
					Traverse.Create(_pluginCtrl).Method("InitCurOutfitTriggerInfo", new object[] { _caller }).GetValue();
				}

				internal void SetAccessoryStateAll()
				{
					if (!_installed) return;
					Traverse.Create(_pluginCtrl).Method("SetAccessoryStateAll", new object[] { true }).GetValue();
				}

				internal void SyncAllAccToggle()
				{
					if (!_installed) return;
					Traverse.Create(_pluginCtrl).Method("SyncAllAccToggle").GetValue();
				}
			}
		}
	}
}
