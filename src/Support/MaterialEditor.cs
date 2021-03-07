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
		internal static class MaterialEditorSupport
		{
			private static BaseUnityPlugin _instance = null;
			private static Dictionary<string, Type> _types = new Dictionary<string, Type>();

			private static readonly List<string> _containerKeys = new List<string>() { "RendererPropertyList", "MaterialShaderList", "MaterialFloatPropertyList", "MaterialColorPropertyList", "MaterialTexturePropertyList" };

			internal static void Init()
			{
				BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue("com.deathweasel.bepinex.materialeditor", out PluginInfo _pluginInfo);
				_instance = _pluginInfo.Instance;
				SupportList.Add("MaterialEditor");

				Assembly _assembly = _instance.GetType().Assembly;
				_types["MaterialAPI"] = _assembly.GetType("MaterialEditorAPI.MaterialAPI");
				_types["MaterialEditorCharaController"] = _assembly.GetType("KK_Plugins.MaterialEditor.MaterialEditorCharaController");
				_types["ObjectType"] = _assembly.GetType("KK_Plugins.MaterialEditor.MaterialEditorCharaController+ObjectType");
			}

			internal static CharaCustomFunctionController GetController(ChaControl _chaCtrl) => Traverse.Create(_instance).Method("GetCharaController", new object[] { _chaCtrl }).GetValue<CharaCustomFunctionController>();

			internal class UrineBag
			{
				private readonly ChaControl _chaCtrl;
				private readonly CharaCustomFunctionController _pluginCtrl;
				private Dictionary<string, object> _extdataLink = new Dictionary<string, object>();
				private Dictionary<string, object> _charaAccData = new Dictionary<string, object>();
				private Dictionary<int, byte[]> _texData = new Dictionary<int, byte[]>();

				internal UrineBag(ChaControl ChaControl)
				{
					_chaCtrl = ChaControl;
					_pluginCtrl = GetController(_chaCtrl);

					foreach (string _key in _containerKeys)
					{
						_extdataLink[_key] = Traverse.Create(_pluginCtrl).Field(_key).GetValue();
						_charaAccData[_key] = _extdataLink[_key].JsonClone();
						Traverse.Create(_charaAccData[_key]).Method("Clear").GetValue();
					}
				}

				internal void Reset()
				{
					foreach (string _key in _containerKeys)
					{
						_extdataLink[_key] = Traverse.Create(_pluginCtrl).Field(_key).GetValue();
						Traverse.Create(_charaAccData[_key]).Method("Clear").GetValue();
					}
					_texData.Clear();
				}

				internal Dictionary<string, string> Save()
				{
					Dictionary<string, string> _json = new Dictionary<string, string>();
					foreach (string _key in _containerKeys)
						_json[_key] = JSONSerializer.Serialize(_charaAccData[_key].GetType(), _charaAccData[_key]);
					return _json;
				}

				internal void Load(Dictionary<string, string> _json)
				{
					Reset();
					if (_json == null) return;
					foreach (string _key in _containerKeys)
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

					foreach (string _key in _containerKeys)
					{
						int n = Traverse.Create(_extdataLink[_key]).Property("Count").GetValue<int>();
						DebugMsg(LogLevel.Warning, $"[MaterialEditor][Backup][{_chaCtrl.GetFullname()}][_extdataLink[{_key}] count: {n}]");

						for (int i = 0; i < n; i++)
						{
							object x = _extdataLink[_key].RefElementAt(i).JsonClone(); // should I null cheack this?

							if (Traverse.Create(x).Field("ObjectType").GetValue<int>() != (int) ObjectType.Accessory) continue;
							if (Traverse.Create(x).Field("CoordinateIndex").GetValue<int>() != _coordinateIndex) continue;
							if (_slots.IndexOf(Traverse.Create(x).Field("Slot").GetValue<int>()) < 0) continue;

							Traverse.Create(x).Field("CoordinateIndex").SetValue(-1);
							Traverse.Create(_charaAccData[_key]).Method("Add", new object[] { x }).GetValue();
						}

						DebugMsg(LogLevel.Warning, $"[MaterialEditor][Backup][{_chaCtrl.GetFullname()}][_charaAccData[{_key}] count: {Traverse.Create(_charaAccData[_key]).Property("Count").GetValue<int>()}]");
						//string json = JSONSerializer.Serialize(_charaAccData[_key].GetType(), _charaAccData[_key], true);
						//DebugMsg(LogLevel.Warning, $"{_charaAccData[_key].GetType()}\n" + json);
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

					foreach (string _key in _containerKeys)
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
					RemovePartsInfo(ev.DestinationSlotIndex);

					int _coordinateIndex = _chaCtrl.fileStatus.coordinateType;
					foreach (string _key in _containerKeys)
					{
						int n = Traverse.Create(_extdataLink[_key]).Property("Count").GetValue<int>();
						for (int i = 0; i < n; i++)
						{
							object _copy = MoveSlot(_extdataLink[_key].RefElementAt(i).JsonClone(), _coordinateIndex, ev.SourceSlotIndex, ev.DestinationSlotIndex);
							if (_copy != null)
								Traverse.Create(_extdataLink[_key]).Method("Add", new object[] { _copy }).GetValue();
						}
					}
				}

				internal void RemovePartsInfo(int _slotIndex)
				{
					Traverse.Create(_pluginCtrl).Method("AccessoryKindChangeEvent", new object[] { null, new AccessorySlotEventArgs(_slotIndex) }).GetValue();
				}

				internal Dictionary<int, byte[]> TexContainer
				{
					get { return _texData; }
					set { _texData = value; }
				}

				private object MoveCoordinateIndex(object _obj, int _srcCoordinateIndex, int _dstCoordinateIndex)
				{
					if (_obj == null) return null;
					Traverse _traverse = Traverse.Create(_obj);
					if (_traverse.Field("ObjectType").GetValue<int>() != (int) ObjectType.Accessory) return null;
					if (_traverse.Field("CoordinateIndex").GetValue<int>() != _srcCoordinateIndex) return null;
					_traverse.Field("CoordinateIndex").SetValue(_dstCoordinateIndex);
					return _obj;
				}

				private object MoveSlot(object _obj, int _coordinateIndex, int _srcSlotIndex, int _dstSlotIndex)
				{
					if (_obj == null) return null;
					Traverse _traverse = Traverse.Create(_obj);
					if (_traverse.Field("ObjectType").GetValue<int>() != (int) ObjectType.Accessory) return null;
					if (_traverse.Field("CoordinateIndex").GetValue<int>() != _coordinateIndex) return null;
					if (_traverse.Field("Slot").GetValue<int>() != _srcSlotIndex) return null;
					_traverse.Field("Slot").SetValue(_dstSlotIndex);
					return _obj;
				}
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
