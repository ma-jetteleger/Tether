using UnityEngine;

public class Level : MonoBehaviour
{
	[Header("References")]
	public Transform line;
	public Transform markedRange;
	public Transform dotLeft;
	public Transform dotRight;

	[Header("Settings")]
	public float dotSpeed = 3f;
	public Vector2 lineExtent = new Vector2(-5f, 5f);
	public float markedRangeMinWidth = 0.5f;
	public float markedRangeMaxWidth = 2f;

	private bool leftLaunched;
	private bool rightLaunched;
	private bool levelActive = true;

	private float markedLeft;
	private float markedRight;

	void Start()
	{
		GenerateNewLevel();
	}

	void Update()
	{
		if (!levelActive) return;

		HandleInput();

		// Move dots
		if (leftLaunched)
			dotLeft.position += Vector3.right * dotSpeed * Time.deltaTime;
		if (rightLaunched)
			dotRight.position += Vector3.left * dotSpeed * Time.deltaTime;

		// Check meeting point
		if (dotLeft.position.x >= dotRight.position.x)
		{
			levelActive = false;
			CheckWinCondition();
		}
	}

	void HandleInput()
	{
		// Keyboard
		if (!leftLaunched && Input.GetKeyDown(KeyCode.A))
			leftLaunched = true;
		if (!rightLaunched && Input.GetKeyDown(KeyCode.L))
			rightLaunched = true;

		// Touch input (mobile)
		if (Input.touchCount > 0)
		{
			foreach (Touch t in Input.touches)
			{
				if (t.phase == TouchPhase.Began)
				{
					Vector3 worldPos = Camera.main.ScreenToWorldPoint(t.position);
					if (worldPos.x < 0 && !leftLaunched)
						leftLaunched = true;
					else if (worldPos.x >= 0 && !rightLaunched)
						rightLaunched = true;
				}
			}
		}

		// Mouse input (for testing mobile on desktop)
		if (Input.GetMouseButtonDown(0))
		{
			Vector3 worldPos = Camera.main.ScreenToWorldPoint(Input.mousePosition);
			if (worldPos.x < 0 && !leftLaunched)
				leftLaunched = true;
			else if (worldPos.x >= 0 && !rightLaunched)
				rightLaunched = true;
		}
	}

	void CheckWinCondition()
	{
		float meetX = (dotLeft.position.x + dotRight.position.x) * 0.5f;
		bool success = meetX >= markedLeft && meetX <= markedRight;

		if (success)
		{
			Debug.Log("Success!");
			Invoke(nameof(GenerateNewLevel), 1f);
		}
		else
		{
			Debug.Log("Missed. Try again.");
			Invoke(nameof(ResetLevel), 1f);
		}
	}

	void GenerateNewLevel()
	{
		float rangeWidth = Random.Range(markedRangeMinWidth, markedRangeMaxWidth);
		float center = Random.Range(lineExtent.x + rangeWidth / 2f, lineExtent.y - rangeWidth / 2f);

		markedRange.position = new Vector3(center, 0f, 0f);
		markedRange.localScale = new Vector3(rangeWidth, markedRange.localScale.y, 1f);

		markedLeft = center - rangeWidth / 2f;
		markedRight = center + rangeWidth / 2f;

		ResetDots();
	}

	void ResetLevel() => ResetDots();

	void ResetDots()
	{
		dotLeft.position = new Vector3(lineExtent.x, 0f, 0f);
		dotRight.position = new Vector3(lineExtent.y, 0f, 0f);
		leftLaunched = rightLaunched = false;
		levelActive = true;
	}
}
