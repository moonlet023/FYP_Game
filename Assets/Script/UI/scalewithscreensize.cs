using UnityEngine;

[ExecuteAlways]
public class scalewithscreensize : MonoBehaviour
{
	[Header("Target")]
	public RectTransform target;

	[Header("Reference Resolution")]
	public Vector2 referenceResolution = new Vector2(1920f, 1080f);

	[Range(0f, 1f)]
	[Tooltip("0 = match width, 1 = match height")]
	public float matchWidthOrHeight = 0.5f;

	[Header("Axis")]
	public bool scaleX = true;
	public bool scaleY = true;

	private Vector3 _baseScale;
	private int _lastScreenWidth;
	private int _lastScreenHeight;

	private void Awake()
	{
		CacheTarget();
		CacheBaseScale();
		ApplyScale();
	}

	private void OnEnable()
	{
		CacheTarget();
		CacheBaseScale();
		ApplyScale();
	}

	private void OnValidate()
	{
		referenceResolution.x = Mathf.Max(1f, referenceResolution.x);
		referenceResolution.y = Mathf.Max(1f, referenceResolution.y);
		matchWidthOrHeight = Mathf.Clamp01(matchWidthOrHeight);
		CacheTarget();
		if (target != null && _baseScale == Vector3.zero)
		{
			CacheBaseScale();
		}
		ApplyScale();
	}

	private void Update()
	{
		if (Screen.width != _lastScreenWidth || Screen.height != _lastScreenHeight)
		{
			ApplyScale();
		}
	}

	[ContextMenu("Recalculate Base Scale")]
	public void RecalculateBaseScale()
	{
		CacheTarget();
		CacheBaseScale();
		ApplyScale();
	}

	private void CacheTarget()
	{
		if (target == null)
		{
			target = GetComponent<RectTransform>();
		}
	}

	private void CacheBaseScale()
	{
		if (target != null)
		{
			_baseScale = target.localScale;
		}
	}

	private void ApplyScale()
	{
		if (target == null)
		{
			return;
		}

		float widthScale = Screen.width / referenceResolution.x;
		float heightScale = Screen.height / referenceResolution.y;

		float logWidth = Mathf.Log(Mathf.Max(0.0001f, widthScale), 2f);
		float logHeight = Mathf.Log(Mathf.Max(0.0001f, heightScale), 2f);
		float logWeightedAverage = Mathf.Lerp(logWidth, logHeight, matchWidthOrHeight);
		float screenScale = Mathf.Pow(2f, logWeightedAverage);

		Vector3 nextScale = _baseScale;
		if (scaleX)
		{
			nextScale.x = _baseScale.x * screenScale;
		}
		if (scaleY)
		{
			nextScale.y = _baseScale.y * screenScale;
		}

		target.localScale = nextScale;
		_lastScreenWidth = Screen.width;
		_lastScreenHeight = Screen.height;
	}
}
