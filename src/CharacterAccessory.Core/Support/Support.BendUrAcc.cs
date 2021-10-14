using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using UniRx;
using ParadoxNotion.Serialization;

using BepInEx;
using HarmonyLib;

using KKAPI.Chara;
using KKAPI.Maker;
using JetPack;

namespace CharacterAccessory
{
	public partial class CharacterAccessory
	{
		internal static class BendUrAccSupport
		{
			internal static BaseUnityPlugin _instance = null;
			internal static bool _installed = false;
			internal static readonly Dictionary<string, Type> _types = new Dictionary<string, Type>();

			internal static void Init()
			{
				BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue("madevil.kk.BendUrAcc", out PluginInfo _pluginInfo);
				_instance = _pluginInfo?.Instance;

				if (_instance != null)
				{
					if (_pluginInfo.Metadata.Version.CompareTo(new Version("1.0.5.0")) < 0)
					{
						_logger.LogError($"BendUrAcc version {_pluginInfo.Metadata.Version} found, minimun version 1.0.5.0 is reqired");
						return;
					}

					_installed = true;
					_supportList.Add("BendUrAcc");

					Assembly _assembly = _instance.GetType().Assembly;
					_types["BendUrAccController"] = _assembly.GetType("BendUrAcc.BendUrAcc+BendUrAccController");
					_types["BendModifier"] = _assembly.GetType("BendUrAcc.BendUrAcc+BendModifier");

					_hooksInstance["General"].Patch(_types["BendUrAccController"].GetMethod("ApplyBendModifierList", AccessTools.all, null, new[] { typeof(string) }, null), prefix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.DuringLoading_Prefix)));
				}
			}

			internal static CharaCustomFunctionController GetController(ChaControl _chaCtrl) => Traverse.Create(_instance).Method("GetController", new object[] { _chaCtrl }).GetValue<CharaCustomFunctionController>();

			internal class UrineBag
			{
				private readonly ChaControl _chaCtrl;
				private readonly CharaCustomFunctionController _pluginCtrl;
				private readonly List<object> _charaAccData = new List<object>();
				private readonly Dictionary<string, Traverse> _traverses = new Dictionary<string, Traverse>();

				internal UrineBag(ChaControl ChaControl)
				{
					if (!_installed) return;

					_chaCtrl = ChaControl;
					_pluginCtrl = GetController(_chaCtrl);
					_traverses["pluginCtrl"] = Traverse.Create(_pluginCtrl);
				}

				internal object GetExtDataLink()
				{
					return _traverses["pluginCtrl"].Field("BendModifierList").GetValue();
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
						_json.Add(JSONSerializer.Serialize(_types["BendModifier"], x));
					return _json;
				}

				internal void Load(List<string> _json)
				{
					if (!_installed) return;

					_charaAccData.Clear();
					if (_json == null) return;

					foreach (string x in _json)
						_charaAccData.Add(JSONSerializer.Deserialize(_types["BendModifier"], x));
				}

				internal void Backup()
				{
					if (!_installed) return;

					_charaAccData.Clear();

					object _extdataLink = GetExtDataLink();
					if (_extdataLink == null) return;

					int _coordinateIndex = _chaCtrl.fileStatus.coordinateType;
					CharacterAccessoryController _controller = CharacterAccessory.GetController(_chaCtrl);
					List<int> _slots = _controller.PartsInfo?.Keys?.ToList();

					int n = (_extdataLink as IList).Count;
					for (int i = 0; i < n; i++)
					{
						object x = _extdataLink.RefElementAt(i).JsonClone();
						Traverse _traverse = Traverse.Create(x);
						if (_traverse.Property("Coordinate").GetValue<int>() != _coordinateIndex) continue;
						if (!_slots.Contains(_traverse.Property("Slot").GetValue<int>())) continue;

						_traverse.Property("Coordinate").SetValue(-1);
						_charaAccData.Add(x);
					}
				}

				internal void Restore()
				{
					if (!_installed) return;

					object _extdataLink = GetExtDataLink();
					if (_extdataLink == null) return;

					int _coordinateIndex = _chaCtrl.fileStatus.coordinateType;
					for (int i = 0; i < _charaAccData.Count; i++)
					{
						object x = _charaAccData[i].JsonClone();
						Traverse.Create(x).Property("Coordinate").SetValue(_coordinateIndex);
						(_extdataLink as IList).Add(x);
					}
				}

				internal void CopyPartsInfo(AccessoryCopyEventArgs _args)
				{
					if (!_installed) return;

					foreach (int _slotIndex in _args.CopiedSlotIndexes)
						_traverses["pluginCtrl"].Method("CloneModifier", new object[] { _slotIndex, _slotIndex, (int) _args.CopySource, (int) _args.CopyDestination }).GetValue();
					return;
				}

				internal void TransferPartsInfo(AccessoryTransferEventArgs _args)
				{
					if (!_installed) return;

					int _coordinateIndex = _chaCtrl.fileStatus.coordinateType;
					_traverses["pluginCtrl"].Method("CloneModifier", new object[] { _args.SourceSlotIndex, _args.DestinationSlotIndex, _coordinateIndex, _coordinateIndex }).GetValue();
					_traverses["pluginCtrl"].Method("RemoveSlotModifier", new object[] { _coordinateIndex, _args.SourceSlotIndex }).GetValue();
				}

				internal void RemovePartsInfo(int _slotIndex)
				{
					if (!_installed) return;

					_traverses["pluginCtrl"].Method("RemoveSlotModifier", new object[] { _slotIndex }).GetValue();
				}
			}
		}
	}
}
