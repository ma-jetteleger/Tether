using System.Collections.Generic;
using UnityEngine;
using Shapes;

public class Level_CatmullRom : MonoBehaviour
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
	[SerializeField] private float _dotSpeed = 5f;
	[SerializeField] private float _goalRangeMinWidth = 1f;
	[SerializeField] private float _goalRangeMaxWidth = 3f;
	[SerializeField] private float _goalRangeMargins = 0.5f;
	[SerializeField] private float _leftAndRightMargins = 0.5f;

	[Header("Spline Settings")]
	[SerializeField, Range(3, 20)] private int _nodeCount = 7;
	[SerializeField] private float _pathAmplitude = 4f;
	[SerializeField] private bool _xyJitter = true;
	[SerializeField] private float _minControlPointSpacing = 0.2f;
	[SerializeField, Range(0f, 2f)] private float _minSegmentCurviness = 0.5f;
	[SerializeField] private int _samplesPerSegment = 20;

	private bool _leftLaunched;
	private bool _rightLaunched;
	private bool player1Confirmed;
	private bool player2Confirmed;
	private bool _levelActive;

	private List<Vector3> _nodes;
	private List<Vector3> _sampledPoints;
	private float[] _cumulativeLengths;
	private float _pathLength;

	private float _goalStartDistance;
	private float _goalEndDistance;

	private System.Random _rng = new System.Random();

	private void Start() => GenerateNewLevel();

	private void Update()
	{
		if (!_levelActive) return;

		HandleInput();

		if (_leftLaunched) MoveDotAlongPath(_leftDot, _dotSpeed * Time.deltaTime, true);
		if (_rightLaunched) MoveDotAlongPath(_rightDot, _dotSpeed * Time.deltaTime, false);

		if (_leftLaunched && _rightLaunched)
		{
			float leftDist = GetDistanceAlongPath(_leftDot.transform.position);
			float rightDist = GetDistanceAlongPath(_rightDot.transform.position);

			if (_winConditionMode == WinConditionMode.MeetInsideRange && leftDist >= rightDist)
			{
				_levelActive = false;
				CheckWinCondition();
			}
		}
	}

	private void HandleInput()
	{
		bool leftTouch = false;
		bool rightTouch = false;

		if (Input.touchCount > 0)
		{
			foreach (var touch in Input.touches)
			{
				if (touch.phase == TouchPhase.Began)
				{
					Vector3 worldPos = Camera.main.ScreenToWorldPoint(touch.position);
					if (worldPos.x < 0 && !_leftLaunched) leftTouch = true;
					else if (worldPos.x >= 0 && !_rightLaunched) rightTouch = true;
				}
			}
		}

		if (Input.GetKeyDown(KeyCode.A))
		{
			if (!_leftLaunched) _leftLaunched = true;
			else if (_winConditionMode == WinConditionMode.ConfirmPressInsideRange && IsDotInsideGoal(_leftDot)) player1Confirmed = true;
		}
		if (Input.GetKeyDown(KeyCode.L))
		{
			if (!_rightLaunched) _rightLaunched = true;
			else if (_winConditionMode == WinConditionMode.ConfirmPressInsideRange && IsDotInsideGoal(_rightDot)) player2Confirmed = true;
		}

		if (Input.GetMouseButtonDown(0))
		{
			Vector3 worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
			if (worldPos.x < 0 && !_leftLaunched) _leftLaunched = true;
			else if (worldPos.x >= 0 && !_rightLaunched) _rightLaunched = true;
		}

		if (Input.GetKeyDown(KeyCode.Return)) GenerateNewLevel();

		if (_winConditionMode == WinConditionMode.ConfirmPressInsideRange && player1Confirmed && player2Confirmed)
		{
			_levelActive = false;
			Debug.Log("Success! (Confirmed press)");
			GenerateNewLevel();
		}
	}

	private void CheckWinCondition()
	{
		float leftDist = GetDistanceAlongPath(_leftDot.transform.position);
		float rightDist = GetDistanceAlongPath(_rightDot.transform.position);
		float meetDist = (leftDist + rightDist) * 0.5f;

		if (meetDist >= _goalStartDistance && meetDist <= _goalEndDistance)
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
		Camera cam = Camera.main;
		float camHalfHeight = cam.orthographicSize;
		float camHalfWidth = cam.aspect * camHalfHeight;

		float leftX = cam.transform.position.x - camHalfWidth + _leftAndRightMargins;
		float rightX = cam.transform.position.x + camHalfWidth - _leftAndRightMargins;

		// --- Generate control nodes ---
		_nodes = new List<Vector3>(_nodeCount);
		float segmentWidth = (rightX - leftX) / (_nodeCount - 1);

		for (int i = 0; i < _nodeCount; i++)
		{
			float x = leftX + i * segmentWidth;
			float jitterX = 0f;
			float jitterY = 0f;

			if (i == 0 || i == _nodeCount - 1)
				jitterY = ((float)_rng.NextDouble() * 2f - 1f) * (_pathAmplitude * 0.5f);
			else
			{
				if (_xyJitter)
				{
					jitterX = ((float)_rng.NextDouble() * 2f - 1f) * _pathAmplitude;
					jitterY = ((float)_rng.NextDouble() * 2f - 1f) * _pathAmplitude;
				}
				else
					jitterY = ((float)_rng.NextDouble() * 2f - 1f) * _pathAmplitude;
			}

			Vector3 candidate = new Vector3(x + jitterX, jitterY, 0f);

			if (_nodes.Count > 0)
			{
				Vector3 prev = _nodes[_nodes.Count - 1];
				float dist = Vector3.Distance(prev, candidate);
				if (dist < _minControlPointSpacing)
				{
					Vector3 dir = (candidate - prev);
					if (dir == Vector3.zero) dir = UnityEngine.Random.insideUnitSphere.normalized;
					dir.z = 0f;
					candidate = prev + dir.normalized * _minControlPointSpacing;
					if (candidate.x < prev.x) candidate.x = prev.x + _minControlPointSpacing * 0.5f;
				}
			}

			_nodes.Add(candidate);
		}

		// --- Sample centripetal Catmull-Rom spline ---
		_sampledPoints = new List<Vector3>();

		for (int i = 0; i < _nodes.Count - 1; i++)
		{
			Vector3 p0 = i == 0 ? _nodes[i] : _nodes[i - 1];
			Vector3 p1 = _nodes[i];
			Vector3 p2 = _nodes[i + 1];
			Vector3 p3 = i + 2 < _nodes.Count ? _nodes[i + 2] : _nodes[i + 1];

			int samples = Mathf.Max(_samplesPerSegment, Mathf.CeilToInt(Vector3.Distance(p1, p2) * 5f));
			for (int s = 0; s < samples; s++)
			{
				float t = (float)s / samples;
				_sampledPoints.Add(EvaluateCatmullRom(p0, p1, p2, p3, t));
			}
		}
		_sampledPoints.Add(_nodes[_nodes.Count - 1]);

		// --- Enforce minimum curviness ---
		for (int i = 1; i < _sampledPoints.Count - 1; i++)
		{
			Vector3 a = _sampledPoints[i - 1];
			Vector3 b = _sampledPoints[i];
			Vector3 c = _sampledPoints[i + 1];

			float angle = Vector3.Angle(b - a, c - b);
			if (angle < _minSegmentCurviness * 30f) // 30 degrees as base for minimal curvature
			{
				b.y += Mathf.Sign(UnityEngine.Random.value - 0.5f) * _pathAmplitude * 0.2f;
				_sampledPoints[i] = b;
			}
		}

		// --- Compute cumulative lengths ---
		int S = _sampledPoints.Count;
		_cumulativeLengths = new float[S];
		_cumulativeLengths[0] = 0f;
		for (int i = 1; i < S; i++)
			_cumulativeLengths[i] = _cumulativeLengths[i - 1] + Vector3.Distance(_sampledPoints[i], _sampledPoints[i - 1]);

		_pathLength = _cumulativeLengths[S - 1];
		_line.SetPoints(_sampledPoints.ToArray());

		// --- Place goal along spline ---
		float rangeWidth = Mathf.Clamp(UnityEngine.Random.Range(_goalRangeMinWidth, _goalRangeMaxWidth), 0.0001f, Mathf.Max(0.0001f, _pathLength));
		float halfRange = Mathf.Min(rangeWidth * 0.5f, _pathLength * 0.5f);
		float minCenter = halfRange;
		float maxCenter = _pathLength - halfRange;
		float centerDist = (float)(_rng.NextDouble() * (maxCenter - minCenter) + minCenter);

		_goalStartDistance = Mathf.Clamp(centerDist - halfRange, 0f, _pathLength);
		_goalEndDistance = Mathf.Clamp(centerDist + halfRange, 0f, _pathLength);

		Vector3 g0 = GetPositionAtDistance(_goalStartDistance);
		Vector3 g1 = GetPositionAtDistance(_goalEndDistance);
		_goalRange.SetPoints(new Vector3[] { g0, g1 });

		// --- Reset state ---
		ResetDots();
		player1Confirmed = false;
		player2Confirmed = false;
		_levelActive = true;
	}

	private Vector3 EvaluateCatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
	{
		float t0 = 0.0f;
		float t1 = GetT(t0, p0, p1);
		float t2 = GetT(t1, p1, p2);
		float t3 = GetT(t2, p2, p3);

		t = Mathf.Lerp(t1, t2, t);
		Vector3 A1 = (t1 - t) / (t1 - t0) * p0 + (t - t0) / (t1 - t0) * p1;
		Vector3 A2 = (t2 - t) / (t2 - t1) * p1 + (t - t1) / (t2 - t1) * p2;
		Vector3 A3 = (t3 - t) / (t3 - t2) * p2 + (t - t2) / (t3 - t2) * p3;

		Vector3 B1 = (t2 - t) / (t2 - t0) * A1 + (t - t0) / (t2 - t0) * A2;
		Vector3 B2 = (t3 - t) / (t3 - t1) * A2 + (t - t1) / (t3 - t1) * A3;

		Vector3 C = (t2 - t) / (t2 - t1) * B1 + (t - t1) / (t2 - t1) * B2;
		return C;
	}

	private float GetT(float t, Vector3 p0, Vector3 p1)
	{
		float a = Mathf.Pow(Vector3.Distance(p0, p1), 0.5f); // centripetal
		return a + t;
	}

	private void MoveDotAlongPath(Disc dot, float deltaDist, bool leftToRight)
	{
		float currentDist = GetDistanceAlongPath(dot.transform.position);
		float targetDist = leftToRight ? currentDist + deltaDist : currentDist - deltaDist;
		targetDist = Mathf.Clamp(targetDist, 0f, _pathLength);
		dot.transform.position = GetPositionAtDistance(targetDist);
	}

	private float GetDistanceAlongPath(Vector3 pos)
	{
		if (_sampledPoints == null || _sampledPoints.Count < 2) return 0f;
		float closestDist = 0f;
		float minDistSqr = float.MaxValue;
		for (int i = 0; i < _sampledPoints.Count; i++)
		{
			float dSqr = (_sampledPoints[i] - pos).sqrMagnitude;
			if (dSqr < minDistSqr)
			{
				minDistSqr = dSqr;
				closestDist = _cumulativeLengths[i];
			}
		}
		return closestDist;
	}

	private Vector3 GetPositionAtDistance(float dist)
	{
		if (_sampledPoints == null || _sampledPoints.Count < 2) return Vector3.zero;
		if (dist <= 0f) return _sampledPoints[0];
		if (dist >= _pathLength) return _sampledPoints[_sampledPoints.Count - 1];

		int i = 0;
		while (i < _cumulativeLengths.Length - 1 && _cumulativeLengths[i + 1] < dist) i++;
		float t = (dist - _cumulativeLengths[i]) / (_cumulativeLengths[i + 1] - _cumulativeLengths[i]);
		return Vector3.Lerp(_sampledPoints[i], _sampledPoints[i + 1], t);
	}

	private bool IsDotInsideGoal(Disc dot)
	{
		float dist = GetDistanceAlongPath(dot.transform.position);
		return dist >= _goalStartDistance && dist <= _goalEndDistance;
	}

	private void ResetLevel() => ResetDots();

	private void ResetDots()
	{
		_leftDot.transform.position = _sampledPoints[0];
		_rightDot.transform.position = _sampledPoints[_sampledPoints.Count - 1];
		_leftLaunched = _rightLaunched = false;
		_levelActive = true;
	}
}
