using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace CharacterAccessory
{
	public partial class CharacterAccessory
	{
		internal static class CumOnOverSupport
		{
			private static BaseUnityPlugin _instance = null;
			private static bool _installed = false;

			internal static void Init()
			{
				BepInEx.Bootstrap.Chainloader.PluginInfos.TryGetValue("madevil.kk.CumOnOver", out PluginInfo _pluginInfo);
				_instance = _pluginInfo?.Instance;
				if (_instance != null)
					_installed = true;

				HooksInit();
			}

			internal static void HooksInit()
			{
				if (!_installed) return;

				HooksInstance["General"].Patch(_instance.GetType().Assembly.GetType("CumOnOver.CumOnOver+Hooks").GetMethod("ChaControl_UpdateClothesSiru", AccessTools.all), prefix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.ChaControl_UpdateClothesSiru_Prefix)));
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
						DebugMsg(LogLevel.Warning, $"[ChaControl_UpdateClothesSiru_Prefix][{__0.GetFullname()}] await loading");
						return false;
					}
					return flag;
				}
			}
		}
	}
}
