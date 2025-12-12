using Shapes;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using static Level;

public class Dot : MonoBehaviour
{
	public enum DotMovementMode
	{
		TapToLaunch,    // Original mode: tap to start, hold to slow
		HoldToMove      // New mode: hold to move forward, release to go back
	}

	[SerializeField] private Level.Side _side = Level.Side.Left;
	[SerializeField] private DotMovementMode _movementMode = DotMovementMode.TapToLaunch;
	[SerializeField] private float _dotSpeed = 3f;
	[SerializeField] private float _minSpeedMultiplier = 0.25f;
	[SerializeField] private float _slowdownRate = 1.0f;
	[SerializeField] private float _recoveryRate = 4.0f;
	[SerializeField] private float _holdTimeThreshold = 0.12f;

	[Header("Hold To Move Settings")]
	[SerializeField] private float _reverseSpeedMultiplier = 1.5f;

	[Header("Boost/Break Settings")]
	[SerializeField] private float _boostSpeedMultiplier = 2.0f;
	[SerializeField] private float _breakSpeedMultiplier = 0.5f;

	public Disc Disc { get; set; }
	public DotMovementMode MovementMode
	{
		get
		{
			return _movementMode;
		}
		set
		{
			_movementMode = value;
		}
	}

	public float DistanceOnPath { get; set; }
	public bool GoalConfirmed { get; set; }

	private HashSet<GameObject> _confirmedObstacles = new HashSet<GameObject>();
	private HashSet<GameObject> _hiddenObstacles = new HashSet<GameObject>();
	private GameObject _currentObstacle = null;

	private Level _level;

	private bool _launched;
	private bool _holding;
	private bool _wasHoldingLastFrame;

	private float _speedMultiplier;
	private float _boostBreakMultiplier;

	private float _holdStart;
	private bool _keyActive;
	private bool _mouseActive;
	private float _mouseStart;
	private int? _touchId;
	private float _touchStart;

	public void Initialize(Level level)
	{
		_level = level;

		Disc = GetComponent<Disc>();
	}

	public void Reinitialize()
	{
		DistanceOnPath = _side == Level.Side.Left ? 0 : _level.PathLength;

		_launched = false;

		Disc.Color = Color.white;

		_speedMultiplier = 1f;
		_boostBreakMultiplier = 1f;
		_holding = false;
		_wasHoldingLastFrame = false;

		_keyActive = false;
		_mouseActive = false;
		_touchId = null;

		GoalConfirmed = false;

		_confirmedObstacles.Clear();
		_currentObstacle = null;

		// In TapToLaunch mode, reset all hidden obstacles
		if (_movementMode == DotMovementMode.TapToLaunch)
		{
			ResetHiddenObstacles();
		}
	}

	private void Update()
	{
		if (!_level.Active)
		{
			return;
		}

		_wasHoldingLastFrame = _holding;
		_holding = false;

		HandleInput();

		if (_movementMode == DotMovementMode.TapToLaunch)
		{
			UpdateTapToLaunchMode();
		}
		else // HoldToMove
		{
			UpdateHoldToMoveMode();
		}

		// Detect release in HoldToMove mode
		if (_movementMode == DotMovementMode.HoldToMove && _wasHoldingLastFrame && !_holding)
		{
			HandleReleaseConfirmation();
		}
	}

	private void UpdateTapToLaunchMode()
	{
		if (_launched)
		{
			if (_holding)
			{
				_speedMultiplier = Mathf.Max(_minSpeedMultiplier, _speedMultiplier - _slowdownRate * Time.deltaTime);
			}
			else
			{
				_speedMultiplier = Mathf.Min(1f, _speedMultiplier + _recoveryRate * Time.deltaTime);
			}
		}
		else
		{
			_speedMultiplier = 1f;
		}

		// Check for boost/break effects
		UpdateBoostBreakMultiplier();

		var delta = _dotSpeed * _speedMultiplier * _boostBreakMultiplier * Time.deltaTime;

		if (_launched)
		{
			DistanceOnPath = _side == Level.Side.Left
				? Mathf.Min(_level.PathLength, DistanceOnPath + delta)
				: Mathf.Max(0f, DistanceOnPath - delta);

			var position = _level.GetPositionAtDistance(DistanceOnPath);

			transform.position = position;

			CheckObstacleStatus();
		}
	}

	private void UpdateHoldToMoveMode()
	{
		// Check for boost/break effects
		UpdateBoostBreakMultiplier();

		float delta;

		if (_holding)
		{
			// Moving forward (in the dot's normal direction)
			delta = _dotSpeed * _boostBreakMultiplier * Time.deltaTime;

			DistanceOnPath = _side == Level.Side.Left
				? Mathf.Min(_level.PathLength, DistanceOnPath + delta)
				: Mathf.Max(0f, DistanceOnPath - delta);
		}
		else
		{
			// Moving backward (reverse direction, faster)
			delta = _dotSpeed * _reverseSpeedMultiplier * _boostBreakMultiplier * Time.deltaTime;

			DistanceOnPath = _side == Level.Side.Left
				? Mathf.Max(0f, DistanceOnPath - delta)
				: Mathf.Min(_level.PathLength, DistanceOnPath + delta);
		}

		var position = _level.GetPositionAtDistance(DistanceOnPath);
		transform.position = position;

		// Check if dot has returned to start in HoldToMove mode
		bool atStart = _side == Level.Side.Left ? DistanceOnPath <= 0.01f : DistanceOnPath >= _level.PathLength - 0.01f;
		if (atStart)
		{
			ResetHiddenObstacles();
		}

		CheckObstacleStatus();
		CheckGoalStatus();
	}

	private void UpdateBoostBreakMultiplier()
	{
		// Check if inside a boost
		var boost = _level.IsDistanceInsideBoost(DistanceOnPath);
		if (boost != null)
		{
			_boostBreakMultiplier = _boostSpeedMultiplier;
			return;
		}

		// Check if inside a break
		var breakRange = _level.IsDistanceInsideBreak(DistanceOnPath);
		if (breakRange != null)
		{
			_boostBreakMultiplier = _breakSpeedMultiplier;
			return;
		}

		// Not in any special range, reset to normal
		_boostBreakMultiplier = 1f;
	}

	private void HandleInput()
	{
		if (Input.touchCount > 0)
		{
			for (var i = 0; i < Input.touchCount; i++)
			{
				var touch = Input.GetTouch(i);
				var worldPos = Camera.main.ScreenToWorldPoint(touch.position);
				var isLeftSide = worldPos.x < Camera.main.transform.position.x;
				var validTouchForSide = isLeftSide && _side == Level.Side.Left || !isLeftSide && _side == Level.Side.Right;

				var tappedOnUi = IsPointerOverUIObject();

				if (tappedOnUi)
				{
					continue;
				}

				if (!validTouchForSide)
				{
					continue;
				}

				if (touch.phase == TouchPhase.Began)
				{
					if (_touchId == null)
					{
						_touchId = touch.fingerId;
						_touchStart = Time.time;

						if (_movementMode == DotMovementMode.TapToLaunch && !_launched)
						{
							_launched = true;
						}
					}
				}
				else if (touch.phase == TouchPhase.Stationary || touch.phase == TouchPhase.Moved)
				{
					if (_touchId == touch.fingerId)
					{
						if (_movementMode == DotMovementMode.TapToLaunch)
						{
							if (_launched && Time.time - _touchStart >= _holdTimeThreshold)
							{
								_holding = true;
							}
						}
						else // HoldToMove
						{
							_holding = true;
						}
					}
				}
				else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
				{
					if (_touchId == touch.fingerId)
					{
						var duration = Time.time - _touchStart;

						if (_movementMode == DotMovementMode.TapToLaunch)
						{
							if (duration < _holdTimeThreshold)
							{
								if (_launched)
								{
									HandleTapConfirmation();
								}
							}
						}
						else // HoldToMove
						{
							if (duration < _holdTimeThreshold)
							{
								HandleTapConfirmation();
							}
						}

						_touchId = null;
						_holding = false;
						_touchStart = 0f;
					}
				}
			}
		}

		if (Input.GetMouseButtonDown(0))
		{
			var worldPosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
			var isLeftSide = worldPosition.x < Camera.main.transform.position.x;
			var validClickForSide = isLeftSide && _side == Level.Side.Left || !isLeftSide && _side == Level.Side.Right;

			var mouseOnUi = IsPointerOverUIObject();

			if (!mouseOnUi && validClickForSide)
			{
				_mouseActive = true;
				_mouseStart = Time.time;

				if (_movementMode == DotMovementMode.TapToLaunch && !_launched)
				{
					_launched = true;
				}
			}
		}

		if (Input.GetMouseButton(0))
		{
			var worldPositon = Camera.main.ScreenToWorldPoint(Input.mousePosition);
			var isLeftSide = worldPositon.x < Camera.main.transform.position.x;
			var validClickForSide = isLeftSide && _side == Level.Side.Left || !isLeftSide && _side == Level.Side.Right;

			if (validClickForSide && _mouseActive)
			{
				if (_movementMode == DotMovementMode.TapToLaunch)
				{
					if (_launched && Time.time - _mouseStart >= _holdTimeThreshold)
					{
						_holding = true;
					}
				}
				else // HoldToMove
				{
					_holding = true;
				}
			}
		}

		if (Input.GetMouseButtonUp(0))
		{
			var worldPosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
			var isLeftSide = worldPosition.x < Camera.main.transform.position.x;
			var validClickForSide = isLeftSide && _side == Level.Side.Left || !isLeftSide && _side == Level.Side.Right;

			if (validClickForSide && _mouseActive)
			{
				var duration = Time.time - _mouseStart;

				if (duration < _holdTimeThreshold)
				{
					if (_movementMode == DotMovementMode.TapToLaunch)
					{
						if (_launched)
						{
							HandleTapConfirmation();
						}
					}
					else // HoldToMove
					{
						HandleTapConfirmation();
					}
				}

				_mouseActive = false;
				_holding = false;
				_mouseStart = 0f;
			}
		}

		if (Input.GetKeyDown(KeyCode.A) && _side == Level.Side.Left || Input.GetKeyDown(KeyCode.L) && _side == Level.Side.Right)
		{
			_keyActive = true;
			_holdStart = Time.time;

			if (_movementMode == DotMovementMode.TapToLaunch && !_launched)
			{
				_launched = true;
			}
		}
		if (Input.GetKeyUp(KeyCode.A) && _side == Level.Side.Left || Input.GetKeyUp(KeyCode.L) && _side == Level.Side.Right)
		{
			var duration = Time.time - _holdStart;

			_keyActive = false;
			_holding = false;
			_holdStart = 0f;

			if (duration < _holdTimeThreshold)
			{
				if (_movementMode == DotMovementMode.TapToLaunch)
				{
					if (_launched)
					{
						HandleTapConfirmation();
					}
				}
				else // HoldToMove
				{
					HandleTapConfirmation();
				}
			}
		}
		if (Input.GetKey(KeyCode.A) && _side == Level.Side.Left || Input.GetKey(KeyCode.L) && _side == Level.Side.Right)
		{
			if (_movementMode == DotMovementMode.TapToLaunch)
			{
				if (_launched && _keyActive && Time.time - _holdStart >= _holdTimeThreshold)
				{
					_holding = true;
				}
			}
			else // HoldToMove
			{
				if (_keyActive)
				{
					_holding = true;
				}
			}
		}
	}

	private void CheckObstacleStatus()
	{
		var obstacle = _level.IsDistanceInsideObstacle(DistanceOnPath);

		if (obstacle != null && obstacle != _currentObstacle)
		{
			_currentObstacle = obstacle;
		}

		else if (obstacle == null && _currentObstacle != null)
		{
			if (!_confirmedObstacles.Contains(_currentObstacle))
			{
				Disc.Color = Color.red;

				_level.Fail();

				return;
			}

			Disc.Color = Color.white;

			_currentObstacle = null;
		}
	}

	private void CheckGoalStatus()
	{
		// Only check goal status in HoldToMove mode with ConfirmPress win condition
		if (_movementMode != DotMovementMode.HoldToMove || _level.WinCondition != WinConditionMode.ConfirmPressInsideRange)
		{
			return;
		}

		bool insideGoal = _level.IsDistanceInsideGoal(DistanceOnPath);

		// If we were confirmed but now we're outside the goal, reset confirmation
		if (GoalConfirmed && !insideGoal)
		{
			GoalConfirmed = false;

			// Reset color to white unless we're in an obstacle
			var obstacle = _level.IsDistanceInsideObstacle(DistanceOnPath);
			if (obstacle == null || !_confirmedObstacles.Contains(obstacle))
			{
				Disc.Color = Color.white;
			}
		}
	}

	private void HandleTapConfirmation()
	{
		// Check if inside an obstacle
		var obstacle = _level.IsDistanceInsideObstacle(DistanceOnPath);
		if (obstacle != null && _level.IsObstacleOwnedBySide(obstacle, _side))
		{
			// Confirm this obstacle
			if (!_confirmedObstacles.Contains(obstacle))
			{
				_confirmedObstacles.Add(obstacle);

				Disc.Color = obstacle.GetComponent<Polyline>().Color;

				// Hide the obstacle
				HideObstacle(obstacle);
			}
		}

		// Check goal confirmation (only in ConfirmPress mode)
		if (_level.WinCondition == WinConditionMode.ConfirmPressInsideRange)
		{
			if (_level.IsDistanceInsideGoal(DistanceOnPath))
			{
				GoalConfirmed = true;
				Disc.Color = _level.GoalRange.Color;
			}
		}
	}

	private void HandleReleaseConfirmation()
	{
		// Check if inside an obstacle
		var obstacle = _level.IsDistanceInsideObstacle(DistanceOnPath);
		if (obstacle != null && _level.IsObstacleOwnedBySide(obstacle, _side))
		{
			// Confirm this obstacle
			if (!_confirmedObstacles.Contains(obstacle))
			{
				_confirmedObstacles.Add(obstacle);

				Disc.Color = obstacle.GetComponent<Polyline>().Color;

				// Hide the obstacle
				HideObstacle(obstacle);
			}
		}

		// Check goal confirmation (only in ConfirmPress mode)
		if (_level.WinCondition == WinConditionMode.ConfirmPressInsideRange)
		{
			if (_level.IsDistanceInsideGoal(DistanceOnPath))
			{
				GoalConfirmed = true;
				Disc.Color = _level.GoalRange.Color;
			}
		}
	}

	private void HideObstacle(GameObject obstacle)
	{
		if (!_hiddenObstacles.Contains(obstacle))
		{
			_hiddenObstacles.Add(obstacle);
			obstacle.SetActive(false);
		}
	}

	private void ResetHiddenObstacles()
	{
		foreach (var obstacle in _hiddenObstacles)
		{
			if (obstacle != null)
			{
				obstacle.SetActive(true);
			}
		}
		_hiddenObstacles.Clear();
		_confirmedObstacles.Clear();
	}

	private bool IsPointerOverUIObject()
	{
		var eventDataCurrentPosition = new PointerEventData(EventSystem.current)
		{
			position = new Vector2(Input.mousePosition.x, Input.mousePosition.y)
		};

		var results = new List<RaycastResult>();

		EventSystem.current.RaycastAll(eventDataCurrentPosition, results);

		return results.Count > 0;
	}
}