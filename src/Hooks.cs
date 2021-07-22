using System.Collections;

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

			internal static bool DuringLoading_IEnumerator_Prefix(CharaCustomFunctionController __instance, ref IEnumerator __result)
			{
				ChaControl _chaCtrl = __instance.ChaControl;
				CharacterAccessoryController _pluginCtrl = GetController(_chaCtrl);
				if (_pluginCtrl.DuringLoading)
				{
#if DEBUG
					DebugMsg(LogLevel.Warning, $"[DuringLoading_Prefix][{_chaCtrl.GetFullname()}] await loading");
#endif
					IEnumerator original = __result;
					__result = new[] { original, YieldBreak() }.GetEnumerator();



					return false;
				}
				return true;

				IEnumerator YieldBreak() { yield break; }
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
			[HarmonyPostfix, HarmonyPatch(typeof(TreeNodeCtrl), nameof(TreeNodeCtrl.SelectSingle), new[] { typeof(TreeNodeObject), typeof(bool) })]
			private static void TreeNodeCtrl_SelectSingle_Postfix(TreeNodeObject _node)
			{
				CharaStudio.GetTreeNodeInfo(_node);
			}

			[HarmonyPostfix, HarmonyPatch(typeof(TreeNodeCtrl), nameof(TreeNodeCtrl.SelectMultiple), new[] { typeof(TreeNodeObject), typeof(TreeNodeObject) })]
			private static void TreeNodeCtrl_SelectMultiple_Postfix(TreeNodeObject _start)
			{
				CharaStudio.GetTreeNodeInfo(_start);
			}

			[HarmonyPrefix, HarmonyPatch(typeof(Studio.Studio), nameof(Studio.Studio.InitScene), new[] { typeof(bool) })]
			private static void Studio_InitScene_Prefix()
			{
				CharaStudio.GetTreeNodeInfo(null);
			}
		}
	}
}
