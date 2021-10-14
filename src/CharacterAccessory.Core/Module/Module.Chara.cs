﻿using System.Collections;
using System.Collections.Generic;
using System.Linq;

using UniRx;
using MessagePack;

using BepInEx.Logging;
using HarmonyLib;
using Sideloader.AutoResolver;

using JetPack;

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

				foreach (string _name in _supportList)
					Traverse.Create(this).Field(_name).Method("Backup").GetValue();
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
				DebugMsg(LogLevel.Warning, $"[RestoreCoroutine][{ChaControl.GetFullName()}] fired");

				yield return Toolbox.WaitForEndOfFrame;
				yield return Toolbox.WaitForEndOfFrame;

				RestorePartsInfo();
			}

			internal void Reset()
			{
				FunctionEnable = false;
				AutoCopyToBlank = false;
				ReferralIndex = -1;
				PartsInfo.Clear();
				PartsResolveInfo.Clear();

				foreach (string _name in _supportList)
					Traverse.Create(this).Field(_name).Method("Reset").GetValue();
			}

			internal void RestorePartsInfo()
			{
				DebugMsg(LogLevel.Warning, $"[RestorePartsInfo][{ChaControl.GetFullName()}] fired");

				if (!DuringLoading)
					return;

				if (!FunctionEnable)
				{
					TaskUnlock();
					return;
				}
				if (PartsInfo.Count == 0)
				{
					_logger.LogMessage($"Nothing to restore");
					TaskUnlock();
					return;
				}

				int _coordinateIndex = ChaControl.fileStatus.coordinateType;
				Dictionary<int, ChaFileAccessory.PartsInfo> RefUsedPartsInfo = MoreAccessoriesSupport.ListUsedPartsInfo(ChaControl, _coordinateIndex);
				if (RefUsedPartsInfo.Count > 0 && RefUsedPartsInfo.Keys.Min() <= PartsInfo.Keys.Max())
				{
					_logger.LogMessage($"Error: parts overlap [RefUsedPartsInfo.Keys.Min(): {RefUsedPartsInfo.Keys.Min()}][PartsInfo.Keys.Max(): {PartsInfo.Keys.Max()}]");
					TaskUnlock();
					return;
				}

				DebugMsg(LogLevel.Info, $"[RestorePartsInfo][{ChaControl.GetFullName()}][Slots: {string.Join(",", PartsInfo.Keys.Select(Slot => Slot.ToString()).ToArray())}]");

				foreach (KeyValuePair<int, ChaFileAccessory.PartsInfo> _part in PartsInfo)
					MoreAccessoriesSupport.SetPartsInfo(ChaControl, _coordinateIndex, _part.Key, _part.Value);

				if (CharaStudio.Running)
				{
					ChaControl.ChangeCoordinateTypeAndReload(false);
					StartCoroutine(RestorePluginSettingCoroutine());
					return;
				}
				else
				{
					foreach (string _name in _supportList)
						Traverse.Create(this).Field(_name).Method("Restore").GetValue();

					StartCoroutine(RefreshCoroutine());
				}
			}

			internal IEnumerator RestorePluginSettingCoroutine()
			{
				DebugMsg(LogLevel.Warning, $"[RestorePluginSettingCoroutine][{ChaControl.GetFullName()}] fired");

				yield return Toolbox.WaitForEndOfFrame;
				yield return Toolbox.WaitForEndOfFrame;

				RestorePluginSetting();
			}

			internal void RestorePluginSetting()
			{
				DebugMsg(LogLevel.Warning, $"[RestorePluginSetting][{ChaControl.GetFullName()}] fired");

				foreach (string _name in _supportList)
					Traverse.Create(this).Field(_name).Method("Restore").GetValue();

				StartCoroutine(RefreshCoroutine());
			}
		}
	}
}
