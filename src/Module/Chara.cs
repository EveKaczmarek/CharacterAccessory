using System.Collections;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using UniRx;
using MessagePack;

using Sideloader.AutoResolver;

namespace CharacterAccessory
{
	public partial class CharacterAccessory
	{
		public partial class CharacterAccessoryController
		{
			internal void Backup()
			{
				int _coordinateIndex = ChaControl.fileStatus.coordinateType;

				List<ChaFileAccessory.PartsInfo> _parts = MoreAccessoriesSupport.ListPartsInfo(ChaControl, _coordinateIndex);

				PartsInfo.Clear();
				PartsResolveInfo.Clear();

				for (int i = 0; i < _parts.Count; i++)
				{
					ChaFileAccessory.PartsInfo _part = MoreAccessoriesSupport.GetPartsInfo(ChaControl, _coordinateIndex, i);
					if (_part.type > 120)
					{
						byte[] _byte = MessagePackSerializer.Serialize(_part);
						PartsInfo[i] = MessagePackSerializer.Deserialize<ChaFileAccessory.PartsInfo>(_byte);

						PartsResolveInfo[i] = UniversalAutoResolver.TryGetResolutionInfo((ChaListDefine.CategoryNo) _part.type, _part.id);
					}
				}

				HairAccessoryCustomizer.Backup();
				MaterialEditor.Backup();
				MaterialRouter.Backup();
				AccStateSync.Backup();
			}

			internal static void MigrateData(ref ResolveInfo extResolve)
			{
				if (extResolve.GUID.IsNullOrWhiteSpace()) return;

				List<MigrationInfo> migrationInfoList = UniversalAutoResolver.GetMigrationInfo(extResolve.GUID);

				if (migrationInfoList.Any(x => x.MigrationType == MigrationType.StripAll))
				{
					extResolve.GUID = "";
					return;
				}

				int slot = extResolve.Slot;
				ChaListDefine.CategoryNo categoryNo = extResolve.CategoryNo;
				foreach (MigrationInfo migrationInfo in migrationInfoList.Where(x => x.IDOld == slot && x.Category == categoryNo))
				{
					if (Sideloader.Sideloader.GetManifest(migrationInfo.GUIDNew) != null)
					{
						extResolve.GUID = migrationInfo.GUIDNew;
						extResolve.Slot = migrationInfo.IDNew;
						return;
					}
				}

				foreach (MigrationInfo migrationInfo in migrationInfoList.Where(x => x.MigrationType == MigrationType.MigrateAll))
				{
					if (Sideloader.Sideloader.GetManifest(migrationInfo.GUIDNew) != null)
						extResolve.GUID = migrationInfo.GUIDNew;
				}
			}

			internal IEnumerator RestorePartsInfoCoroutine()
			{
				Logger.LogWarning($"[RestoreCoroutine][{ChaControl.GetFullname()}] fired");

				yield return new WaitForEndOfFrame();
				yield return new WaitForEndOfFrame();

				RestorePartsInfo();
			}

			internal void Reset()
			{
				FunctionEnable = false;
				ReferralIndex = RefMax;
				PartsInfo.Clear();
				PartsResolveInfo.Clear();
				HairAccessoryCustomizer.Reset();
				MaterialEditor.Reset();
				MaterialEditor._texData.Clear();
				MaterialRouter.Reset();
				AccStateSync.Reset();
			}

			internal void RestorePartsInfo()
			{
				Logger.LogWarning($"[RestorePartsInfo][{ChaControl.GetFullname()}] fired");

				if (!DuringLoading)
					return;

				if (!FunctionEnable)
				{
					TaskUnlock();
					return;
				}
				if (PartsInfo.Count == 0)
				{
					Logger.LogMessage($"Nothing to restore");
					TaskUnlock();
					return;
				}

				int _coordinateIndex = ChaControl.fileStatus.coordinateType;
				Dictionary<int, ChaFileAccessory.PartsInfo> RefUsedPartsInfo = MoreAccessoriesSupport.ListUsedPartsInfo(ChaControl, _coordinateIndex);
				if (RefUsedPartsInfo.Count > 0 && RefUsedPartsInfo.Keys.Min() <= PartsInfo.Keys.Max())
				{
					Logger.LogMessage($"Error: parts overlap [RefUsedPartsInfo.Keys.Min(): {RefUsedPartsInfo.Keys.Min()}][PartsInfo.Keys.Max(): {PartsInfo.Keys.Max()}]");
					TaskUnlock();
					return;
				}

				Logger.LogInfo($"[RestorePartsInfo][{ChaControl.GetFullname()}][Slots: {string.Join(",", PartsInfo.Keys.Select(Slot => Slot.ToString()).ToArray())}]");

				foreach (KeyValuePair<int, ChaFileAccessory.PartsInfo> _part in PartsInfo)
					MoreAccessoriesSupport.SetPartsInfo(ChaControl, _coordinateIndex, _part.Key, _part.Value);

				if (CharaStudio.Running)
				{
					ChaControl.ChangeCoordinateTypeAndReload(false);
					ChaControl.StartCoroutine(RestorePluginSettingCoroutine());
					return;
				}
				else
				{
					HairAccessoryCustomizer.Restore();
					MaterialEditor.Restore();
					MaterialRouter.Restore();
					AccStateSync.Restore();

					TaskUnlock();

					ChaControl.StartCoroutine(RefreshCoroutine());
				}
			}

			internal IEnumerator RestorePluginSettingCoroutine()
			{
				Logger.LogWarning($"[RestorePluginSettingCoroutine][{ChaControl.GetFullname()}] fired");

				yield return new WaitForEndOfFrame();
				yield return new WaitForEndOfFrame();

				RestorePluginSetting();
			}

			internal void RestorePluginSetting()
			{
				Logger.LogWarning($"[RestorePluginSetting][{ChaControl.GetFullname()}] fired");

				HairAccessoryCustomizer.Restore();
				MaterialEditor.Restore();
				MaterialRouter.Restore();
				AccStateSync.Restore();

				TaskUnlock();

				ChaControl.StartCoroutine(RefreshCoroutine());
			}
		}
	}
}
