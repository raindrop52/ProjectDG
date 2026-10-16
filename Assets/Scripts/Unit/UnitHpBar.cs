using System.Collections.Generic;
using TMPro;
using UnityEngine;

// 캐릭터와 몬스터가 공통으로 사용하는 월드 공간 체력 UI입니다.
// Unit의 체력 이벤트를 받아 게이지를 갱신하고 대미지 숫자를 풀 방식으로 표시합니다.
[DisallowMultipleComponent]
[RequireComponent(typeof(Canvas))]
public sealed class UnitHpBar : MonoBehaviour
{
    #region Inspector 설정

    [Header("HP Bar")]
    [Tooltip("현재 체력 비율에 따라 가로 길이를 변경할 Bar의 RectTransform입니다.")]
    [SerializeField] private RectTransform fillRect;
    [Tooltip("캐릭터 및 몬스터 스프라이트보다 앞에 표시할 Canvas 정렬 순서입니다.")]
    [SerializeField] private int sortingOrder = 10;

    [Header("Damage Text")]
    [Tooltip("비어 있으면 프로젝트의 기본 TextMeshPro 폰트를 사용합니다.")]
    [SerializeField] private TMP_FontAsset damageFont;
    [SerializeField, Min(0.01f)] private float damageFontSize = 0.18f;
    [SerializeField] private Color damageTextColor = Color.white;
    [SerializeField] private Vector2 damageTextSize = new Vector2(0.8f, 0.3f);
    [SerializeField] private Vector2 damageTextStartOffset = new Vector2(0f, 0.16f);
    [SerializeField] private Vector2 randomHorizontalOffset = new Vector2(-0.08f, 0.08f);
    [SerializeField, Min(0f)] private float damageTextRiseDistance = 0.25f;
    [SerializeField, Min(0.01f)] private float damageTextDuration = 0.65f;
    [SerializeField, Min(1)] private int initialDamageTextPoolSize = 4;

    #endregion

    #region Runtime 상태

    private readonly List<FloatingDamageText> damageTextPool = new List<FloatingDamageText>();
    private Unit targetUnit;
    private Canvas worldCanvas;

    #endregion

    #region Unity 생명주기

    private void Awake()
    {
        targetUnit = GetComponentInParent<Unit>();
        worldCanvas = GetComponent<Canvas>();
        ConfigureCanvas();
        ResolveFillRect();
        PrepareDamageTextPool();
    }

    private void OnEnable()
    {
        ResolveTargetUnit();
        SubscribeUnitEvents();
        RefreshHealth();
    }

    private void OnDisable()
    {
        UnsubscribeUnitEvents();
        HideAllDamageTexts();
    }

    #endregion

    #region 초기화

    // 월드 공간 UI가 스프라이트보다 앞에 그려지도록 정렬 값을 적용합니다.
    private void ConfigureCanvas()
    {
        if (worldCanvas == null)
        {
            return;
        }

        worldCanvas.overrideSorting = true;
        worldCanvas.sortingOrder = sortingOrder;
    }

    // Inspector 참조가 비어 있을 때 기존 HpBar 프리팹의 Bar 자식을 자동으로 찾습니다.
    private void ResolveFillRect()
    {
        if (fillRect != null)
        {
            return;
        }

        Transform barTransform = transform.Find("Bar");
        fillRect = barTransform != null ? barTransform.GetComponent<RectTransform>() : null;
    }

    // StageManager가 이 프리팹을 유닛 아래에 생성한 직후 대상 Unit을 확인합니다.
    private void ResolveTargetUnit()
    {
        if (targetUnit == null)
        {
            targetUnit = GetComponentInParent<Unit>();
        }
    }

    // 전투 중 동시에 발생하는 대미지 숫자를 처리할 기본 풀을 준비합니다.
    private void PrepareDamageTextPool()
    {
        for (int i = damageTextPool.Count; i < initialDamageTextPoolSize; i++)
        {
            CreateDamageText();
        }
    }

    #endregion

    #region Unit 이벤트 연결

    private void SubscribeUnitEvents()
    {
        if (targetUnit == null)
        {
            return;
        }

        targetUnit.HealthChanged -= HandleHealthChanged;
        targetUnit.DamageTaken -= HandleDamageTaken;
        targetUnit.HealthChanged += HandleHealthChanged;
        targetUnit.DamageTaken += HandleDamageTaken;
    }

    private void UnsubscribeUnitEvents()
    {
        if (targetUnit == null)
        {
            return;
        }

        targetUnit.HealthChanged -= HandleHealthChanged;
        targetUnit.DamageTaken -= HandleDamageTaken;
    }

    private void HandleHealthChanged(int currentHp, int maxHp)
    {
        SetHealthRatio(maxHp > 0 ? (float)currentHp / maxHp : 0f);
    }

    private void HandleDamageTaken(int damage)
    {
        if (damage <= 0)
        {
            return;
        }

        FloatingDamageText damageText = GetDamageText();
        float randomX = Random.Range(randomHorizontalOffset.x, randomHorizontalOffset.y);
        Vector2 startPosition = damageTextStartOffset + Vector2.right * randomX;

        damageText.Show(
            damage,
            startPosition,
            damageTextRiseDistance,
            damageTextDuration,
            damageFont,
            damageFontSize,
            damageTextColor,
            ReleaseDamageText
        );
    }

    #endregion

    #region 체력 게이지

    // 현재 대상의 체력을 즉시 반영하여 활성화 및 재사용 시 빈 프레임을 방지합니다.
    private void RefreshHealth()
    {
        if (targetUnit == null)
        {
            SetHealthRatio(0f);
            return;
        }

        SetHealthRatio(targetUnit.HealthRatio);
    }

    // 왼쪽 기준 Anchor를 유지하면서 체력 비율만큼 Bar의 가로 범위를 조절합니다.
    private void SetHealthRatio(float ratio)
    {
        if (fillRect == null)
        {
            return;
        }

        Vector2 anchorMax = fillRect.anchorMax;
        anchorMax.x = Mathf.Clamp01(ratio);
        fillRect.anchorMax = anchorMax;
    }

    #endregion

    #region 대미지 텍스트 풀

    private FloatingDamageText GetDamageText()
    {
        for (int i = 0; i < damageTextPool.Count; i++)
        {
            if (!damageTextPool[i].gameObject.activeSelf)
            {
                return damageTextPool[i];
            }
        }

        // 기본 풀보다 동시 타격이 많으면 누락하지 않고 필요한 만큼 확장합니다.
        return CreateDamageText();
    }

    private FloatingDamageText CreateDamageText()
    {
        GameObject textObject = new GameObject(
            "DamageText",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(CanvasGroup),
            typeof(TextMeshProUGUI),
            typeof(FloatingDamageText)
        );

        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.SetParent(transform, false);
        textRect.anchorMin = new Vector2(0.5f, 0.5f);
        textRect.anchorMax = new Vector2(0.5f, 0.5f);
        textRect.pivot = new Vector2(0.5f, 0.5f);
        textRect.sizeDelta = damageTextSize;

        FloatingDamageText damageText = textObject.GetComponent<FloatingDamageText>();
        damageText.Initialize();
        textObject.SetActive(false);
        damageTextPool.Add(damageText);
        return damageText;
    }

    private static void ReleaseDamageText(FloatingDamageText damageText)
    {
        if (damageText != null)
        {
            damageText.HideImmediate();
        }
    }

    private void HideAllDamageTexts()
    {
        for (int i = 0; i < damageTextPool.Count; i++)
        {
            if (damageTextPool[i] != null)
            {
                damageTextPool[i].HideImmediate();
            }
        }
    }

    #endregion
}
