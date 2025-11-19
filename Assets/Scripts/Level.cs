using UnityEngine;
using Shapes;

public class Level : MonoBehaviour
{
	public enum WinConditionMode
	{
		MeetInsideRange,
		ConfirmPressInsideRange
	}

	[Header("References")]
	[SerializeField] private Polyline _line;
	[SerializeField] private Polyline _goalRange;
	[SerializeField] private Disc _leftDot;
	[SerializeField] private Disc _rightDot;

	[Header("Settings")]
	[SerializeField] private WinConditionMode _winConditionMode;
	[SerializeField] private float _dotSpeed;
	[SerializeField] private float _goalRangeMinWidth;
	[SerializeField] private float _goalRangeMaxWidth;
	[SerializeField] private float _goalRangeMargins;
	[SerializeField] private float _leftAndRightMargins;

	private bool _leftLaunched;
	private bool _rightLaunched;
	private bool _leftConfirmed;
	private bool _rightConfirmed;
	private bool _levelActive;
	private Color _originalDotsColor;

	private void Awake()
	{
		_originalDotsColor = _leftDot.Color;
	}

	private void Start()
	{
		GenerateNewLevel();
	}

	private void Update()
	{
		if (!_levelActive)
		{
			return;
		}

		HandleInput();

		// Move dots
		if (_leftLaunched)
		{
			_leftDot.transform.position += Vector3.right * _dotSpeed * Time.deltaTime;
		}

		if (_rightLaunched)
		{
			_rightDot.transform.position += Vector3.left * _dotSpeed * Time.deltaTime;
		}

		// Check meeting point
		if (_winConditionMode == WinConditionMode.MeetInsideRange && _leftDot.transform.position.x >= _rightDot.transform.position.x)
		{
			_levelActive = false;

			CheckWinCondition();
		}
		else if (_winConditionMode == WinConditionMode.ConfirmPressInsideRange)
		{
			if (_leftConfirmed && _rightConfirmed)
			{
				Debug.Log("Success!");

				GenerateNewLevel();
			}

			// If dots cross each other before confirmations => failure
			if (_leftDot.transform.position.x > _goalRange.transform.position.x + _goalRange.points[_goalRange.points.Count - 1].point.x 
				|| _rightDot.transform.position.x < _goalRange.transform.position.x + _goalRange.points[0].point.x)
			{
				ResetLevel();
			}
		}
	}

	private void HandleInput()
	{
		// Touch input (mobile)
		for (var i = 0; i < Input.touchCount; i++)
		{
			var touch = Input.GetTouch(i);

			if (touch.phase != TouchPhase.Began)
			{
				continue;
			}

			var worldPos = Camera.main.ScreenToWorldPoint(touch.position);

			if (worldPos.x < 0)
			{
				HandleLeftInput();
			}
			else
			{
				HandleRightInput();
			}
		}

		if (Input.GetKeyDown(KeyCode.A))
		{
			HandleLeftInput();
		}
		
		if(Input.GetKeyDown(KeyCode.L))
		{
			HandleRightInput();
		}

		// Mouse input (for testing mobile on desktop)
		if (Input.GetMouseButtonDown(0))
		{
			var worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);

			if (worldPos.x < 0 && !_leftLaunched)
			{
				HandleLeftInput();
			}
			else if (worldPos.x >= 0 && !_rightLaunched)
			{
				HandleRightInput();
			}
		}

		if(Input.GetKeyDown(KeyCode.Return))
		{
			GenerateNewLevel();
		}
	}

	private void HandleLeftInput()
	{
		// If not launched yet: launch
		if (!_leftLaunched)
		{
			_leftLaunched = true;

			return;
		}

		// If using confirm-inside-range mode: treat as confirmation
		if (_winConditionMode == WinConditionMode.ConfirmPressInsideRange)
		{
			if (IsInsideGoalRange(_leftDot))
			{
				_leftConfirmed = true;

				_leftDot.Color = _goalRange.Color;
			}
		}
	}

	private void HandleRightInput()
	{
		if (!_rightLaunched)
		{
			_rightLaunched = true;

			return;
		}

		if (_winConditionMode == WinConditionMode.ConfirmPressInsideRange)
		{
			if (IsInsideGoalRange(_rightDot))
			{
				_rightConfirmed = true;

				_rightDot.Color = _goalRange.Color;
			}
		}
	}

	private bool IsInsideGoalRange(Disc dot)
	{
		var dotX = dot.transform.position.x;

		var leftGoalX = _goalRange.transform.position.x + _goalRange.points[0].point.x;
		var rightGoalX = _goalRange.transform.position.x + _goalRange.points[_goalRange.points.Count - 1].point.x;

		return dotX >= leftGoalX && dotX <= rightGoalX;
	}

	private void CheckWinCondition()
	{
		var meetX = (_leftDot.transform.position.x + _rightDot.transform.position.x) * 0.5f;

		var leftGoalX = _goalRange.transform.position.x + _goalRange.points[0].point.x;
		var rightGoalX = _goalRange.transform.position.x + _goalRange.points[_goalRange.points.Count - 1].point.x;

		var success = meetX >= leftGoalX && meetX <= rightGoalX;

		if (success)
		{
			Debug.Log("Success!");

			GenerateNewLevel();
		}
		else
		{
			Debug.Log("Missed. Try again.");

			ResetLevel();
		}
	}

	private void GenerateNewLevel()
	{
		var camHalfHeight = Camera.main.orthographicSize;
		var camHalfWidth = Camera.main.aspect * camHalfHeight;

		var leftX = Camera.main.transform.position.x - camHalfWidth + _leftAndRightMargins;
		var rightX = Camera.main.transform.position.x + camHalfWidth - _leftAndRightMargins;

		_line.SetPoints(new Vector3[] 
		{ 
			new Vector3(leftX, 0f, 0f), 
			new Vector3(rightX, 0f, 0f)
		});

		var rangeWidth = Random.Range(_goalRangeMinWidth, _goalRangeMaxWidth);

		_goalRange.transform.position = new Vector3(
			Random.Range(
				leftX + _goalRangeMargins + rangeWidth / 2f, 
				rightX - _goalRangeMargins - rangeWidth / 2f), 
			0f, 
			0f);

		_goalRange.SetPoints(new Vector3[]{ 
			new Vector3(- rangeWidth / 2f, 0f, 0f), 
			new Vector3(+ rangeWidth / 2f, 0f, 0f) });

		ResetDots();

		_leftConfirmed = false;
		_rightConfirmed = false;

		_levelActive = true;
	}

	private void ResetLevel()
	{
		ResetDots();

		_leftConfirmed = false;
		_rightConfirmed = false;
	}

	private void ResetDots()
	{
		_leftDot.transform.position = _line.points[0].point;
		_rightDot.transform.position = _line.points[_line.points.Count - 1].point;
		_leftLaunched = _rightLaunched = false;
		_levelActive = true;

		_leftDot.Color = _originalDotsColor;
		_rightDot.Color = _originalDotsColor;
	}
}
