using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

using ParadoxNotion.Serialization;

using BepInEx;
using HarmonyLib;

using KKAPI.Chara;
using KKAPI.Maker;

namespace CharacterAccessory
{
	public partial class CharacterAccessory
	{
		internal static class MaterialRouterSupport
		{
			internal static BaseUnityPlugin _instance = null;
			internal static bool _installed = false;
			internal static Dictionary<string, Type> _types = new Dictionary<string, Type>();

			internal static void Init()
			{
				BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue("madevil.kk.mr", out PluginInfo _pluginInfo);
				_instance = _pluginInfo?.Instance;

				if (_instance != null)
				{
					_installed = true;
					_types["MaterialRouterController"] = _instance.GetType().Assembly.GetType("MaterialRouter.MaterialRouter+MaterialRouterController");
					_types["RouteRule"] = _instance.GetType().Assembly.GetType("MaterialRouter.MaterialRouter+RouteRule");
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
					List<int> _slots = _controller.PartsInfo.Keys.ToList();

					object OutfitTriggers = Traverse.Create(_pluginCtrl).Field("OutfitTriggers").GetValue();
					//Logger.LogWarning($"[MaterialRouter][Backup][OutfitTriggers.Count: {(OutfitTriggers as IDictionary).Count}]");

					object CurOutfitTrigger = OutfitTriggers.RefTryGetValue(_coordinateIndex);
					if (CurOutfitTrigger == null) return;
					//Logger.LogWarning($"[MaterialRouter][Backup][CurOutfitTrigger.Count: {(CurOutfitTrigger as IList).Count}]");

					int n = (CurOutfitTrigger as IList).Count;
					for (int i = 0; i < n; i++)
					{
						object _rule = CurOutfitTrigger.RefElementAt(i);
						string _path = Traverse.Create(_rule).Property("GameObjectPath").GetValue<string>();
						if (!_path.Contains($"/ca_slot")) continue;

						//Logger.LogWarning($"[MaterialRouter][Backup][{_path}]");
						foreach (int _slotIndex in _slots)
						{
							if (!_path.Contains($"/ca_slot{_slotIndex:00}/")) continue;
							Logger.LogWarning($"[MaterialRouter][Backup][{_slotIndex}]");
							_charaAccData.Add(_rule.JsonClone());
						}
					}
					//Logger.LogWarning($"[MaterialRouter][Backup][{_charaAccData.Count}]");
				}

				internal void Restore()
				{
					if (!_installed) return;
					int _coordinateIndex = _chaCtrl.fileStatus.coordinateType;

					object CurOutfitTrigger = Traverse.Create(_pluginCtrl).Field("OutfitTriggers").GetValue().RefTryGetValue(_coordinateIndex);
					if (CurOutfitTrigger == null) return;

					for (int i = 0; i < _charaAccData.Count; i++)
						Traverse.Create(CurOutfitTrigger).Method("Add", new object[] { _charaAccData[i].JsonClone() }).GetValue();
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
