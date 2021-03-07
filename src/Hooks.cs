using Studio;

using BepInEx.Logging;
using HarmonyLib;

using KKAPI.Chara;

namespace CharacterAccessory
{
	public partial class CharacterAccessory
	{
		internal class Hooks
		{
			internal static bool DuringLoading_Prefix(CharaCustomFunctionController __instance)
			{
				ChaControl _chaCtrl = __instance.ChaControl;
				CharacterAccessoryController _pluginCtrl = GetController(_chaCtrl);
				if (_pluginCtrl.DuringLoading)
				{
#if DEBUG
					DebugMsg(LogLevel.Warning, $"[DuringLoading_Prefix][{_chaCtrl.GetFullname()}] await loading");
#endif
					return false;
				}
				return true;
			}
		}

		internal class HooksMaker
		{
			internal static bool DuringLoading_Prefix()
			{
				ChaControl _chaCtrl = ChaCustom.CustomBase.Instance.chaCtrl;
				CharacterAccessoryController _pluginCtrl = GetController(_chaCtrl);
				if (_pluginCtrl == null) return true;

				if (_pluginCtrl.DuringLoading)
				{
#if DEBUG
					DebugMsg(LogLevel.Warning, $"[DuringLoading_Prefix][{_chaCtrl.GetFullname()}] await loading");
#endif
					return false;
				}
				return true;
			}
		}

		internal class HooksStudio
		{
			[HarmonyPostfix, HarmonyPatch(typeof(TreeNodeCtrl), nameof(TreeNodeCtrl.SelectSingle))]
			private static void TreeNodeCtrl_SelectSingle_Postfix(TreeNodeCtrl __instance, TreeNodeObject _node, bool _deselect)
			{
				CharaStudio.GetTreeNodeInfo(_node);
			}

			[HarmonyPostfix, HarmonyPatch(typeof(TreeNodeCtrl), nameof(TreeNodeCtrl.SelectMultiple))]
			private static void TreeNodeCtrl_SelectMultiple_Postfix(TreeNodeCtrl __instance, TreeNodeObject _start, TreeNodeObject _end)
			{
				CharaStudio.GetTreeNodeInfo(_start);
			}

			[HarmonyPrefix, HarmonyPatch(typeof(Studio.Studio), nameof(Studio.Studio.InitScene))]
			private static void Studio_InitScene_Prefix(bool _close)
			{
				CharaStudio.GetTreeNodeInfo(null);
			}
		}
	}
}
