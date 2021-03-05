using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using ParadoxNotion.Serialization;

using BepInEx;
using HarmonyLib;

using KKAPI.Chara;
using KKAPI.Maker;

namespace CharacterAccessory
{
	public partial class CharacterAccessory
	{
		internal static class HairAccessoryCustomizerSupport
		{
			internal static BaseUnityPlugin _instance = null;
			internal static bool _installed = false;
			internal static Dictionary<string, Type> _types = new Dictionary<string, Type>();

			internal static void Init()
			{
				BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue("com.deathweasel.bepinex.hairaccessorycustomizer", out PluginInfo _pluginInfo);
				_instance = _pluginInfo?.Instance;

				if (_instance != null)
				{
					_installed = true;

					_types["HairAccessoryController"] = _instance.GetType().Assembly.GetType("KK_Plugins.HairAccessoryCustomizer+HairAccessoryController");
					_types["HairAccessoryInfo"] = _instance.GetType().Assembly.GetType("KK_Plugins.HairAccessoryCustomizer+HairAccessoryController+HairAccessoryInfo");

					HooksInstance["General"].Patch(_types["HairAccessoryController"].GetMethod("UpdateAccessories", AccessTools.all, null, new[] { typeof(bool) }, null), prefix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.DuringLoading_Prefix)));
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
					_charaAccData.Clear();
				}

				internal Dictionary<int, string> Save()
				{
					if (!_installed) return null;

					Dictionary<int, string> ContainerJson = new Dictionary<int, string>();
					foreach (KeyValuePair<int, object> x in _charaAccData)
					{
						FakeHairAccessoryInfo _info = new FakeHairAccessoryInfo(x.Value);
						ContainerJson[x.Key] = JSONSerializer.Serialize(typeof(FakeHairAccessoryInfo), _info);
#if DEBUG
						Logger.LogWarning($"[{x.Key}]\n{DisplayObjectInfo(_info)}\n\n");
#endif
					}

					return ContainerJson;
				}

				internal void Load(Dictionary<int, string> _json)
				{
					if (!_installed) return;
					_charaAccData.Clear();
					if (_json == null) return;

					foreach (KeyValuePair<int, string> x in _json)
					{
						FakeHairAccessoryInfo _info = JSONSerializer.Deserialize<FakeHairAccessoryInfo>(x.Value);
						object _instance = Activator.CreateInstance(_types["HairAccessoryInfo"]);
						Traverse _traverse = Traverse.Create(_instance);
						_traverse.Field<bool>("HairGloss").Value = _info.HairGloss;
						_traverse.Field<bool>("ColorMatch").Value = _info.ColorMatch;
						_traverse.Field<Color>("OutlineColor").Value = _info.OutlineColor;
						_traverse.Field<Color>("AccessoryColor").Value = _info.AccessoryColor;
						_traverse.Field<float>("HairLength").Value = _info.HairLength;
						_charaAccData[x.Key] = _instance;
#if DEBUG
						Logger.LogWarning($"[{x.Key}][_charaAccData]\n{DisplayObjectInfo(_charaAccData[x.Key])}\n\n");
#endif
					}
				}

				internal void Backup()
				{
					if (!_installed) return;
					_charaAccData.Clear();

					int _coordinateIndex = _chaCtrl.fileStatus.coordinateType;
					
					List<int> _keys = Traverse.Create(_pluginCtrl).Field("HairAccessories").Property("Keys").GetValue<ICollection<int>>().ToList();
					if (_keys.IndexOf(_coordinateIndex) < 0) return;
					object _hairAccessoryInfos = Traverse.Create(_pluginCtrl).Field("HairAccessories").Method("get_Item", new object[] { _coordinateIndex }).GetValue();
					//Logger.LogWarning($"[HairAccessoryCustomizer][Backup][HairAccessories.Count: {Traverse.Create(HairAccessories).Property("Count").GetValue<int>()}]");
					//Logger.LogWarning($"[HairAccessoryCustomizer][Backup][_hairAccessoryInfos.Count: {Traverse.Create(_hairAccessoryInfos).Property("Count").GetValue<int>()}]");
					if (_hairAccessoryInfos == null) return;

					List<int> _slots = Traverse.Create(_hairAccessoryInfos).Property("Keys").GetValue<ICollection<int>>().ToList();
					//Logger.LogWarning($"[HairAccessoryCustomizer][Backup][keys: {string.Join(",", _slots.Select(x => x.ToString()).ToArray())}]");
					foreach (int _slotIndex in _slots)
					{
						if (!MoreAccessoriesSupport.IsHairAccessory(_chaCtrl, _slotIndex)) continue;

						object HairAccessoryInfo = _hairAccessoryInfos.RefTryGetValue(_slotIndex);
						if (HairAccessoryInfo == null) continue;

						_charaAccData[_slotIndex] = HairAccessoryInfo.JsonClone();
					}
				}

				internal void Restore()
				{
					if (!_installed) return;

					int _coordinateIndex = _chaCtrl.fileStatus.coordinateType;

					List<int> _keys = Traverse.Create(_pluginCtrl).Field("HairAccessories").Property("Keys").GetValue<ICollection<int>>().ToList();
					if (_keys.IndexOf(_coordinateIndex) < 0) return;

					object _hairAccessoryInfos = Traverse.Create(_pluginCtrl).Field("HairAccessories").Method("get_Item", new object[] { _coordinateIndex }).GetValue();
					if (_hairAccessoryInfos == null) return;

					foreach (KeyValuePair<int, object> x in _charaAccData)
					{
						if (_hairAccessoryInfos.RefTryGetValue(x.Key) != null)
						{
							Logger.LogDebug($"[HairAccessoryCustomizer][Restore] remove HairAccessoryInfo on Slot{x.Key + 1:00}");
							Traverse.Create(_hairAccessoryInfos).Method("Remove", new object[] { x.Key }).GetValue();
						}
#if DEBUG
						Logger.LogWarning($"Slot{x.Key + 1:00}\n{DisplayObjectInfo(x.Value)}");
#endif
						Traverse.Create(_hairAccessoryInfos).Method("Add", new object[] { x.Key, x.Value.JsonClone() }).GetValue();
					}
				}

				internal class FakeHairAccessoryInfo
				{
					public bool HairGloss = false;
					public bool ColorMatch = false;
					public Color OutlineColor = Color.white;
					public Color AccessoryColor = Color.white;
					public float HairLength = 0;

					public FakeHairAccessoryInfo(object _info)
					{
						Traverse _traverse = Traverse.Create(_info);
						HairGloss = _traverse.Field("HairGloss").GetValue<bool>();
						ColorMatch = _traverse.Field("ColorMatch").GetValue<bool>();
						OutlineColor = _traverse.Field("OutlineColor").GetValue<Color>();
						AccessoryColor = _traverse.Field("AccessoryColor").GetValue<Color>();
						HairLength = _traverse.Field("HairLength").GetValue<float>();
					}
				}
#if DEBUG
				internal void DumpInfo(bool local)
				{
					if (local)
					{
						foreach (var x in _charaAccData)
							Logger.LogWarning($"[{x.Key}]\n{DisplayObjectInfo(x.Value)}\n\n");
					}
					else
					{
						int _coordinateIndex = _chaCtrl.fileStatus.coordinateType;

						List<int> _keys = Traverse.Create(_pluginCtrl).Field("HairAccessories").Property("Keys").GetValue<ICollection<int>>().ToList();
						if (_keys.IndexOf(_coordinateIndex) < 0) return;

						object _hairAccessoryInfos = Traverse.Create(_pluginCtrl).Field("HairAccessories").Method("get_Item", new object[] { _coordinateIndex }).GetValue();
						if (_hairAccessoryInfos == null) return;

						List<int> _slots = Traverse.Create(_hairAccessoryInfos).Property("Keys").GetValue<ICollection<int>>().ToList();
						foreach (int _slotIndex in _slots)
						{
							object HairAccessoryInfo = _hairAccessoryInfos.RefTryGetValue(_slotIndex);
							if (HairAccessoryInfo == null) continue;

							Logger.LogWarning($"[{_slotIndex}]\n{DisplayObjectInfo(HairAccessoryInfo)}\n\n");
						}
					}
				}
#endif
				internal void CopyPartsInfo(AccessoryCopyEventArgs ev)
				{
					if (!_installed) return;
					Traverse.Create(_pluginCtrl).Method("CopyAccessoriesHandler", new object[] { ev }).GetValue();
				}

				internal void TransferPartsInfo(AccessoryTransferEventArgs ev)
				{
					if (!_installed) return;
					Traverse.Create(_pluginCtrl).Method("TransferAccessoriesHandler", new object[] { ev }).GetValue();
				}

				internal void RemovePartsInfo(int _slotIndex)
				{
					if (!_installed) return;
					Traverse.Create(_pluginCtrl).Method("RemoveHairAccessoryInfo", new object[] { _slotIndex }).GetValue();
				}
			}
		}
	}
}
