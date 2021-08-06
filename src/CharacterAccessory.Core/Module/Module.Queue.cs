using System.Collections;
using System.Collections.Generic;
using System.Linq;

using UniRx;

using BepInEx.Logging;

using KKAPI.Maker;
using JetPack;

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

				DebugMsg(LogLevel.Warning, $"[OnCoordinateChanged][{ChaControl.GetFullName()}][CurrentCoordinateIndex: {CurrentCoordinateIndex}]");

				if (!AutoCopyToBlank)
					go = false;
				if (!FunctionEnable)
					go = false;
				if (ReferralIndex == -1 && PartsInfo.Count == 0)
					go = false;
				if (ReferralIndex > -1 && ReferralIndex < ChaControl.chaFile.coordinate.Length && ReferralIndex == CurrentCoordinateIndex)
					go = false;
				if (MakerAPI.InsideAndLoaded && !_cfgMakerMasterSwitch.Value)
					go = false;

				if (go)
					ChaControl.StartCoroutine(OnCoordinateChangedCoroutine());
			}

			internal IEnumerator OnCoordinateChangedCoroutine()
			{
				DebugMsg(LogLevel.Warning, $"[OnCoordinateChangedCoroutine][{ChaControl.GetFullName()}] fired");

				yield return Toolbox.WaitForEndOfFrame;
				yield return Toolbox.WaitForEndOfFrame;

				if (MoreAccessoriesSupport.ListUsedPartsInfo(ChaControl, CurrentCoordinateIndex).Count > 0)
					yield break;

				TaskLock();
				if (ReferralIndex > -1 && ReferralIndex < ChaControl.chaFile.coordinate.Length)
					CopyPartsInfo();
				else
					RestorePartsInfo();
			}

			internal void PrepareQueue()
			{
				QueueList = new List<QueueItem>();

				if (ReferralIndex >= ChaControl.chaFile.coordinate.Length)
				{
					TaskUnlock();
					return;
				}
				if (ReferralIndex > -1 && ReferralIndex < ChaControl.chaFile.coordinate.Length && ReferralIndex == CurrentCoordinateIndex)
				{
					TaskUnlock();
					return;
				}

				int RefLastNotEmpty = -1;

				if (ReferralIndex > -1 && ReferralIndex < ChaControl.chaFile.coordinate.Length)
				{
					Dictionary<int, ChaFileAccessory.PartsInfo> RefUsedPartsInfo = MoreAccessoriesSupport.ListUsedPartsInfo(ChaControl, ReferralIndex);
					RefLastNotEmpty = (RefUsedPartsInfo.Count == 0) ? -1 : RefUsedPartsInfo.Keys.Max();
				}
				else if (ReferralIndex  == -1)
					RefLastNotEmpty = (PartsInfo.Count == 0) ? -1 : PartsInfo.Keys.Max();

				DebugMsg(LogLevel.Warning, $"[PrepareQueue][{ChaControl.GetFullName()}][ReferralIndex: {ReferralIndex}][SrcLastNotEmpty: Slot{RefLastNotEmpty + 1:00}]");

				if (RefLastNotEmpty < 0)
				{
					TaskUnlock();
					return;
				}

				Dictionary<int, ChaFileAccessory.PartsInfo> CurUsedPartsInfo = MoreAccessoriesSupport.ListUsedPartsInfo(ChaControl, CurrentCoordinateIndex);
				if (CurUsedPartsInfo.Count == 0)
				{
					if (ReferralIndex > -1 && ReferralIndex < ChaControl.chaFile.coordinate.Length)
						CopyPartsInfo();
					else if (ReferralIndex == -1)
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
				DebugMsg(LogLevel.Warning, $"[PrepareQueue][{ChaControl.GetFullName()}][CurrentCoordinateIndex: {CurrentCoordinateIndex}][CurFirstNotEmpty: Slot{CurFirstNotEmpty + 1:00}][CurLastNotEmpty: Slot{CurLastNotEmpty + 1:00}]");

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
					StartCoroutine(TransferPartsInfoCoroutine());
				}
				else
				{
					if (QueueList.Count > 0)
						TransferPartsInfo();
					else
					{
						if (ReferralIndex > -1 && ReferralIndex < ChaControl.chaFile.coordinate.Length)
							CopyPartsInfo();
						else if (ReferralIndex == -1)
						{
							ChaControl.ChangeCoordinateTypeAndReload(false);
							StartCoroutine(RestorePartsInfoCoroutine());
						}
					}
				}
			}

			internal class QueueItem
			{
				public int SrcSlot { get; set; }
				public int DstSlot { get; set; }
				public QueueItem(int _src, int _dst)
				{
					SrcSlot = _src;
					DstSlot = _dst;
				}
			}
		}
	}
}
