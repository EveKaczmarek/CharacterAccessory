using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;

using ChaCustom;
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
using JetPack;

namespace CharacterAccessory
{
	public partial class CharacterAccessory
	{
		public static CharacterAccessoryController GetController(ChaControl ChaControl) => ChaControl?.gameObject?.GetComponent<CharacterAccessoryController>();
		public static CharacterAccessoryController GetController(OCIChar OCIChar) => GetController(OCIChar?.charInfo);

		public partial class CharacterAccessoryController : CharaCustomFunctionController
		{
			internal HairAccessoryCustomizerSupport.UrineBag HairAccessoryCustomizer;
			internal MaterialEditorSupport.UrineBag MaterialEditor;
			internal MaterialRouterSupport.UrineBag MaterialRouter;
			internal AccStateSyncSupport.UrineBag AccStateSync;
			internal DynamicBoneEditorSupport.UrineBag DynamicBoneEditor;
			internal AAAPKSupport.UrineBag AAAPK;
			internal BendUrAccSupport.UrineBag BendUrAcc;

			internal Dictionary<int, ChaFileAccessory.PartsInfo> PartsInfo = new Dictionary<int, ChaFileAccessory.PartsInfo>();
			internal Dictionary<int, ResolveInfo> PartsResolveInfo = new Dictionary<int, ResolveInfo>();

			internal int ReferralIndex = -1;
			internal bool FunctionEnable = false;
			internal bool AutoCopyToBlank = false;
			internal bool DuringLoading = false;
			internal int CurrentCoordinateIndex => ChaControl.fileStatus.coordinateType;

			protected override void Start()
			{
				if (KoikatuAPI.GetCurrentGameMode() == GameMode.MainGame)
					return;
				HairAccessoryCustomizer = new HairAccessoryCustomizerSupport.UrineBag(ChaControl);
				MaterialEditor = new MaterialEditorSupport.UrineBag(ChaControl);
				MaterialRouter = new MaterialRouterSupport.UrineBag(ChaControl);
				AccStateSync = new AccStateSyncSupport.UrineBag(ChaControl);
				DynamicBoneEditor = new DynamicBoneEditorSupport.UrineBag(ChaControl);
				AAAPK = new AAAPKSupport.UrineBag(ChaControl);
				BendUrAcc = new BendUrAccSupport.UrineBag(ChaControl);

				CurrentCoordinate.Subscribe(value => { OnCoordinateChanged(); });
				base.Start();
			}

			private void OnCoordinateChanged()
			{
				TaskUnlock();
				AutoCopyCheck();
			}

			protected override void OnCardBeingSaved(GameMode currentGameMode)
			{
				TaskUnlock();

				PluginData ExtendedData = new PluginData() { version = PluginDataVersion };

				ExtendedData.data.Add("MoreAccessoriesExtdata", MessagePackSerializer.Serialize(PartsInfo));
				ExtendedData.data.Add("ResolutionInfoExtdata", MessagePackSerializer.Serialize(PartsResolveInfo));

				foreach (string _name in SupportList)
				{
					var _extData = Traverse.Create(this).Field(_name).Method("Save").GetValue();
					ExtendedData.data.Add($"{_name}Extdata", MessagePackSerializer.Serialize(_extData));
				}

				ExtendedData.data.Add("FunctionEnable", FunctionEnable);
				ExtendedData.data.Add("AutoCopyToBlank", AutoCopyToBlank);
				ExtendedData.data.Add("ReferralIndex", ReferralIndex);
				ExtendedData.data.Add("TextureContainer", MessagePackSerializer.Serialize(MaterialEditor.TexContainer));

				SetExtendedData(ExtendedData);
			}

			protected override void OnReload(GameMode currentGameMode)
			{
				TaskUnlock();

				PluginData ExtendedData = GetExtendedData();
				PartsInfo.Clear();
				PartsResolveInfo.Clear();
				FunctionEnable = false;
				AutoCopyToBlank = false;
				ReferralIndex = -1;
				MaterialEditor.Reset();

				if (ExtendedData != null)
				{
					if (ExtendedData.version > PluginDataVersion)
					{
						Logger.Log(LogLevel.Error | LogLevel.Message, $"[OnReload] ExtendedData.version: {ExtendedData.version} is newer than your plugin");
						base.OnReload(currentGameMode);
						return;
					}
					else if (ExtendedData.version < PluginDataVersion)
						Logger.Log(LogLevel.Info, $"[OnReload] Migrating from ver. {ExtendedData.version}");

					if (ExtendedData.data.TryGetValue("MoreAccessoriesExtdata", out object loadedMoreAccessoriesExtdata) && loadedMoreAccessoriesExtdata != null)
						PartsInfo = MessagePackSerializer.Deserialize<Dictionary<int, ChaFileAccessory.PartsInfo>>((byte[]) loadedMoreAccessoriesExtdata);
					if (ExtendedData.data.TryGetValue("ResolutionInfoExtdata", out object loadedResolutionInfoExtdata) && loadedResolutionInfoExtdata != null)
						PartsResolveInfo = MessagePackSerializer.Deserialize<Dictionary<int, ResolveInfo>>((byte[]) loadedResolutionInfoExtdata);

					foreach (string _name in SupportList)
					{
						if (ExtendedData.data.TryGetValue($"{_name}Extdata", out object loadedExtdata) && loadedExtdata != null)
						{
							if (_name == "HairAccessoryCustomizer")
								Traverse.Create(this).Field(_name).Method("Load", new object[] { MessagePackSerializer.Deserialize<Dictionary<int, string>>((byte[]) loadedExtdata) }).GetValue();
							else if (_name == "AccStateSync")
							{
								if (ExtendedData.version < 2)
									Traverse.Create(this).Field(_name).Method("Migrate", new object[] { MessagePackSerializer.Deserialize<Dictionary<int, string>>((byte[]) loadedExtdata) }).GetValue();
								else
									Traverse.Create(this).Field(_name).Method("Load", new object[] { MessagePackSerializer.Deserialize<Dictionary<string, string>>((byte[]) loadedExtdata) }).GetValue();
							}
							else if (_name == "MaterialEditor")
								Traverse.Create(this).Field(_name).Method("Load", new object[] { MessagePackSerializer.Deserialize<Dictionary<string, string>>((byte[]) loadedExtdata) }).GetValue();
							else if ((_name == "MaterialRouter") || (_name == "DynamicBoneEditor") || (_name == "AAAPK") || (_name == "BendUrAcc"))
								Traverse.Create(this).Field(_name).Method("Load", new object[] { MessagePackSerializer.Deserialize<List<string>>((byte[]) loadedExtdata) }).GetValue();
						}
					}

					if (ExtendedData.data.TryGetValue("FunctionEnable", out object loadedFunctionEnable) && loadedFunctionEnable != null)
						FunctionEnable = (bool) loadedFunctionEnable;
					if (ExtendedData.data.TryGetValue("AutoCopyToBlank", out object loadedAutoCopyToBlank) && loadedAutoCopyToBlank != null)
						AutoCopyToBlank = (bool) loadedAutoCopyToBlank;
					
					if (ExtendedData.data.TryGetValue("ReferralIndex", out object loadedReferralIndex) && loadedReferralIndex != null)
					{
						if (ExtendedData.version < 3)
							SetReferralIndex(-1);
						else
							SetReferralIndex((int) loadedReferralIndex);

						DebugMsg(LogLevel.Info, $"[OnReload][{ChaControl.GetFullName()}][ReferralIndex: {ReferralIndex}]");
					}
					if (ExtendedData.data.TryGetValue("TextureContainer", out object loadedTextureContainer) && loadedTextureContainer != null)
						MaterialEditor.TexContainer = MessagePackSerializer.Deserialize<Dictionary<int, byte[]>>((byte[]) loadedTextureContainer);

					if (PartsInfo?.Count > 0 && PartsResolveInfo?.Count > 0)
					{
						foreach (KeyValuePair<int, ChaFileAccessory.PartsInfo> _part in PartsInfo)
						{
							PartsResolveInfo.TryGetValue(_part.Key, out ResolveInfo _info);
							if (_info == null) continue;

							MigrateData(ref _info);
							if (_info == null || _info.GUID.IsNullOrWhiteSpace()) continue;

							_info = UniversalAutoResolver.TryGetResolutionInfo(PartsResolveInfo[_part.Key].Slot, PartsResolveInfo[_part.Key].CategoryNo, PartsResolveInfo[_part.Key].GUID);
							if (_info == null) continue;

							PartsResolveInfo[_part.Key] = _info.JsonClone() as ResolveInfo;
							_part.Value.id = _info.LocalSlot;
						}
					}
				}

				if (MakerAPI.InsideAndLoaded)
				{
					MakerToggleEnable.Value = FunctionEnable;
					MakerToggleAutoCopyToBlank.Value = AutoCopyToBlank;
#if DEBUG
					MoreOutfitsSupport.BuildMakerDropdownRef();
#endif
				}
#if DEBUG
				if (CharaStudio.Running && CharaStudio.CurOCIChar?.charInfo == ChaControl)
				{
					MoreOutfitsSupport.BuildStudioDropdownRef();
				}
#endif
				IEnumerator OnReloadCoroutine()
				{
					DebugMsg(LogLevel.Warning, $"[OnReloadCoroutine][{ChaControl.GetFullName()}] fired");

					yield return Toolbox.WaitForEndOfFrame;
					yield return Toolbox.WaitForEndOfFrame;

					AutoCopyCheck();
				}

				ChaControl.StartCoroutine(OnReloadCoroutine());

				base.OnReload(currentGameMode);
			}

			protected override void OnCoordinateBeingLoaded(ChaFileCoordinate _coordinate)
			{
				TaskUnlock();
				bool go = true;

				DebugMsg(LogLevel.Warning, $"[OnCoordinateBeingLoaded][{ChaControl.GetFullName()}][FunctionEnable: {FunctionEnable}][ReferralIndex: {ReferralIndex}][PartsInfo.Count: {PartsInfo.Count}]");

				if (!FunctionEnable)
					go = false;
				if (ReferralIndex == -1 && PartsInfo.Count == 0)
					go = false;
				if (MakerAPI.InsideAndLoaded && !_cfgMakerMasterSwitch.Value)
					go = false;

				CoordinateLoadFlags _loadFlags = MakerAPI.GetCoordinateLoadFlags();
				if (MakerAPI.InsideAndLoaded && _loadFlags != null && !_loadFlags.Accessories)
					go = false;

				if (go)
				{
					TaskLock();
					ChaControl.StartCoroutine(OnCoordinateBeingLoadedCoroutine());
				}
				else
				{
					if (MakerAPI.InsideAndLoaded)
						CustomBase.Instance.updateCustomUI = true;
				}
				base.OnCoordinateBeingLoaded(_coordinate);
			}

			internal IEnumerator OnCoordinateBeingLoadedCoroutine()
			{
				DebugMsg(LogLevel.Warning, $"[OnCoordinateBeingLoadedCoroutine][{ChaControl.GetFullName()}] fired");

				yield return Toolbox.WaitForEndOfFrame;
				yield return Toolbox.WaitForEndOfFrame;

				TaskLock();
				PrepareQueue();
			}

			internal IEnumerator RefreshCoroutine()
			{
				DebugMsg(LogLevel.Warning, $"[RefreshCoroutine][{ChaControl.GetFullName()}] fired");

				yield return Toolbox.WaitForEndOfFrame;
				yield return Toolbox.WaitForEndOfFrame;

				TaskUnlock();

				if (CharaStudio.Running)
				{
					if (_cfgStudioFallbackReload.Value)
						BigReload();
					else
					{
						FastReload();
						ChaControl.ChangeCoordinateTypeAndReload(false);
					}
				}
				else
				{
					ChaControl.ChangeCoordinateTypeAndReload(false);

					if (MakerAPI.InsideAndLoaded)
						CustomBase.Instance.updateCustomUI = true;
				}
				//StartCoroutine(PreviewCoroutine());
			}

			internal IEnumerator PreviewCoroutine()
			{
				DebugMsg(LogLevel.Warning, $"[PreviewCoroutine][{ChaControl.GetFullName()}] fired");

				yield return Toolbox.WaitForEndOfFrame;
				yield return Toolbox.WaitForEndOfFrame;

				AccStateSync.InitCurOutfitTriggerInfo("OnCoordinateBeingLoaded");

				if (CharaStudio.Loaded)
					StartCoroutine(RefreshCharaStatePanelCoroutine());
			}

			internal IEnumerator RefreshCharaStatePanelCoroutine()
			{
				DebugMsg(LogLevel.Warning, $"[RefreshCharaStatePanelCoroutine][{ChaControl.GetFullName()}] fired");

				yield return Toolbox.WaitForEndOfFrame;
				yield return Toolbox.WaitForEndOfFrame;

				HairAccessoryCustomizer.UpdateAccessories(false);
				//AccStateSync.SyncAllAccToggle();
				CharaStudio.RefreshCharaStatePanel();
				MoreAccessoriesSupport.UpdateStudioUI(ChaControl);
			}

			internal void SetReferralIndex(int _index)
			{
				if (ReferralIndex != _index)
				{
					if (_index >= ChaControl.chaFile.coordinate.Length || _index < 0)
						ReferralIndex = -1;
					else
						ReferralIndex = _index;
				}

				DebugMsg(LogLevel.Warning, $"[SetReferralIndex][{ChaControl.GetFullName()}][_index: {_index}][ReferralIndex: {ReferralIndex}]");
			}

			internal int GetReferralIndex()
			{
#if DEBUG
				if (CharaStudio.Running && CharaStudio.CurOCIChar?.charInfo == ChaControl)
				{
					MoreOutfitsSupport.BuildStudioDropdownRef();
				}
#endif
				int _index = ReferralIndex < 0 ? ChaControl.chaFile.coordinate.Length : ReferralIndex;
				DebugMsg(LogLevel.Info, $"[GetReferralIndex][{ChaControl.GetFullName()}][_index: {_index}][ReferralIndex: {ReferralIndex}]");

				return _index;
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

			internal string GetCordName() => GetCordName(CurrentCoordinateIndex);
			internal string GetCordName(int CoordinateIndex)
			{
				if (CoordinateIndex < CordNames.Count)
					return CordNames[CoordinateIndex];

				return $"Extra {CoordinateIndex - CordNames.Count + 1}";
			}
		}
	}
}
