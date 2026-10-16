using System;
using System.Collections;
using TMPro;
using UnityEngine;

// Area 전투 종료 알림과 전투 후 행동 버튼을 표시하는 uGUI입니다.
// UI는 표시와 입력 전달만 담당하며 실제 Stage 진행은 StageManager가 처리합니다.
public sealed class AreaClearUI : MonoBehaviour
{
    [Header("Upper Clear Notice")]
    [SerializeField] private GameObject upperClearPanel;
    [SerializeField] private TextMeshProUGUI clearLabel;
    [SerializeField, Min(0f)] private float clearNoticeDuration = 1.5f;

    [Header("Lower Actions")]
    [SerializeField] private GameObject lowerActionPanel;
    [SerializeField] private UnityEngine.UI.Button moveButton;

    private Coroutine hideNoticeCoroutine;

    public event Action MoveRequested;

    #region Unity 생명주기

    private void Awake()
    {
        if (moveButton != null)
        {
            moveButton.onClick.AddListener(HandleMoveButtonClicked);
        }

        HideAll();
    }

    private void OnDestroy()
    {
        if (moveButton != null)
        {
            moveButton.onClick.RemoveListener(HandleMoveButtonClicked);
        }
    }

    #endregion

    #region 표시 제어

    // 상단에는 클리어 알림을 표시하고 하단에는 전투 후 행동 버튼을 유지합니다.
    public void ShowAreaClear(int areaNumber)
    {
        if (clearLabel != null)
        {
            clearLabel.text = $"AREA {areaNumber} CLEAR";
        }

        if (upperClearPanel != null)
        {
            upperClearPanel.SetActive(true);
        }

        if (lowerActionPanel != null)
        {
            lowerActionPanel.SetActive(true);
        }

        if (hideNoticeCoroutine != null)
        {
            StopCoroutine(hideNoticeCoroutine);
        }

        if (clearNoticeDuration > 0f)
        {
            hideNoticeCoroutine = StartCoroutine(HideClearNoticeAfterDelay());
        }
    }

    public void HideAll()
    {
        if (hideNoticeCoroutine != null)
        {
            StopCoroutine(hideNoticeCoroutine);
            hideNoticeCoroutine = null;
        }

        if (upperClearPanel != null)
        {
            upperClearPanel.SetActive(false);
        }

        if (lowerActionPanel != null)
        {
            lowerActionPanel.SetActive(false);
        }
    }

    private IEnumerator HideClearNoticeAfterDelay()
    {
        yield return new WaitForSeconds(clearNoticeDuration);

        if (upperClearPanel != null)
        {
            upperClearPanel.SetActive(false);
        }

        hideNoticeCoroutine = null;
    }

    #endregion

    #region 입력 전달

    private void HandleMoveButtonClicked()
    {
        MoveRequested?.Invoke();
    }

    #endregion

    #region 테스트 UI 생성

    // 정식 UI 프리팹이 연결되지 않은 테스트 단계에서 기존 Screen Space Canvas 아래에 임시 UI를 생성합니다.
    public static AreaClearUI CreateTemporary()
    {
        Canvas targetCanvas = FindScreenSpaceCanvas();
        if (targetCanvas == null)
        {
            Debug.LogError("Area Clear UI를 생성할 Screen Space Canvas가 없습니다.");
            return null;
        }

        GameObject root = CreateUiObject("AreaClearUI", targetCanvas.transform);
        StretchToParent(root.GetComponent<RectTransform>());

        AreaClearUI areaClearUI = root.AddComponent<AreaClearUI>();
        areaClearUI.upperClearPanel = CreatePanel(
            "UpperClearPanel",
            root.transform,
            new Vector2(0.25f, 0.82f),
            new Vector2(0.75f, 0.94f),
            new Color(0.06f, 0.07f, 0.1f, 0.88f));
        areaClearUI.clearLabel = CreateLabel("ClearLabel", areaClearUI.upperClearPanel.transform, 54f);

        areaClearUI.lowerActionPanel = CreatePanel(
            "LowerActionPanel",
            root.transform,
            new Vector2(0.1f, 0.02f),
            new Vector2(0.9f, 0.16f),
            new Color(0.06f, 0.07f, 0.1f, 0.88f));
        areaClearUI.moveButton = CreateMoveButton(areaClearUI.lowerActionPanel.transform);
        areaClearUI.moveButton.onClick.AddListener(areaClearUI.HandleMoveButtonClicked);
        areaClearUI.HideAll();

        return areaClearUI;
    }

    private static Canvas FindScreenSpaceCanvas()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < canvases.Length; i++)
        {
            if (canvases[i] != null && canvases[i].renderMode != RenderMode.WorldSpace)
            {
                return canvases[i];
            }
        }

        return null;
    }

    private static GameObject CreatePanel(
        string objectName,
        Transform parent,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Color color)
    {
        GameObject panel = CreateUiObject(objectName, parent);
        RectTransform rectTransform = panel.GetComponent<RectTransform>();
        SetStretchAnchors(rectTransform, anchorMin, anchorMax);

        UnityEngine.UI.Image background = panel.AddComponent<UnityEngine.UI.Image>();
        background.color = color;
        background.raycastTarget = false;
        return panel;
    }

    private static TextMeshProUGUI CreateLabel(string objectName, Transform parent, float fontSize)
    {
        GameObject labelObject = CreateUiObject(objectName, parent);
        StretchToParent(labelObject.GetComponent<RectTransform>());

        TextMeshProUGUI label = labelObject.AddComponent<TextMeshProUGUI>();
        label.text = "AREA CLEAR";
        label.fontSize = fontSize;
        label.fontStyle = FontStyles.Bold;
        label.alignment = TextAlignmentOptions.Center;
        label.color = new Color(1f, 0.88f, 0.38f, 1f);
        label.raycastTarget = false;
        return label;
    }

    private static UnityEngine.UI.Button CreateMoveButton(Transform parent)
    {
        GameObject buttonObject = CreateUiObject("MoveButton", parent);
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        SetStretchAnchors(buttonRect, new Vector2(0.68f, 0.18f), new Vector2(0.96f, 0.82f));

        UnityEngine.UI.Image buttonImage = buttonObject.AddComponent<UnityEngine.UI.Image>();
        buttonImage.color = new Color(0.66f, 0.43f, 0.18f, 1f);
        buttonImage.raycastTarget = true;

        UnityEngine.UI.Button button = buttonObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = buttonImage;

        TextMeshProUGUI label = CreateLabel("Label", buttonObject.transform, 40f);
        label.text = "이동";
        label.color = Color.white;
        return button;
    }

    private static GameObject CreateUiObject(string objectName, Transform parent)
    {
        GameObject gameObject = new GameObject(objectName, typeof(RectTransform));
        gameObject.layer = LayerMask.NameToLayer("UI");
        gameObject.transform.SetParent(parent, false);
        return gameObject;
    }

    private static void StretchToParent(RectTransform rectTransform)
    {
        SetStretchAnchors(rectTransform, Vector2.zero, Vector2.one);
    }

    private static void SetStretchAnchors(RectTransform rectTransform, Vector2 anchorMin, Vector2 anchorMax)
    {
        rectTransform.anchorMin = anchorMin;
        rectTransform.anchorMax = anchorMax;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    #endregion
}
