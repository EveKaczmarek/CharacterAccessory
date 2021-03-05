using System.Collections;
using System.Collections.Generic;

using UnityEngine;

using KKAPI.Maker;

namespace CharacterAccessory
{
	public partial class CharacterAccessory
	{
		public partial class CharacterAccessoryController
		{
			internal IEnumerator TransferPartsInfoCoroutine()
			{
				Logger.LogWarning($"[TransferPartsInfoCoroutine][{ChaControl.GetFullname()}] fired");

				yield return new WaitForEndOfFrame();
				yield return new WaitForEndOfFrame();

				TransferPartsInfo();
			}

			internal void TransferPartsInfo()
			{
				Logger.LogWarning($"[TransferPartsInfo][{ChaControl.GetFullname()}] fired");

				if (QueueList.Count == 0)
				{
					TaskUnlock();
					return;
				}

				for (int i = 0; i < QueueList.Count; i++)
				{
					int srcIndex = QueueList[i].srcSlot;
					int dstIndex = QueueList[i].dstSlot;
					Logger.LogInfo($"[TransferPartsInfo][{ChaControl.GetFullname()}][{srcIndex}][{dstIndex}]");
					AccessoryTransferEventArgs ev = new AccessoryTransferEventArgs(srcIndex, dstIndex);

					MoreAccessoriesSupport.TransferPartsInfo(ChaControl, ev);
					MoreAccessoriesSupport.RemovePartsInfo(ChaControl, CurrentCoordinateIndex, srcIndex);
					HairAccessoryCustomizer.TransferPartsInfo(ev);
					HairAccessoryCustomizer.RemovePartsInfo(srcIndex);
					MaterialEditor.TransferPartsInfo(ev);
					MaterialEditor.RemovePartsInfo(srcIndex);
					AccStateSync.TransferPartsInfo(ev);
					AccStateSync.RemovePartsInfo(srcIndex);
					MaterialRouter.TransferPartsInfo(ev);
					MaterialRouter.RemovePartsInfo(srcIndex);
				}

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
}
