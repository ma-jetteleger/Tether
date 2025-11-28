using System;
using System.Collections.Generic;
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
	[SerializeField] private Dot _leftDot;
	[SerializeField] private Dot _rightDot;

	[Header("Settings")]
	[SerializeField] private WinConditionMode _winConditionMode = WinConditionMode.MeetInsideRange;
	[SerializeField] private float _goalRangeMinWidth = 0.5f;       // interpreted as spline-distance
	[SerializeField] private float _goalRangeMaxWidth = 2f;         // interpreted as spline-distance
	[SerializeField] private float _goalRangeMargins = 0.5f;        // world-space X margin when choosing goal location
	[SerializeField] private float _leftAndRightMargins = 0.5f;     // world-space X margin for endpoints

	[Header("Spline / Curviness (Centripetal Catmull-Rom)")]
	[SerializeField, Tooltip("Total number of control points including start and end. Must be >= 2.")] private int _pathControlPoints = 7;
	[SerializeField, Tooltip("Minimum allowed distance between consecutive control points after jitter.")] private float _minControlPointSpacing = 0.5f;
	[SerializeField, Tooltip("Maximum jitter amplitude applied to control points (X).")] private float _pathXAmplitude = 2f;
	[SerializeField, Tooltip("Maximum jitter amplitude applied to control points (Y).")] private float _pathYAmplitude = 2f;
	[SerializeField, Tooltip("How many samples per spline segment (higher = smoother & more accurate distance mapping).")] private int _samplesPerSegment = 12;
	[SerializeField, Tooltip("Random seed (0 = random seed).")] private int _seed = 0;

	public bool Active { get; set; }
	public float PathLength { get; set; }

	public WinConditionMode WinCondition
	{
		get
		{
			return _winConditionMode;
		}
		set
		{
			_winConditionMode = value;
		}
	}

	public Polyline GoalRange => _goalRange;

	private float _leftDistance => _leftDot.DistanceOnPath;
	private float _rightDistance => _rightDot.DistanceOnPath;

	// spline / sampling data
	private List<Vector3> _controlPoints;      // padded list used for Catmull-Rom (we'll build with extra endpoints)
	private List<Vector3> _sampledPoints;      // sampled positions along spline (visual + distance mapping)
	private float[] _cumulativeLengths;        // cumulative distance per sampled point

	// goal in distance-space
	private float _goalStartDistance;
	private float _goalEndDistance;

	private System.Random _rng;

	private void Start()
	{
		_rng = _seed == 0 ? new System.Random() : new System.Random(_seed);

		_pathControlPoints = Mathf.Max(2, _pathControlPoints);
		_samplesPerSegment = Mathf.Max(4, _samplesPerSegment);

		_leftDot.Initialize(this);
		_rightDot.Initialize(this);

		GenerateNewLevel();
	}

	private void Update()
	{
		if (!Active)
		{
			return;
		}

		if (Input.GetKeyDown(KeyCode.Return))
		{
			GenerateNewLevel();

			return;
		}

		if (_winConditionMode == WinConditionMode.MeetInsideRange)
		{
			if (_leftDistance >= _rightDistance)
			{
				Active = false;

				var meetDistance = Mathf.Clamp01((_leftDistance + _rightDistance) * 0.5f) * (PathLength == 0f ? 0f : 1f);

				meetDistance = (_leftDistance + _rightDistance) * 0.5f;

				var success = meetDistance >= _goalStartDistance && meetDistance <= _goalEndDistance;

				if (success)
				{
					_leftDot.Disc.Color = _goalRange.Color;
					_rightDot.Disc.Color = _goalRange.Color;

					Invoke(nameof(GenerateNewLevel), 1f);
				}
				else
				{
					_leftDot.Disc.Color = Color.red;
					_rightDot.Disc.Color = Color.red;

					Invoke(nameof(ResetLevel), 1f);
				}
			}
		}
		else
		{
			if (_leftDot.Confirmed && _rightDot.Confirmed)
			{
				Active = false;

				Invoke(nameof(GenerateNewLevel), 1f);

				return;
			}

			var fail = false;

			if (_leftDistance > _goalEndDistance)
			{
				_leftDot.Disc.Color = Color.red;

				fail = true;
			}

			if (_rightDistance < _goalStartDistance)
			{
				_rightDot.Disc.Color = Color.red;

				fail = true;
			}

			if (fail)
			{
				Active = false;

				Invoke(nameof(ResetLevel), 1f);

				return;
			}
		}
	}

	private void GenerateNewLevel()
	{
		var cam = Camera.main;

		var camHalfHeight = cam.orthographicSize;
		var camHalfWidth = cam.aspect * camHalfHeight;

		var leftX = cam.transform.position.x - camHalfWidth + _leftAndRightMargins;
		var rightX = cam.transform.position.x + camHalfWidth - _leftAndRightMargins;

		GeneratePath(leftX, rightX);
		GenerateGoal(leftX, rightX);
		
		ResetDots();

		Active = true;
	}

	private void GeneratePath(float leftX, float rightX)
	{
		// Build base linear control points between left and right (inclusive)
		int totalPoints = Mathf.Max(2, _pathControlPoints); // must be at least 2
		List<Vector3> basePoints = new List<Vector3>(totalPoints);

		for (int i = 0; i < totalPoints; i++)
		{
			float t = totalPoints == 1 ? 0f : (float)i / (totalPoints - 1);
			float x = Mathf.Lerp(leftX, rightX, t);
			float y = 0f;

			// apply jitter
			float jitterX = (float)(_rng.NextDouble() * 2.0 - 1.0) * _pathXAmplitude;
			float jitterY = (float)(_rng.NextDouble() * 2.0 - 1.0) * _pathYAmplitude;

			// ensure endpoints stay exactly at leftX/rightX horizontally (no jitter on endpoint X)
			if (i == 0) jitterX = 0f;
			if (i == totalPoints - 1) jitterX = 0f;

			Vector3 cp = new Vector3(x + jitterX, y + jitterY, 0f);

			// --- Enforce minimum spacing between consecutive control points ---
			if (basePoints.Count > 0)
			{
				Vector3 prev = basePoints[basePoints.Count - 1];
				float dist = Vector3.Distance(prev, cp);

				if (dist < _minControlPointSpacing)
				{
					// Push cp away from prev along their direction
					Vector3 dir = (cp - prev).normalized;

					// If they are exactly overlapping (rare), choose a random direction
					if (dir == Vector3.zero)
						dir = UnityEngine.Random.insideUnitSphere.normalized;

					cp = prev + dir * _minControlPointSpacing;
				}
			}
			// -----------------------------------------------------------------

			basePoints.Add(cp);

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
		PathLength = _cumulativeLengths[N - 1];

		// set the _line polyline visual (Shapes expects world points in your use)
		_line.SetPoints(_sampledPoints.ToArray());
	}

	private void GenerateGoal(float leftX, float rightX)
	{
		// Choose a goal center distance (ensuring margins on screen X are respected)
		float rangeWidth = Mathf.Clamp(UnityEngine.Random.Range(_goalRangeMinWidth, _goalRangeMaxWidth), 0.0001f, Mathf.Max(0.0001f, PathLength));
		float halfRange = Mathf.Min(rangeWidth * 0.5f, PathLength * 0.5f);

		// Determine allowed min/max distances on the path that respect world X margins (convert world X margins into allowed sample indices)
		float leftEdgeX = leftX + _goalRangeMargins;
		float rightEdgeX = rightX - _goalRangeMargins;

		float minAllowedDist = 0f;
		float maxAllowedDist = PathLength;
		for (int i = 0; i < _sampledPoints.Count; i++)
		{
			if (_sampledPoints[i].x >= leftEdgeX)
			{
				minAllowedDist = _cumulativeLengths[i];
				break;
			}
		}
		for (int i = _sampledPoints.Count - 1; i >= 0; i--)
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
			centerDist = Mathf.Clamp(PathLength * 0.5f, halfRange, PathLength - halfRange);
		}
		else
		{
			centerDist = (float)(_rng.NextDouble() * (maxCenter - minCenter) + minCenter);
		}

		_goalStartDistance = Mathf.Clamp(centerDist - halfRange, 0f, PathLength);
		_goalEndDistance = Mathf.Clamp(centerDist + halfRange, 0f, PathLength);

		// Set goal polyline visuals: from start to end positions on spline
		// Build goal-range polyline following the exact curve
		List<Vector3> goalPoints = new List<Vector3>();

		// --- helper: find closest sample index for a given distance
		int FindIndex(float dist)
		{
			int idx = Array.BinarySearch(_cumulativeLengths, dist);
			if (idx >= 0)
				return idx;
			int insert = ~idx;
			return Mathf.Clamp(insert - 1, 0, _cumulativeLengths.Length - 1);
		}

		int startIdx = FindIndex(_goalStartDistance);
		int endIdx = FindIndex(_goalEndDistance);

		// Interpolate the exact start position
		goalPoints.Add(GetPositionAtDistance(_goalStartDistance));

		// Add all intermediate sampled points
		for (int i = startIdx + 1; i <= endIdx; i++)
		{
			goalPoints.Add(_sampledPoints[i]);
		}

		// Interpolate the exact end position
		goalPoints.Add(GetPositionAtDistance(_goalEndDistance));

		// Assign to Shapes polyline
		_goalRange.SetPoints(goalPoints.ToArray());
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

	public bool IsDistanceInsideGoal(float distance)
	{
		return distance >= _goalStartDistance && distance <= _goalEndDistance;
	}

	public Vector3 GetPositionAtDistance(float distance)
	{
		if (_sampledPoints == null || _sampledPoints.Count == 0)
		{
			return Vector3.zero;
		}

		distance = Mathf.Clamp(distance, 0f, PathLength);

		var idx = Array.BinarySearch(_cumulativeLengths, distance);

		if (idx >= 0)
		{
			return _sampledPoints[idx];
		}

		var insert = ~idx;

		if (insert <= 0)
		{
			return _sampledPoints[0];
		}

		if (insert >= _sampledPoints.Count)
		{
			return _sampledPoints[_sampledPoints.Count - 1];
		}

		var d0 = _cumulativeLengths[insert - 1];
		var d1 = _cumulativeLengths[insert];
		var frac = (distance - d0) / Mathf.Max(0.000001f, d1 - d0);

		return Vector3.Lerp(_sampledPoints[insert - 1], _sampledPoints[insert], frac);
	}

	private void ResetLevel()
	{
		ResetDots();
	}

	private void ResetDots()
	{
		_leftDot.Reinitialize();
		_rightDot.Reinitialize();

		if (_sampledPoints != null && _sampledPoints.Count > 0)
		{
			_leftDot.transform.position = GetPositionAtDistance(_leftDistance);
			_rightDot.transform.position = GetPositionAtDistance(_rightDistance);
		}

		Active = true;
	}
}
