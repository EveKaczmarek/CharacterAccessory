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

using KKAPI.Maker;
using JetPack;

using KK_Plugins;
using KK_Plugins.MaterialEditor;
using static KK_Plugins.MaterialEditor.MaterialEditorCharaController;

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
				_supportList.Add("MaterialEditor");

				Assembly _assembly = _instance.GetType().Assembly;
				_types["MaterialAPI"] = _assembly.GetType("MaterialEditorAPI.MaterialAPI");
				_types["MaterialEditorCharaController"] = _assembly.GetType("KK_Plugins.MaterialEditor.MaterialEditorCharaController");
				_types["ObjectType"] = _assembly.GetType("KK_Plugins.MaterialEditor.MaterialEditorCharaController+ObjectType");

				_legacy = _pluginInfo.Metadata.Version.CompareTo(new Version("3.0")) < 0;

				if (_legacy)
					_logger.LogWarning($"Material Editor version {_pluginInfo.Metadata.Version} found, running in legacy mode");
				else
					_containerKeys.Add("MaterialCopyList");

				_hooksInstance["General"].Patch(_types["MaterialEditorCharaController"].GetMethod("LoadData", AccessTools.all, null, new[] { typeof(bool), typeof(bool), typeof(bool) }, null), prefix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.DuringLoading_IEnumerator_Prefix)));
			}

			internal static MaterialEditorCharaController GetController(ChaControl _chaCtrl) => _chaCtrl?.gameObject?.GetComponent<MaterialEditorCharaController>();

			internal class UrineBag
			{
				private readonly ChaControl _chaCtrl;
				private readonly MaterialEditorCharaController _pluginCtrl;
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
						DebugMsg(LogLevel.Warning, $"[MaterialEditor][Backup][{_chaCtrl.GetFullName()}][_extdataLink[{_key}] count: {n}]");

						for (int i = 0; i < n; i++)
						{
							object x = _extdataLink[_key].RefElementAt(i).JsonClone(); // should I null cheack this?
							Traverse _traverse = Traverse.Create(x);

							if (_traverse.Field("ObjectType").Method("ToString").GetValue<string>() != "Accessory") continue;
							if (_traverse.Field("CoordinateIndex").GetValue<int>() != _coordinateIndex) continue;
							if (_slots.IndexOf(_traverse.Field("Slot").GetValue<int>()) < 0) continue;

							_traverse.Field("CoordinateIndex").SetValue(-1);
							(_charaAccData[_key] as IList).Add(x);
						}
					}

					foreach (MaterialTextureProperty x in _charaAccData["MaterialTexturePropertyList"] as List<MaterialTextureProperty>)
					{
						if (x.TexID != null)
						{
							int TexID = (int) x.TexID;
							if (_texData.ContainsKey(TexID)) continue;

							_pluginCtrl.TextureDictionary.TryGetValue(TexID, out TextureContainer _tex);
							if (_tex != null)
							{
								_texData[TexID] = _tex.Data;
								DebugMsg(LogLevel.Warning, $"[TexID: {TexID}][Length: {_texData[TexID].Length}]");
							}
						}
					}
				}

				internal void Restore()
				{
					int _coordinateIndex = _chaCtrl.fileStatus.coordinateType;

					Dictionary<int, int> _mapping = new Dictionary<int, int>();
					foreach (KeyValuePair<int, byte[]> x in _texData)
						_mapping[x.Key] = _pluginCtrl.SetAndGetTextureID(x.Value);

					foreach (string _key in _containerKeys)
					{
						int n = Traverse.Create(_charaAccData[_key]).Property("Count").GetValue<int>();
						for (int i = 0; i < n; i++)
						{
							object x = _charaAccData[_key].RefElementAt(i).JsonClone();
							Traverse _traverse = Traverse.Create(x);
							_traverse.Field("CoordinateIndex").SetValue(_coordinateIndex);

							if (_key == "MaterialTexturePropertyList")
							{
								int? TexID = _traverse.Field("TexID").GetValue<int?>();
								if (TexID != null)
									_traverse.Field("TexID").SetValue(_mapping[(int) TexID]);
							}
							(_extdataLink[_key] as IList).Add(x);
						}
					}
				}

				internal void CopyPartsInfo(AccessoryCopyEventArgs _args)
				{
					_pluginCtrl.AccessoriesCopiedEvent(null, _args);
				}

				internal void TransferPartsInfo(AccessoryTransferEventArgs _args)
				{
					RemovePartsInfo(_args.DestinationSlotIndex);

					int _coordinateIndex = _chaCtrl.fileStatus.coordinateType;
					foreach (string _key in _containerKeys)
					{
						int n = Traverse.Create(_extdataLink[_key]).Property("Count").GetValue<int>();
						for (int i = 0; i < n; i++)
						{
							object _copy = MoveSlot(_extdataLink[_key].RefElementAt(i).JsonClone(), _coordinateIndex, _args.SourceSlotIndex, _args.DestinationSlotIndex);
							if (_copy != null)
								(_extdataLink[_key] as IList).Add(_copy);
						}
					}
				}

				internal void RemovePartsInfo(int _slotIndex)
				{
					_pluginCtrl.AccessoryKindChangeEvent(null, new AccessorySlotEventArgs(_slotIndex));
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
