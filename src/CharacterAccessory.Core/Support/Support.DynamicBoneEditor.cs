using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using UniRx;
using ParadoxNotion.Serialization;

using BepInEx;
using HarmonyLib;

using KKAPI.Maker;
using JetPack;

using KK_Plugins.DynamicBoneEditor;

namespace CharacterAccessory
{
	public partial class CharacterAccessory
	{
		internal static class DynamicBoneEditorSupport
		{
			internal static BaseUnityPlugin _instance = null;
			internal static bool _installed = false;
			internal static readonly Dictionary<string, Type> _types = new Dictionary<string, Type>();

			internal static void Init()
			{
				_instance = JetPack.Toolbox.GetPluginInstance("com.deathweasel.bepinex.dynamicboneeditor");

				if (_instance != null)
				{
					_installed = true;
					_supportList.Add("DynamicBoneEditor");

					Assembly _assembly = _instance.GetType().Assembly;
					_types["CharaController"] = _assembly.GetType("KK_Plugins.DynamicBoneEditor.CharaController");
					_types["DynamicBoneData"] = _assembly.GetType("KK_Plugins.DynamicBoneEditor.DynamicBoneData");

					_hooksInstance["General"].Patch(_types["CharaController"].GetMethod("ApplyData", AccessTools.all), prefix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.DuringLoading_IEnumerator_Prefix)));
				}
			}

			internal static CharaController GetController(ChaControl _chaCtrl) => Plugin.GetCharaController(_chaCtrl);

			internal class UrineBag
			{
				private readonly ChaControl _chaCtrl;
				private readonly CharaController _pluginCtrl;
				private readonly List<DynamicBoneData> _charaAccData = new List<DynamicBoneData>();

				internal UrineBag(ChaControl ChaControl)
				{
					if (!_installed) return;

					_chaCtrl = ChaControl;
					_pluginCtrl = GetController(_chaCtrl);
				}

				internal List<DynamicBoneData> GetExtDataLink()
				{
					return _pluginCtrl.AccessoryDynamicBoneData;
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
						_json.Add(JSONSerializer.Serialize(typeof(DynamicBoneData), x));
					return _json;
				}

				internal void Load(List<string> _json)
				{
					if (!_installed) return;
					_charaAccData.Clear();
					if (_json == null) return;

					foreach (string x in _json)
						_charaAccData.Add(JSONSerializer.Deserialize<DynamicBoneData>(x));
				}

				internal void Backup()
				{
					if (!_installed) return;
					_charaAccData.Clear();

					List<DynamicBoneData> _extdataLink = GetExtDataLink();
					if (_extdataLink == null) return;

					int _coordinateIndex = _chaCtrl.fileStatus.coordinateType;
					CharacterAccessoryController _controller = CharacterAccessory.GetController(_chaCtrl);
					List<int> _slots = _controller.PartsInfo?.Keys?.ToList();

					_charaAccData.AddRange(_extdataLink.Where(x => x.CoordinateIndex == _coordinateIndex && _slots.Contains(x.Slot)).ToList().JsonClone<List<DynamicBoneData>>());
					_charaAccData.ForEach(x => x.CoordinateIndex = -1);

					int n = (_extdataLink as IList).Count;
					for (int i = 0; i < n; i++)
					{
						DynamicBoneData x = _extdataLink.ElementAtOrDefault(i).JsonClone<DynamicBoneData>();
						Traverse _traverse = Traverse.Create(x);

						if (_traverse.Field("CoordinateIndex").GetValue<int>() != _coordinateIndex) continue;
						if (_slots.IndexOf(_traverse.Field("Slot").GetValue<int>()) < 0) continue;

						_traverse.Field("CoordinateIndex").SetValue(-1);
						_charaAccData.Add(x);
					}
				}

				internal void Restore()
				{
					if (!_installed) return;
					List<DynamicBoneData> _extdataLink = GetExtDataLink();
					if (_extdataLink == null) return;

					int _coordinateIndex = _chaCtrl.fileStatus.coordinateType;
					List<DynamicBoneData> _temp = _charaAccData.JsonClone<List<DynamicBoneData>>();
					_temp.ForEach(x => x.CoordinateIndex = _coordinateIndex);
					_extdataLink.AddRange(_temp);
				}

				internal string Report()
				{
					if (!_installed) return "";

					return JSONSerializer.Serialize(_charaAccData.GetType(), _charaAccData, true);
				}

				internal void CopyPartsInfo(AccessoryCopyEventArgs _args)
				{
					if (!_installed) return;
					_pluginCtrl.AccessoriesCopiedEvent(null, _args);
				}

				internal void TransferPartsInfo(AccessoryTransferEventArgs _args)
				{
					if (!_installed) return;
					List<DynamicBoneData> _extdataLink = GetExtDataLink();
					if (_extdataLink == null) return;

					RemovePartsInfo(_args.DestinationSlotIndex);

					int _coordinateIndex = _chaCtrl.fileStatus.coordinateType;
					List<DynamicBoneData> _temp = _extdataLink.Where(x => x.CoordinateIndex == _coordinateIndex && x.Slot == _args.SourceSlotIndex).ToList().JsonClone<List<DynamicBoneData>>();
					_temp.ForEach(x => x.Slot = _args.DestinationSlotIndex);
					_extdataLink.AddRange(_temp);
				}

				internal void RemovePartsInfo(int _slotIndex)
				{
					if (!_installed) return;
					_pluginCtrl.AccessoryKindChangeEvent(null, new AccessorySlotEventArgs(_slotIndex));
				}
			}
		}
	}
}
