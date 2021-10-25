using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using UnityEngine;
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
		internal static class HairAccessoryCustomizerSupport
		{
			internal static BaseUnityPlugin _instance = null;
			internal static bool _installed = false;
			internal static readonly Dictionary<string, Type> _types = new Dictionary<string, Type>();

			internal static void Init()
			{
				_instance = JetPack.Toolbox.GetPluginInstance("com.deathweasel.bepinex.hairaccessorycustomizer");

				if (_instance != null)
				{
					_installed = true;
					_supportList.Add("HairAccessoryCustomizer");

					Assembly _assembly = _instance.GetType().Assembly;
					_types["HairAccessoryController"] = _assembly.GetType("KK_Plugins.HairAccessoryCustomizer+HairAccessoryController");
					_types["HairAccessoryInfo"] = _assembly.GetType("KK_Plugins.HairAccessoryCustomizer+HairAccessoryController+HairAccessoryInfo");

					_hooksInstance["General"].Patch(_types["HairAccessoryController"].GetMethod("UpdateAccessories", AccessTools.all, null, new[] { typeof(bool) }, null), prefix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.DuringLoading_Prefix)));
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
				private readonly Dictionary<int, object> _charaAccData = new Dictionary<int, object>();
				private readonly Dictionary<string, Traverse> _traverses = new Dictionary<string, Traverse>();

				internal UrineBag(ChaControl ChaControl)
				{
					if (!_installed) return;

					_chaCtrl = ChaControl;
					_pluginCtrl = GetController(_chaCtrl);
					_traverses["pluginCtrl"] = Traverse.Create(_pluginCtrl);
				}

				internal object GetExtDataLink(int _coordinateIndex)
				{
					object HairAccessories = _traverses["pluginCtrl"].Field("HairAccessories").GetValue();
					if (HairAccessories == null)
						return null;
					return HairAccessories.RefTryGetValue(_coordinateIndex);
				}

				internal void Reset()
				{
					_charaAccData.Clear();
				}

				internal Dictionary<int, string> Save()
				{
					if (!_installed) return null;

					Dictionary<int, string> _json = new Dictionary<int, string>();
					foreach (KeyValuePair<int, object> x in _charaAccData)
					{
						FakeHairAccessoryInfo _info = new FakeHairAccessoryInfo(x.Value);
						_json[x.Key] = JSONSerializer.Serialize(typeof(FakeHairAccessoryInfo), _info);
					}
					return _json;
				}

				internal void Load(Dictionary<int, string> _json)
				{
					if (!_installed) return;

					_charaAccData.Clear();
					if (_json == null) return;

					foreach (KeyValuePair<int, string> x in _json)
					{
						FakeHairAccessoryInfo _info = JSONSerializer.Deserialize<FakeHairAccessoryInfo>(x.Value);
						_charaAccData[x.Key] = _info.Convert();
					}
				}

				internal void Backup()
				{
					if (!_installed) return;

					_charaAccData.Clear();

					int _coordinateIndex = _chaCtrl.fileStatus.coordinateType;
					object _extdataLink = GetExtDataLink(_coordinateIndex);
					if (_extdataLink == null) return;

					List<int> _slots = Traverse.Create(_extdataLink).Property("Keys").GetValue<ICollection<int>>().ToList();
					foreach (int _slotIndex in _slots)
					{
						if (!MoreAccessoriesSupport.IsHairAccessory(_chaCtrl, _slotIndex)) continue;

						object HairAccessoryInfo = _extdataLink.RefTryGetValue(_slotIndex);
						if (HairAccessoryInfo == null) continue;

						_charaAccData[_slotIndex] = HairAccessoryInfo.JsonClone();
					}
				}

				internal void Restore()
				{
					if (!_installed) return;

					int _coordinateIndex = _chaCtrl.fileStatus.coordinateType;
					object _extdataLink = GetExtDataLink(_coordinateIndex);
					if (_extdataLink == null) return;

					foreach (KeyValuePair<int, object> x in _charaAccData)
					{
						if (_extdataLink.RefTryGetValue(x.Key) != null)
						{
							//DebugMsg(LogLevel.Warning, $"[HairAccessoryCustomizer][Restore][{_chaCtrl.GetFullName()}][{x.Key}] remove HairAccessoryInfo");
							(_extdataLink as IDictionary).Remove(x.Key);
						}
						(_extdataLink as IDictionary).Add(x.Key, x.Value.JsonClone());
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

					public object Convert()
					{
						object _instance = Activator.CreateInstance(_types["HairAccessoryInfo"]);
						Traverse _traverse = Traverse.Create(_instance);
						_traverse.Field<bool>("HairGloss").Value = HairGloss;
						_traverse.Field<bool>("ColorMatch").Value = ColorMatch;
						_traverse.Field<Color>("OutlineColor").Value = OutlineColor;
						_traverse.Field<Color>("AccessoryColor").Value = AccessoryColor;
						_traverse.Field<float>("HairLength").Value = HairLength;
						return _instance;
					}
				}

				internal void UpdateAccessories(bool _updateHairInfo = true) // false would actually work
				{
					if (!_installed) return;

					_traverses["pluginCtrl"].Method("UpdateAccessories", new object[] { _updateHairInfo }).GetValue();
				}

				internal void CopyPartsInfo(AccessoryCopyEventArgs _args)
				{
					if (!_installed) return;

					_traverses["pluginCtrl"].Method("CopyAccessoriesHandler", new object[] { _args }).GetValue();
				}

				internal void TransferPartsInfo(AccessoryTransferEventArgs _args)
				{
					if (!_installed) return;

					_traverses["pluginCtrl"].Method("TransferAccessoriesHandler", new object[] { _args }).GetValue();
				}

				internal void RemovePartsInfo(int _slotIndex)
				{
					if (!_installed) return;

					_traverses["pluginCtrl"].Method("RemoveHairAccessoryInfo", new object[] { _slotIndex }).GetValue();
				}
			}
		}
	}
}
