using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using UniRx;
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
		internal static class MaterialEditorSupport
		{
			internal static BaseUnityPlugin _instance = null;
			internal static bool _legacy = false;
			internal static readonly Dictionary<string, Type> _types = new Dictionary<string, Type>();

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

				_legacy = _pluginInfo.Metadata.Version.CompareTo(new Version("3.0")) < 0;

				if (_legacy)
					Logger.LogWarning($"Material Editor version {_pluginInfo.Metadata.Version} found, running in legacy mode");
				else
					_containerKeys.Add("MaterialCopyList");

				HooksInstance["General"].Patch(_types["MaterialEditorCharaController"].GetMethod("LoadData", AccessTools.all, null, new[] { typeof(bool), typeof(bool), typeof(bool) }, null), prefix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.DuringLoading_IEnumerator_Prefix)));
			}

			internal static CharaCustomFunctionController GetController(ChaControl _chaCtrl) => Traverse.Create(_instance).Method("GetCharaController", new object[] { _chaCtrl }).GetValue<CharaCustomFunctionController>();

			internal class UrineBag
			{
				private readonly ChaControl _chaCtrl;
				private readonly CharaCustomFunctionController _pluginCtrl;
				private readonly Dictionary<string, object> _extdataLink = new Dictionary<string, object>();
				private readonly Dictionary<string, object> _charaAccData = new Dictionary<string, object>();
				private Dictionary<int, byte[]> _texData = new Dictionary<int, byte[]>();

				internal UrineBag(ChaControl ChaControl)
				{
					_chaCtrl = ChaControl;
					_pluginCtrl = GetController(_chaCtrl);

					foreach (string _key in _containerKeys)
					{
						string _name = "KK_Plugins.MaterialEditor.MaterialEditorCharaController+" + _key.Replace("List", "");
						Type _type = _instance.GetType().Assembly.GetType(_name);
						Type _generic = typeof(List<>).MakeGenericType(_type);
						_charaAccData[_key] = Activator.CreateInstance(_generic);
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

							if (Traverse.Create(x).Field("ObjectType").Method("ToString").GetValue<string>() != "Accessory") continue;
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
								DebugMsg(LogLevel.Warning, $"[TexID: {TexID}][Length: {_texData[(int) TexID].Length}]");
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
					/*
					GameObject _gameObject = _chaCtrl.GetAccessoryObject(_slotIndex);
					if (_gameObject != null)
					{
						Renderer[] _renderers = _gameObject.GetComponentsInChildren<Renderer>();
						foreach (Renderer _renderer in _renderers)
						{
							foreach (Material _material in _renderer.materials)
							{
								PropertyInfo[] _infos = _material.GetType().GetProperties();
								foreach (PropertyInfo _info in _infos)
								{
									var _property = _info.GetValue(_material, null);
									if (_property != null && _property.GetType() == typeof(Texture2D))
										Destroy((Texture2D) _property);
								}
							}
						}
					}
					*/
					Traverse.Create(_pluginCtrl).Method("AccessoryKindChangeEvent", new object[] { null, new AccessorySlotEventArgs(_slotIndex) }).GetValue();
				}

				internal Dictionary<int, byte[]> TexContainer
				{
					get { return _texData; }
					set { _texData = value; }
				}

				private object MoveSlot(object _obj, int _coordinateIndex, int _srcSlotIndex, int _dstSlotIndex)
				{
					if (_obj == null) return null;
					Traverse _traverse = Traverse.Create(_obj);
					if (_traverse.Field("ObjectType").Method("ToString").GetValue<string>() != "Accessory") return null;
					if (_traverse.Field("CoordinateIndex").GetValue<int>() != _coordinateIndex) return null;
					if (_traverse.Field("Slot").GetValue<int>() != _srcSlotIndex) return null;
					_traverse.Field("Slot").SetValue(_dstSlotIndex);
					return _obj;
				}
			}
		}
	}
}
