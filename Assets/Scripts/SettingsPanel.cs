using Shapes;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SettingsPanel : MonoBehaviour
{
	[SerializeField] private Level _level;
	[SerializeField] private Toggle _winConditionToggle;

	private void Start()
	{
		_winConditionToggle.isOn = _level.WinCondition == Level.WinConditionMode.ConfirmPressInsideRange;

		UpdateWinCondition();
	}

	private void UpdateWinCondition()
	{
		_level.WinCondition = _winConditionToggle.isOn ? Level.WinConditionMode.ConfirmPressInsideRange : Level.WinConditionMode.MeetInsideRange;
	}

	public void UI_UpdateWinCondition()
	{
		UpdateWinCondition();
	}
}
