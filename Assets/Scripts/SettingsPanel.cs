using Shapes;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class SettingsPanel : MonoBehaviour
{
	[SerializeField] private Level _level;
	[SerializeField] private Dot _leftDot;
	[SerializeField] private Dot _rightDot;
	[SerializeField] private Toggle _straightLineToggle;
	[SerializeField] private Toggle _winConditionToggle;
	[SerializeField] private TextMeshProUGUI _obstacleCountText;
	[SerializeField] private TextMeshProUGUI _boostCountText;
	[SerializeField] private TextMeshProUGUI _breakCountText;
	[SerializeField] private Toggle _leftInputModeToggle;
	[SerializeField] private Toggle _rightInputModeToggle;

	private void Start()
	{
		_straightLineToggle.isOn = _level.StraightLine;
		_winConditionToggle.isOn = _level.WinCondition == Level.WinConditionMode.ConfirmPressInsideRange;
		_obstacleCountText.text = _level.ObstaclesPerSide.ToString();
		_boostCountText.text = _level.BoostsPerSide.ToString();
		_breakCountText.text = _level.BreaksPerSide.ToString();

		_leftInputModeToggle.isOn = _leftDot.MovementMode == Dot.DotMovementMode.HoldToMove;
		_rightInputModeToggle.isOn = _rightDot.MovementMode == Dot.DotMovementMode.HoldToMove;

		//UpdateStraightLine();
		//UpdateWinCondition();
		//UpdateObstacleCount();
	}

	private void UpdateStraightLine()
	{
		_level.StraightLine = _straightLineToggle.isOn;
	}

	private void UpdateWinCondition()
	{
		_level.WinCondition = _winConditionToggle.isOn ? Level.WinConditionMode.ConfirmPressInsideRange : Level.WinConditionMode.MeetInsideRange;
	}

	private void UpdateObstacleCount()
	{
		var currentValue = int.Parse(_obstacleCountText.text);
		var newValue = currentValue + 1;

		if(newValue > 3)
		{
			newValue = 0;
		}

		_level.ObstaclesPerSide = newValue;

		_obstacleCountText.text = newValue.ToString();
	}

	private void UpdateBoostCount()
	{
		var currentValue = int.Parse(_boostCountText.text);
		var newValue = currentValue + 1;

		if (newValue > 3)
		{
			newValue = 0;
		}

		_level.BoostsPerSide = newValue;

		_boostCountText.text = newValue.ToString();
	}

	private void UpdateBreakCount()
	{
		var currentValue = int.Parse(_breakCountText.text);
		var newValue = currentValue + 1;

		if (newValue > 3)
		{
			newValue = 0;
		}

		_level.BreaksPerSide = newValue;

		_breakCountText.text = newValue.ToString();
	}

	private void UpdateLeftInputMode()
	{
		_leftDot.MovementMode = _leftInputModeToggle.isOn ? Dot.DotMovementMode.HoldToMove : Dot.DotMovementMode.TapToLaunch;
	}

	private void UpdateRightInputMode()
	{
		_rightDot.MovementMode = _rightInputModeToggle.isOn ? Dot.DotMovementMode.HoldToMove : Dot.DotMovementMode.TapToLaunch;
	}

	public void UI_UpdateStraightLine()
	{
		UpdateStraightLine();
	}

	public void UI_UpdateWinCondition()
	{
		UpdateWinCondition();
	}

	public void UI_UpdateObstacleCount()
	{
		UpdateObstacleCount();
	}

	public void UI_UpdateBoostCount()
	{
		UpdateBoostCount();
	}

	public void UI_UpdateBreakCount()
	{
		UpdateBreakCount();
	}

	public void UI_UpdateLeftInputMode()
	{
		UpdateLeftInputMode();
	}

	public void UI_UpdateRightInputMode()
	{
		UpdateRightInputMode();
	}

	public void UI_GenerateNewLevel()
	{
		_level.GenerateNewLevel();
	}
}
