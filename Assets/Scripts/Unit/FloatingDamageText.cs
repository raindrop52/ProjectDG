using System;
using System.Collections;
using TMPro;
using UnityEngine;

// 유닛 위에 표시되는 대미지 숫자의 상승 및 페이드 연출을 담당합니다.
// UnitHpBar가 생성한 오브젝트 풀에서 재사용되므로 표시가 끝나면 파괴하지 않고 비활성화합니다.
[DisallowMultipleComponent]
public sealed class FloatingDamageText : MonoBehaviour
{
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private TextMeshProUGUI damageLabel;
    private Coroutine playRoutine;
    private Action<FloatingDamageText> releaseCallback;

    #region 초기화

    // 런타임에 생성된 TextMeshPro 구성 요소를 한 번만 저장합니다.
    public void Initialize()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        damageLabel = GetComponent<TextMeshProUGUI>();

        damageLabel.alignment = TextAlignmentOptions.Center;
        damageLabel.raycastTarget = false;
        damageLabel.textWrappingMode = TextWrappingModes.NoWrap;
        damageLabel.overflowMode = TextOverflowModes.Overflow;
    }

    #endregion

    #region 표시 제어

    // 지정된 위치에서 대미지 숫자 표시를 시작합니다.
    public void Show(
        int damage,
        Vector2 startPosition,
        float riseDistance,
        float duration,
        TMP_FontAsset font,
        float fontSize,
        Color color,
        Action<FloatingDamageText> onFinished)
    {
        if (rectTransform == null || canvasGroup == null || damageLabel == null)
        {
            Initialize();
        }

        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
        }

        releaseCallback = onFinished;
        rectTransform.anchoredPosition = startPosition;
        rectTransform.SetAsLastSibling();
        damageLabel.text = damage.ToString();
        damageLabel.fontSize = fontSize;
        damageLabel.color = color;

        if (font != null)
        {
            damageLabel.font = font;
        }

        canvasGroup.alpha = 1f;
        gameObject.SetActive(true);
        playRoutine = StartCoroutine(PlayEffect(startPosition, riseDistance, duration));
    }

    // 유닛이 비활성화될 때 진행 중인 텍스트를 즉시 풀로 반환합니다.
    public void HideImmediate()
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
            playRoutine = null;
        }

        releaseCallback = null;
        gameObject.SetActive(false);
    }

    // 위로 이동시키면서 알파 값을 줄인 뒤 풀 반환 콜백을 실행합니다.
    private IEnumerator PlayEffect(Vector2 startPosition, float riseDistance, float duration)
    {
        float elapsed = 0f;
        float safeDuration = Mathf.Max(0.01f, duration);
        Vector2 endPosition = startPosition + Vector2.up * riseDistance;

        while (elapsed < safeDuration)
        {
            elapsed += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsed / safeDuration);
            rectTransform.anchoredPosition = Vector2.Lerp(startPosition, endPosition, progress);
            canvasGroup.alpha = 1f - progress;
            yield return null;
        }

        playRoutine = null;
        Action<FloatingDamageText> callback = releaseCallback;
        releaseCallback = null;
        callback?.Invoke(this);
    }

    #endregion
}
