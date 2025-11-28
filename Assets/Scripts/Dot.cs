using Shapes;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using static Level;

public class Dot : MonoBehaviour
{
	public enum Side
	{
		Left,
		Right
	}

	[SerializeField] private Side _side = Side.Left;
	[SerializeField] private float _dotSpeed = 3f;
	[SerializeField] private float _minSpeedMultiplier = 0.25f;
	[SerializeField] private float _slowdownRate = 1.0f;
	[SerializeField] private float _recoveryRate = 4.0f;
	[SerializeField] private float _holdTimeThreshold = 0.12f;

	public Disc Disc { get; set; }

	public float DistanceOnPath { get; set; }
	public bool Confirmed { get; set; }

	private Level _level;
	
	private bool _launched;
	private bool _holding;
	
	private float _speedMultiplier;

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
		DistanceOnPath = _side == Side.Left ? 0 : _level.PathLength;

		_launched = false;

		Disc.Color = Color.white;

		_speedMultiplier = 1f;
		_holding = false;

		_keyActive = false;
		_mouseActive = false;
		_touchId = null;

		Confirmed = false;
	}

	private void Update()
	{
		if (!_level.Active)
		{
			return;
		}

		_holding = false;

		HandleInput();

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

		var delta = _dotSpeed * _speedMultiplier * Time.deltaTime;

		if (_launched)
		{
			DistanceOnPath = _side == Side.Left
				? Mathf.Min(_level.PathLength, DistanceOnPath + delta)
				: Mathf.Max(0f, DistanceOnPath - delta);

			var position = _level.GetPositionAtDistance(DistanceOnPath);

			transform.position = position;
		}
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
				var validTouchForSide = isLeftSide && _side == Side.Left|| !isLeftSide && _side == Side.Right;

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

						if (!_launched)
						{
							_launched = true;
						}
					}
				}
				else if (touch.phase == TouchPhase.Stationary || touch.phase == TouchPhase.Moved)
				{
					if (_touchId == touch.fingerId)
					{
						if (_launched && Time.time - _touchStart >= _holdTimeThreshold)
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

						if (duration < _holdTimeThreshold)
						{
							if (_launched && _level.WinCondition == WinConditionMode.ConfirmPressInsideRange)
							{
								if (_level.IsDistanceInsideGoal(DistanceOnPath))
								{
									Confirmed = true;
									Disc.Color = _level.GoalRange.Color;
								}
							}
						}

						_touchId = null;
						_holding = false;
						_touchStart = 0f;
					}
				}
			}
		}
		else
		{
			
		}

		if (Input.GetMouseButtonDown(0))
		{
			var worldPosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
			var isLeftSide = worldPosition.x < Camera.main.transform.position.x;
			var validClickForSide = isLeftSide && _side == Side.Left || !isLeftSide && _side == Side.Right;

			var mouseOnUi = IsPointerOverUIObject();

			if (!mouseOnUi && validClickForSide)
			{
				_mouseActive = true;
				_mouseStart = Time.time;

				if (!_launched)
				{
					_launched = true;
				}
			}
		}

		if (Input.GetMouseButton(0))
		{
			var worldPositon = Camera.main.ScreenToWorldPoint(Input.mousePosition);
			var isLeftSide = worldPositon.x < Camera.main.transform.position.x;
			var validClickForSide = isLeftSide && _side == Side.Left || !isLeftSide && _side == Side.Right;

			if (validClickForSide && _launched && _mouseActive && Time.time - _mouseStart >= _holdTimeThreshold)
			{
				_holding = true;
			}
		}

		if (Input.GetMouseButtonUp(0)) 
		{
			var worldPosition = Camera.main.ScreenToWorldPoint(Input.mousePosition);
			var isLeftSide = worldPosition.x < Camera.main.transform.position.x;
			var validClickForSide = isLeftSide && _side == Side.Left || !isLeftSide && _side == Side.Right;

			if (validClickForSide && _mouseActive)
			{
				var duration = Time.time - _mouseStart;

				if (duration < _holdTimeThreshold)
				{
					if (_launched && _level.WinCondition == WinConditionMode.ConfirmPressInsideRange)
					{
						if (_level.IsDistanceInsideGoal(DistanceOnPath))
						{
							Confirmed = true;

							Disc.Color = _level.GoalRange.Color;
						}
					}
				}

				_mouseActive = false;
				_holding = false;
				_mouseStart = 0f;
			}
		}

		if (Input.GetKeyDown(KeyCode.A) && _side == Side.Left || Input.GetKeyDown(KeyCode.L) && _side == Side.Right)
		{
			_keyActive = true;
			_holdStart = Time.time;

			if (!_launched)
			{
				_launched = true;
			}
		}
		if (Input.GetKeyUp(KeyCode.A) && _side == Side.Left || Input.GetKeyUp(KeyCode.L) && _side == Side.Right)
		{
			var duration = Time.time - _holdStart;

			_keyActive = false;
			_holding = false;
			_holdStart = 0f;

			if (duration < _holdTimeThreshold)
			{
				if (_launched && _level.WinCondition == WinConditionMode.ConfirmPressInsideRange)
				{
					if (_level.IsDistanceInsideGoal(DistanceOnPath))
					{
						Confirmed = true;

						Disc.Color = _level.GoalRange.Color;
					}
				}
			}
		}
		if (Input.GetKey(KeyCode.A) && _side == Side.Left || Input.GetKey(KeyCode.L) && _side == Side.Right)
		{
			if (_launched && _keyActive && Time.time - _holdStart >= _holdTimeThreshold)
			{
				_holding = true;
			}
		}
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
