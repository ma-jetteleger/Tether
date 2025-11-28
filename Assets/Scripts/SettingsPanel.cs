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
	[SerializeField] private Toggle _straightLineToggle;
	[SerializeField] private Toggle _winConditionToggle;
	[SerializeField] private TextMeshProUGUI _obstacleCountText;

	private void Start()
	{
		_straightLineToggle.isOn = _level.StraightLine;
		_winConditionToggle.isOn = _level.WinCondition == Level.WinConditionMode.ConfirmPressInsideRange;
		_obstacleCountText.text = _level.ObstaclesPerSide.ToString();

		//UpdateStraightLine();
		//UpdateWinCondition();
		//UpdateObstacleCount();
	}

	private void UpdateStraightLine()
	{
		_level.StraightLine = _straightLineToggle.isOn;
	}

	public void UI_UpdateStraightLine()
	{
		UpdateStraightLine();
	}

	private void UpdateWinCondition()
	{
		_level.WinCondition = _winConditionToggle.isOn ? Level.WinConditionMode.ConfirmPressInsideRange : Level.WinConditionMode.MeetInsideRange;
	}

	public void UI_UpdateWinCondition()
	{
		UpdateWinCondition();
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

	public void UI_UpdateObstacleCount()
	{
		UpdateObstacleCount();
	}

	public void UI_GenerateNewLevel()
	{
		_level.GenerateNewLevel();
	}
}
