using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using ParadoxNotion.Serialization;

using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

using KKAPI.Chara;
using KKAPI.Maker;

namespace CharacterAccessory
{
	public partial class CharacterAccessory
	{
		internal static class AccStateSyncSupport
		{
			private static BaseUnityPlugin _instance = null;
			private static bool _installed = false;
			private static bool _legacy = false;
			private static Dictionary<string, Type> _types = new Dictionary<string, Type>();
			private static Dictionary<string, MethodInfo> _methods = new Dictionary<string, MethodInfo>();

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
					SupportList.Add("AccStateSync");

					Assembly _assembly = _instance.GetType().Assembly;
					_types["AccStateSyncController"] = _assembly.GetType("AccStateSync.AccStateSync+AccStateSyncController");
					_types["AccTriggerInfo"] = _assembly.GetType("AccStateSync.AccStateSync+AccTriggerInfo");

					HooksInstance["General"].Patch(_types["AccStateSyncController"].GetMethod("SetAccessoryStateAll", AccessTools.all, null, new[] { typeof(bool) }, null), prefix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.DuringLoading_Prefix)));
					HooksInstance["General"].Patch(_types["AccStateSyncController"].GetMethod("SyncAllAccToggle", AccessTools.all), prefix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.DuringLoading_Prefix)));
					HooksInstance["General"].Patch(_types["AccStateSyncController"].GetMethod("AccSlotChangedHandler", AccessTools.all, null, new[] { typeof(int) }, null), prefix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.DuringLoading_Prefix)));
					HooksInstance["General"].Patch(_types["AccStateSyncController"].GetMethod("ToggleByClothesState", AccessTools.all, null, new[] { typeof(int), typeof(int) }, null), prefix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.DuringLoading_Prefix)));
					HooksInstance["General"].Patch(_types["AccStateSyncController"].GetMethod("ToggleByShoesType", AccessTools.all, null, new[] { typeof(int), typeof(int) }, null), prefix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.DuringLoading_Prefix)));
					HooksInstance["General"].Patch(_types["AccStateSyncController"].GetMethod("SyncOutfitVirtualGroupInfo", AccessTools.all, null, new[] { typeof(int) }, null), prefix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.DuringLoading_Prefix)));

					_methods["CloneSlotTriggerInfo"] = _types["AccStateSyncController"].GetMethod("CloneSlotTriggerInfo", AccessTools.all, null, new[] { typeof(int), typeof(int), typeof(int), typeof(int) }, null);
					Logger.LogWarning($"[AccStateSyncSupport][CloneSlotTriggerInfo: {!(_methods["CloneSlotTriggerInfo"] == null)}]");
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

				internal object GetExtDataLink(int _coordinateIndex)
				{
					object CharaTriggerInfo = Traverse.Create(_pluginCtrl).Field("CharaTriggerInfo").GetValue();
					if (CharaTriggerInfo == null)
						return null;
					return CharaTriggerInfo.RefTryGetValue(_coordinateIndex);
				}

				internal void Reset()
				{
					if (!_installed) return;
					_charaAccData.Clear();
				}

				internal Dictionary<int, string> Save()
				{
					if (!_installed) return null;
					Dictionary<int, string> _json = new Dictionary<int, string>();
					foreach (KeyValuePair<int, object> x in _charaAccData)
						_json[x.Key] = JSONSerializer.Serialize(_types["AccTriggerInfo"], x.Value);
					return _json;
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
					List<int> _slots = _controller.PartsInfo?.Keys?.ToList();

					object _extdataLink = GetExtDataLink(_coordinateIndex);
					if (_extdataLink == null) return;

					object Parts = Traverse.Create(_extdataLink).Property("Parts").GetValue();
					List<int> _keys = Traverse.Create(Parts).Property("Keys").GetValue<ICollection<int>>().ToList();
					//List<int> _keys = new List<int>((Parts as IDictionary).Keys as ICollection<int>);
#if DEBUG
					DebugMsg(LogLevel.Warning, $"[AccStateSync][Backup][{_chaCtrl.GetFullname()}][keys: {string.Join(",", _keys.Select(x => x.ToString()).ToArray())}]");
#endif
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
					object _extdataLink = GetExtDataLink(_coordinateIndex);
					if (_extdataLink == null) return;

					Traverse _traverse = Traverse.Create(_extdataLink).Property("Parts");
					object Parts = _traverse.GetValue();
					foreach (KeyValuePair<int, object> x in _charaAccData)
					{
						if (Parts.RefTryGetValue(x.Key) != null)
							_traverse.Method("Remove", new object[] { x.Key }).GetValue();
						_traverse.Method("Add", new object[] { x.Key, x.Value.JsonClone() }).GetValue();
					}
				}

				internal void CopyPartsInfo(AccessoryCopyEventArgs ev)
				{
					if (!_installed) return;

					if (_methods["CloneSlotTriggerInfo"] != null)
					{
						foreach (int _slotIndex in ev.CopiedSlotIndexes)
							Traverse.Create(_pluginCtrl).Method("CloneSlotTriggerInfo", new object[] { _slotIndex, _slotIndex, ev.CopySource, ev.CopyDestination }).GetValue();
						return;
					}

					object CharaTriggerInfo = Traverse.Create(_pluginCtrl).Field("CharaTriggerInfo").GetValue();
					object _src = CharaTriggerInfo.RefElementAt((int) ev.CopySource);
					object _dst = CharaTriggerInfo.RefElementAt((int) ev.CopyDestination);
					if (_src == null || _dst == null) return;

					object _srcOutfitTriggerInfo = Traverse.Create(_src).Property("Parts").GetValue();
					object _dstOutfitTriggerInfo = Traverse.Create(_dst).Property("Parts").GetValue();
					Traverse _traverseDst = Traverse.Create(_dst).Property("Parts");
					foreach (int _slotIndex in ev.CopiedSlotIndexes)
					{
						if (_dstOutfitTriggerInfo.RefTryGetValue(_slotIndex) != null)
							_traverseDst.Method("Remove", new object[] { _slotIndex }).GetValue();

						object _copy = _srcOutfitTriggerInfo.RefTryGetValue(_slotIndex);
						if (_copy == null) continue;

						_traverseDst.Method("Add", new object[] { _slotIndex, _copy.JsonClone() }).GetValue();
					}
				}

				internal void TransferPartsInfo(AccessoryTransferEventArgs ev)
				{
					if (!_installed) return;

					int _coordinateIndex = _chaCtrl.fileStatus.coordinateType;
					if (_methods["CloneSlotTriggerInfo"] != null)
					{
						Traverse.Create(_pluginCtrl).Method("CloneSlotTriggerInfo", new object[] { ev.SourceSlotIndex, ev.DestinationSlotIndex, _coordinateIndex, _coordinateIndex }).GetValue();
						return;
					}

					RemovePartsInfo(ev.DestinationSlotIndex);

					object _extdataLink = GetExtDataLink(_coordinateIndex);
					if (_extdataLink == null) return;

					object Parts = Traverse.Create(_extdataLink).Property("Parts").GetValue();
					object AccTriggerInfo = Parts.RefTryGetValue(ev.SourceSlotIndex);
					if (AccTriggerInfo == null) return;

					object _copy = AccTriggerInfo.JsonClone();
					Traverse.Create(_copy).Property("Slot").SetValue(ev.DestinationSlotIndex);
					Traverse.Create(Parts).Method("Add", new object[] { ev.DestinationSlotIndex, _copy }).GetValue();
				}

				internal void RemovePartsInfo(int _slotIndex) => RemovePartsInfo(_chaCtrl.fileStatus.coordinateType, _slotIndex);
				internal void RemovePartsInfo(int _coordinateIndex, int _slotIndex)
				{
					if (!_installed) return;

					object _extdataLink = GetExtDataLink(_coordinateIndex);
					if (_extdataLink == null) return;

					Traverse.Create(_extdataLink).Property("Parts").Method("Remove", new object[] { _slotIndex }).GetValue();
				}

				internal void InitCurOutfitTriggerInfo(string _caller)
				{
					if (!_installed) return;
					Traverse.Create(_pluginCtrl).Method("InitCurOutfitTriggerInfo", new object[] { _caller }).GetValue();
				}

				internal void SetAccessoryStateAll(bool _show = true)
				{
					if (!_installed) return;
					Traverse.Create(_pluginCtrl).Method("SetAccessoryStateAll", new object[] { _show }).GetValue();
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
