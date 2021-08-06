using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

using JetPack;

namespace CharacterAccessory
{
	public partial class CharacterAccessory
	{
		internal static class CumOnOverSupport
		{
			internal static BaseUnityPlugin _instance = null;
			internal static bool _installed = false;

			internal static void Init()
			{
				_instance = JetPack.Toolbox.GetPluginInstance("madevil.kk.CumOnOver");
				if (_instance != null)
					_installed = true;

				if (_installed)
					_hooksInstance["General"].Patch(_instance.GetType().Assembly.GetType("CumOnOver.CumOnOver+Hooks").GetMethod("ChaControl_UpdateClothesSiru", AccessTools.all), prefix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.ChaControl_UpdateClothesSiru_Prefix)));
			}

			internal static class Hooks
			{
				internal static bool ChaControl_UpdateClothesSiru_Prefix(ChaControl __0)
				{
					bool flag = true;

					if (GetController(__0).DuringLoading)
						flag = false;

					if (!flag)
					{
						DebugMsg(LogLevel.Warning, $"[ChaControl_UpdateClothesSiru_Prefix][{__0.GetFullName()}] await loading");
						return false;
					}
					return flag;
				}
			}
		}
	}
}
