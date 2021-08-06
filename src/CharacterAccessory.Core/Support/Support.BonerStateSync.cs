using BepInEx;
using HarmonyLib;

namespace CharacterAccessory
{
	public partial class CharacterAccessory
	{
		internal static class BonerStateSync
		{
			internal static BaseUnityPlugin _instance = null;
			internal static bool _installed = false;

			internal static void Init()
			{
				_instance = JetPack.Toolbox.GetPluginInstance("BonerStateSync");
				if (_instance != null)
					_installed = true;

				if (!_installed)
                {
					_instance = JetPack.Toolbox.GetPluginInstance("madevil.kk.BonerStateSync");
					if (_instance != null)
						_installed = true;
				}

				if (_installed)
					_hooksInstance["General"].Patch(_instance.GetType().Assembly.GetType("BonerStateSync.BonerStateSync+BonerStateSyncController").GetMethod("InitCurOutfitTriggerInfo", AccessTools.all, null, new[] { typeof(string) }, null), prefix: new HarmonyMethod(typeof(Hooks), nameof(Hooks.DuringLoading_Prefix)));
			}
		}
	}
}
