using System;
using System.Collections.Generic;
using UnityEngine;
using Shapes;

public class LevelAB : MonoBehaviour
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
	[SerializeField] private WinConditionMode _winConditionMode = WinConditionMode.MeetInsideRange;
	[SerializeField] private float _dotSpeed = 3f;                  // units per second along the spline
	[SerializeField] private float _goalRangeMinWidth = 0.5f;       // interpreted as spline-distance
	[SerializeField] private float _goalRangeMaxWidth = 2f;         // interpreted as spline-distance
	[SerializeField] private float _goalRangeMargins = 0.5f;        // world-space X margin when choosing goal location
	[SerializeField] private float _leftAndRightMargins = 0.5f;     // world-space X margin for endpoints

	[Header("Spline / Curviness (Centripetal Catmull-Rom)")]
	[SerializeField, Tooltip("Total number of control points including start and end. Must be >= 2.")] private int _pathControlPoints = 7;
	[SerializeField, Tooltip("Maximum jitter amplitude applied to control points (both X and Y when XY jitter enabled).")] private float _pathAmplitude = 2f;
	[SerializeField, Tooltip("If true, jitter is applied on both X and Y. If false, jitter only affects Y.")] private bool _xyJitter = true;
	[SerializeField, Tooltip("How many samples per spline segment (higher = smoother & more accurate distance mapping).")] private int _samplesPerSegment = 12;
	[SerializeField, Tooltip("Random seed (0 = random seed).")] private int _seed = 0;

	// runtime state
	private bool _leftLaunched;
	private bool _rightLaunched;
	private bool player1Confirmed;
	private bool player2Confirmed;
	private bool _levelActive;

	// spline / sampling data
	private List<Vector3> _controlPoints;      // padded list used for Catmull-Rom (we'll build with extra endpoints)
	private List<Vector3> _sampledPoints;      // sampled positions along spline (visual + distance mapping)
	private float[] _cumulativeLengths;        // cumulative distance per sampled point
	private float _pathLength = 0f;            // total spline length

	// dot positions tracked as distances along the spline (0.._pathLength)
	private float _leftDistance;
	private float _rightDistance;

	// goal in distance-space
	private float _goalStartDistance;
	private float _goalEndDistance;

	private System.Random _rng;

	private void Start()
	{
		_rng = _seed == 0 ? new System.Random() : new System.Random(_seed);
		// clamp settings to safe ranges
		_pathControlPoints = Mathf.Max(2, _pathControlPoints);
		_samplesPerSegment = Mathf.Max(4, _samplesPerSegment);

		GenerateNewLevel();
	}

	private void Update()
	{
		if (!_levelActive) return;

		HandleInput();

		// Move dots along the spline by distance (constant speed)
		float delta = _dotSpeed * Time.deltaTime;

		if (_leftLaunched)
		{
			_leftDistance = Mathf.Min(_pathLength, _leftDistance + delta);
			Vector3 pos = GetPositionAtDistance(_leftDistance);
			_leftDot.transform.position = pos;
		}

		if (_rightLaunched)
		{
			_rightDistance = Mathf.Max(0f, _rightDistance - delta);
			Vector3 pos = GetPositionAtDistance(_rightDistance);
			_rightDot.transform.position = pos;
		}

		// Win/Fail checks depend on chosen mode (in distance-space)
		if (_winConditionMode == WinConditionMode.MeetInsideRange)
		{
			if (_leftDistance >= _rightDistance)
			{
				_levelActive = false;
				CheckWinCondition_MeetMode();
			}
		}
		else // ConfirmPressInsideRange
		{
			// success if both confirmed while inside range
			if (player1Confirmed && player2Confirmed)
			{
				_levelActive = false;
				Debug.Log("Success (confirm inside range)!");
				GenerateNewLevel();
				return;
			}

			// if dots cross before confirmation => fail
			if (_leftDistance > _goalEndDistance || _rightDistance < _goalStartDistance)
			{
				_levelActive = false;
				Debug.Log("Failed: dots crossed before confirmations.");
				ResetLevel();
				return;
			}
		}
	}

	private void HandleInput()
	{
		// Touch input (mobile)
		if (Input.touchCount > 0)
		{
			foreach (Touch t in Input.touches)
			{
				if (t.phase != TouchPhase.Began) continue;
				Vector3 worldPos = Camera.main.ScreenToWorldPoint(t.position);

				if (worldPos.x < Camera.main.transform.position.x)
					HandleLeftPlayerInput();
				else
					HandleRightPlayerInput();
			}
		}

		// Keyboard input
		if (Input.GetKeyDown(KeyCode.A)) HandleLeftPlayerInput();
		if (Input.GetKeyDown(KeyCode.L)) HandleRightPlayerInput();

		// Mouse (desktop testing for taps)
		if (Input.GetMouseButtonDown(0))
		{
			Vector3 worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
			if (worldPos.x < Camera.main.transform.position.x)
				HandleLeftPlayerInput();
			else
				HandleRightPlayerInput();
		}

		// debug: regenerate
		if (Input.GetKeyDown(KeyCode.Return)) GenerateNewLevel();
	}

	private void HandleLeftPlayerInput()
	{
		if (!_leftLaunched)
		{
			_leftLaunched = true;
			return;
		}

		if (_winConditionMode == WinConditionMode.ConfirmPressInsideRange)
		{
			if (IsDistanceInsideGoal(_leftDistance))
			{
				player1Confirmed = true;
				// optional feedback place
				Debug.Log("Player 1 confirmed inside goal range.");
			}
		}
	}

	private void HandleRightPlayerInput()
	{
		if (!_rightLaunched)
		{
			_rightLaunched = true;
			return;
		}

		if (_winConditionMode == WinConditionMode.ConfirmPressInsideRange)
		{
			if (IsDistanceInsideGoal(_rightDistance))
			{
				player2Confirmed = true;
				Debug.Log("Player 2 confirmed inside goal range.");
			}
		}
	}

	// --- Meet-mode check (old behavior, but in distance-space) ---
	private void CheckWinCondition_MeetMode()
	{
		// meeting distance = average of the two distances at crossing
		float meetDistance = Mathf.Clamp01((_leftDistance + _rightDistance) * 0.5f) * (_pathLength == 0f ? 0f : 1f);
		// since leftDistance & rightDistance are already in distance units, compute average directly
		meetDistance = (_leftDistance + _rightDistance) * 0.5f;

		bool success = meetDistance >= _goalStartDistance && meetDistance <= _goalEndDistance;

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

	// --- goal checks ---
	private bool IsDistanceInsideGoal(float distance)
	{
		return distance >= _goalStartDistance && distance <= _goalEndDistance;
	}

	// ---------------------------
	// Generation: control points, sampling, distances, goal
	// ---------------------------
	private void GenerateNewLevel()
	{
		Camera cam = Camera.main;
		float camHalfHeight = cam.orthographicSize;
		float camHalfWidth = cam.aspect * camHalfHeight;

		float leftX = cam.transform.position.x - camHalfWidth + _leftAndRightMargins;
		float rightX = cam.transform.position.x + camHalfWidth - _leftAndRightMargins;

		// Build base linear control points between left and right (inclusive)
		int totalPoints = Mathf.Max(2, _pathControlPoints); // must be at least 2
		List<Vector3> basePoints = new List<Vector3>(totalPoints);

		for (int i = 0; i < totalPoints; i++)
		{
			float t = totalPoints == 1 ? 0f : (float)i / (totalPoints - 1);
			float x = Mathf.Lerp(leftX, rightX, t);
			float y = 0f;

			// apply jitter
			float jitterX = 0f;
			float jitterY = 0f;
			if (_xyJitter)
			{
				jitterX = (float)(_rng.NextDouble() * 2.0 - 1.0) * _pathAmplitude;
				jitterY = (float)(_rng.NextDouble() * 2.0 - 1.0) * _pathAmplitude;
			}
			else
			{
				// Y-only
				jitterY = (float)(_rng.NextDouble() * 2.0 - 1.0) * _pathAmplitude;
			}

			// ensure endpoints stay exactly at leftX/rightX horizontally (no jitter on endpoint X)
			if (i == 0) jitterX = 0f;
			if (i == totalPoints - 1) jitterX = 0f;

			basePoints.Add(new Vector3(x + jitterX, y + jitterY, 0f));
		}

		// Build padded control list for Catmull-Rom (we'll duplicate endpoints at ends)
		_controlPoints = new List<Vector3>(basePoints.Count + 2);
		_controlPoints.Add(basePoints[0]);            // p0 duplicate
		_controlPoints.AddRange(basePoints);         // p1..pn
		_controlPoints.Add(basePoints[basePoints.Count - 1]); // pN+1 duplicate

		// Sample the centripetal Catmull-Rom spline into _sampledPoints
		_sampledPoints = new List<Vector3>();
		int segmentCount = _controlPoints.Count - 3; // count of segments
		for (int seg = 0; seg < segmentCount; seg++)
		{
			// For each segment evaluate local t in [0,1]
			for (int s = 0; s < _samplesPerSegment; s++)
			{
				float localT = (float)s / _samplesPerSegment;
				Vector3 p = EvaluateCentripetalSegment(_controlPoints[seg], _controlPoints[seg + 1], _controlPoints[seg + 2], _controlPoints[seg + 3], localT);
				_sampledPoints.Add(p);
			}
		}
		// add final control point to close samples
		_sampledPoints.Add(_controlPoints[_controlPoints.Count - 2]);

		// compute cumulative lengths
		int N = _sampledPoints.Count;
		_cumulativeLengths = new float[N];
		_cumulativeLengths[0] = 0f;
		for (int i = 1; i < N; i++)
		{
			_cumulativeLengths[i] = _cumulativeLengths[i - 1] + Vector3.Distance(_sampledPoints[i], _sampledPoints[i - 1]);
		}
		_pathLength = _cumulativeLengths[N - 1];

		// set the _line polyline visual (Shapes expects world points in your use)
		_line.SetPoints(_sampledPoints.ToArray());

		// Choose a goal center distance (ensuring margins on screen X are respected)
		float rangeWidth = Mathf.Clamp(UnityEngine.Random.Range(_goalRangeMinWidth, _goalRangeMaxWidth), 0.0001f, Mathf.Max(0.0001f, _pathLength));
		float halfRange = Mathf.Min(rangeWidth * 0.5f, _pathLength * 0.5f);

		// Determine allowed min/max distances on the path that respect world X margins (convert world X margins into allowed sample indices)
		float leftEdgeX = leftX + _goalRangeMargins;
		float rightEdgeX = rightX - _goalRangeMargins;

		float minAllowedDist = 0f;
		float maxAllowedDist = _pathLength;
		for (int i = 0; i < N; i++)
		{
			if (_sampledPoints[i].x >= leftEdgeX)
			{
				minAllowedDist = _cumulativeLengths[i];
				break;
			}
		}
		for (int i = N - 1; i >= 0; i--)
		{
			if (_sampledPoints[i].x <= rightEdgeX)
			{
				maxAllowedDist = _cumulativeLengths[i];
				break;
			}
		}

		// Choose a random center distance within allowed range while ensuring the range fits
		float minCenter = minAllowedDist + halfRange;
		float maxCenter = maxAllowedDist - halfRange;
		float centerDist;
		if (maxCenter <= minCenter)
		{
			centerDist = Mathf.Clamp(_pathLength * 0.5f, halfRange, _pathLength - halfRange);
		}
		else
		{
			centerDist = (float)(_rng.NextDouble() * (maxCenter - minCenter) + minCenter);
		}

		_goalStartDistance = Mathf.Clamp(centerDist - halfRange, 0f, _pathLength);
		_goalEndDistance = Mathf.Clamp(centerDist + halfRange, 0f, _pathLength);

		// Set goal polyline visuals: from start to end positions on spline
		Vector3 g0 = GetPositionAtDistance(_goalStartDistance);
		Vector3 g1 = GetPositionAtDistance(_goalEndDistance);
		_goalRange.SetPoints(new Vector3[] { g0, g1 });

		// Reset dots at start & end
		ResetDots();

		// reset confirm flags
		player1Confirmed = false;
		player2Confirmed = false;

		_levelActive = true;
	}

	// Evaluate centripetal Catmull-Rom segment (p0..p3) at local t in [0,1]
	// This implementation follows the centripetal parameterization (alpha = 0.5)
	private Vector3 EvaluateCentripetalSegment(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
	{
		float alpha = 0.5f;

		float d01 = Mathf.Pow(Vector3.Distance(p0, p1), alpha);
		float d12 = Mathf.Pow(Vector3.Distance(p1, p2), alpha);
		float d23 = Mathf.Pow(Vector3.Distance(p2, p3), alpha);

		// avoid zero distances
		d01 = Mathf.Max(d01, 0.0001f);
		d12 = Mathf.Max(d12, 0.0001f);
		d23 = Mathf.Max(d23, 0.0001f);

		float t0 = 0f;
		float t1 = t0 + d01;
		float t2 = t1 + d12;
		float t3 = t2 + d23;

		float tt = Mathf.Lerp(t1, t2, t); // mapping local t to [t1,t2]

		Vector3 A1 = ((t1 - tt) / d01) * p0 + ((tt - t0) / d01) * p1;
		Vector3 A2 = ((t2 - tt) / d12) * p1 + ((tt - t1) / d12) * p2;
		Vector3 A3 = ((t3 - tt) / d23) * p2 + ((tt - t2) / d23) * p3;

		float denomB1 = (t2 - t0);
		float denomB2 = (t3 - t1);
		denomB1 = Mathf.Max(denomB1, 0.0001f);
		denomB2 = Mathf.Max(denomB2, 0.0001f);

		Vector3 B1 = ((t2 - tt) / denomB1) * A1 + ((tt - t0) / denomB1) * A2;
		Vector3 B2 = ((t3 - tt) / denomB2) * A2 + ((tt - t1) / denomB2) * A3;

		float denomC = (t2 - t1);
		denomC = Mathf.Max(denomC, 0.0001f);

		Vector3 C = ((t2 - tt) / denomC) * B1 + ((tt - t1) / denomC) * B2;
		return C;
	}

	// Get world position along the sampled path at a given distance (clamped)
	private Vector3 GetPositionAtDistance(float distance)
	{
		if (_sampledPoints == null || _sampledPoints.Count == 0) return Vector3.zero;
		distance = Mathf.Clamp(distance, 0f, _pathLength);

		// binary search into cumulative lengths
		int idx = Array.BinarySearch(_cumulativeLengths, distance);
		if (idx >= 0)
			return _sampledPoints[idx];

		int insert = ~idx;
		if (insert <= 0) return _sampledPoints[0];
		if (insert >= _sampledPoints.Count) return _sampledPoints[_sampledPoints.Count - 1];

		float d0 = _cumulativeLengths[insert - 1];
		float d1 = _cumulativeLengths[insert];
		float frac = (distance - d0) / Mathf.Max(0.000001f, d1 - d0);
		return Vector3.Lerp(_sampledPoints[insert - 1], _sampledPoints[insert], frac);
	}

	// Map a distance to a normalized t in [0..1] using the cumulative table (accurate)
	private float TAtDistance(float distance)
	{
		if (_pathLength <= 0f) return 0f;
		distance = Mathf.Clamp(distance, 0f, _pathLength);

		int idx = Array.BinarySearch(_cumulativeLengths, distance);
		if (idx >= 0) return (float)idx / (_cumulativeLengths.Length - 1);

		int insert = ~idx;
		if (insert <= 0) return 0f;
		if (insert >= _cumulativeLengths.Length) return 1f;

		float d0 = _cumulativeLengths[insert - 1];
		float d1 = _cumulativeLengths[insert];
		float frac = (distance - d0) / Mathf.Max(0.000001f, d1 - d0);
		float t0 = (float)(insert - 1) / (_cumulativeLengths.Length - 1);
		float t1 = (float)insert / (_cumulativeLengths.Length - 1);
		return Mathf.Lerp(t0, t1, frac);
	}

	// ---------------------------
	// Reset helpers
	// ---------------------------
	private void ResetLevel()
	{
		ResetDots();
		player1Confirmed = false;
		player2Confirmed = false;
	}

	private void ResetDots()
	{
		// left at start (distance 0) and right at end (distance = pathLength)
		_leftDistance = 0f;
		_rightDistance = _pathLength;

		_leftLaunched = false;
		_rightLaunched = false;

		if (_sampledPoints != null && _sampledPoints.Count > 0)
		{
			_leftDot.transform.position = GetPositionAtDistance(_leftDistance);
			_rightDot.transform.position = GetPositionAtDistance(_rightDistance);
		}

		_levelActive = true;
	}
}
