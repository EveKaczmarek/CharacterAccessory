using System.Collections;
using System.Collections.Generic;
using System.Linq;

using UniRx;

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
			internal void CopyPartsInfo()
			{
				DebugMsg(LogLevel.Warning, $"[CopyPartsInfo][{ChaControl.GetFullName()}] fired");

				if (!DuringLoading)
				{
					TaskUnlock();
					return;
				}
				if (!FunctionEnable)
				{
					TaskUnlock();
					return;
				}
				if (ReferralIndex == CurrentCoordinateIndex)
				{
					TaskUnlock();
					return;
				}

				List<ChaFileAccessory.PartsInfo> PartsInfo = MoreAccessoriesSupport.ListPartsInfo(ChaControl, ReferralIndex);
				List<int> SlotIndexes = new List<int>();
				for (int i = 0; i < PartsInfo.Count; i++)
				{
					if (PartsInfo[i].type > 120)
						SlotIndexes.Add(i);
				}
				DebugMsg(LogLevel.Warning, $"[CopyPartsInfo][{ChaControl.GetFullName()}][Slots: {string.Join(",", SlotIndexes.Select(Slot => Slot.ToString()).ToArray())}]");
				AccessoryCopyEventArgs ev = new AccessoryCopyEventArgs(SlotIndexes, (ChaFileDefine.CoordinateType) ReferralIndex, (ChaFileDefine.CoordinateType) CurrentCoordinateIndex);

				MoreAccessoriesSupport.CopyPartsInfo(ChaControl, ev);

				if (CharaStudio.Running)
				{
					ChaControl.ChangeCoordinateTypeAndReload(false);
					ChaControl.StartCoroutine(CopyPluginSettingCoroutine(ev));
					return;
				}
				else
				{
					foreach (string _name in SupportList)
						Traverse.Create(this).Field(_name).Method("CopyPartsInfo", new object[] { ev }).GetValue();

					ChaControl.StartCoroutine(RefreshCoroutine());
				}
			}

			internal IEnumerator CopyPluginSettingCoroutine(AccessoryCopyEventArgs ev)
			{
				DebugMsg(LogLevel.Warning, $"[CopyPluginSettingCoroutine][{ChaControl.GetFullName()}] fired");

				yield return Toolbox.WaitForEndOfFrame;
				yield return Toolbox.WaitForEndOfFrame;

				CopyPluginSetting(ev);
			}

			internal void CopyPluginSetting(AccessoryCopyEventArgs ev)
			{
				DebugMsg(LogLevel.Warning, $"[CopyPluginSetting][{ChaControl.GetFullName()}] fired");

				foreach (string _name in SupportList)
					Traverse.Create(this).Field(_name).Method("CopyPartsInfo", new object[] { ev }).GetValue();

				ChaControl.StartCoroutine(RefreshCoroutine());
			}
		}
	}
}
