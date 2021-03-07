using System.Collections;
using System.Collections.Generic;

using UnityEngine;

using BepInEx.Logging;
using HarmonyLib;

using KKAPI.Maker;

namespace CharacterAccessory
{
	public partial class CharacterAccessory
	{
		public partial class CharacterAccessoryController
		{
			internal IEnumerator TransferPartsInfoCoroutine()
			{
				DebugMsg(LogLevel.Warning, $"[TransferPartsInfoCoroutine][{ChaControl.GetFullname()}] fired");

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
					DebugMsg(LogLevel.Warning, $"[TransferPartsInfo][{ChaControl.GetFullname()}][{srcIndex}][{dstIndex}]");
					AccessoryTransferEventArgs ev = new AccessoryTransferEventArgs(srcIndex, dstIndex);

					MoreAccessoriesSupport.TransferPartsInfo(ChaControl, ev);
					MoreAccessoriesSupport.RemovePartsInfo(ChaControl, CurrentCoordinateIndex, srcIndex);

					foreach (string _name in SupportList)
					{
						Traverse.Create(this).Field(_name).Method("TransferPartsInfo", new object[] { ev }).GetValue();
						Traverse.Create(this).Field(_name).Method("RemovePartsInfo", new object[] { srcIndex }).GetValue();
					}
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
