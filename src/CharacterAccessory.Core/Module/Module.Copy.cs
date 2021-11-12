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

				List<ChaFileAccessory.PartsInfo> _parts = MoreAccessoriesSupport.ListPartsInfo(ChaControl, ReferralIndex);
				List<int> _queue = new List<int>();
				for (int i = 0; i < _parts.Count; i++)
				{
					if (_parts[i].type > 120)
						_queue.Add(i);
				}
				DebugMsg(LogLevel.Warning, $"[CopyPartsInfo][{ChaControl.GetFullName()}][Slots: {string.Join(",", _queue.Select(x => x.ToString()).ToArray())}]");
				AccessoryCopyEventArgs _args = new AccessoryCopyEventArgs(_queue, (ChaFileDefine.CoordinateType) ReferralIndex, (ChaFileDefine.CoordinateType) CurrentCoordinateIndex);

				MoreAccessoriesSupport.CopyPartsInfo(ChaControl, _args);

				if (CharaStudio.Running)
				{
					ChaControl.ChangeCoordinateTypeAndReload(false);
					ChaControl.StartCoroutine(CopyPluginSettingCoroutine(_args));
					return;
				}
				else
				{
					foreach (string _name in _supportList)
						Traverse.Create(this).Field(_name).Method("CopyPartsInfo", new object[] { _args }).GetValue();

					ChaControl.StartCoroutine(RefreshCoroutine());
				}
			}

			internal IEnumerator CopyPluginSettingCoroutine(AccessoryCopyEventArgs _args)
			{
				DebugMsg(LogLevel.Warning, $"[CopyPluginSettingCoroutine][{ChaControl.GetFullName()}] fired");

				yield return Toolbox.WaitForEndOfFrame;
				yield return Toolbox.WaitForEndOfFrame;

				CopyPluginSetting(_args);
			}

			internal void CopyPluginSetting(AccessoryCopyEventArgs ev)
			{
				DebugMsg(LogLevel.Warning, $"[CopyPluginSetting][{ChaControl.GetFullName()}] fired");

				foreach (string _name in _supportList)
					Traverse.Create(this).Field(_name).Method("CopyPartsInfo", new object[] { ev }).GetValue();

				ChaControl.StartCoroutine(RefreshCoroutine());
			}
		}
	}
}
