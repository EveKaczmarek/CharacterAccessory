using System.Collections;

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
					return false;
				return true;
			}

			internal static bool DuringLoading_IEnumerator_Prefix(CharaCustomFunctionController __instance, ref IEnumerator __result)
			{
				ChaControl _chaCtrl = __instance.ChaControl;
				CharacterAccessoryController _pluginCtrl = GetController(_chaCtrl);
				if (_pluginCtrl.DuringLoading)
				{
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
					return false;
				return true;
			}
		}
	}
}
