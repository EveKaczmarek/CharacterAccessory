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

				int _refLastUsedSlot = -1;

				if (ReferralIndex > -1 && ReferralIndex < ChaControl.chaFile.coordinate.Length)
				{
					Dictionary<int, ChaFileAccessory.PartsInfo> _refUsedParts = MoreAccessoriesSupport.ListUsedPartsInfo(ChaControl, ReferralIndex);
					_refLastUsedSlot = (_refUsedParts.Count == 0) ? -1 : _refUsedParts.Keys.Max();
				}
				else if (ReferralIndex  == -1)
					_refLastUsedSlot = (PartsInfo.Count == 0) ? -1 : PartsInfo.Keys.Max();

				DebugMsg(LogLevel.Warning, $"[PrepareQueue][{ChaControl.GetFullName()}][ReferralIndex: {ReferralIndex}][SrcLastNotEmpty: Slot{_refLastUsedSlot + 1:00}]");

				if (_refLastUsedSlot < 0)
				{
					TaskUnlock();
					return;
				}

				Dictionary<int, ChaFileAccessory.PartsInfo> _curUsedParts = MoreAccessoriesSupport.ListUsedPartsInfo(ChaControl, CurrentCoordinateIndex);
				if (_curUsedParts.Count == 0)
				{
					if (ReferralIndex > -1 && ReferralIndex < ChaControl.chaFile.coordinate.Length)
						CopyPartsInfo();
					else if (ReferralIndex == -1)
						RestorePartsInfo();
					return;
				}

				int _toAdd = 0;
				int _shift = 0;
				int _curFirstUsedSlot = _curUsedParts.Keys.Min();
				int _curLastUsedSlot = _curUsedParts.Keys.Max();

				List<int> _keysUsedParts = _curUsedParts.Keys.ToList();
				List<int> _keysUsedPartsRev = new List<int>();
				_keysUsedPartsRev.AddRange(_keysUsedParts);
				_keysUsedPartsRev.Reverse();
				DebugMsg(LogLevel.Warning, $"[PrepareQueue][{ChaControl.GetFullName()}][CurrentCoordinateIndex: {CurrentCoordinateIndex}][CurFirstNotEmpty: Slot{_curFirstUsedSlot + 1:00}][CurLastNotEmpty: Slot{_curLastUsedSlot + 1:00}]");

				if (_curFirstUsedSlot <= _refLastUsedSlot)
				{
					_shift = _refLastUsedSlot - _curFirstUsedSlot + 1;
					int _newMaxIndex = _curLastUsedSlot + _shift;
					_toAdd = _newMaxIndex - MoreAccessoriesSupport.GetPartsCount(ChaControl, CurrentCoordinateIndex);
				}

				if (_shift > 0)
				{
					foreach (int _slot in _keysUsedPartsRev)
						QueueList.Add(new QueueItem(_slot, _slot + _shift));
				}

				if (_toAdd > 0)
				{
					MoreAccessoriesSupport.CheckAndPadPartInfo(ChaControl, CurrentCoordinateIndex, _curLastUsedSlot + _shift);
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
