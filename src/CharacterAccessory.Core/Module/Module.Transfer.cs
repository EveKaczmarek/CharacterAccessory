using System.Collections;

using BepInEx.Logging;
using HarmonyLib;

using KKAPI.Maker;
using JetPack;

namespace CharacterAccessory
{
	public partial class CharacterAccessory
	{
		public partial class CharacterAccessoryController
		{
			internal IEnumerator TransferPartsInfoCoroutine()
			{
				DebugMsg(LogLevel.Warning, $"[TransferPartsInfoCoroutine][{ChaControl.GetFullName()}] fired");

				yield return Toolbox.WaitForEndOfFrame;
				yield return Toolbox.WaitForEndOfFrame;

				TransferPartsInfo();
			}

			internal void TransferPartsInfo()
			{
				DebugMsg(LogLevel.Warning, $"[TransferPartsInfo][{ChaControl.GetFullName()}] fired");

				if (QueueList.Count == 0)
				{
					TaskUnlock();
					return;
				}

				for (int i = 0; i < QueueList.Count; i++)
				{
					int _srcIndex = QueueList[i].SrcSlot;
					int _dstIndex = QueueList[i].DstSlot;
					DebugMsg(LogLevel.Warning, $"[TransferPartsInfo][{ChaControl.GetFullName()}][{_srcIndex}][{_dstIndex}]");
					AccessoryTransferEventArgs ev = new AccessoryTransferEventArgs(_srcIndex, _dstIndex);

					MoreAccessoriesSupport.TransferPartsInfo(ChaControl, ev);
					MoreAccessoriesSupport.RemovePartsInfo(ChaControl, CurrentCoordinateIndex, _srcIndex);

					foreach (string _name in _supportList)
					{
						Traverse.Create(this).Field(_name).Method("TransferPartsInfo", new object[] { ev }).GetValue();
						Traverse.Create(this).Field(_name).Method("RemovePartsInfo", new object[] { _srcIndex }).GetValue();
					}
				}

				AAAPK.UpdatePartsInfoList();
				AAAPK.RefreshCache();

				if (ReferralIndex > -1 && ReferralIndex < ChaControl.chaFile.coordinate.Length)
					CopyPartsInfo();
				else if (ReferralIndex == -1)
				{
					ChaControl.ChangeCoordinateTypeAndReload(false);
					ChaControl.StartCoroutine(RestorePartsInfoCoroutine());
				}
			}
		}
	}
}
