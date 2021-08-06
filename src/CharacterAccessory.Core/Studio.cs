using System;
using System.Collections.Generic;
using System.Linq;

using UniRx;

using KKAPI.Studio;
using KKAPI.Studio.UI;

namespace CharacterAccessory
{
	public partial class CharacterAccessory
	{
		internal static void RegisterStudioControls()
		{
			CurrentStateCategorySwitch _tglEnable = new CurrentStateCategorySwitch("Enable", OCIChar => (bool) GetController(OCIChar)?.FunctionEnable);
			_tglEnable.Value.Subscribe(_value =>
			{
				CharacterAccessoryController _pluginCtrl = StudioAPI.GetSelectedControllers<CharacterAccessoryController>().FirstOrDefault();
				if (_pluginCtrl == null) return;
				_pluginCtrl.FunctionEnable = _value;
			});
			StudioAPI.GetOrCreateCurrentStateCategory("CharaAcc").AddControl(_tglEnable);

			CurrentStateCategorySwitch _tglAutoCopy = new CurrentStateCategorySwitch("Copy To Blank", OCIChar => (bool) GetController(OCIChar)?.AutoCopyToBlank);
			_tglAutoCopy.Value.Subscribe(_value =>
			{
				CharacterAccessoryController _pluginCtrl = StudioAPI.GetSelectedControllers<CharacterAccessoryController>().FirstOrDefault();
				if (_pluginCtrl == null) return;
				_pluginCtrl.AutoCopyToBlank = _value;
			});
			StudioAPI.GetOrCreateCurrentStateCategory("CharaAcc").AddControl(_tglAutoCopy);

			List<string> _coordinateList = Enum.GetNames(typeof(ChaFileDefine.CoordinateType)).ToList();
			_coordinateList.Add("CharaAcc");
			CurrentStateCategoryDropdown _ddRef = new CurrentStateCategoryDropdown("Referral", _coordinateList.ToArray(), OCIChar => (int) GetController(OCIChar)?.GetReferralIndex());
			_ddRef.Value.Subscribe(_value =>
			{
				CharacterAccessoryController _pluginCtrl = StudioAPI.GetSelectedControllers<CharacterAccessoryController>().FirstOrDefault();
				if (_pluginCtrl == null) return;
				_pluginCtrl.SetReferralIndex(_value);
			});
			StudioAPI.GetOrCreateCurrentStateCategory("CharaAcc").AddControl(_ddRef);
		}
	}
}
