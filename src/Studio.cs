using System;
using System.Collections.Generic;
using System.Linq;

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UniRx;
using ParadoxNotion.Serialization;
using MessagePack;
using Studio;

using HarmonyLib;

using KKAPI.Studio;
using KKAPI.Studio.UI;

namespace CharacterAccessory
{
	public partial class CharacterAccessory
	{
		internal partial class CharaStudio
		{
			internal static bool Running = false;
			internal static bool Loaded = false;

			internal static OCIChar CurOCIChar;
			internal static int CurTreeNodeObjID = -1;

			internal static bool RefreshCharaStatePanel()
			{
				if (!Loaded)
					return false;
				MPCharCtrl MPCharCtrl = FindObjectOfType<MPCharCtrl>();
				if (MPCharCtrl == null)
					return false;
				int select = Traverse.Create(MPCharCtrl).Field<int>("select").Value;
				if (select != 0)
					return false;
				MPCharCtrl.OnClickRoot(0);
				return true;
			}

			internal static List<TreeNodeObject> ListSelectNodes => Traverse.Create(Studio.Studio.Instance.treeNodeCtrl).Property("selectNodes").GetValue<TreeNodeObject[]>().ToList();

			internal static void GetTreeNodeInfo(TreeNodeObject _node)
			{
				int OldTreeNodeObjID = CurTreeNodeObjID;

				if (_node == null || ListSelectNodes.Count == 0)
				{
					if (OldTreeNodeObjID > -1)
					{
						CurOCIChar = null;
						CurTreeNodeObjID = -1;
					}
					return;
				}

				if (Studio.Studio.Instance.dicInfo.TryGetValue(_node, out ObjectCtrlInfo info))
				{
					CurTreeNodeObjID = StudioObjectExtensions.GetSceneId(info);
					if (OldTreeNodeObjID != CurTreeNodeObjID)
					{
						OCIChar selected = info as OCIChar;
						if (selected?.GetType() != null)
							CurOCIChar = selected;
						else
							CurOCIChar = null;
					}
				}
			}

			internal static void RegisterStudioControls()
			{
				if (!Running) return;
				Loaded = true;

				List<string> coordinateList = Enum.GetNames(typeof(ChaFileDefine.CoordinateType)).ToList();
				coordinateList.Add("CharaAcc");
				CurrentStateCategorySwitch StudioToggleEnable = new CurrentStateCategorySwitch("Enable", OCIChar => (bool) OCIChar?.charInfo?.GetComponent<CharacterAccessoryController>().FunctionEnable);
				StudioToggleEnable.Value.Subscribe(_value =>
				{
					CharacterAccessoryController _pluginCtrl = StudioAPI.GetSelectedControllers<CharacterAccessoryController>().FirstOrDefault();
					if (_pluginCtrl == null) return;
					_pluginCtrl.FunctionEnable = _value;
				});
				StudioAPI.GetOrCreateCurrentStateCategory("CharaAcc").AddControl(StudioToggleEnable);

				CurrentStateCategoryDropdown StudioDropdownRef = new CurrentStateCategoryDropdown("Referral", coordinateList.ToArray(), OCIChar => (int) OCIChar?.charInfo?.GetComponent<CharacterAccessoryController>().ReferralIndex);
				StudioDropdownRef.Value.Subscribe(_value =>
				{
					CharacterAccessoryController _pluginCtrl = StudioAPI.GetSelectedControllers<CharacterAccessoryController>().FirstOrDefault();
					if (_pluginCtrl == null) return;
					_pluginCtrl.SetReferralIndex(_value);
				});
				StudioAPI.GetOrCreateCurrentStateCategory("CharaAcc").AddControl(StudioDropdownRef);
			}
		}
	}
}
