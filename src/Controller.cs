using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;

using ChaCustom;
using UnityEngine;
using UniRx;
using MessagePack;
using Studio;

using BepInEx.Logging;
using HarmonyLib;
using ExtensibleSaveFormat;
using Sideloader.AutoResolver;

using KKAPI;
using KKAPI.Chara;
using KKAPI.Maker;

namespace CharacterAccessory
{
	public partial class CharacterAccessory
	{
		public static CharacterAccessoryController GetController(ChaControl ChaControl) => ChaControl?.gameObject?.GetComponent<CharacterAccessoryController>();
		public static CharacterAccessoryController GetController(OCIChar OCIChar) => OCIChar?.charInfo?.gameObject?.GetComponent<CharacterAccessoryController>();

		public partial class CharacterAccessoryController : CharaCustomFunctionController
		{
			internal HairAccessoryCustomizerSupport.UrineBag HairAccessoryCustomizer;
			internal MaterialEditorSupport.UrineBag MaterialEditor;
			internal MaterialRouterSupport.UrineBag MaterialRouter;
			internal AccStateSyncSupport.UrineBag AccStateSync;
			internal DynamicBoneEditorSupport.UrineBag DynamicBoneEditor;

			internal Dictionary<int, ChaFileAccessory.PartsInfo> PartsInfo = new Dictionary<int, ChaFileAccessory.PartsInfo>();
			internal Dictionary<int, ResolveInfo> PartsResolveInfo = new Dictionary<int, ResolveInfo>();

			internal int ReferralIndex = -1;
			internal bool FunctionEnable = false;
			internal bool DuringLoading = false;
			internal int CurrentCoordinateIndex => ChaControl.fileStatus.coordinateType;

			protected override void Start()
			{
				HairAccessoryCustomizer = new HairAccessoryCustomizerSupport.UrineBag(ChaControl);
				MaterialEditor = new MaterialEditorSupport.UrineBag(ChaControl);
				MaterialRouter = new MaterialRouterSupport.UrineBag(ChaControl);
				AccStateSync = new AccStateSyncSupport.UrineBag(ChaControl);
				DynamicBoneEditor = new DynamicBoneEditorSupport.UrineBag(ChaControl);

				CurrentCoordinate.Subscribe(value => { OnCoordinateChanged(); });
				base.Start();
			}

			private void OnCoordinateChanged()
			{
				DuringLoading = false;
			}

			protected override void OnCardBeingSaved(GameMode currentGameMode)
			{
				DuringLoading = false;

				PluginData ExtendedData = new PluginData();

				ExtendedData.data.Add("MoreAccessoriesExtdata", MessagePackSerializer.Serialize(PartsInfo));
				ExtendedData.data.Add("ResolutionInfoExtdata", MessagePackSerializer.Serialize(PartsResolveInfo));

				foreach (string _name in SupportList)
				{
					var _extData = Traverse.Create(this).Field(_name).Method("Save").GetValue();
					ExtendedData.data.Add($"{_name}Extdata", MessagePackSerializer.Serialize(_extData));
				}

				ExtendedData.data.Add("FunctionEnable", FunctionEnable);
				ExtendedData.data.Add("ReferralIndex", ReferralIndex);
				ExtendedData.data.Add("TextureContainer", MessagePackSerializer.Serialize(MaterialEditor.TexContainer));

				SetExtendedData(ExtendedData);
			}

			protected override void OnReload(GameMode currentGameMode)
			{
				DuringLoading = false;

				PluginData ExtendedData = GetExtendedData();
				PartsInfo.Clear();
				PartsResolveInfo.Clear();
				FunctionEnable = false;
				ReferralIndex = RefMax;
				MaterialEditor.Reset();

				if (ExtendedData != null)
				{
					if (ExtendedData.data.TryGetValue("MoreAccessoriesExtdata", out object loadedMoreAccessoriesExtdata) && loadedMoreAccessoriesExtdata != null)
						PartsInfo = MessagePackSerializer.Deserialize<Dictionary<int, ChaFileAccessory.PartsInfo>>((byte[]) loadedMoreAccessoriesExtdata);
					if (ExtendedData.data.TryGetValue("ResolutionInfoExtdata", out object loadedResolutionInfoExtdata) && loadedResolutionInfoExtdata != null)
						PartsResolveInfo = MessagePackSerializer.Deserialize<Dictionary<int, ResolveInfo>>((byte[]) loadedResolutionInfoExtdata);

					foreach (string _name in SupportList)
					{
						if (ExtendedData.data.TryGetValue($"{_name}Extdata", out object loadedExtdata) && loadedExtdata != null)
						{
							if ((_name == "HairAccessoryCustomizer") || (_name == "AccStateSync"))
								Traverse.Create(this).Field(_name).Method("Load", new object[] { MessagePackSerializer.Deserialize<Dictionary<int, string>>((byte[]) loadedExtdata) }).GetValue();
							else if (_name == "MaterialEditor")
								Traverse.Create(this).Field(_name).Method("Load", new object[] { MessagePackSerializer.Deserialize<Dictionary<string, string>>((byte[]) loadedExtdata) }).GetValue();
							else if ((_name == "MaterialRouter") || (_name == "DynamicBoneEditor"))
								Traverse.Create(this).Field(_name).Method("Load", new object[] { MessagePackSerializer.Deserialize<List<string>>((byte[]) loadedExtdata) }).GetValue();
						}
					}

					if (ExtendedData.data.TryGetValue("FunctionEnable", out object loadedFunctionEnable) && loadedFunctionEnable != null)
						FunctionEnable = (bool) loadedFunctionEnable;
					if (ExtendedData.data.TryGetValue("ReferralIndex", out object loadedReferralIndex) && loadedReferralIndex != null)
						SetReferralIndex((int) loadedReferralIndex);
					if (ExtendedData.data.TryGetValue("TextureContainer", out object loadedTextureContainer) && loadedTextureContainer != null)
						MaterialEditor.TexContainer = MessagePackSerializer.Deserialize<Dictionary<int, byte[]>>((byte[]) loadedTextureContainer);

					foreach (KeyValuePair<int, ChaFileAccessory.PartsInfo> _part in PartsInfo)
					{
						if (!PartsResolveInfo.ContainsKey(_part.Key)) continue;
						if (PartsResolveInfo[_part.Key] == null) continue;

						ResolveInfo _info = PartsResolveInfo[_part.Key];
						MigrateData(ref _info);

						if (_info != null)
						{
							if (!_info.GUID.IsNullOrEmpty())
								_info = UniversalAutoResolver.TryGetResolutionInfo(PartsResolveInfo[_part.Key].Slot, PartsResolveInfo[_part.Key].CategoryNo, PartsResolveInfo[_part.Key].GUID);
							PartsResolveInfo[_part.Key] = _info.JsonClone() as ResolveInfo;
							_part.Value.id = _info.LocalSlot;
						}
						else
							PartsResolveInfo[_part.Key] = null;
					}
				}

				if (MakerAPI.InsideAndLoaded)
				{
					MakerToggleEnable.Value = FunctionEnable;
					MakerDropdownRef.Value = ReferralIndex;
				}

				base.OnReload(currentGameMode);
			}

			protected override void OnCoordinateBeingLoaded(ChaFileCoordinate _coordinate)
			{
				DuringLoading = false;
				bool go = true;

				DebugMsg(LogLevel.Warning, $"[OnCoordinateBeingLoaded][{ChaControl.GetFullname()}][FunctionEnable: {FunctionEnable}][ReferralIndex: {ReferralIndex}][PartsInfo.Count: {PartsInfo.Count}]");

				if (!FunctionEnable)
					go = false;
				if (ReferralIndex == 7 && PartsInfo.Count == 0)
					go = false;
				if (MakerAPI.InsideAndLoaded && !CfgMakerMasterSwitch.Value)
					go = false;

				if (go)
				{
					TaskLock();
					ChaControl.StartCoroutine(OnCoordinateBeingLoadedCoroutine());
				}
				base.OnCoordinateBeingLoaded(_coordinate);
			}

			internal IEnumerator OnCoordinateBeingLoadedCoroutine()
			{
				DebugMsg(LogLevel.Warning, $"[OnCoordinateBeingLoadedCoroutine][{ChaControl.GetFullname()}] fired");

				yield return new WaitForEndOfFrame();
				yield return new WaitForEndOfFrame();

				TaskLock();
				PrepareQueue();
			}

			internal IEnumerator RefreshCoroutine()
			{
				DebugMsg(LogLevel.Warning, $"[RefreshCoroutine][{ChaControl.GetFullname()}] fired");

				yield return new WaitForEndOfFrame();
				yield return new WaitForEndOfFrame();

				TaskUnlock();

				if (CharaStudio.Running)
				{
					if (CfgStudioFallbackReload.Value)
						BigReload();
					else
						FastReload();
				}
				else
				{
					ChaControl.ChangeCoordinateTypeAndReload(false);

					if (MakerAPI.InsideAndLoaded)
						CustomBase.Instance.updateCustomUI = true;
				}
				ChaControl.StartCoroutine(PreviewCoroutine());
			}

			internal IEnumerator PreviewCoroutine()
			{
				DebugMsg(LogLevel.Warning, $"[PreviewCoroutine][{ChaControl.GetFullname()}] fired");

				yield return new WaitForEndOfFrame();
				yield return new WaitForEndOfFrame();

				AccStateSync.InitCurOutfitTriggerInfo("OnCoordinateBeingLoaded");

				if (CharaStudio.Loaded)
					ChaControl.StartCoroutine(RefreshCharaStatePanelCoroutine());
			}

			internal IEnumerator RefreshCharaStatePanelCoroutine()
			{
				DebugMsg(LogLevel.Warning, $"[RefreshCharaStatePanelCoroutine][{ChaControl.GetFullname()}] fired");

				yield return new WaitForEndOfFrame();
				yield return new WaitForEndOfFrame();

				HairAccessoryCustomizer.UpdateAccessories(false);
				//AccStateSync.SyncAllAccToggle();
				CharaStudio.RefreshCharaStatePanel();
				MoreAccessoriesSupport.UpdateStudioUI(ChaControl);
			}

			internal void SetReferralIndex(int _index)
			{
				DebugMsg(LogLevel.Warning, $"[SetReferralIndex][{ChaControl.GetFullname()}][_index: {_index}]");

				if (ReferralIndex != _index)
					ReferralIndex = MathfEx.RangeEqualOn(0, _index, RefMax) ? _index : RefMax;
			}

			internal void FastReload(bool _noLoadStatus = true)
			{
				byte[] _buffer = null;
				using (MemoryStream _memoryStream = new MemoryStream())
				{
					using (BinaryWriter _writer = new BinaryWriter(_memoryStream))
					{
						ChaControl.chaFile.SaveCharaFile(_writer, false);
						_buffer = _memoryStream.ToArray();
					}
				}
				using (MemoryStream _input = new MemoryStream(_buffer))
				{
					using (BinaryReader _reader = new BinaryReader(_input))
					{
						ChaControl.chaFile.LoadCharaFile(_reader, true, _noLoadStatus);
					}
				}
			}

			internal void BigReload()
			{
				string CardPath = Path.Combine(Path.GetTempPath(), Path.GetFileNameWithoutExtension(BepInEx.Paths.ExecutablePath) + "_CA.png");
				using (FileStream fileStream = new FileStream(CardPath, FileMode.Create, FileAccess.Write))
					ChaControl.chaFile.SaveCharaFile(fileStream, true);
				Studio.Studio.Instance.dicInfo.Values.OfType<OCIChar>().FirstOrDefault(x => x.charInfo == ChaControl).ChangeChara(CardPath);
			}
		}
	}
}
