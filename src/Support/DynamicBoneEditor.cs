using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using UnityEngine;
using UniRx;
using ParadoxNotion.Serialization;
using MessagePack;

using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

using KKAPI.Chara;
using KKAPI.Maker;

namespace CharacterAccessory
{
	public partial class CharacterAccessory
	{
		internal static class DynamicBoneEditorSupport
		{
			private static BaseUnityPlugin _instance = null;
			private static bool _installed = false;
			private static Dictionary<string, Type> _types = new Dictionary<string, Type>();

			internal static void Init()
			{
				BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue("com.deathweasel.bepinex.dynamicboneeditor", out PluginInfo _pluginInfo);
				_instance = _pluginInfo?.Instance;

				if (_instance != null)
				{
					_installed = true;
					SupportList.Add("DynamicBoneEditor");

					Assembly _assembly = _instance.GetType().Assembly;
					_types["CharaController"] = _assembly.GetType("KK_Plugins.DynamicBoneEditor.CharaController");
					_types["DynamicBoneData"] = _assembly.GetType("KK_Plugins.DynamicBoneEditor.DynamicBoneData");
				}
			}

			internal static CharaCustomFunctionController GetController(ChaControl _chaCtrl) => Traverse.Create(_instance).Method("GetCharaController", new object[] { _chaCtrl }).GetValue<CharaCustomFunctionController>();

			internal class UrineBag
			{
				private readonly ChaControl _chaCtrl;
				private readonly CharaCustomFunctionController _pluginCtrl;
				private List<object> _charaAccData = new List<object>();

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

				internal List<string> Save()
				{
					if (!_installed) return null;
					List<string> _json = new List<string>();
					foreach (object x in _charaAccData)
						_json.Add(JSONSerializer.Serialize(_types["DynamicBoneData"], x));
#if DEBUG
					DebugMsg(LogLevel.Debug, $"[DynamicBoneEditor][Save][{_chaCtrl.GetFullname()}]\n{JSONSerializer.Serialize(_json.GetType(), _json, true)}");
#endif
					return _json;
				}

				internal void Load(List<string> _json)
				{
					if (!_installed) return;
					_charaAccData?.Clear();
					if (_json == null) return;

					foreach (string x in _json)
						_charaAccData.Add(JSONSerializer.Deserialize(_types["DynamicBoneData"], x));
#if DEBUG
					DebugMsg(LogLevel.Debug, $"[DynamicBoneEditor][Load][{_chaCtrl.GetFullname()}]\n{JSONSerializer.Serialize(_json.GetType(), _json, true)}");
#endif
				}

				internal void Backup()
				{
					if (!_installed) return;
					_charaAccData.Clear();

					int _coordinateIndex = _chaCtrl.fileStatus.coordinateType;
					CharacterAccessoryController _controller = CharacterAccessory.GetController(_chaCtrl);
					List<int> _slots = _controller.PartsInfo.Keys.ToList();

					object AccessoryDynamicBoneData = Traverse.Create(_pluginCtrl).Field("AccessoryDynamicBoneData").GetValue();
					if (AccessoryDynamicBoneData == null) return;

					int n = (AccessoryDynamicBoneData as IList).Count;
					for (int i = 0; i < n; i++)
					{
						object x = AccessoryDynamicBoneData.RefElementAt(i).JsonClone(); // should I null cheack this?

						if (Traverse.Create(x).Field("CoordinateIndex").GetValue<int>() != _coordinateIndex) continue;
						if (_slots.IndexOf(Traverse.Create(x).Field("Slot").GetValue<int>()) < 0) continue;

						Traverse.Create(x).Field("CoordinateIndex").SetValue(-1);
						Traverse.Create(_charaAccData).Method("Add", new object[] { x }).GetValue();
#if DEBUG
						DebugMsg(LogLevel.Warning, $"[DynamicBoneEditor][Backup][Slot: {_chaCtrl.GetFullname()}][{Traverse.Create(x).Field("Slot").GetValue<int>()}]");
#endif
					}
					DebugMsg(LogLevel.Warning, $"[DynamicBoneEditor][Backup][Count: {_chaCtrl.GetFullname()}][{_charaAccData.Count}]");
				}

				internal void Restore()
				{
					if (!_installed) return;

					object AccessoryDynamicBoneData = Traverse.Create(_pluginCtrl).Field("AccessoryDynamicBoneData").GetValue();
					if (AccessoryDynamicBoneData == null) return;

					int _coordinateIndex = _chaCtrl.fileStatus.coordinateType;
					for (int i = 0; i < _charaAccData.Count; i++)
					{
						object x = _charaAccData[i].JsonClone();
						Traverse.Create(x).Field("CoordinateIndex").SetValue(_coordinateIndex);
						Traverse.Create(AccessoryDynamicBoneData).Method("Add", new object[] { x }).GetValue();
					}
				}

				internal void CopyPartsInfo(AccessoryCopyEventArgs ev)
				{
					if (!_installed) return;
					Traverse.Create(_pluginCtrl).Method("AccessoriesCopiedEvent", new object[] { null, ev }).GetValue();
				}

				internal void TransferPartsInfo(AccessoryTransferEventArgs ev)
				{
					if (!_installed) return;

					object AccessoryDynamicBoneData = Traverse.Create(_pluginCtrl).Field("AccessoryDynamicBoneData").GetValue();
					if (AccessoryDynamicBoneData == null) return;

					RemovePartsInfo(ev.DestinationSlotIndex);

					int _coordinateIndex = _chaCtrl.fileStatus.coordinateType;

					int n = (AccessoryDynamicBoneData as IList).Count;
					for (int i = 0; i < n; i++)
					{
						object x = AccessoryDynamicBoneData.RefElementAt(i).JsonClone();

						if (Traverse.Create(x).Field("CoordinateIndex").GetValue<int>() != _coordinateIndex) continue;
						if (Traverse.Create(x).Field("Slot").GetValue<int>() != ev.SourceSlotIndex) continue;

						Traverse.Create(x).Field("Slot").SetValue(ev.DestinationSlotIndex);
						Traverse.Create(AccessoryDynamicBoneData).Method("Add", new object[] { x }).GetValue();
					}
				}

				internal void RemovePartsInfo(int _slotIndex)
				{
					if (!_installed) return;
					Traverse.Create(_pluginCtrl).Method("AccessoryKindChangeEvent", new object[] { null, new AccessorySlotEventArgs(_slotIndex) }).GetValue();
				}
			}
		}
	}
}
