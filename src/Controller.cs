using System.Collections;
using System.Collections.Generic;

using ChaCustom;
using UnityEngine;
using UniRx;
using MessagePack;
using Studio;

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

				Dictionary<int, string> HairAccessoryCustomizerExtdata = HairAccessoryCustomizer.Save();
				Dictionary<string, string> MaterialEditorExtdata = MaterialEditor.Save();
				List<string> MaterialRouterExtdata = MaterialRouter.Save();
				Dictionary<int, string> AccStateSyncExtdata = AccStateSync.Save();

				PluginData ExtendedData = new PluginData();
				ExtendedData.data.Add("MoreAccessoriesExtdata", MessagePackSerializer.Serialize(PartsInfo));
				ExtendedData.data.Add("ResolutionInfoExtdata", MessagePackSerializer.Serialize(PartsResolveInfo));
				ExtendedData.data.Add("HairAccessoryCustomizerExtdata", MessagePackSerializer.Serialize(HairAccessoryCustomizerExtdata));
				ExtendedData.data.Add("MaterialEditorExtdata", MessagePackSerializer.Serialize(MaterialEditorExtdata));
				ExtendedData.data.Add("MaterialRouterExtdata", MessagePackSerializer.Serialize(MaterialRouterExtdata));
				ExtendedData.data.Add("AccStateSyncExtdata", MessagePackSerializer.Serialize(AccStateSyncExtdata));

				ExtendedData.data.Add("FunctionEnable", FunctionEnable);
				ExtendedData.data.Add("ReferralIndex", ReferralIndex);
				ExtendedData.data.Add("TextureContainer", MessagePackSerializer.Serialize(MaterialEditor._texData));
				SetExtendedData(ExtendedData);
			}

			protected override void OnReload(GameMode currentGameMode)
			{
				DuringLoading = false;

				PluginData ExtendedData = GetExtendedData();
				PartsInfo.Clear();
				Dictionary<int, string> HairAccessoryCustomizerExtdata = new Dictionary<int, string>();
				Dictionary<string, string> MaterialEditorExtdata = new Dictionary<string, string>();
				List<string> MaterialRouterExtdata = new List<string>();
				Dictionary<int, string> AccStateSyncExtdata = new Dictionary<int, string>();
				FunctionEnable = false;
				ReferralIndex = RefMax;

				MaterialEditor.Reset();
				byte[] _tempHolder = null;
				MaterialEditor._texData.Clear();

				if (ExtendedData != null)
				{
					if (ExtendedData.data.TryGetValue("MoreAccessoriesExtdata", out object loadedMoreAccessoriesExtdata) && loadedMoreAccessoriesExtdata != null)
						PartsInfo = MessagePackSerializer.Deserialize<Dictionary<int, ChaFileAccessory.PartsInfo>>((byte[]) loadedMoreAccessoriesExtdata);
					if (ExtendedData.data.TryGetValue("ResolutionInfoExtdata", out object loadedResolutionInfoExtdata) && loadedResolutionInfoExtdata != null)
						PartsResolveInfo = MessagePackSerializer.Deserialize<Dictionary<int, ResolveInfo>>((byte[]) loadedResolutionInfoExtdata);
					if (ExtendedData.data.TryGetValue("HairAccessoryCustomizerExtdata", out object loadedHairAccessoryCustomizerExtdata) && loadedHairAccessoryCustomizerExtdata != null)
						HairAccessoryCustomizerExtdata = MessagePackSerializer.Deserialize<Dictionary<int, string>>((byte[]) loadedHairAccessoryCustomizerExtdata);
					if (ExtendedData.data.TryGetValue("MaterialEditorExtdata", out object loadedMaterialEditorExtdata) && loadedMaterialEditorExtdata != null)
						MaterialEditorExtdata = MessagePackSerializer.Deserialize<Dictionary<string, string>>((byte[]) loadedMaterialEditorExtdata);
					if (ExtendedData.data.TryGetValue("MaterialRouterExtdata", out object loadedMaterialRouterExtdata) && loadedMaterialRouterExtdata != null)
						MaterialRouterExtdata = MessagePackSerializer.Deserialize<List<string>>((byte[]) loadedMaterialRouterExtdata);
					if (ExtendedData.data.TryGetValue("AccStateSyncExtdata", out object loadedAccStateSyncExtdata) && loadedAccStateSyncExtdata != null)
						AccStateSyncExtdata = MessagePackSerializer.Deserialize<Dictionary<int, string>>((byte[]) loadedAccStateSyncExtdata);

					if (ExtendedData.data.TryGetValue("FunctionEnable", out object loadedFunctionEnable) && loadedFunctionEnable != null)
						FunctionEnable = (bool) loadedFunctionEnable;
					if (ExtendedData.data.TryGetValue("ReferralIndex", out object loadedReferralIndex) && loadedReferralIndex != null)
						SetReferralIndex((int) loadedReferralIndex);
					if (ExtendedData.data.TryGetValue("TextureContainer", out object loadedTextureContainer) && loadedTextureContainer != null)
						_tempHolder = (byte[]) loadedTextureContainer;

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

				HairAccessoryCustomizer.Load(HairAccessoryCustomizerExtdata);
				MaterialEditor.Load(MaterialEditorExtdata);
				MaterialRouter.Load(MaterialRouterExtdata);
				AccStateSync.Load(AccStateSyncExtdata);

				if (_tempHolder != null)
					MaterialEditor._texData = MessagePackSerializer.Deserialize<Dictionary<int, byte[]>>(_tempHolder);

				base.OnReload(currentGameMode);
			}

			protected override void OnCoordinateBeingLoaded(ChaFileCoordinate coordinate)
			{
				DuringLoading = false;
				bool go = true;

				Logger.LogWarning($"[OnCoordinateBeingLoaded][{ChaControl.GetFullname()}][FunctionEnable: {FunctionEnable}][ReferralIndex: {ReferralIndex}][PartsInfo.Count: {PartsInfo.Count}]");

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
				base.OnCoordinateBeingLoaded(coordinate);
			}

			internal IEnumerator OnCoordinateBeingLoadedCoroutine()
			{
				Logger.LogWarning($"[OnCoordinateBeingLoadedCoroutine][{ChaControl.GetFullname()}] fired");

				yield return new WaitForEndOfFrame();
				yield return new WaitForEndOfFrame();

				TaskLock();
				PrepareQueue();
			}

			internal IEnumerator RefreshCoroutine()
			{
				Logger.LogWarning($"[RefreshCoroutine][{ChaControl.GetFullname()}] fired");

				yield return new WaitForEndOfFrame();
				yield return new WaitForEndOfFrame();

				ChaControl.ChangeCoordinateTypeAndReload(false);

				if (MakerAPI.InsideAndLoaded)
					CustomBase.Instance.updateCustomUI = true;
				else if (CharaStudio.Loaded)
					CharaStudio.RefreshCharaStatePanel();

				ChaControl.StartCoroutine(PreviewCoroutine());
			}

			internal IEnumerator PreviewCoroutine()
			{
				Logger.LogWarning($"[PreviewCoroutine][{ChaControl.GetFullname()}] fired");

				yield return new WaitForEndOfFrame();
				yield return new WaitForEndOfFrame();
				/*
				AccStateSync.SetAccessoryStateAll();
				AccStateSync.SyncAllAccToggle();
				*/
				AccStateSync.InitCurOutfitTriggerInfo("OnCoordinateBeingLoaded");
			}

			internal void SetReferralIndex(int _index)
			{
				Logger.LogWarning($"[SetReferralIndex][{ChaControl.GetFullname()}][_index: {_index}]");

				if (ReferralIndex != _index)
					ReferralIndex = MathfEx.RangeEqualOn(0, _index, RefMax) ? _index : RefMax;
			}
		}
	}
}
