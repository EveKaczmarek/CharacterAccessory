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
using JetPack;

namespace CharacterAccessory
{
	public partial class CharacterAccessory
	{
		internal static class MaterialRouterSupport
		{
			internal static BaseUnityPlugin _instance = null;
			internal static bool _installed = false;
			internal static readonly Dictionary<string, Type> _types = new Dictionary<string, Type>();

			internal static void Init()
			{
				_instance = JetPack.Toolbox.GetPluginInstance("madevil.kk.mr");

				if (_instance != null)
				{
					_installed = true;
					SupportList.Add("MaterialRouter");

					Assembly _assembly = _instance.GetType().Assembly;
					_types["MaterialRouterController"] = _assembly.GetType("MaterialRouter.MaterialRouter+MaterialRouterController");
					_types["RouteRule"] = _assembly.GetType("MaterialRouter.MaterialRouter+RouteRule");

					_hooksInstance["General"].Patch(_types["MaterialRouterController"].GetMethod("BuildCheckList", AccessTools.all), prefix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.DuringLoading_Prefix)));
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
				//private object OutfitTriggers;
				private readonly List<object> _charaAccData = new List<object>();

				internal UrineBag(ChaControl ChaControl)
				{
					if (!_installed) return;

					_chaCtrl = ChaControl;
					_pluginCtrl = GetController(_chaCtrl);
				}

				internal object GetExtDataLink(int _coordinateIndex)
				{
					object OutfitTriggers = Traverse.Create(_pluginCtrl).Field("OutfitTriggers").GetValue();
					if (OutfitTriggers == null)
						return null;
					return OutfitTriggers.RefTryGetValue(_coordinateIndex);
				}

				internal void Reset()
				{
					if (!_installed) return;
					_charaAccData.Clear();
				}

				internal List<string> Save()
				{
					if (!_installed) return null;
					List<string> ContainerJson = new List<string>();
					foreach (object x in _charaAccData)
						ContainerJson.Add(JSONSerializer.Serialize(_types["RouteRule"], x));
					return ContainerJson;
				}

				internal void Load(List<string> _json)
				{
					if (!_installed) return;
					_charaAccData?.Clear();
					if (_json == null) return;

					foreach (string x in _json)
						_charaAccData.Add(JSONSerializer.Deserialize(_types["RouteRule"], x));
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

					int n = (_extdataLink as IList).Count;
					for (int i = 0; i < n; i++)
					{
						object _rule = _extdataLink.RefElementAt(i);
						string _path = Traverse.Create(_rule).Property("GameObjectPath").GetValue<string>();
						if (!_path.Contains($"/ca_slot")) continue;

						//DebugMsg(LogLevel.Warning, $"[MaterialRouter][Backup][{_chaCtrl.GetFullName()}][{_path}]");
						foreach (int _slotIndex in _slots)
						{
							if (!_path.Contains($"/ca_slot{_slotIndex:00}/")) continue;
							DebugMsg(LogLevel.Warning, $"[MaterialRouter][Backup][{_slotIndex}]");
							_charaAccData.Add(_rule.JsonClone());
						}
					}
					//DebugMsg(LogLevel.Warning, $"[MaterialRouter][Backup][{_chaCtrl.GetFullName()}][{_charaAccData.Count}]");
				}

				internal void Restore()
				{
					if (!_installed) return;

					int _coordinateIndex = _chaCtrl.fileStatus.coordinateType;
					object _extdataLink = GetExtDataLink(_coordinateIndex);
					if (_extdataLink == null) return;

					for (int i = 0; i < _charaAccData.Count; i++)
						Traverse.Create(_extdataLink).Method("Add", new object[] { _charaAccData[i].JsonClone() }).GetValue();
				}

				internal void CopyPartsInfo(AccessoryCopyEventArgs ev)
				{
					if (!_installed) return;
					Traverse.Create(_pluginCtrl).Method("AccessoryCopyEvent", new object[] { ev }).GetValue();
				}

				internal void TransferPartsInfo(AccessoryTransferEventArgs ev)
				{
					if (!_installed) return;
					int _coordinateIndex = _chaCtrl.fileStatus.coordinateType;
					Traverse.Create(_pluginCtrl).Method("TransferAccSlotInfo", new object[] { _coordinateIndex, ev }).GetValue();
				}

				internal void RemovePartsInfo(int _slotIndex)
				{
					if (!_installed) return;
					int _coordinateIndex = _chaCtrl.fileStatus.coordinateType;
					Traverse.Create(_pluginCtrl).Method("RemoveAccSlotInfo", new object[] { _coordinateIndex, _slotIndex }).GetValue();
				}
			}
		}
	}
}
