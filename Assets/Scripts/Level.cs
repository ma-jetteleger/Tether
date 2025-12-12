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

	public enum RangeType
	{
		Obstacle,
		Boost,
		Break
	}

	[Header("References")]
	[SerializeField] private Polyline _line;
	[SerializeField] private Polyline _goalRange;
	[SerializeField] private Polyline _obstacleRangeTemplate;
	[SerializeField] private Polyline _boostRangeTemplate;
	[SerializeField] private Polyline _breakRangeTemplate;
	[SerializeField] private Dot _leftDot;
	[SerializeField] private Dot _rightDot;

	[Header("Level Settings")]
	[SerializeField] private bool _straightLine = true;
	[SerializeField] private WinConditionMode _winConditionMode = WinConditionMode.MeetInsideRange;
	[SerializeField] private int _obstaclesPerSide = 0;
	[SerializeField] private int _boostsPerSide = 0;
	[SerializeField] private int _breaksPerSide = 0;

	[Header("Level Generation Parameters")]
	[SerializeField] private float _leftAndRightMargins = 0.5f;
	[SerializeField] private float _goalRangeMinWidth = 0.5f;
	[SerializeField] private float _goalRangeMaxWidth = 2f;
	[SerializeField] private float _goalRangeMargins = 0.5f;
	[SerializeField] private float _obstacleRangeMinWidth = 0.3f;
	[SerializeField] private float _obstacleRangeMaxWidth = 1.2f;
	[SerializeField] private float _boostRangeMinWidth = 0.3f;
	[SerializeField] private float _boostRangeMaxWidth = 1.2f;
	[SerializeField] private float _breakRangeMinWidth = 0.3f;
	[SerializeField] private float _breakRangeMaxWidth = 1.2f;
	[SerializeField] private float _obstacleToGoalMargin = 0.4f;
	[SerializeField] private float _obstacleToObstacleMargin = 0.25f;
	[SerializeField] private int _maxRangesGenerationTries = 30;

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

	public bool StraightLine
	{
		get
		{
			return _straightLine;
		}
		set
		{
			_straightLine = value;
		}
	}

	public int ObstaclesPerSide
	{
		get
		{
			return _obstaclesPerSide;
		}
		set
		{
			_obstaclesPerSide = value;
		}
	}

	public int BoostsPerSide
	{
		get
		{
			return _boostsPerSide;
		}
		set
		{
			_boostsPerSide = value;
		}
	}

	public int BreakPerSide
	{
		get
		{
			return _breaksPerSide;
		}
		set
		{
			_breaksPerSide = value;
		}
	}

	public Polyline GoalRange => _goalRange;

	private float _leftDistance => _leftDot.DistanceOnPath;
	private float _rightDistance => _rightDot.DistanceOnPath;

	private float _leftX;
	private float _rightX;

	// spline / sampling data
	private List<Vector3> _controlPoints;
	private List<Vector3> _sampledPoints;
	private float[] _cumulativeLengths;

	// goal in distance-space
	private float _goalStartDistance;
	private float _goalEndDistance;

	private Dictionary<GameObject, (float startDistance, float endDistance)> _obstacleRanges;
	private Dictionary<GameObject, (float startDistance, float endDistance)> _boostRanges;
	private Dictionary<GameObject, (float startDistance, float endDistance)> _breakRanges;

	private System.Random _rng;

	private void Start()
	{
		_rng = _seed == 0 ? new System.Random() : new System.Random(_seed);

		_pathControlPoints = Mathf.Max(2, _pathControlPoints);
		_samplesPerSegment = Mathf.Max(4, _samplesPerSegment);

		_leftDot.Initialize(this);
		_rightDot.Initialize(this);

		_obstacleRangeTemplate.gameObject.SetActive(false);
		if (_boostRangeTemplate != null) _boostRangeTemplate.gameObject.SetActive(false);
		if (_breakRangeTemplate != null) _breakRangeTemplate.gameObject.SetActive(false);

		_obstacleRanges = new Dictionary<GameObject, (float startDistance, float endDistance)>();
		_boostRanges = new Dictionary<GameObject, (float startDistance, float endDistance)>();
		_breakRanges = new Dictionary<GameObject, (float startDistance, float endDistance)>();

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

		if(Input.GetKeyDown(KeyCode.Backspace))
		{
			GenerateGoal(_leftX, _rightX);

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

					Win();
				}
				else
				{
					_leftDot.Disc.Color = Color.red;
					_rightDot.Disc.Color = Color.red;

					Fail();
				}
			}
		}
		else
		{
			if (_leftDot.GoalConfirmed && _rightDot.GoalConfirmed)
			{
				Win();

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
				Fail();

				return;
			}
		}
	}

	public void GenerateNewLevel()
	{
		var cam = Camera.main;

		var camHalfHeight = cam.orthographicSize;
		var camHalfWidth = cam.aspect * camHalfHeight;

		_leftX = cam.transform.position.x - camHalfWidth + _leftAndRightMargins;
		_rightX = cam.transform.position.x + camHalfWidth - _leftAndRightMargins;

		GeneratePath(_leftX, _rightX);
		GenerateGoal(_leftX, _rightX);

		GenerateAllRanges(_leftX, _rightX);

		ResetDots();

		Active = true;
	}

	private void GeneratePath(float leftX, float rightX)
	{
		// Build base linear control points between left and right (inclusive)
		int totalPoints = _straightLine ? 2 : Mathf.Max(2, _pathControlPoints);
		List<Vector3> basePoints = new List<Vector3>(totalPoints);

		for (int i = 0; i < totalPoints; i++)
		{
			float t = totalPoints == 1 ? 0f : (float)i / (totalPoints - 1);
			float x = Mathf.Lerp(leftX, rightX, t);
			float y = 0f;

			// apply jitter
			float jitterX = (float)(_rng.NextDouble() * 2.0 - 1.0) * _pathXAmplitude;
			float jitterY = (float)(_rng.NextDouble() * 2.0 - 1.0) * _pathYAmplitude;

			// ensure endpoints stay exactly at leftX/rightX horizontally
			if (i == 0) jitterX = 0f;
			if (i == totalPoints - 1) jitterX = 0f;

			Vector3 cp = new Vector3(x + jitterX, y + jitterY, 0f);

			// Enforce minimum spacing between consecutive control points
			if (basePoints.Count > 0)
			{
				Vector3 prev = basePoints[basePoints.Count - 1];
				float dist = Vector3.Distance(prev, cp);

				if (dist < _minControlPointSpacing)
				{
					Vector3 dir = (cp - prev).normalized;

					if (dir == Vector3.zero)
						dir = UnityEngine.Random.insideUnitSphere.normalized;

					cp = prev + dir * _minControlPointSpacing;
				}
			}

			basePoints.Add(cp);
		}

		// Build padded control list for Catmull-Rom
		_controlPoints = new List<Vector3>(basePoints.Count + 2);
		_controlPoints.Add(basePoints[0]);
		_controlPoints.AddRange(basePoints);
		_controlPoints.Add(basePoints[basePoints.Count - 1]);

		// Sample the centripetal Catmull-Rom spline
		_sampledPoints = new List<Vector3>();
		int segmentCount = _controlPoints.Count - 3;
		for (int seg = 0; seg < segmentCount; seg++)
		{
			for (int s = 0; s < _samplesPerSegment; s++)
			{
				float localT = (float)s / _samplesPerSegment;
				Vector3 p = EvaluateCentripetalSegment(_controlPoints[seg], _controlPoints[seg + 1], _controlPoints[seg + 2], _controlPoints[seg + 3], localT);
				_sampledPoints.Add(p);
			}
		}
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

		_line.SetPoints(_sampledPoints.ToArray());
	}

	private void GenerateGoal(float leftX, float rightX)
	{
		var rangeWidth = Mathf.Clamp(UnityEngine.Random.Range(_goalRangeMinWidth, _goalRangeMaxWidth), 0.0001f, Mathf.Max(0.0001f, PathLength));
		var halfRange = Mathf.Min(rangeWidth * 0.5f, PathLength * 0.5f);

		var leftEdgeX = leftX + _goalRangeMargins;
		var rightEdgeX = rightX - _goalRangeMargins;

		var minAllowedDist = 0f;
		var maxAllowedDist = PathLength;

		for (var i = 0; i < _sampledPoints.Count; i++)
		{
			if (_sampledPoints[i].x >= leftEdgeX)
			{
				minAllowedDist = _cumulativeLengths[i];
				break;
			}
		}
		for (var i = _sampledPoints.Count - 1; i >= 0; i--)
		{
			if (_sampledPoints[i].x <= rightEdgeX)
			{
				maxAllowedDist = _cumulativeLengths[i];
				break;
			}
		}

		var minCenter = minAllowedDist + halfRange;
		var maxCenter = maxAllowedDist - halfRange;

		var centerDist = maxCenter <= minCenter
			? Mathf.Clamp(PathLength * 0.5f, halfRange, PathLength - halfRange)
			: (float)(_rng.NextDouble() * (maxCenter - minCenter) + minCenter);

		_goalStartDistance = Mathf.Clamp(centerDist - halfRange, 0f, PathLength);
		_goalEndDistance = Mathf.Clamp(centerDist + halfRange, 0f, PathLength);

		var goalPoints = new List<Vector3>();

		int FindIndex(float dist)
		{
			var idx = Array.BinarySearch(_cumulativeLengths, dist);
			if (idx >= 0) return idx;
			var insert = ~idx;
			return Mathf.Clamp(insert - 1, 0, _cumulativeLengths.Length - 1);
		}

		var startIdx = FindIndex(_goalStartDistance);
		var endIdx = FindIndex(_goalEndDistance);

		goalPoints.Add(GetPositionAtDistance(_goalStartDistance));

		for (var i = startIdx + 1; i <= endIdx; i++)
		{
			goalPoints.Add(_sampledPoints[i]);
		}

		goalPoints.Add(GetPositionAtDistance(_goalEndDistance));

		_goalRange.SetPoints(goalPoints.ToArray());
	}

	private void GenerateAllRanges(float leftX, float rightX)
	{
		// Clear all existing ranges
		ClearRanges(_obstacleRanges);
		ClearRanges(_boostRanges);
		ClearRanges(_breakRanges);

		// Calculate boundary distances
		float? FindDistanceAtXThresholdLeft(float targetX)
		{
			for (var i = 0; i < _sampledPoints.Count; i++)
			{
				if (_sampledPoints[i].x >= targetX)
				{
					return _cumulativeLengths[i];
				}
			}
			return null;
		}

		float? FindDistanceAtXThresholdRight(float targetX)
		{
			for (int i = _sampledPoints.Count - 1; i >= 0; i--)
			{
				if (_sampledPoints[i].x <= targetX)
				{
					return _cumulativeLengths[i];
				}
			}
			return null;
		}

		var leftLimitDist = FindDistanceAtXThresholdLeft(leftX + _goalRangeMargins) ?? 0f;
		var rightLimitDist = FindDistanceAtXThresholdRight(rightX - _goalRangeMargins) ?? PathLength;

		var leftRegionStart = leftLimitDist;
		var leftRegionEnd = Mathf.Max(leftLimitDist, _goalStartDistance - _obstacleToGoalMargin);

		var rightRegionStart = Mathf.Min(rightLimitDist, _goalEndDistance + _obstacleToGoalMargin);
		var rightRegionEnd = rightLimitDist;

		// Generate all range types
		GenerateRangesOfType(RangeType.Obstacle, leftRegionStart, leftRegionEnd, _obstaclesPerSide);
		GenerateRangesOfType(RangeType.Obstacle, rightRegionStart, rightRegionEnd, _obstaclesPerSide);

		GenerateRangesOfType(RangeType.Boost, leftRegionStart, leftRegionEnd, _boostsPerSide);
		GenerateRangesOfType(RangeType.Boost, rightRegionStart, rightRegionEnd, _boostsPerSide);

		GenerateRangesOfType(RangeType.Break, leftRegionStart, leftRegionEnd, _breaksPerSide);
		GenerateRangesOfType(RangeType.Break, rightRegionStart, rightRegionEnd, _breaksPerSide);
	}

	private void ClearRanges(Dictionary<GameObject, (float startDistance, float endDistance)> ranges)
	{
		foreach (var range in ranges)
		{
			Destroy(range.Key);
		}
		ranges.Clear();
	}

	private void GenerateRangesOfType(RangeType rangeType, float regionStart, float regionEnd, int count)
	{
		// Get parameters based on range type
		float minWidth, maxWidth;
		Polyline template;
		Dictionary<GameObject, (float startDistance, float endDistance)> targetDictionary;

		switch (rangeType)
		{
			case RangeType.Obstacle:
				minWidth = _obstacleRangeMinWidth;
				maxWidth = _obstacleRangeMaxWidth;
				template = _obstacleRangeTemplate;
				targetDictionary = _obstacleRanges;
				break;
			case RangeType.Boost:
				minWidth = _boostRangeMinWidth;
				maxWidth = _boostRangeMaxWidth;
				template = _boostRangeTemplate;
				targetDictionary = _boostRanges;
				break;
			case RangeType.Break:
				minWidth = _breakRangeMinWidth;
				maxWidth = _breakRangeMaxWidth;
				template = _breakRangeTemplate;
				targetDictionary = _breakRanges;
				break;
			default:
				return;
		}

		if (template == null)
		{
			Debug.LogWarning($"Template for {rangeType} is null. Skipping generation.");
			return;
		}

		var regionLength = regionEnd - regionStart;

		if (regionLength < minWidth)
		{
			return;
		}

		// Get all existing placed ranges in this region (from all types)
		var placedRanges = new List<(float a, float b)>();
		CollectPlacedRanges(_obstacleRanges, regionStart, regionEnd, placedRanges);
		CollectPlacedRanges(_boostRanges, regionStart, regionEnd, placedRanges);
		CollectPlacedRanges(_breakRanges, regionStart, regionEnd, placedRanges);

		int FindIndex(float dist)
		{
			var idx = Array.BinarySearch(_cumulativeLengths, dist);
			if (idx >= 0) return idx;
			var insert = ~idx;
			return Mathf.Clamp(insert - 1, 0, _cumulativeLengths.Length - 1);
		}

		for (var n = 0; n < count; n++)
		{
			var width = UnityEngine.Random.Range(minWidth, maxWidth);
			width = Mathf.Min(width, regionLength);

			var half = width * 0.5f;

			var minCenter = regionStart + half;
			var maxCenter = regionEnd - half;

			if (maxCenter <= minCenter)
			{
				break;
			}

			var validPlacement = false;

			for (int attempt = 0; attempt < _maxRangesGenerationTries; attempt++)
			{
				var centerDist = Mathf.Lerp(minCenter, maxCenter, UnityEngine.Random.value);
				var startDistance = centerDist - half;
				var endDistance = centerDist + half;

				var overlaps = false;

				foreach (var (start, end) in placedRanges)
				{
					if (!(endDistance + _obstacleToObstacleMargin < start || startDistance - _obstacleToObstacleMargin > end))
					{
						overlaps = true;
						break;
					}
				}

				if (overlaps)
				{
					continue;
				}

				placedRanges.Add((startDistance, endDistance));

				var points = new List<Vector3>();

				var startIndex = FindIndex(startDistance);
				var endIndex = FindIndex(endDistance);

				points.Add(GetPositionAtDistance(startDistance));

				for (var i = startIndex + 1; i <= endIndex; i++)
				{
					points.Add(_sampledPoints[i]);
				}

				points.Add(GetPositionAtDistance(endDistance));

				var newRange = Instantiate(template, transform);
				newRange.SetPoints(points.ToArray());
				newRange.gameObject.SetActive(true);

				targetDictionary.Add(newRange.gameObject, (startDistance, endDistance));

				validPlacement = true;
				break;
			}

			if (!validPlacement)
			{
				continue;
			}
		}
	}

	private void CollectPlacedRanges(Dictionary<GameObject, (float startDistance, float endDistance)> ranges,
		float regionStart, float regionEnd, List<(float a, float b)> output)
	{
		foreach (var range in ranges.Values)
		{
			// Only include ranges that overlap with this region
			if (!(range.endDistance < regionStart || range.startDistance > regionEnd))
			{
				output.Add((range.startDistance, range.endDistance));
			}
		}
	}

	private Vector3 EvaluateCentripetalSegment(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
	{
		float alpha = 0.5f;

		float d01 = Mathf.Pow(Vector3.Distance(p0, p1), alpha);
		float d12 = Mathf.Pow(Vector3.Distance(p1, p2), alpha);
		float d23 = Mathf.Pow(Vector3.Distance(p2, p3), alpha);

		d01 = Mathf.Max(d01, 0.0001f);
		d12 = Mathf.Max(d12, 0.0001f);
		d23 = Mathf.Max(d23, 0.0001f);

		float t0 = 0f;
		float t1 = t0 + d01;
		float t2 = t1 + d12;
		float t3 = t2 + d23;

		float tt = Mathf.Lerp(t1, t2, t);

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

	public GameObject IsDistanceInsideObstacle(float distance)
	{
		foreach (var obstacle in _obstacleRanges)
		{
			if (distance >= obstacle.Value.startDistance && distance <= obstacle.Value.endDistance)
			{
				return obstacle.Key;
			}
		}
		return null;
	}

	public bool IsDistanceInsideBoost(float distance)
	{
		foreach (var boost in _boostRanges)
		{
			if (distance >= boost.Value.startDistance && distance <= boost.Value.endDistance)
			{
				return true;
			}
		}
		return false;
	}

	public bool IsDistanceInsideBreak(float distance)
	{
		foreach (var breakRange in _breakRanges)
		{
			if (distance >= breakRange.Value.startDistance && distance <= breakRange.Value.endDistance)
			{
				return true;
			}
		}
		return false;
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

	public void Win()
	{
		Active = false;
		Invoke(nameof(GenerateNewLevel), 1f);
	}

	public void Fail()
	{
		Active = false;
		Invoke(nameof(ResetLevel), 1f);
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