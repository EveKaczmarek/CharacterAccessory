using System.Collections;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using UniRx;

using BepInEx.Logging;

using KKAPI.Maker;

namespace CharacterAccessory
{
	public partial class CharacterAccessory
	{
		public partial class CharacterAccessoryController
		{
			internal List<QueueItem> QueueList = new List<QueueItem>();

			internal void TaskLock()
			{
				DuringLoading = true;
			}

			internal void TaskUnlock()
			{
				DuringLoading = false;
			}

			internal void AutoCopyCheck()
			{
				bool go = true;

				DebugMsg(LogLevel.Warning, $"[OnCoordinateChanged][{ChaControl.GetFullname()}][CurrentCoordinateIndex: {CurrentCoordinateIndex}]");

				if (!AutoCopyToBlank)
					go = false;
				if (!FunctionEnable)
					go = false;
				if (ReferralIndex == 7 && PartsInfo.Count == 0)
					go = false;
				if (ReferralIndex < 7 && ReferralIndex == CurrentCoordinateIndex)
					go = false;
				if (MakerAPI.InsideAndLoaded && !CfgMakerMasterSwitch.Value)
					go = false;

				IEnumerator OnCoordinateChangedCoroutine()
				{
					DebugMsg(LogLevel.Warning, $"[OnCoordinateChangedCoroutine][{ChaControl.GetFullname()}] fired");

					yield return new WaitForEndOfFrame();
					yield return new WaitForEndOfFrame();

					if (MoreAccessoriesSupport.ListUsedPartsInfo(ChaControl, CurrentCoordinateIndex).Count > 0)
						yield break;

					TaskLock();
					if (ReferralIndex < 7)
						CopyPartsInfo();
					else
						RestorePartsInfo();
				}

				if (go)
					ChaControl.StartCoroutine(OnCoordinateChangedCoroutine());
			}

			internal void PrepareQueue()
			{
				QueueList = new List<QueueItem>();

				if (ReferralIndex < 0)
				{
					TaskUnlock();
					return;
				}
				if (ReferralIndex < 7 && ReferralIndex == CurrentCoordinateIndex)
                {
					TaskUnlock();
					return;
				}

				int RefLastNotEmpty = -1;

				if (ReferralIndex < 7)
				{
					Dictionary<int, ChaFileAccessory.PartsInfo> RefUsedPartsInfo = MoreAccessoriesSupport.ListUsedPartsInfo(ChaControl, ReferralIndex);
					RefLastNotEmpty = (RefUsedPartsInfo.Count == 0) ? -1 : RefUsedPartsInfo.Keys.Max();
				}
				else
					RefLastNotEmpty = (PartsInfo.Count == 0) ? -1 : PartsInfo.Keys.Max();

				DebugMsg(LogLevel.Warning, $"[PrepareQueue][{ChaControl.GetFullname()}][ReferralIndex: {ReferralIndex}][SrcLastNotEmpty: Slot{RefLastNotEmpty + 1:00}]");

				if (RefLastNotEmpty < 0)
				{
					TaskUnlock();
					return;
				}

				Dictionary<int, ChaFileAccessory.PartsInfo> CurUsedPartsInfo = MoreAccessoriesSupport.ListUsedPartsInfo(ChaControl, CurrentCoordinateIndex);
				if (CurUsedPartsInfo.Count == 0)
				{
					if (ReferralIndex < 7)
						CopyPartsInfo();
					else
						RestorePartsInfo();
					return;
				}

				int ToAdd = 0;
				int shift = 0;
				int CurFirstNotEmpty = CurUsedPartsInfo.Keys.Min();
				int CurLastNotEmpty = CurUsedPartsInfo.Keys.Max();

				List<int> UsedParts = CurUsedPartsInfo.Keys.ToList();
				List<int> UsedPartsRev = new List<int>();
				UsedPartsRev.AddRange(UsedParts);
				UsedPartsRev.Reverse();
				DebugMsg(LogLevel.Warning, $"[PrepareQueue][{ChaControl.GetFullname()}][CurrentCoordinateIndex: {CurrentCoordinateIndex}][CurFirstNotEmpty: Slot{CurFirstNotEmpty + 1:00}][CurLastNotEmpty: Slot{CurLastNotEmpty + 1:00}]");

				if (CurFirstNotEmpty <= RefLastNotEmpty)
				{
					shift = RefLastNotEmpty - CurFirstNotEmpty + 1;
					int NewMaxIndex = CurLastNotEmpty + shift;
					ToAdd = NewMaxIndex - MoreAccessoriesSupport.GetPartsCount(ChaControl, CurrentCoordinateIndex);
				}

				if (shift > 0)
				{
					foreach (int _slot in UsedPartsRev)
						QueueList.Add(new QueueItem(_slot, _slot + shift));
				}

				if (ToAdd > 0)
				{
					MoreAccessoriesSupport.CheckAndPadPartInfo(ChaControl, CurrentCoordinateIndex, CurLastNotEmpty + shift);
					ChaControl.StartCoroutine(TransferPartsInfoCoroutine());
				}
				else
				{
					if (QueueList.Count > 0)
						TransferPartsInfo();
					else
					{
						if (ReferralIndex < 7)
							CopyPartsInfo();
						else
						{
							ChaControl.ChangeCoordinateTypeAndReload(false);
							ChaControl.StartCoroutine(RestorePartsInfoCoroutine());
						}
					}
				}
			}

			internal class QueueItem
			{
				public int srcSlot { get; set; }
				public int dstSlot { get; set; }
				public QueueItem(int _src, int _dst)
				{
					srcSlot = _src;
					dstSlot = _dst;
				}
			}
		}
	}
}
