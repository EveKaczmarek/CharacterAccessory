namespace CharacterAccessory
{
	public static partial class Extension
	{
		internal static string GetFullname(this ChaControl _chaCtrl) => _chaCtrl?.chaFile?.parameter?.fullname?.Trim();
	}
}
