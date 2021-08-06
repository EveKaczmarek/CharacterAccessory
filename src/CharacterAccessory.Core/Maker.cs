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

		private void RegisterCustomSubCategories(object _sender, RegisterSubCategoriesEvent _args)
		{
			ChaControl _chaCtrl = CustomBase.Instance.chaCtrl;
			CharacterAccessoryController _pluginCtrl = GetController(_chaCtrl);

			SidebarToggleEnable = _args.AddSidebarControl(new SidebarToggle("CharaAcc", _cfgMakerMasterSwitch.Value, this));
			SidebarToggleEnable.ValueChanged.Subscribe(_value => _cfgMakerMasterSwitch.Value = _value);

			MakerCategory _category = new MakerCategory("03_ClothesTop", "tglCharaAcc", MakerConstants.Clothes.Copy.Position + 1, "CharaAcc");
			_args.AddSubCategory(_category);

			_args.AddControl(new MakerText("The set to be used as a template to clone on load", _category, this));

			List<string> _coordinateList = CordNames.ToList();
			_coordinateList.Add("CharaAcc");
			MakerDropdownRef = new MakerDropdown("Referral", _coordinateList.ToArray(), _category, 7, this);

			MakerDropdownRef.ValueChanged.Subscribe(_value => _pluginCtrl.SetReferralIndex(_value));

			_args.AddControl(MakerDropdownRef);

			MakerToggleEnable = _args.AddControl(new MakerToggle(_category, "Enable", false, this));
			MakerToggleEnable.ValueChanged.Subscribe(_value => _pluginCtrl.FunctionEnable = _value);

			MakerToggleAutoCopyToBlank = _args.AddControl(new MakerToggle(_category, "Auto Copy To Blank", false, this));
			MakerToggleAutoCopyToBlank.ValueChanged.Subscribe(_value => _pluginCtrl.AutoCopyToBlank = _value);

			_args.AddControl(new MakerButton("Backup", _category, this)).OnClick.AddListener(delegate
			{
				if (_pluginCtrl.DuringLoading) return;
				_pluginCtrl.Backup();
				_pluginCtrl.SetReferralIndex(-1);
				_pluginCtrl.FunctionEnable = true;
				MakerToggleEnable.Value = _pluginCtrl.FunctionEnable;
			});

			_args.AddControl(new MakerButton("Restore", _category, this)).OnClick.AddListener(delegate
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

			_args.AddControl(new MakerButton("Reset", _category, this)).OnClick.AddListener(delegate
			{
				if (_pluginCtrl.DuringLoading) return;
				_pluginCtrl.Reset();
				_pluginCtrl.SetReferralIndex(-1);
				MakerToggleEnable.Value = _pluginCtrl.FunctionEnable;
				MakerToggleAutoCopyToBlank.Value = _pluginCtrl.AutoCopyToBlank;
			});
		}
	}
}
