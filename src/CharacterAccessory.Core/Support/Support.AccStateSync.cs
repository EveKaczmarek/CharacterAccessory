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
		internal static class AccStateSyncSupport
		{
			internal static BaseUnityPlugin _instance = null;
			internal static bool _installed = false;
			internal static bool _legacy = false;
			internal static readonly Dictionary<string, Type> _types = new Dictionary<string, Type>();
			internal static readonly List<string> _containerKeys = new List<string>() { "TriggerPropertyList", "TriggerGroupList" };
			internal static readonly Dictionary<string, string> _accParentNames = new Dictionary<string, string>();
			internal static readonly Dictionary<string, int> _guidMapping = new Dictionary<string, int>();

			internal static void Init()
			{
				BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue("madevil.kk.ass", out PluginInfo _pluginInfo);
				_instance = _pluginInfo?.Instance;

				if (_instance != null)
				{
					_legacy = _pluginInfo.Metadata.Version.CompareTo(new Version("4.0.0.0")) < 0;
					if (_legacy)
					{
						_logger.LogError($"AccStateSync version {_pluginInfo.Metadata.Version} found, minimun version 4 is reqired");
						return;
					}

					_installed = true;
					_supportList.Add("AccStateSync");

					Assembly _assembly = _instance.GetType().Assembly;
					_types["AccStateSyncController"] = _assembly.GetType("AccStateSync.AccStateSync+AccStateSyncController");
					_types["TriggerProperty"] = _assembly.GetType("AccStateSync.AccStateSync+TriggerProperty");
					_types["TriggerGroup"] = _assembly.GetType("AccStateSync.AccStateSync+TriggerGroup");

					foreach (object _key in Enum.GetValues(typeof(ChaAccessoryDefine.AccessoryParentKey)))
						_accParentNames[_key.ToString()] = ChaAccessoryDefine.dictAccessoryParent[(int) _key];
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
				private readonly Dictionary<string, object> _charaAccData = new Dictionary<string, object>();
				private readonly Dictionary<string, Traverse> _traverses = new Dictionary<string, Traverse>();

				internal UrineBag(ChaControl ChaControl)
				{
					if (!_installed) return;
					_chaCtrl = ChaControl;
					_pluginCtrl = GetController(_chaCtrl);
					_traverses["pluginCtrl"] = Traverse.Create(_pluginCtrl);

					foreach (string _key in _containerKeys)
					{
						Type _type = _types[_key.Replace("List", "")];
						Type _generic = typeof(List<>).MakeGenericType(_type);
						_charaAccData[_key] = Activator.CreateInstance(_generic);
						_types[_key] = _generic;
					}
				}

				internal object GetTriggerPropertyList()
				{
					return _traverses["pluginCtrl"].Field("TriggerPropertyList").GetValue();
				}

				internal object GetTriggerGroupList()
				{
					return _traverses["pluginCtrl"].Field("TriggerGroupList").GetValue();
				}

				internal void Reset()
				{
					if (!_installed) return;
					(_charaAccData["TriggerPropertyList"] as IList).Clear();
					(_charaAccData["TriggerGroupList"] as IList).Clear();
					_guidMapping.Clear();
				}

				internal Dictionary<string, string> Save()
				{
					if (!_installed) return null;
					Dictionary<string, string> _json = new Dictionary<string, string>();
					/*
					_json["TriggerPropertyList"] = JSONSerializer.Serialize(_charaAccData["TriggerPropertyList"].GetType(), _charaAccData["TriggerPropertyList"]);
					_json["TriggerGroupList"] = JSONSerializer.Serialize(_charaAccData["TriggerGroupList"].GetType(), _charaAccData["TriggerGroupList"]);
					*/
					foreach (string _key in _containerKeys)
						_json[_key] = JSONSerializer.Serialize(_types[_key], _charaAccData[_key]);

					return _json;
				}

				internal void Migrate(Dictionary<int, string> _json)
				{
					if (!_installed) return;
					Reset();
					if (_json == null) return;

					List<AccTriggerInfo> _oldData = new List<AccTriggerInfo>();
					int _coordinateIndex = -1; // _chaCtrl.fileStatus.coordinateType;
					int _baseID = 9; //GetNextGroupID(_coordinateIndex);

					Dictionary<string, int> _oldGroup = new Dictionary<string, int>();

					foreach (string x in _json.Values)
					{
						AccTriggerInfo _trigger = JSONSerializer.Deserialize<AccTriggerInfo>(x);
						_oldData.Add(_trigger);
						if (_trigger.Kind >= 9)
							_oldGroup[_trigger.Group] = _trigger.Kind;
					}

					_oldGroup = _oldGroup.OrderBy(x => x.Value).ThenBy(x => x.Key).ToDictionary(x => x.Key, x => x.Value);

					Dictionary<string, int> _mapping = new Dictionary<string, int>();
					foreach (string _name in _oldGroup.Keys)
					{
						_mapping[_name] = _baseID;
						string _label = _accParentNames.ContainsKey(_name) ? _accParentNames[_name] : "";
						object _group = Activator.CreateInstance(_types["TriggerGroup"], new object[] { _coordinateIndex, _baseID, _label });
						(_charaAccData["TriggerGroupList"] as IList).Add(_group);
						_baseID ++;
					}

					foreach (AccTriggerInfo _info in _oldData)
					{
						if (_info.Kind >= 9)
						{
							/*
							if (!_mapping.ContainsKey(_info.Group))
								Logger.LogError($"[_mapping] missing key [{_info.Group}]");
							else
							*/
								_info.Kind = _mapping[_info.Group];
							{
								object _trigger = Activator.CreateInstance(_types["TriggerProperty"], new object[] { _coordinateIndex, _info.Slot, _info.Kind, 0, _info.State[0], 0 });
								(_charaAccData["TriggerPropertyList"] as IList).Add(_trigger);
							}
							{
								object _trigger = Activator.CreateInstance(_types["TriggerProperty"], new object[] { _coordinateIndex, _info.Slot, _info.Kind, 1, _info.State[3], 0 });
								(_charaAccData["TriggerPropertyList"] as IList).Add(_trigger);
							}
						}
						else
						{
							for (int i = 0; i <= 3; i++)
							{
								object _trigger = Activator.CreateInstance(_types["TriggerProperty"], new object[] { _coordinateIndex, _info.Slot, _info.Kind, i, _info.State[i], 0 });
								(_charaAccData["TriggerPropertyList"] as IList).Add(_trigger);
							}
						}
					}

					for (int i = 0; i < (_charaAccData["TriggerGroupList"] as IList).Count; i++)
					{
						object x = _charaAccData["TriggerGroupList"].RefElementAt(i);
						Traverse _traverse = Traverse.Create(x);
						int _kind = _traverse.Property("Kind").GetValue<int>();
						string _guid = _traverse.Property("GUID").GetValue<string>();
						_guidMapping[_guid] = _kind;
					}
				}

				internal void Load(Dictionary<string, string> _json)
				{
					if (!_installed) return;
					Reset();
					if (_json == null) return;

					if (!_json.ContainsKey("TriggerPropertyList")) return;

					_charaAccData["TriggerPropertyList"] = JSONSerializer.Deserialize(_charaAccData["TriggerPropertyList"].GetType(), _json["TriggerPropertyList"]);
					_charaAccData["TriggerGroupList"] = JSONSerializer.Deserialize(_charaAccData["TriggerGroupList"].GetType(), _json["TriggerGroupList"]);
					for (int i = 0; i < (_charaAccData["TriggerGroupList"] as IList).Count; i++)
					{
						object x = _charaAccData["TriggerGroupList"].RefElementAt(i);
						Traverse _traverse = Traverse.Create(x);
						int _kind = _traverse.Property("Kind").GetValue<int>();
						string _guid = _traverse.Property("GUID").GetValue<string>();
						_guidMapping[_guid] = _kind;
					}
				}

				internal void Backup()
				{
					if (!_installed) return;
					Reset();
					RefreshCache();

					object TriggerPropertyList = _traverses["pluginCtrl"].Field("_cachedCoordinatePropertyList").GetValue();
					object TriggerGroupList = _traverses["pluginCtrl"].Field("_cachedCoordinateGroupList").GetValue();
					if (TriggerPropertyList == null) return;

					CharacterAccessoryController _controller = CharacterAccessory.GetController(_chaCtrl);
					HashSet<int> _slots = new HashSet<int>(_controller.PartsInfo?.Keys);

					for (int i = 0; i < (TriggerPropertyList as IList).Count; i++)
					{
						object x = TriggerPropertyList.RefElementAt(i).JsonClone();
						Traverse _traverse = Traverse.Create(x);
						if (!_slots.Contains(_traverse.Property("Slot").GetValue<int>())) continue;

						_traverse.Property("Coordinate").SetValue(-1);
						(_charaAccData["TriggerPropertyList"] as IList).Add(x);
					}

					for (int i = 0; i < (TriggerGroupList as IList).Count; i++)
					{
						object x = TriggerGroupList.RefElementAt(i).JsonClone();
						Traverse _traverse = Traverse.Create(x);

						_traverse.Property("Coordinate").SetValue(-1);
						(_charaAccData["TriggerGroupList"] as IList).Add(x);

						int _kind = _traverse.Property("Kind").GetValue<int>();
						string _guid = _traverse.Property("GUID").GetValue<string>();
						_guidMapping[_guid] = _kind;
					}
				}

				internal void Restore()
				{
					if (!_installed) return;

					object TriggerPropertyList = _traverses["pluginCtrl"].Field("TriggerPropertyList").GetValue();
					object TriggerGroupList = _traverses["pluginCtrl"].Field("TriggerGroupList").GetValue();
					if (TriggerPropertyList == null) return;

					int _coordinateIndex = _chaCtrl.fileStatus.coordinateType;

					Dictionary<int, int> _mapping = new Dictionary<int, int>();

					foreach (string _guid in _guidMapping.Keys)
					{
						object _copy = null;
						foreach (object x in _charaAccData["TriggerGroupList"] as IList)
						{
							if (Traverse.Create(x).Property("GUID").GetValue<string>() != _guid) continue;
							_copy = x.JsonClone();
						}

						if (_copy == null)
						{
							DebugMsg(LogLevel.Error, $"[Restore] cannot find group setting for {_guid}");
							continue;
						}

						Traverse _traverse = Traverse.Create(_copy);
						_traverse.Property("Coordinate").SetValue(_coordinateIndex);

						object _group = GetTriggerGroupByGUID(_coordinateIndex, _guid);
						int _kindOld = _guidMapping[_guid];
						if (_group == null)
						{
							int _kindNew = GetNextGroupID(_coordinateIndex);
							_mapping[_kindOld] = _kindNew;

							_traverse.Property("Kind").SetValue(_kindNew);
						}
						(TriggerGroupList as IList).Add(_copy);
					}

					foreach (object x in _charaAccData["TriggerPropertyList"] as IList)
					{
						object _copy = x.JsonClone();
						Traverse _traverse = Traverse.Create(_copy);
						_traverse.Property("Coordinate").SetValue(_coordinateIndex);
						int _kind = _traverse.Property("RefKind").GetValue<int>();
						if (_mapping.ContainsKey(_kind))
							_traverse.Property("RefKind").SetValue(_mapping[_kind]);
						(TriggerPropertyList as IList).Add(_copy);
					}

					RefreshCache();
				}

				internal void CopyPartsInfo(AccessoryCopyEventArgs _args)
				{
					if (!_installed) return;

					foreach (int _slotIndex in _args.CopiedSlotIndexes)
						_traverses["pluginCtrl"].Method("CloneSlotTriggerProperty", new object[] { _slotIndex, _slotIndex, (int) _args.CopySource, (int) _args.CopyDestination }).GetValue();
					return;
				}

				internal void TransferPartsInfo(AccessoryTransferEventArgs _args)
				{
					if (!_installed) return;

					int _coordinateIndex = _chaCtrl.fileStatus.coordinateType;
					_traverses["pluginCtrl"].Method("CloneSlotTriggerProperty", new object[] { _args.SourceSlotIndex, _args.DestinationSlotIndex, _coordinateIndex, _coordinateIndex }).GetValue();
				}

				internal void RemovePartsInfo(int _slotIndex) => RemovePartsInfo(_chaCtrl.fileStatus.coordinateType, _slotIndex);
				internal void RemovePartsInfo(int _coordinateIndex, int _slotIndex)
				{
					if (!_installed) return;

					_traverses["pluginCtrl"].Method("RemoveSlotTriggerProperty", new object[] { _coordinateIndex, _slotIndex }).GetValue();
				}

				internal object GetTriggerGroupByGUID(int _coordinateIndex, string _guid)
				{
					if (!_installed) return -1;
					return _traverses["pluginCtrl"].Method("GetTriggerGroupByGUID", new object[] { _coordinateIndex, _guid }).GetValue();
				}

				internal int GetNextGroupID(int _coordinateIndex)
				{
					if (!_installed) return -1;
					return _traverses["pluginCtrl"].Method("GetNextGroupID", new object[] { _coordinateIndex }).GetValue<int>();
				}

				internal void PackGroupID(int _coordinateIndex)
				{
					if (!_installed) return;
					_traverses["pluginCtrl"].Method("PackGroupID", new object[] { _coordinateIndex }).GetValue();
				}

				internal void RefreshCache()
				{
					if (!_installed) return;
					_traverses["pluginCtrl"].Method("RefreshCache").GetValue();
				}

				internal void InitCurOutfitTriggerInfo(string _caller)
				{
					if (!_installed) return;
					_traverses["pluginCtrl"].Method("InitCurOutfitTriggerInfo", new object[] { _caller }).GetValue();
				}

				internal void SetAccessoryStateAll(bool _show = true)
				{
					if (!_installed) return;
					_traverses["pluginCtrl"].Method("SetAccessoryStateAll", new object[] { _show }).GetValue();
				}

				internal void SyncAllAccToggle(string _caller)
				{
					if (!_installed) return;
					_traverses["pluginCtrl"].Method("SyncAllAccToggle", new object[] { _caller }).GetValue();
				}
			}

			public class AccTriggerInfo
			{
				public int Slot { get; set; }
				public int Kind { get; set; } = -1;
				public string Group { get; set; } = "";
				public List<bool> State { get; set; } = new List<bool>() { true, false, false, false };

				public AccTriggerInfo(int slot) { Slot = slot; }
			}
		}
	}
}
