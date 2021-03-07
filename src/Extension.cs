using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using UnityEngine;
using MessagePack;
using ParadoxNotion.Serialization;

using HarmonyLib;

namespace CharacterAccessory
{
	public static partial class Extension
	{
		internal static string GetFullname(this ChaControl _chaCtrl) => _chaCtrl?.chaFile?.parameter?.fullname?.Trim();

		//internal static List<ChaFileAccessory.PartsInfo> ListPartsInfo(this ChaControl _chaCtrl, int _coordinateIndex) => CharacterAccessory.MoreAccessoriesSupport.ListPartsInfo(_chaCtrl, _coordinateIndex);

		public static string NameFormatted(this Material go) => go == null ? "" : go.name.Replace("(Instance)", "").Replace(" Instance", "").Trim();
		public static string NameFormatted(this string name) => name.Replace("(Instance)", "").Replace(" Instance", "").Trim();

		public static T[] AddToArray<T>(this T[] arr, T item)
		{
			List<T> list = arr.ToList();
			list.Add(item);
			return list.ToArray();
		}

		public static object RefTryGetValue(this object self, object key)
		{
			if (self == null) return null;

			MethodInfo tryMethod = AccessTools.Method(self.GetType(), "TryGetValue");
			object[] parameters = new object[] { key, null };
			tryMethod.Invoke(self, parameters);
			return parameters[1];
		}

		public static object RefElementAt(this object self, int key)
		{
			if (self == null)
				return null;
			if (key > (Traverse.Create(self).Property("Count").GetValue<int>() - 1))
				return null;

			return Traverse.Create(self).Method("get_Item", new object[] { key }).GetValue();
		}

		public static object JsonClone(this object self)
		{
			if (self == null)
				return null;
			string json = JSONSerializer.Serialize(self.GetType(), self);
			return JSONSerializer.Deserialize(self.GetType(), json);
		}

		public static T MessagepackClone<T>(T sourceObj)
		{
			byte[] bytes = MessagePackSerializer.Serialize(sourceObj);
			return MessagePackSerializer.Deserialize<T>(bytes);
		}
	}
}
