using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using System.Reflection;

// Remade: Left-click toggles a particle effect around the card.
// Right-click toggles (open/close) a RawImage panel.
// Works for UI cards (via IPointerClickHandler) and 3D cards (via OnMouseDown).
public class leftRightClickCard : MonoBehaviour, IPointerClickHandler
{
	// Particle effect prefab (UI prefab with RectTransform or 3D/Particle).
	public GameObject particleEffectPrefab;

	// Spawn style for UI: generate multiple instances around the card instead of one at center
	public bool spawnMultipleAroundCard = true;
	public int edgePoints = 8; // points around rect (>=4 uses corners + edges)
	public float uiOutwardOffset = 8f; // pixels outward from card bounds for UI
	public bool autoDestroyEffects = true;
	public float autoDestroySeconds = 1.2f;

	// UI effect visibility: ensure particles are above the card
	public bool bringEffectToFront = true;
	public int effectSortingOrder = 5000; // local Canvas sorting order for effect (higher)

	// Perimeter mode: place particles continuously along the card edges
	public bool spawnPerimeterAroundCard = true;
	public float perimeterSpacingPixels = 14f; // pixel spacing along edges (denser)
	public float perimeterOutwardOffset = 8f; // outward pixel offset from edge

	// UI root where UI effects will be parented (e.g., a RawImage container or any RectTransform).
	// If not set, the script will auto-use the parent Canvas's RectTransform or create one.
	public RectTransform uiRoot;

	// Optional: assign a RawImage directly; we'll use its rectTransform as uiRoot.
	public RawImage uiRootImage;
	// Runtime find helpers (prefab-safe):
	public string uiRootObjectName;   // Find by name at runtime (GameObject.Find)
	public string uiRootTag;          // Find by tag at runtime (GameObject.FindWithTag)
	public bool fallbackToAnyCanvasRoot = true; // Fallback to any scene Canvas if none provided

	// Panel to open on right-click (RawImage). We'll toggle its GameObject active state.
	public RawImage rightClickPanel;
	public string rightClickPanelName; // Runtime find by name
	public string rightClickPanelTag;  // Runtime find by tag
	// Prefab-based generation if panel is missing
	public GameObject rightClickPanelPrefab; // Prefab to instantiate as zoom panel (should include RawImage)
	public bool generatePanelIfMissing = true; // Auto-create panel if not found
	public Vector2 generatedPanelSize = new Vector2(512, 512); // Default size for generated panel
	public bool centerPanelOnOpen = true; // Center panel when enabled

	// Panel visibility options
	public bool ensurePanelCanvasSorting = true; // add a local Canvas to panel and set high sorting
	public int panelSortingOrder = 2000;         // sorting order when ensurePanelCanvasSorting is true
	public bool bringPanelToFront = true;        // move panel to last sibling when enabling

	public bool debugLogs = true;

	// --- Combat integration: selection mode only (logic moved to Attack.cs) ---
	[Header("Combat")]
	public bool attackableOnLeftClick = true; // 左鍵選擇後可進入攻擊模式
	private bool _isAttackMode = false;       // 當前是否在攻擊模式
	public int selectedAttackDamage = 0;      // 外部設定的本次攻擊傷害
	// 當此卡牌可作為攻擊目標時（例如敵方卡），將其狀態腳本指到此欄位；
	// 不再由攻擊者持有敵方參考，而是由目標卡在被點擊時提供狀態。
	public MonoBehaviour targetStatusBehaviour; // 實作 IEnemyStatus 的腳本（僅目標卡需要）

	[Header("Card Data (Optional)")]
	public bool autoPullAttackFromCardData = true; // 進入攻擊模式時自動讀取卡片 atk
	public MonoBehaviour cardDataBehaviour;        // 指向卡片資料腳本（含 atk/attack/attackPower）
	public string[] attackFieldNames = new string[] { "atk", "attack", "Attack", "attackPower", "AttackPower" };

	private GameObject _particleInstance; // for single-spawn mode
	private List<GameObject> _particleInstances; // for multi-spawn mode
	private Canvas _canvas; // For coordinate conversion and raycast setup

	void Awake()
	{
		if (debugLogs) Debug.Log($"leftRightClickCard: Awake start on {name}");
		// Resolve hints first (prefab-safe): name/tag for uiRoot and rightClickPanel
		ResolveUiRootHints();
		EnsureUIRootAndCanvas();
		EnsureClickableGraphicIfUI();
		ResolveRightClickPanelHints();
		EnsurePanelIsSceneInstance();

		// Ensure EventSystem exists for UI pointer events
		EnsureEventSystem();
		// Auto-resolve target status so敵方卡可被選為目標
		ResolveTargetStatusBehaviour();

		// Reminder for 3D objects: OnMouseDown requires a Collider
		if (GetComponent<Collider>() == null && GetComponent<Collider2D>() == null)
		{
			if (debugLogs) Debug.LogWarning($"leftRightClickCard: {name} has no Collider; OnMouseDown works only with colliders.");
		}
	}

	void OnEnable()
	{
		if (debugLogs) Debug.Log($"leftRightClickCard: OnEnable on {name}");
	}

	// 3D click support
	void OnMouseDown()
	{
		if (debugLogs) Debug.Log($"leftRightClickCard: OnMouseDown on {name}");
		if (Input.GetMouseButtonDown(0))
		{
			if (debugLogs) Debug.Log($"leftRightClickCard: OnMouseDown Left (awaitingTarget={AttackTargetingManager.Instance.IsAwaitingTarget}, hasTargetStatus={targetStatusBehaviour != null}, selectedDamage={selectedAttackDamage})");
			// 若目前在等待目標、且本卡具備可攻擊目標的狀態，則直接套用攻擊到此目標
			if (AttackTargetingManager.Instance.IsAwaitingTarget)
			{
				if (targetStatusBehaviour != null)
				{
					AttackTargetingManager.Instance.TryApplyAttackToTarget(targetStatusBehaviour, debugLogs);
				}
				else
				{
					if (debugLogs) Debug.LogWarning("leftRightClickCard: this card is not a valid target (no IEnemyStatus)");
					return; // 在等待目標時，避免重啟攻擊
				}
			}
			else
			{
				ToggleParticle();
				EnterAttackMode();
				ResolveSelectedAttackDamageFromCardData();
				// 交由目標管理器等待下一次點擊作為目標（允許0攻擊）
				if (attackableOnLeftClick)
				{
					AttackTargetingManager.Instance.BeginAttack(this, selectedAttackDamage, debugLogs);
				}
			}
		}
		else if (Input.GetMouseButtonDown(1))
		{
			if (debugLogs) Debug.Log("leftRightClickCard: OnMouseDown detected Right");
			ToggleRightPanel();
		}
	}

	// UI click support
	public void OnPointerClick(PointerEventData eventData)
	{
		if (debugLogs) Debug.Log($"leftRightClickCard: OnPointerClick {eventData.button} on {name}");
		if (eventData.button == PointerEventData.InputButton.Left)
		{
			// UI 路徑同樣支援目標選取
			if (AttackTargetingManager.Instance.IsAwaitingTarget)
			{
				if (targetStatusBehaviour != null)
				{
					AttackTargetingManager.Instance.TryApplyAttackToTarget(targetStatusBehaviour, debugLogs);
				}
				else
				{
					if (debugLogs) Debug.LogWarning("leftRightClickCard: UI click on non-target card (no IEnemyStatus)");
					return;
				}
			}
			else
			{
				ToggleParticle();
				EnterAttackMode();
				ResolveSelectedAttackDamageFromCardData();
				if (attackableOnLeftClick)
				{
					AttackTargetingManager.Instance.BeginAttack(this, selectedAttackDamage, debugLogs);
				}
			}
		}
		else if (eventData.button == PointerEventData.InputButton.Right)
		{
			ToggleRightPanel();
		}
	}
	private void EnsureEventSystem()
	{
		var es = FindObjectOfType<UnityEngine.EventSystems.EventSystem>();
		if (es == null)
		{
			var go = new GameObject("EventSystem");
			go.AddComponent<UnityEngine.EventSystems.EventSystem>();
			go.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
			if (debugLogs) Debug.Log("leftRightClickCard: created EventSystem for UI clicks");
		}
	}

	private void ResolveTargetStatusBehaviour()
	{
		if (targetStatusBehaviour != null) return;
		// 先找本物件
		var selfBehaviours = GetComponents<MonoBehaviour>();
		for (int i = 0; i < selfBehaviours.Length; i++)
		{
			if (selfBehaviours[i] is IEnemyStatus)
			{
				targetStatusBehaviour = selfBehaviours[i];
				if (debugLogs) Debug.Log($"leftRightClickCard: resolved targetStatusBehaviour (self) -> {selfBehaviours[i].GetType().Name}");
				return;
			}
		}

		// 再找子物件（例如狀態腳本掛在子節點）
		var childBehaviours = GetComponentsInChildren<MonoBehaviour>(true);
		for (int i = 0; i < childBehaviours.Length; i++)
		{
			if (childBehaviours[i] is IEnemyStatus)
			{
				targetStatusBehaviour = childBehaviours[i];
				if (debugLogs) Debug.Log($"leftRightClickCard: resolved targetStatusBehaviour (child) -> {childBehaviours[i].GetType().Name}");
				return;
			}
		}

		// 最後嘗試父物件（有時卡牌視覺與狀態分層）
		var parentBehaviours = GetComponentsInParent<MonoBehaviour>(true);
		for (int i = 0; i < parentBehaviours.Length; i++)
		{
			if (parentBehaviours[i] != this && parentBehaviours[i] is IEnemyStatus)
			{
				targetStatusBehaviour = parentBehaviours[i];
				if (debugLogs) Debug.Log($"leftRightClickCard: resolved targetStatusBehaviour (parent) -> {parentBehaviours[i].GetType().Name}");
				return;
			}
		}

		if (debugLogs) Debug.Log("leftRightClickCard: no IEnemyStatus found on self/children/parents; this card won't act as a target");
	}

	private void ResolveSelectedAttackDamageFromCardData()
	{
		if (!autoPullAttackFromCardData) return;
		int resolved;
		if (TryResolveAttackFromBehaviour(cardDataBehaviour, out resolved))
		{
			selectedAttackDamage = resolved;
			if (debugLogs) Debug.Log($"leftRightClickCard: attack damage resolved from assigned cardData -> {resolved}");
			return;
		}

		// 依序在本物件、子物件、父物件搜尋
		if (TryResolveAttackFromBehaviours(GetComponents<MonoBehaviour>(), out resolved) ||
			TryResolveAttackFromBehaviours(GetComponentsInChildren<MonoBehaviour>(true), out resolved) ||
			TryResolveAttackFromBehaviours(GetComponentsInParent<MonoBehaviour>(true), out resolved))
		{
			selectedAttackDamage = resolved;
			if (debugLogs) Debug.Log($"leftRightClickCard: attack damage auto-resolved -> {resolved}");
		}
		else if (debugLogs)
		{
			Debug.Log("leftRightClickCard: failed to resolve attack damage from card data (fallback to existing selectedAttackDamage)");
		}
	}

	private bool TryResolveAttackFromBehaviours(MonoBehaviour[] behaviours, out int value)
	{
		for (int i = 0; i < behaviours.Length; i++)
		{
			if (TryResolveAttackFromBehaviour(behaviours[i], out value)) return true;
		}
		value = 0;
		return false;
	}

	private bool TryResolveAttackFromBehaviour(MonoBehaviour mb, out int value)
	{
		value = 0;
		if (mb == null) return false;
		var t = mb.GetType();
		// 先找欄位
		for (int i = 0; i < attackFieldNames.Length; i++)
		{
			var f = t.GetField(attackFieldNames[i], BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			if (f != null)
			{
				var obj = f.GetValue(mb);
				if (obj is int iv) { value = Mathf.Max(0, iv); return true; }
				if (obj is float fv) { value = Mathf.Max(0, Mathf.RoundToInt(fv)); return true; }
			}
		}
		// 再找屬性
		for (int i = 0; i < attackFieldNames.Length; i++)
		{
			var p = t.GetProperty(attackFieldNames[i], BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
			if (p != null && p.CanRead)
			{
				var obj = p.GetValue(mb, null);
				if (obj is int iv) { value = Mathf.Max(0, iv); return true; }
				if (obj is float fv) { value = Mathf.Max(0, Mathf.RoundToInt(fv)); return true; }
			}
		}
		return false;
	}

	private void ToggleParticle()
	{
		// Multi-spawn mode: create a burst of effects around the card; toggle clears them
		if (spawnMultipleAroundCard)
		{
			if (_particleInstances == null || _particleInstances.Count == 0)
			{
				_particleInstances = spawnPerimeterAroundCard ? SpawnPerimeterEffectsAroundCard() : SpawnEffectsAroundCard();
				if (debugLogs) Debug.Log($"leftRightClickCard: spawned {_particleInstances?.Count ?? 0} effects around {name}");
			}
			else
			{
				for (int i = 0; i < _particleInstances.Count; i++)
				{
					var go = _particleInstances[i];
					if (go != null) Destroy(go);
				}
				_particleInstances.Clear();
				if (debugLogs) Debug.Log($"leftRightClickCard: cleared burst effects for {name}");
			}
			return;
		}

		// Single-spawn mode: previous behavior
		if (_particleInstance == null)
		{
			_particleInstance = SpawnEffectAroundCard();
			if (debugLogs) Debug.Log($"leftRightClickCard: spawned effect for {name}");
		}
		else
		{
			Destroy(_particleInstance);
			_particleInstance = null;
			if (debugLogs) Debug.Log($"leftRightClickCard: cleared effect for {name}");
		}
	}

	private void ToggleRightPanel()
	{
		if (rightClickPanel == null)
		{
			if (debugLogs) Debug.LogWarning("leftRightClickCard: rightClickPanel (RawImage) not assigned");
			return;
		}

		if (debugLogs) Debug.Log($"leftRightClickCard: ToggleRightPanel invoked; current active={rightClickPanel.gameObject.activeSelf}");

		// Safety: do not toggle a panel that is the UI root or an ancestor of this card
		if (uiRootImage != null && rightClickPanel == uiRootImage)
		{
			if (debugLogs) Debug.LogWarning("leftRightClickCard: resolved panel equals uiRootImage; skipping toggle to avoid hiding area");
			return;
		}
		if (transform.IsChildOf(rightClickPanel.transform))
		{
			if (debugLogs) Debug.LogWarning("leftRightClickCard: resolved panel is an ancestor of this card; skipping toggle to avoid hiding area");
			return;
		}
		var go = rightClickPanel.gameObject;
		bool newState = !go.activeSelf;
		go.SetActive(newState);

		if (newState)
		{
			EnsurePanelVisible();
			if (debugLogs)
			{
				var disabledAncestors = GetDisabledAncestors(go);
				if (!string.IsNullOrEmpty(disabledAncestors))
				{
					Debug.LogWarning($"leftRightClickCard: panel enabled but some ancestors are inactive: {disabledAncestors}");
				}
			}
		}
	}

	private GameObject SpawnEffectAroundCard()
	{
		if (particleEffectPrefab == null)
		{
			if (debugLogs) Debug.LogWarning("leftRightClickCard: particleEffectPrefab not assigned");
			return null;
		}

		// UI prefab case
		if (particleEffectPrefab.GetComponent<RectTransform>() != null)
		{
			// Prefer spawning under the card's own parent to keep coordinate spaces consistent
			var cardRT = GetComponent<RectTransform>();
			if (cardRT == null)
			{
				// Fallback to previous behavior
				EnsureUIRootAndCanvas();
				var parentRT = uiRoot;
				var inst = Instantiate(particleEffectPrefab, parentRT);
				var instRT = inst.GetComponent<RectTransform>();
				var cam = _canvas != null ? _canvas.worldCamera : null; // null for Overlay
				var screen = RectTransformUtility.WorldToScreenPoint(cam, transform.position);
				RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRT, screen, cam, out var localPos);
				instRT.anchoredPosition = localPos;
				return inst;
			}

			var canvas = cardRT.GetComponentInParent<Canvas>();
			var cam2 = canvas != null ? canvas.worldCamera : null;
			var parentForUI = cardRT.parent as RectTransform;
			if (parentForUI == null)
			{
				EnsureUIRootAndCanvas();
				parentForUI = uiRoot;
			}
			var inst2 = Instantiate(particleEffectPrefab, parentForUI);
			var instRT2 = inst2.GetComponent<RectTransform>();
			var screen2 = RectTransformUtility.WorldToScreenPoint(cam2, cardRT.position);
			RectTransformUtility.ScreenPointToLocalPointInRectangle(parentForUI, screen2, cam2, out var localPos2);
			instRT2.anchoredPosition = localPos2;
			EnsureEffectVisible(inst2);
			if (autoDestroyEffects) Destroy(inst2, autoDestroySeconds);
			return inst2;
		}
		else
		{
			// 3D/Particle case
			var inst = Instantiate(particleEffectPrefab, transform);
			inst.transform.localPosition = Vector3.zero;
			if (autoDestroyEffects) Destroy(inst, autoDestroySeconds);
			return inst;
		}
	}

	private List<GameObject> SpawnEffectsAroundCard()
	{
		var results = new List<GameObject>();
		if (particleEffectPrefab == null)
		{
			if (debugLogs) Debug.LogWarning("leftRightClickCard: particleEffectPrefab not assigned");
			return results;
		}

		// UI path: place instances around card RectTransform (corners + edge points)
		if (particleEffectPrefab.GetComponent<RectTransform>() != null)
		{
			var cardRT = GetComponent<RectTransform>();
			if (cardRT == null)
			{
				// If not a UI element, fallback to single spawn under 3D parent
				var single = SpawnEffectAroundCard();
				if (single != null) results.Add(single);
				return results;
			}

			var canvas = cardRT.GetComponentInParent<Canvas>();
			var cam = canvas != null ? canvas.worldCamera : null;
			var parentRT = cardRT.parent as RectTransform;
			if (parentRT == null)
			{
				EnsureUIRootAndCanvas();
				parentRT = uiRoot;
			}

			// Get world corners
			Vector3[] corners = new Vector3[4];
			cardRT.GetWorldCorners(corners);
			Vector3 center = (corners[0] + corners[1] + corners[2] + corners[3]) * 0.25f;

			var positionsWorld = new List<Vector3>();
			// Always include 4 corners, slightly offset outward
			for (int i = 0; i < 4; i++)
			{
				var dirOut = (corners[i] - center).normalized;
				positionsWorld.Add(corners[i] + dirOut * uiOutwardOffset);
			}

			// Add edge points if requested (>4)
			int extra = Mathf.Max(0, edgePoints - 4);
			int perEdge = extra > 0 ? Mathf.Max(1, extra / 4) : 0;
			if (perEdge > 0)
			{
				for (int e = 0; e < 4; e++)
				{
					Vector3 a = corners[e];
					Vector3 b = corners[(e + 1) % 4];
					for (int k = 1; k <= perEdge; k++)
					{
						float t = (float)k / (perEdge + 1);
						Vector3 p = Vector3.Lerp(a, b, t);
						var dirOut = (p - center).normalized;
						positionsWorld.Add(p + dirOut * uiOutwardOffset);
					}
				}
			}

			// Instantiate at computed positions
			for (int i = 0; i < positionsWorld.Count; i++)
			{
				var screen = RectTransformUtility.WorldToScreenPoint(cam, positionsWorld[i]);
				RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRT, screen, cam, out var localPos);
				var inst = Instantiate(particleEffectPrefab, parentRT);
				var instRT = inst.GetComponent<RectTransform>();
				instRT.anchoredPosition = localPos;
				EnsureEffectVisible(inst);
				if (autoDestroyEffects) Destroy(inst, autoDestroySeconds);
				results.Add(inst);
			}
			return results;
		}

		// 3D path: spawn a small ring around the object's bounds center
		{
			var rend = GetComponentInChildren<Renderer>();
			var center = rend != null ? rend.bounds.center : transform.position;
			var radius = rend != null ? rend.bounds.extents.magnitude * 0.5f : 0.5f;
			int count = Mathf.Max(4, edgePoints);
			for (int i = 0; i < count; i++)
			{
				float ang = (Mathf.PI * 2f) * (i / (float)count);
				Vector3 offset = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * (radius + 0.05f);
				var inst = Instantiate(particleEffectPrefab, transform);
				inst.transform.position = center + offset;
				if (autoDestroyEffects) Destroy(inst, autoDestroySeconds);
				results.Add(inst);
			}
			return results;
		}
	}

	private List<GameObject> SpawnPerimeterEffectsAroundCard()
	{
		var results = new List<GameObject>();
		if (particleEffectPrefab == null)
		{
			if (debugLogs) Debug.LogWarning("leftRightClickCard: particleEffectPrefab not assigned");
			return results;
		}

		// UI path: sample edge lengths in screen space and place instances continuously
		if (particleEffectPrefab.GetComponent<RectTransform>() != null)
		{
			var cardRT = GetComponent<RectTransform>();
			if (cardRT == null)
			{
				// Fallback to multi-point when not a UI element
				return SpawnEffectsAroundCard();
			}

			var canvas = cardRT.GetComponentInParent<Canvas>();
			var cam = canvas != null ? canvas.worldCamera : null;
			var parentRT = cardRT.parent as RectTransform;
			if (parentRT == null)
			{
				EnsureUIRootAndCanvas();
				parentRT = uiRoot;
			}

			Vector3[] corners = new Vector3[4];
			cardRT.GetWorldCorners(corners);
			// Screen-space center for outward direction
			var centerWorld = (corners[0] + corners[1] + corners[2] + corners[3]) * 0.25f;
			var centerScreen = RectTransformUtility.WorldToScreenPoint(cam, centerWorld);

			for (int e = 0; e < 4; e++)
			{
				Vector3 aW = corners[e];
				Vector3 bW = corners[(e + 1) % 4];
				Vector2 aS = RectTransformUtility.WorldToScreenPoint(cam, aW);
				Vector2 bS = RectTransformUtility.WorldToScreenPoint(cam, bW);
				float edgeLenPx = Vector2.Distance(aS, bS);
				int steps = Mathf.Max(1, Mathf.RoundToInt(edgeLenPx / Mathf.Max(1f, perimeterSpacingPixels)));
				for (int k = 0; k <= steps; k++)
				{
					float t = (float)k / steps;
					Vector3 pW = Vector3.Lerp(aW, bW, t);
					Vector2 pS = RectTransformUtility.WorldToScreenPoint(cam, pW);
					Vector2 dirOutS = (pS - centerScreen).normalized;
					Vector2 pSOut = pS + dirOutS * perimeterOutwardOffset;
					RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRT, pSOut, cam, out var localPos);
					var inst = Instantiate(particleEffectPrefab, parentRT);
					var instRT = inst.GetComponent<RectTransform>();
					instRT.anchoredPosition = localPos;
					if (autoDestroyEffects) Destroy(inst, autoDestroySeconds);
					results.Add(inst);
				}
			}
			return results;
		}

		// 3D path: sample axis-aligned bounds perimeter (approximation)
		{
			var rend = GetComponentInChildren<Renderer>();
			if (rend == null)
			{
				var single = SpawnEffectAroundCard();
				if (single != null) results.Add(single);
				return results;
			}
			var b = rend.bounds;
			Vector3[] rect = new Vector3[4];
			rect[0] = new Vector3(b.min.x, b.min.y, b.center.z);
			rect[1] = new Vector3(b.max.x, b.min.y, b.center.z);
			rect[2] = new Vector3(b.max.x, b.max.y, b.center.z);
			rect[3] = new Vector3(b.min.x, b.max.y, b.center.z);

			for (int e = 0; e < 4; e++)
			{
				Vector3 a = rect[e];
				Vector3 b2 = rect[(e + 1) % 4];
				float edgeLen = Vector3.Distance(a, b2);
				int steps = Mathf.Max(1, Mathf.RoundToInt(edgeLen / 0.1f)); // approx spacing
				for (int k = 0; k <= steps; k++)
				{
					float t = (float)k / steps;
					Vector3 p = Vector3.Lerp(a, b2, t);
					var inst = Instantiate(particleEffectPrefab, transform);
					inst.transform.position = p;
					if (autoDestroyEffects) Destroy(inst, autoDestroySeconds);
					results.Add(inst);
				}
			}
			return results;
		}
	}

	private void EnsureUIRootAndCanvas()
	{
		// Guard: ignore non-scene uiRoot (e.g., prefab asset assigned)
		if (uiRoot != null && !uiRoot.gameObject.scene.IsValid())
		{
			if (debugLogs) Debug.LogWarning("leftRightClickCard: uiRoot points to a prefab asset; ignoring and resolving scene Canvas");
			uiRoot = null;
		}

		// If RawImage provided, prefer its rectTransform
		if (uiRoot == null && uiRootImage != null)
		{
			// If uiRootImage is not a scene object, do not use it
			if (!uiRootImage.gameObject.scene.IsValid())
			{
				if (debugLogs) Debug.LogWarning("leftRightClickCard: uiRootImage points to a prefab asset; skipping");
			}
			else
			{
			uiRoot = uiRootImage.rectTransform;
			}
		}

		// Try parent Canvas first
		if (uiRoot != null)
		{
			_canvas = uiRoot.GetComponentInParent<Canvas>();
			if (_canvas != null)
			{
				EnsureRaycaster(_canvas);
				return;
			}
		}

		var parentCanvas = GetComponentInParent<Canvas>();
		if (parentCanvas != null)
		{
			_canvas = parentCanvas;
			EnsureRaycaster(_canvas);
			if (uiRoot == null) uiRoot = parentCanvas.GetComponent<RectTransform>();
			return;
		}

		var anyCanvas = FindObjectOfType<Canvas>();
		if (anyCanvas != null)
		{
			_canvas = anyCanvas;
			EnsureRaycaster(_canvas);
			if (uiRoot == null) uiRoot = anyCanvas.GetComponent<RectTransform>();
			return;
		}

		// Create an overlay canvas as fallback
		var c = CreateAutoCanvas();
		_canvas = c;
		if (uiRoot == null) uiRoot = c.GetComponent<RectTransform>();
		if (debugLogs) Debug.Log("leftRightClickCard: created AutoCanvas (ScreenSpaceOverlay)");
	}

	// Ensure rightClickPanel references a scene instance; if a prefab asset was assigned, instantiate under uiRoot
	private void EnsurePanelIsSceneInstance()
	{
		if (rightClickPanel == null) return;
		if (rightClickPanel.gameObject.scene.IsValid()) return;

		if (debugLogs) Debug.LogWarning("leftRightClickCard: rightClickPanel points to a prefab asset; instantiating a scene instance");
		EnsureUIRootAndCanvas();
		var parentRT = uiRoot;
		if (parentRT == null)
		{
			var c = CreateAutoCanvas();
			parentRT = c.GetComponent<RectTransform>();
		}

		var prefabGO = rightClickPanel.gameObject;
		var panelGO = Instantiate(prefabGO, parentRT);
		rightClickPanel = panelGO.GetComponent<RawImage>();
		if (rightClickPanel == null)
		{
			rightClickPanel = panelGO.AddComponent<RawImage>();
		}
		panelGO.SetActive(false);
		if (debugLogs) Debug.Log("leftRightClickCard: instantiated scene instance of rightClickPanel from prefab asset");
	}

	private void EnsureClickableGraphicIfUI()
	{
		// Ensure this UI object can receive pointer clicks (has a Graphic with raycastTarget)
		if (GetComponent<RectTransform>() != null)
		{
			var g = GetComponent<Graphic>();
			if (g == null)
			{
				var img = gameObject.AddComponent<Image>();
				img.color = new Color(0, 0, 0, 0);
				img.raycastTarget = true;
				if (debugLogs) Debug.Log("leftRightClickCard: added transparent Image for raycast");
			}
		}
	}

	private void EnsureRaycaster(Canvas c)
	{
		if (c == null) return;
		var gr = c.GetComponent<GraphicRaycaster>();
		if (gr == null) c.gameObject.AddComponent<GraphicRaycaster>();
	}

	private Canvas CreateAutoCanvas()
	{
		var go = new GameObject("AutoCanvas");
		var c = go.AddComponent<Canvas>();
		c.renderMode = RenderMode.ScreenSpaceOverlay;
		EnsureRaycaster(c);
		return c;
	}

	private void EnsureEffectVisible(GameObject effectGO)
	{
		if (!bringEffectToFront || effectGO == null) return;
		var rt = effectGO.GetComponent<RectTransform>();
		if (rt != null)
		{
			// Move to top of sibling order under the chosen parent
			rt.SetAsLastSibling();
			// Add a local canvas to guarantee sorting above the card
			var localCanvas = effectGO.GetComponent<Canvas>();
			if (localCanvas == null) localCanvas = effectGO.AddComponent<Canvas>();
			localCanvas.overrideSorting = true;
			localCanvas.sortingOrder = effectSortingOrder;
			EnsureRaycaster(localCanvas);
		}
	}

	// --- Prefab-safe resolution helpers ---
	private void ResolveUiRootHints()
	{
		// Prefer explicit RawImage mapping
		if (uiRoot == null && uiRootImage != null)
		{
			uiRoot = uiRootImage.rectTransform;
		}

		// Try tag-based lookup
		if (uiRoot == null && !string.IsNullOrEmpty(uiRootTag))
		{
			var tagged = GameObject.FindWithTag(uiRootTag);
			if (tagged != null)
			{
				var rt = tagged.GetComponent<RectTransform>();
				if (rt != null) uiRoot = rt;
			}
		}

		// Try name-based lookup
		if (uiRoot == null && !string.IsNullOrEmpty(uiRootObjectName))
		{
			var named = GameObject.Find(uiRootObjectName);
			if (named != null)
			{
				var rt = named.GetComponent<RectTransform>();
				if (rt != null) uiRoot = rt;
			}
		}

		// Optional: fallback to any scene Canvas root
		if (uiRoot == null && fallbackToAnyCanvasRoot)
		{
			var anyCanvas = FindObjectOfType<Canvas>();
			if (anyCanvas != null)
			{
				uiRoot = anyCanvas.GetComponent<RectTransform>();
			}
		}
	}

	private void ResolveRightClickPanelHints()
	{
		if (rightClickPanel != null) return;

		// Tag-based lookup
		if (!string.IsNullOrEmpty(rightClickPanelTag))
		{
			if (debugLogs) Debug.Log($"leftRightClickCard: trying tag '{rightClickPanelTag}' for rightClickPanel");
			var tagged = GameObject.FindWithTag(rightClickPanelTag);
			if (tagged != null)
			{
				var ri = tagged.GetComponent<RawImage>();
				if (ri != null)
				{
					// Exclude uiRootImage or ancestor containers
					if (uiRootImage != null && ri == uiRootImage) { if (debugLogs) Debug.Log("leftRightClickCard: tag object equals uiRootImage; skipping"); }
					else if (transform.IsChildOf(ri.transform)) { if (debugLogs) Debug.Log("leftRightClickCard: tag object is an ancestor container; skipping"); }
					else { rightClickPanel = ri; if (debugLogs) Debug.Log($"leftRightClickCard: resolved rightClickPanel via tag '{rightClickPanelTag}' -> {ri.name}"); }
				}
				else
				{
					if (debugLogs) Debug.Log($"leftRightClickCard: object '{tagged.name}' found by tag but has no RawImage; skipping");
				}
			}
			else
			{
				if (debugLogs) Debug.Log($"leftRightClickCard: no object found with tag '{rightClickPanelTag}'");
			}
		}

		if (rightClickPanel != null) return;

		// Name-based lookup
		if (!string.IsNullOrEmpty(rightClickPanelName))
		{
			var named = GameObject.Find(rightClickPanelName);
			if (named != null)
			{
				var ri = named.GetComponent<RawImage>();
				if (ri != null)
				{
					if (uiRootImage != null && ri == uiRootImage) { /* skip */ }
					else if (transform.IsChildOf(ri.transform)) { /* skip */ }
					else { rightClickPanel = ri; if (debugLogs) Debug.Log($"leftRightClickCard: resolved rightClickPanel via name '{rightClickPanelName}' -> {ri.name}"); }
				}
			}
		}

		// Hardcoded fallbacks: tag "Zoom" then name "CardZoom"
		if (rightClickPanel == null)
		{
			if (debugLogs) Debug.Log("leftRightClickCard: trying hardcoded tag 'Zoom' for rightClickPanel");
			var tagged = GameObject.FindWithTag("Zoom");
			if (tagged != null)
			{
				var ri = tagged.GetComponent<RawImage>();
				if (ri != null)
				{
					if (uiRootImage != null && ri == uiRootImage) { /* skip */ }
					else if (transform.IsChildOf(ri.transform)) { /* skip */ }
					else { rightClickPanel = ri; if (debugLogs) Debug.Log("leftRightClickCard: resolved rightClickPanel via hardcoded tag 'Zoom'"); }
				}
				else
				{
					if (debugLogs) Debug.Log($"leftRightClickCard: object '{tagged.name}' found by tag 'Zoom' but has no RawImage; skipping");
				}
			}
			else
			{
				if (debugLogs) Debug.Log("leftRightClickCard: no object found with hardcoded tag 'Zoom'");
			}
		}

		if (rightClickPanel == null)
		{
			var named = GameObject.Find("CardZoom");
			if (named != null)
			{
				var ri = named.GetComponent<RawImage>();
				if (ri != null)
				{
					if (uiRootImage != null && ri == uiRootImage) { /* skip */ }
					else if (transform.IsChildOf(ri.transform)) { /* skip */ }
					else { rightClickPanel = ri; if (debugLogs) Debug.Log("leftRightClickCard: resolved rightClickPanel via hardcoded name 'CardZoom'"); }
				}
			}
		}

		// No risky proximity fallback to avoid hiding placement areas
	}

	private void EnsurePanelVisible()
	{
		if (rightClickPanel == null) return;

		// Optional: bring to front in sibling order
		if (bringPanelToFront)
		{
			rightClickPanel.rectTransform.SetAsLastSibling();
		}

		// Ensure local Canvas with high sorting for visibility
		if (ensurePanelCanvasSorting)
		{
			var panelGO = rightClickPanel.gameObject;
			var localCanvas = panelGO.GetComponent<Canvas>();
			if (localCanvas == null)
			{
				localCanvas = panelGO.AddComponent<Canvas>();
			}
			localCanvas.overrideSorting = true;
			localCanvas.sortingOrder = panelSortingOrder;
			EnsureRaycaster(localCanvas);
		}

		// Ensure CanvasGroup visible and interactive
		var cg = rightClickPanel.GetComponent<CanvasGroup>();
		if (cg == null) cg = rightClickPanel.gameObject.AddComponent<CanvasGroup>();
		cg.alpha = 1f;
		cg.interactable = true;
		cg.blocksRaycasts = true;
    
		// Optional: center panel when opened
		if (centerPanelOnOpen)
		{
			var rt = rightClickPanel.rectTransform;
			rt.anchorMin = new Vector2(0.5f, 0.5f);
			rt.anchorMax = new Vector2(0.5f, 0.5f);
			rt.pivot = new Vector2(0.5f, 0.5f);
			rt.anchoredPosition = Vector2.zero;
		}
	}

	private string GetDisabledAncestors(GameObject go)
	{
		System.Text.StringBuilder sb = new System.Text.StringBuilder();
		Transform t = go.transform.parent;
		while (t != null)
		{
			if (!t.gameObject.activeSelf)
			{
				sb.Append(t.name).Append(" ");
			}
			t = t.parent;
		}
		return sb.ToString();
	}

	private void GenerateRightClickPanelIfNeeded()
    {
	if (rightClickPanel != null || !generatePanelIfMissing) return;

	// Determine parent for generated panel
	EnsureUIRootAndCanvas();
	var parentRT = uiRoot;
	if (parentRT == null)
	{
		// As a last resort, create an overlay canvas and use it
		var c = CreateAutoCanvas();
		parentRT = c.GetComponent<RectTransform>();
	}

	GameObject panelGO = null;
	if (rightClickPanelPrefab != null)
	{
		panelGO = Instantiate(rightClickPanelPrefab, parentRT);
		if (debugLogs) Debug.Log("leftRightClickCard: instantiated rightClickPanel from prefab");
	}
	else
	{
		panelGO = new GameObject("CardZoomPanel", typeof(RectTransform));
		panelGO.transform.SetParent(parentRT, false);
		var ri = panelGO.AddComponent<RawImage>();
		ri.color = Color.white;
	}

	var rt = panelGO.GetComponent<RectTransform>();
	rt.anchorMin = new Vector2(0.5f, 0.5f);
	rt.anchorMax = new Vector2(0.5f, 0.5f);
	rt.pivot = new Vector2(0.5f, 0.5f);
	rt.sizeDelta = generatedPanelSize;
	rt.anchoredPosition = Vector2.zero;

	var riPanel = panelGO.GetComponent<RawImage>();
	if (riPanel == null)
	{
		riPanel = panelGO.AddComponent<RawImage>();
	}

	// Try to assign tag if provided (may throw if tag not defined)
	if (!string.IsNullOrEmpty(rightClickPanelTag))
	{
		try { panelGO.tag = rightClickPanelTag; }
		catch (System.Exception ex) { if (debugLogs) Debug.LogWarning($"leftRightClickCard: failed to set tag '{rightClickPanelTag}' on generated panel: {ex.Message}"); }
	}

	// Assign and ensure visibility
	rightClickPanel = riPanel;
	EnsurePanelVisible();
	panelGO.SetActive(false); // default closed until first toggle
	if (debugLogs) Debug.Log("leftRightClickCard: generated rightClickPanel and set default inactive");
	}

	// --- Combat helpers ---
	private IEnemyStatus GetEnemyStatusOrNull()
	{
		// 此方法已不再使用固定敵方參考；保留以防外部仍呼叫。
		return null;
	}

	public void EnterAttackMode()
	{
		if (!attackableOnLeftClick) return;
		_isAttackMode = true;
		if (debugLogs) Debug.Log("leftRightClickCard: attack mode enabled");
	}

	public void ExitAttackMode()
	{
		_isAttackMode = false;
		if (debugLogs) Debug.Log("leftRightClickCard: attack mode disabled");
	}

	// 提供一個便捷入口：在攻擊模式下，使用 selectedAttackDamage 執行攻擊（邏輯由 AttackLogic 提供）
	public AttackResolution PerformSelectedAttack()
	{
		if (!_isAttackMode)
		{
			if (debugLogs) Debug.Log("leftRightClickCard: PerformSelectedAttack ignored (not in attack mode)");
			return new AttackResolution();
		}

		if (selectedAttackDamage <= 0)
		{
			if (debugLogs) Debug.Log("leftRightClickCard: PerformSelectedAttack ignored (no damage set)");
			return new AttackResolution();
		}

		var status = GetEnemyStatusOrNull();
		var res = AttackLogic.PerformAttack(status, selectedAttackDamage, debugLogs);
		ExitAttackMode(); // 預設攻擊後離開攻擊模式（可依需求調整）
		return res;
	}
}
