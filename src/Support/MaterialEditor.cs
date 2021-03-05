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
using HarmonyLib;

using KKAPI.Chara;
using KKAPI.Maker;

namespace CharacterAccessory
{
	public partial class CharacterAccessory
	{
		internal static class MaterialEditorSupport
		{
			internal static BaseUnityPlugin _instance = null;
			internal static Dictionary<string, Type> _types = new Dictionary<string, Type>();

			private static readonly List<string> ContainerKeys = new List<string>() { "RendererPropertyList", "MaterialShaderList", "MaterialFloatPropertyList", "MaterialColorPropertyList", "MaterialTexturePropertyList" };

			internal static void Init()
			{
				BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue("com.deathweasel.bepinex.materialeditor", out PluginInfo _pluginInfo);
				_instance = _pluginInfo.Instance;

				_types["MaterialAPI"] = _instance.GetType().Assembly.GetType("MaterialEditorAPI.MaterialAPI");
				_types["MaterialEditorCharaController"] = _instance.GetType().Assembly.GetType("KK_Plugins.MaterialEditor.MaterialEditorCharaController");
				_types["ObjectType"] = _instance.GetType().Assembly.GetType("KK_Plugins.MaterialEditor.MaterialEditorCharaController+ObjectType");
#if DEBUG
				//HooksInstance["General"].Patch(_types["MaterialAPI"].GetMethod("SetTexture", AccessTools.all, null, new[] { typeof(GameObject), typeof(string), typeof(string), typeof(Texture2D) }, null), prefix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.MaterialAPI_SetTexture_Prefix)));
				//HooksInstance["General"].Patch(_types["MaterialEditorCharaController"].GetMethod("FindGameObject", AccessTools.all, null, new[] { _types["ObjectType"], typeof(int) }, null), prefix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.MaterialEditorCharaController_FindGameObject_Prefix)));
				//HooksInstance["General"].Patch(_types["MaterialEditorCharaController"].GetMethod("FindGameObject", AccessTools.all, null, new[] { _types["ObjectType"], typeof(int) }, null), postfix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.MaterialEditorCharaController_FindGameObject_Postfix)));
				//HooksInstance["General"].Patch(_types["MaterialEditorCharaController"].GetMethod("LoadData", AccessTools.all, null, new[] { typeof(bool), typeof(bool), typeof(bool) }, null), prefix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.MaterialEditorCharaController_LoadData_Prefix)));
#endif
			}

			internal static class Hooks
			{
#if DEBUG
				/*
				internal static bool MaterialEditorCharaController_LoadData_Prefix(CharaCustomFunctionController __instance)
				{
					if (!CfgMEHookLoadData.Value) return true;

					CharacterAccessoryController _controller = CharacterAccessory.GetController(__instance.ChaControl);
					if (!_controller.DuringLoading) return true;
					return false;
				}
				internal static bool MaterialAPI_SetTexture_Prefix(GameObject __0, string __1)
				{
					if (__0 == null)
						return true; // let ME handle it

					ChaControl _chaCtrl = __0?.GetComponentInParent<ChaControl>();
					if (__0 != null && _chaCtrl == null) return true;
					CharacterAccessoryController _controller = CharacterAccessory.GetController(_chaCtrl);
					if (_controller == null) return true;
					if (!_controller.DuringLoading) return true;

					return false;
				}
				internal static bool MaterialEditorCharaController_FindGameObject_Prefix(CharaCustomFunctionController __instance, ObjectType __0, int __1)
				{
					string name = Traverse.Create(__0).Method("ToString").GetValue<string>();
					if (name == "Accessory")
					{
						Logger.LogWarning($"[MaterialEditorCharaController_FindGameObject_Prefix][{__instance.ChaControl.GetFullname()}][{name}][{__1}][{__instance.ChaControl.GetAccessoryObject(__1)?.name}]");
					}
					return true; // let ME handle it
				}

				internal static void MaterialEditorCharaController_FindGameObject_Postfix(CharaCustomFunctionController __instance, GameObject __result, ObjectType __0, int __1)
				{
					if (__0.ToString() == "Accessory")
					{
						Logger.LogWarning($"[MaterialEditorCharaController_FindGameObject_Postfix][{__instance.ChaControl.GetFullname()}][{__result?.name}][{__1}][{__instance.ChaControl.GetAccessoryObject(__1)?.name}]");
					}
					return; // let ME handle it
				}
				*/
#endif
			}

			internal static CharaCustomFunctionController GetController(ChaControl _chaCtrl) => Traverse.Create(_instance).Method("GetCharaController", new object[] { _chaCtrl }).GetValue<CharaCustomFunctionController>();

			internal class UrineBag
			{
				private readonly ChaControl _chaCtrl;
				private readonly CharaCustomFunctionController _pluginCtrl;
				private Dictionary<string, object> _extdataLink = new Dictionary<string, object>();
				private Dictionary<string, object> _charaAccData = new Dictionary<string, object>();
				internal Dictionary<int, byte[]> _texData = new Dictionary<int, byte[]>();

				internal UrineBag(ChaControl ChaControl)
				{
					_chaCtrl = ChaControl;
					_pluginCtrl = GetController(_chaCtrl);

					foreach (string _key in ContainerKeys)
					{
						_extdataLink[_key] = Traverse.Create(_pluginCtrl).Field(_key).GetValue();
						_charaAccData[_key] = _extdataLink[_key].JsonClone();
						Traverse.Create(_charaAccData[_key]).Method("Clear").GetValue();
					}
				}

				internal void Reset()
				{
					foreach (string _key in ContainerKeys)
					{
						_extdataLink[_key] = Traverse.Create(_pluginCtrl).Field(_key).GetValue();
						Traverse.Create(_charaAccData[_key]).Method("Clear").GetValue();
					}
					_texData.Clear();
				}

				internal Dictionary<string, string> Save()
				{
					Dictionary<string, string> ContainerJson = new Dictionary<string, string>();
					foreach (string _key in ContainerKeys)
						ContainerJson[_key] = JSONSerializer.Serialize(_charaAccData[_key].GetType(), _charaAccData[_key]);
					return ContainerJson;
				}

				internal void Load(Dictionary<string, string> _json)
				{
					Reset();
					if (_json == null) return;
					foreach (string _key in ContainerKeys)
					{
						if (!_json.ContainsKey(_key)) continue;
						if (_json[_key] == null) continue;
						_charaAccData[_key] = JSONSerializer.Deserialize(_charaAccData[_key].GetType(), _json[_key]);
					}
				}

				internal void Backup()
				{
					Reset();
					CharacterAccessoryController _controller = CharacterAccessory.GetController(_chaCtrl);
					int _coordinateIndex = _chaCtrl.fileStatus.coordinateType;
					List<int> _slots = _controller.PartsInfo.Keys.ToList();

					foreach (string _key in ContainerKeys)
					{
						int n = Traverse.Create(_extdataLink[_key]).Property("Count").GetValue<int>();
						Logger.LogWarning($"_extdataLink[{_key}] count: {n}");

						for (int i = 0; i < n; i++)
						{
							object x = _extdataLink[_key].RefElementAt(i).JsonClone();

							if (Traverse.Create(x).Field("ObjectType").GetValue<int>() != (int) ObjectType.Accessory) continue;
							if (Traverse.Create(x).Field("CoordinateIndex").GetValue<int>() != _coordinateIndex) continue;
							if (_slots.IndexOf(Traverse.Create(x).Field("Slot").GetValue<int>()) < 0) continue;

							Traverse.Create(x).Field("CoordinateIndex").SetValue(-1);
							Traverse.Create(_charaAccData[_key]).Method("Add", new object[] { x }).GetValue();
						}

						Logger.LogWarning($"_charaAccData[{_key}] count: {Traverse.Create(_charaAccData[_key]).Property("Count").GetValue<int>()}");
						//string json = JSONSerializer.Serialize(_charaAccData[_key].GetType(), _charaAccData[_key], true);
						//Logger.LogWarning($"{_charaAccData[_key].GetType()}\n" + json);
					}

					object TextureDictionary = Traverse.Create(_pluginCtrl).Field("TextureDictionary").GetValue();
					foreach (object x in _charaAccData["MaterialTexturePropertyList"] as IList)
					{
						int? TexID = Traverse.Create(x).Field("TexID").GetValue<int?>();
						if (TexID != null)
						{
							if (_texData.ContainsKey((int) TexID)) continue;

							object _tex = TextureDictionary.RefTryGetValue(TexID);
							if (_tex != null)
							{
								_texData[(int) TexID] = Traverse.Create(_tex).Property("Data").GetValue<byte[]>();
								Logger.LogWarning($"[TexID: {TexID}][Length: {_texData[(int) TexID].Length}]");
							}
						}
					}
				}

				internal void Restore()
				{
					int _coordinateIndex = _chaCtrl.fileStatus.coordinateType;

					Dictionary<int, int> _mapping = new Dictionary<int, int>();
					foreach (KeyValuePair<int, byte[]> x in _texData)
					{
						int _id = Traverse.Create(_pluginCtrl).Method("SetAndGetTextureID", new object[] { x.Value }).GetValue<int>();
						_mapping[x.Key] = _id;
					}

					foreach (string _key in ContainerKeys)
					{
						int n = Traverse.Create(_charaAccData[_key]).Property("Count").GetValue<int>();
						for (int i = 0; i < n; i++)
						{
							object x = _charaAccData[_key].RefElementAt(i).JsonClone();
							Traverse.Create(x).Field("CoordinateIndex").SetValue(_coordinateIndex);

							if (_key == "MaterialTexturePropertyList")
							{
								int? TexID = Traverse.Create(x).Field("TexID").GetValue<int?>();
								if (TexID != null)
									Traverse.Create(x).Field("TexID").SetValue(_mapping[(int) TexID]);
							}

							Traverse.Create(_extdataLink[_key]).Method("Add", new object[] { x }).GetValue();
						}
					}
				}

				internal void CopyPartsInfo(AccessoryCopyEventArgs ev)
				{
					Traverse.Create(_pluginCtrl).Method("AccessoriesCopiedEvent", new object[] { null, ev }).GetValue();
				}

				internal void TransferPartsInfo(AccessoryTransferEventArgs ev)
				{
					//Traverse.Create(_pluginCtrl).Method("AccessoryTransferredEvent", new object[] { null, ev }).GetValue();

					RemovePartsInfo(ev.DestinationSlotIndex);

					int _coordinateIndex = _chaCtrl.fileStatus.coordinateType;
					foreach (string _key in ContainerKeys)
					{
						int n = Traverse.Create(_extdataLink[_key]).Property("Count").GetValue<int>();

						for (int i = 0; i < n; i++)
						{
							object x = _extdataLink[_key].RefElementAt(i).JsonClone();

							if (Traverse.Create(x).Field("ObjectType").GetValue<int>() != (int) ObjectType.Accessory) continue;
							if (Traverse.Create(x).Field("CoordinateIndex").GetValue<int>() != _coordinateIndex) continue;
							if (Traverse.Create(x).Field("Slot").GetValue<int>() != ev.SourceSlotIndex) continue;

							Traverse.Create(x).Field("Slot").SetValue(ev.DestinationSlotIndex);
							Traverse.Create(_extdataLink[_key]).Method("Add", new object[] { x }).GetValue();
						}
					}
				}

				internal void RemovePartsInfo(int _slotIndex)
				{
					Traverse.Create(_pluginCtrl).Method("AccessoryKindChangeEvent", new object[] { null, new AccessorySlotEventArgs(_slotIndex) }).GetValue();
				}

				/*
				private void MoveCoordinateIndex(object _obj, int _src, int _dst)
				{
					if (_obj == null) return;
					if (Traverse.Create(_obj).Field("ObjectType").GetValue<int>() != (int) ObjectType.Accessory) return;
					if (Traverse.Create(_obj).Field("CoordinateIndex").GetValue<int>() != _src) return;
					Traverse.Create(_obj).Field("CoordinateIndex").SetValue(_dst);
				}

				private void MoveSlot(object _obj, int _co, int _src, int _dst)
				{
					if (_obj == null) return;
					if (Traverse.Create(_obj).Field("ObjectType").GetValue<int>() != (int) ObjectType.Accessory) return;
					if (Traverse.Create(_obj).Field("CoordinateIndex").GetValue<int>() != _co) return;
					if (Traverse.Create(_obj).Field("Slot").GetValue<int>() != _src) return;
					Traverse.Create(_obj).Field("Slot").SetValue(_dst);
				}
				*/
			}

			public enum ObjectType
			{
				Unknown,
				Clothing,
				Accessory,
				Hair,
				Character
			};
		}
	}
}
