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
		internal static MakerDropdown _makerDropdownReferral;
		internal static MakerToggle _makerToggleEnable;
		internal static MakerToggle _makerToggleAutoCopyToBlank;
		internal static SidebarToggle _sidebarToggleEnable;

		private void RegisterCustomSubCategories(object _sender, RegisterSubCategoriesEvent _args)
		{
			ChaControl _chaCtrl = CustomBase.Instance.chaCtrl;
			CharacterAccessoryController _pluginCtrl = GetController(_chaCtrl);

			_sidebarToggleEnable = _args.AddSidebarControl(new SidebarToggle("CharaAcc", _cfgMakerMasterSwitch.Value, this));
			_sidebarToggleEnable.ValueChanged.Subscribe(_value => _cfgMakerMasterSwitch.Value = _value);

			MakerCategory _category = new MakerCategory("03_ClothesTop", "tglCharaAcc", MakerConstants.Clothes.Copy.Position + 1, "CharaAcc");
			_args.AddSubCategory(_category);

			_args.AddControl(new MakerText("The set to be used as a template to clone on load", _category, this));

			List<string> _coordinateList = _cordNames.ToList();
			_coordinateList.Add("CharaAcc");
			_makerDropdownReferral = new MakerDropdown("Referral", _coordinateList.ToArray(), _category, 7, this);

			_makerDropdownReferral.ValueChanged.Subscribe(_value => _pluginCtrl.SetReferralIndex(_value));

			_args.AddControl(_makerDropdownReferral);

			_makerToggleEnable = _args.AddControl(new MakerToggle(_category, "Enable", false, this));
			_makerToggleEnable.ValueChanged.Subscribe(_value => _pluginCtrl.FunctionEnable = _value);

			_makerToggleAutoCopyToBlank = _args.AddControl(new MakerToggle(_category, "Auto Copy To Blank", false, this));
			_makerToggleAutoCopyToBlank.ValueChanged.Subscribe(_value => _pluginCtrl.AutoCopyToBlank = _value);

			_args.AddControl(new MakerButton("Backup", _category, this)).OnClick.AddListener(delegate
			{
				if (_pluginCtrl.DuringLoading) return;
				_pluginCtrl.Backup();
				_pluginCtrl.SetReferralIndex(-1);
				_pluginCtrl.FunctionEnable = true;
				_makerToggleEnable.Value = _pluginCtrl.FunctionEnable;
				_logger.LogMessage($"Character Accessory data saved");
			});

			_args.AddControl(new MakerButton("Restore", _category, this)).OnClick.AddListener(delegate
			{
				if (_pluginCtrl.DuringLoading) return;
				if (MoreAccessoriesSupport.ListUsedPartsInfo(_chaCtrl, _chaCtrl.fileStatus.coordinateType).Count > 0)
				{
					_logger.LogMessage("Please clear the accessories on current coordinate before using this function");
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
				_makerToggleEnable.Value = _pluginCtrl.FunctionEnable;
				_makerToggleAutoCopyToBlank.Value = _pluginCtrl.AutoCopyToBlank;
				_logger.LogMessage($"Character Accessory data cleared");
			});

			if (JetPack.Game.ConsoleActive)
			{
				_args.AddControl(new MakerSeparator(_category, this));

				_args.AddControl(new MakerText("Diagnostic info", _category, this));

				_args.AddControl(new MakerButton("AAAPK", _category, this)).OnClick.AddListener(delegate
				{
					_logger.LogInfo("[AAAPK]\n" + _pluginCtrl.AAAPK.Report());
				});

				_args.AddControl(new MakerButton("AccStateSync", _category, this)).OnClick.AddListener(delegate
				{
					_logger.LogInfo("[AccStateSync]\n" + _pluginCtrl.AccStateSync.Report());
				});

				_args.AddControl(new MakerButton("BendUrAcc", _category, this)).OnClick.AddListener(delegate
				{
					_logger.LogInfo("[BendUrAcc]\n" + _pluginCtrl.BendUrAcc.Report());
				});

				_args.AddControl(new MakerButton("Dynamic Bone Editor", _category, this)).OnClick.AddListener(delegate
				{
					_logger.LogInfo("[Dynamic Bone Editor]\n" + _pluginCtrl.DynamicBoneEditor.Report());
				});

				_args.AddControl(new MakerButton("Hair Accessory Customizer", _category, this)).OnClick.AddListener(delegate
				{
					_logger.LogInfo("[Hair Accessory Customizer]\n" + _pluginCtrl.HairAccessoryCustomizer.Report());
				});

				_args.AddControl(new MakerButton("Material Editor", _category, this)).OnClick.AddListener(delegate
				{
					_logger.LogInfo("[Material Editor]\n" + _pluginCtrl.MaterialEditor.Report());
				});

				_args.AddControl(new MakerButton("Material Router", _category, this)).OnClick.AddListener(delegate
				{
					_logger.LogInfo("[Material Router]\n" + _pluginCtrl.MaterialRouter.Report());
				});
			}
		}
	}
}
