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
				BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue("madevil.kk.mr", out PluginInfo _pluginInfo);
				_instance = _pluginInfo?.Instance;

				if (_instance != null)
				{
					if (_pluginInfo.Metadata.Version.CompareTo(new Version("2.0.0.0")) < 0)
					{
						Logger.LogError($"Material Router version {_pluginInfo.Metadata.Version} found, minimun version 2 is reqired");
						return;
					}

					_installed = true;
					SupportList.Add("MaterialRouter");

					Assembly _assembly = _instance.GetType().Assembly;
					_types["MaterialRouterController"] = _assembly.GetType("MaterialRouter.MaterialRouter+MaterialRouterController");
					_types["RouteRule"] = _assembly.GetType("MaterialRouter.MaterialRouter+RouteRule");
					_types["RouteRuleV1"] = _assembly.GetType("MaterialRouter.MaterialRouter+RouteRuleV1");
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
				private readonly List<object> _charaAccData = new List<object>();

				internal UrineBag(ChaControl ChaControl)
				{
					if (!_installed) return;

					_chaCtrl = ChaControl;
					_pluginCtrl = GetController(_chaCtrl);
				}

				internal object GetExtDataLink()
				{
					return Traverse.Create(_pluginCtrl).Field("RouteRuleList").GetValue();
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

					bool _migration = false;
					Type _typeListRouteRuleV1 = typeof(List<>).MakeGenericType(_types["RouteRuleV1"]);
					object _listRouteRuleV1 = Activator.CreateInstance(_typeListRouteRuleV1);

					foreach (string x in _json)
					{
						if (x.IndexOf("GameObjectPath") > -1)
						{
							_migration = true;
							(_listRouteRuleV1 as IList).Add(JSONSerializer.Deserialize(_types["RouteRuleV1"], x));
						}
						else
							_charaAccData.Add(JSONSerializer.Deserialize(_types["RouteRule"], x));
					}

					if (_migration)
					{
						DebugMsg(LogLevel.Warning, $"[MaterialRouterSupport][Migration]");
						object _rules = Traverse.Create(_instance).Method("MigrationV1", new Type[] { _typeListRouteRuleV1 }, new object[] { _listRouteRuleV1 }).GetValue();
						foreach (object x in _rules as IList)
							_charaAccData.Add(x);
					}
				}

				internal void Backup()
				{
					if (!_installed) return;
					_charaAccData.Clear();

					CharacterAccessoryController _controller = CharacterAccessory.GetController(_chaCtrl);
					int _coordinateIndex = _chaCtrl.fileStatus.coordinateType;
					List<int> _slots = _controller.PartsInfo?.Keys?.ToList();

					object _extdataLink = GetExtDataLink();
					if (_extdataLink == null) return;

					int n = (_extdataLink as IList).Count;
					for (int i = 0; i < n; i++)
					{
						object x = _extdataLink.RefElementAt(i).JsonClone();
						Traverse _traverse = Traverse.Create(x);
						if (_traverse.Property("ObjectType").Method("ToString").GetValue<string>() != "Accessory") continue;
						if (_traverse.Property("Coordinate").GetValue<int>() != _coordinateIndex) continue;
						int _slotIndex = int.Parse(_traverse.Property("GameObjectName").GetValue<string>().Replace("ca_slot", ""));
						if (!_slots.Contains(_slotIndex)) continue;

						_traverse.Property("Coordinate").SetValue(-1);
						(_charaAccData as IList).Add(x);
					}
				}

				internal void Restore()
				{
					if (!_installed) return;

					int _coordinateIndex = _chaCtrl.fileStatus.coordinateType;
					object _extdataLink = GetExtDataLink();
					if (_extdataLink == null) return;

					for (int i = 0; i < _charaAccData.Count; i++)
					{
						object x = _charaAccData[i].JsonClone();
						Traverse.Create(x).Property("Coordinate").SetValue(_chaCtrl.fileStatus.coordinateType);
						(_extdataLink as IList).Add(x);
					}
				}

				internal string Report()
				{
					if (!_installed) return "";
					return JSONSerializer.Serialize(_charaAccData.GetType(), _charaAccData, true);
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
