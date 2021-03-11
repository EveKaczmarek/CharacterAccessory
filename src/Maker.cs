using System;
using System.Collections.Generic;
using System.Linq;

using ChaCustom;
using UniRx;

using KKAPI.Maker;
using KKAPI.Maker.UI;
using KKAPI.Maker.UI.Sidebar;

namespace CharacterAccessory
{
	public partial class CharacterAccessory
	{
		internal static MakerDropdown MakerDropdownRef;
		internal static MakerToggle MakerToggleEnable;
		internal static MakerToggle MakerToggleAutoCopyToBlank;
		internal static SidebarToggle SidebarToggleEnable;

		private void RegisterCustomSubCategories(object sender, RegisterSubCategoriesEvent args)
		{
			ChaControl _chaCtrl = CustomBase.Instance.chaCtrl;
			CharacterAccessoryController _pluginCtrl = GetController(_chaCtrl);

			SidebarToggleEnable = args.AddSidebarControl(new SidebarToggle("CharaAcc", CfgMakerMasterSwitch.Value, this));
			SidebarToggleEnable.ValueChanged.Subscribe(value => CfgMakerMasterSwitch.Value = value);

			MakerCategory category = new MakerCategory("03_ClothesTop", "tglCharaAcc", MakerConstants.Clothes.Copy.Position + 1, "CharaAcc");
			args.AddSubCategory(category);

			args.AddControl(new MakerText("The set to be used as a template to clone on load", category, this));

			List<string> coordinateList = Enum.GetNames(typeof(ChaFileDefine.CoordinateType)).ToList();
			coordinateList.Add("CharaAcc");
			MakerDropdownRef = new MakerDropdown("Referral", coordinateList.ToArray(), category, 0, this);
			MakerDropdownRef.ValueChanged.Subscribe(value =>
			{
				_pluginCtrl.SetReferralIndex(value);
			});
			args.AddControl(MakerDropdownRef);

			MakerToggleEnable = args.AddControl(new MakerToggle(category, "Enable", false, this));
			MakerToggleEnable.ValueChanged.Subscribe(value =>
			{
				_pluginCtrl.FunctionEnable = value;
			});

			MakerToggleAutoCopyToBlank = args.AddControl(new MakerToggle(category, "Auto Copy To Blank", false, this));
			MakerToggleAutoCopyToBlank.ValueChanged.Subscribe(value =>
			{
				_pluginCtrl.AutoCopyToBlank = value;
			});

			args.AddControl(new MakerButton("Backup", category, this)).OnClick.AddListener(delegate
			{
				if (_pluginCtrl.DuringLoading) return;
				_pluginCtrl.Backup();
			});

			args.AddControl(new MakerButton("Restore", category, this)).OnClick.AddListener(delegate
			{
				if (_pluginCtrl.DuringLoading) return;
				if (MoreAccessoriesSupport.ListUsedPartsInfo(_chaCtrl, _chaCtrl.fileStatus.coordinateType).Count > 0)
				{
					Logger.LogMessage("Please clear the accessories on current coordinate before using this function");
					return;
				}
				_pluginCtrl.TaskLock();
				_pluginCtrl.RestorePartsInfo();
			});

			args.AddControl(new MakerButton("Reset", category, this)).OnClick.AddListener(delegate
			{
				if (_pluginCtrl.DuringLoading) return;
				_pluginCtrl.Reset();
				MakerDropdownRef.Value = _pluginCtrl.ReferralIndex;
				MakerToggleEnable.Value = _pluginCtrl.FunctionEnable;
				MakerToggleAutoCopyToBlank.Value = _pluginCtrl.AutoCopyToBlank;
			});
#if DEBUG
			args.AddControl(new MakerButton("Transfer", category, this)).OnClick.AddListener(delegate
			{
				if (_pluginCtrl.DuringLoading) return;
				if (_pluginCtrl.ReferralIndex > 6)
				{
					Logger.LogMessage("Please choose a coordinate other than CharaAcc as referral");
					return;
				}
				_pluginCtrl.TaskLock();
				_pluginCtrl.PrepareQueue();
			});

			args.AddControl(new MakerButton("Hair info (local)", category, this)).OnClick.AddListener(delegate
			{
				_pluginCtrl.HairAccessoryCustomizer.DumpInfo(true);
			});

			args.AddControl(new MakerButton("Hair info", category, this)).OnClick.AddListener(delegate
			{
				_pluginCtrl.HairAccessoryCustomizer.DumpInfo(false);
			});
#endif
		}
	}
}
