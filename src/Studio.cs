using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;

using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;
using UnityEngine.SceneManagement;
using Studio;
using UniRx;

using BepInEx;
using BepInEx.Logging;
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

				if (Studio.Studio.Instance.dicInfo.TryGetValue(_node, out ObjectCtrlInfo _info))
				{
					CurTreeNodeObjID = StudioObjectExtensions.GetSceneId(_info);
					if (OldTreeNodeObjID != CurTreeNodeObjID)
					{
						OCIChar selected = _info as OCIChar;
						if (selected?.GetType() != null)
							CurOCIChar = selected;
						else
							CurOCIChar = null;
					}
				}
			}

			internal static void StartupCheck(Scene _scene, LoadSceneMode _loadSceneMode)
			{
#if DEBUG
				DebugMsg(LogLevel.Warning, $"[StartupCheck][{_scene.name}][{_loadSceneMode}]");
#endif
				if (!Loaded && _scene.name == "Studio")
					RegisterStudioControls();
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

				SceneManager.sceneLoaded -= StartupCheck;
#if DEBUG
				ScrollRect charalist = SetupList("StudioScene/Canvas Main Menu/02_Manipulate/00_Chara/00_Root");
				CreateCharaButton("tglReload", "Reload", charalist, () =>
				{
					OCIChar CurOCIChar = CharaStudio.CurOCIChar;
					if ((CurOCIChar == null) || (CurOCIChar.charInfo == null))
						return;

					CharacterAccessoryController _pluginCtrl = GetController(CurOCIChar);
					if (_pluginCtrl == null) return;
					_pluginCtrl.BigReload();
				});

				CreateCharaButton("tglQuickReload", "Quick Reload", charalist, () =>
				{
					OCIChar CurOCIChar = CharaStudio.CurOCIChar;
					if ((CurOCIChar == null) || (CurOCIChar.charInfo == null))
						return;

					CharacterAccessoryController _pluginCtrl = GetController(CurOCIChar);
					if (_pluginCtrl == null) return;
					_pluginCtrl.FastReload();
				});
#endif
			}

			internal static ScrollRect SetupList(string goPath)
			{
				GameObject listObject = GameObject.Find(goPath);
				ScrollRect scrollRect = listObject.GetComponent<ScrollRect>();
				scrollRect.content.gameObject.GetOrAddComponent<VerticalLayoutGroup>();
				scrollRect.content.gameObject.GetOrAddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
				scrollRect.scrollSensitivity = 25;

				foreach (Transform item in scrollRect.content.transform)
				{
					LayoutElement layoutElement = item.gameObject.GetOrAddComponent<LayoutElement>();
					layoutElement.preferredHeight = 40;
				}

				return scrollRect;
			}

			internal static Button CreateCharaButton(string name, string label, ScrollRect scrollRect, UnityAction onClickEvent)
			{
				return CreateButton(name, label, scrollRect, onClickEvent, "StudioScene/Canvas Main Menu/02_Manipulate/00_Chara/00_Root/Viewport/Content/State");
			}

			internal static Button CreateButton(string name, string label, ScrollRect scrollRect, UnityAction onClickEvent, string goPath)
			{
				GameObject template = GameObject.Find(goPath);
				GameObject newObject = Instantiate(template, scrollRect.content.transform);
				newObject.name = name;
				Text textComponent = newObject.GetComponentInChildren<Text>();
				textComponent.text = label;
				Button buttonComponent = newObject.GetComponent<Button>();
				for (int i = 0; i < buttonComponent.onClick.GetPersistentEventCount(); i++)
					buttonComponent.onClick.SetPersistentListenerState(i, UnityEventCallState.Off);
				buttonComponent.onClick.RemoveAllListeners();
				buttonComponent.onClick.AddListener(onClickEvent);
				return buttonComponent;
			}
		}
	}
}
