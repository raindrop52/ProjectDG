using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

[DisallowMultipleComponent]
public class StageManager : MonoBehaviour
{
    public enum FormationSlot
    {
        Front1,
        Front2,
        Front3,
        Front4,
        Rear1,
        Rear2,
        Rear3,
        Rear4
    }

    public enum AreaState
    {
        EnteringArea,
        Ready,
        Fighting,
        AreaCleared,
        ExitingArea,
        FloorCleared
    }

    [Serializable]
    private sealed class CharacterPlacement
    {
        [FormerlySerializedAs("character")]
        [SerializeField] private Character characterPrefab;
        [SerializeField] private FormationSlot slot;

        public Character CharacterPrefab => characterPrefab;
        public FormationSlot Slot => slot;
    }

    [Serializable]
    private sealed class FloorMonsterGroup
    {
        [SerializeField, Min(1)] private int floor = 1;
        [SerializeField] private Monster[] monsterPrefabs = Array.Empty<Monster>();
        [SerializeField] private Monster[] bossPrefabs = Array.Empty<Monster>();

        public int Floor => floor;
        public Monster[] MonsterPrefabs => monsterPrefabs;
        public Monster[] BossPrefabs => bossPrefabs;
    }

    [Serializable]
    private sealed class FloorStageEnvironment
    {
        [SerializeField, Min(1)] private int floor = 1;
        [SerializeField] private GameObject backgroundPrefab;
        [Tooltip("해당 Floor의 TileMap과 충돌체를 포함하는 최상위 오브젝트입니다.")]
        [SerializeField] private GameObject[] tilemapRoots = Array.Empty<GameObject>();
        [SerializeField] private Vector2 backgroundStartPosition = Vector2.zero;

        public int Floor => floor;
        public GameObject BackgroundPrefab => backgroundPrefab;
        public GameObject[] TilemapRoots => tilemapRoots;
        public Vector2 BackgroundStartPosition => backgroundStartPosition;
    }

    [Header("Stage Progress")]
    [SerializeField, Min(1)] private int currentFloor = 1;
    [SerializeField, Min(1)] private int currentArea = 1;
    [SerializeField, Min(0)] private int currentWave;

    public int CurrentFloor => currentFloor;
    public int CurrentArea => currentArea;
    public int CurrentWave => currentWave;
    public AreaState CurrentAreaState => areaState;

    [Header("Character Formation")]
    [Tooltip("생성된 캐릭터의 부모입니다. 비어 있으면 Character 오브젝트 또는 StageManager를 사용합니다.")]
    [SerializeField] private Transform characterRoot;
    [Tooltip("비어 있으면 GameScene의 CharPos 오브젝트를 기준점으로 사용합니다.")]
    [SerializeField] private Transform characterFormationOrigin;
    [SerializeField] private CharacterPlacement[] characterPlacements = Array.Empty<CharacterPlacement>();
    [SerializeField] private Vector2[] frontSlotOffsets =
    {
        Vector2.zero,
        new Vector2(-0.45f, 0f),
        new Vector2(-0.9f, 0f),
        new Vector2(-1.35f, 0f)
    };
    [SerializeField] private Vector2[] rearSlotOffsets =
    {
        new Vector2(-1.8f, 0f),
        new Vector2(-2.25f, 0f),
        new Vector2(-2.7f, 0f),
        new Vector2(-3.15f, 0f)
    };
    [SerializeField, Min(0.01f)] private float formationGizmoRadius = 0.16f;

    [Header("Area Movement")]
    [Tooltip("편성 기준점이 Area 입장 전에 위치할 좌측 화면 밖 기준점입니다.")]
    [SerializeField] private Transform characterEntryPoint;
    [Tooltip("편성 기준점이 Area 종료 후 도착할 우측 화면 밖 기준점입니다.")]
    [SerializeField] private Transform characterExitPoint;
    [SerializeField, Min(0.01f)] private float characterAreaMoveSpeed = 2f;
    [SerializeField, Min(0f)] private float areaClearDelay = 0.5f;

    [Header("Monster Waves")]
    [Tooltip("Floor별로 사용할 일반 몬스터 프리팹과 보스 프리팹을 지정합니다.")]
    [SerializeField] private FloorMonsterGroup[] floorMonsterGroups = Array.Empty<FloorMonsterGroup>();
    [SerializeField, Min(1)] private int poolSizePerPrefab = 5;
    [Tooltip("비어 있으면 Enemy 오브젝트를 생성 부모로 사용합니다.")]
    [SerializeField] private Transform monsterRoot;
    [Tooltip("웨이브의 첫 몬스터가 출현할 위치입니다. 비어 있으면 Monster Root 위치를 사용합니다.")]
    [SerializeField] private Transform monsterSpawnPoint;
    [SerializeField, Min(0.01f)] private float minimumSpawnSpacing = 0.35f;
    [SerializeField, Range(0.01f, 1.5f)] private float maximumSpawnSpacing = 1.5f;
    [SerializeField, Min(1)] private int totalWaves = 4;
    [SerializeField, Min(0f)] private float nextWaveDelay = 1f;
    [SerializeField] private int waveRandomSeed = 1001;

    [Header("Battle UI")]
    [Tooltip("캐릭터와 몬스터의 자식으로 자동 생성할 공용 HpBar 프리팹입니다.")]
    [SerializeField] private UnitHpBar unitHpBarPrefab;
    [Tooltip("Area 클리어 알림과 전투 후 행동 버튼을 관리합니다. 비어 있으면 테스트용 UI를 생성합니다.")]
    [SerializeField] private AreaClearUI areaClearUI;

    private readonly List<Character> battleCharacters = new List<Character>();
    private readonly List<Character> spawnedCharacters = new List<Character>();
    private readonly Dictionary<Character, Vector3> characterFormationPositions = new Dictionary<Character, Vector3>();
    private readonly List<Monster> activeMonsters = new List<Monster>();
    private readonly HashSet<Monster> aliveWaveMonsters = new HashSet<Monster>();
    private readonly Dictionary<MonsterRole, List<Monster>> monsterPool = new Dictionary<MonsterRole, List<Monster>>();
    private System.Random waveRandom;
    private AreaState areaState = AreaState.EnteringArea;
    private bool battleInitialized;
    private bool isAdvancingWave;

    public event Action<int, int> WaveStarted;

    [Header("Floor Environment")]
    [SerializeField] private Transform backgroundRoot;
    [SerializeField] private FloorStageEnvironment[] floorEnvironments = Array.Empty<FloorStageEnvironment>();

    private Transform background1;
    private readonly List<GameObject> activeFloorTilemapRoots = new List<GameObject>();

    #region Unity 생명주기

    private void Start()
    {
        InitializeBattle();
    }

    private void OnDestroy()
    {
        UnsubscribeAreaClearUI();
    }

    private void Update()
    {
        if (areaState != AreaState.Fighting)
        {
            return;
        }

        if (IsPartyDefeated())
        {
            FinishBattleAsDefeat();
            return;
        }

    }

    #endregion

    #region 전투 초기화

    private void InitializeBattle()
    {
        ResolveSceneReferences();

        if (!InitializeFloor(currentFloor))
        {
            enabled = false;
            return;
        }

        PlaceCharacters();
        PrepareMonsterPool();

        if (battleCharacters.Count == 0)
        {
            Debug.LogError("전투에 배치할 캐릭터가 없습니다.", this);
            enabled = false;
            return;
        }

        if (!HasAnyPooledMonster())
        {
            Debug.LogError($"{currentFloor}층에 사용할 몬스터 프리팹 또는 생성된 풀이 없습니다.", this);
            enabled = false;
            return;
        }

        waveRandom = new System.Random(waveRandomSeed);
        currentWave = 0;
        battleInitialized = true;
        isAdvancingWave = false;
        StartCoroutine(EnterArea());
    }

    private void ResolveSceneReferences()
    {
        if (characterRoot == null)
        {
            GameObject characterRootObject = GameObject.Find("Character");
            characterRoot = characterRootObject != null ? characterRootObject.transform : transform;
        }

        if (characterFormationOrigin == null)
        {
            GameObject characterOriginObject = GameObject.Find("CharPos");
            characterFormationOrigin = characterOriginObject != null ? characterOriginObject.transform : transform;
        }

        if (monsterRoot == null)
        {
            GameObject enemyObject = GameObject.Find("Enemy");
            monsterRoot = enemyObject != null ? enemyObject.transform : transform;
        }

        if (monsterSpawnPoint == null)
        {
            monsterSpawnPoint = monsterRoot;
        }

        if (backgroundRoot == null)
        {
            backgroundRoot = transform;
        }

        ResolveAreaClearUI();
    }

    private void ResolveAreaClearUI()
    {
        if (areaClearUI == null)
        {
            areaClearUI = FindFirstObjectByType<AreaClearUI>();
        }

        if (areaClearUI == null)
        {
            areaClearUI = AreaClearUI.CreateTemporary();
        }

        if (areaClearUI == null)
        {
            return;
        }

        areaClearUI.MoveRequested -= HandleMoveRequested;
        areaClearUI.MoveRequested += HandleMoveRequested;
        areaClearUI.HideAll();
    }

    private void UnsubscribeAreaClearUI()
    {
        if (areaClearUI != null)
        {
            areaClearUI.MoveRequested -= HandleMoveRequested;
        }
    }

    #endregion

    #region 캐릭터 배치

    private void PlaceCharacters()
    {
        battleCharacters.Clear();
        spawnedCharacters.Clear();
        characterFormationPositions.Clear();

        for (int i = 0; i < characterPlacements.Length && battleCharacters.Count < 4; i++)
        {
            CharacterPlacement placement = characterPlacements[i];
            if (placement == null || placement.CharacterPrefab == null)
            {
                continue;
            }

            Character character = Instantiate(placement.CharacterPrefab, characterRoot);
            character.name = placement.CharacterPrefab.name;
            AttachUnitHpBar(character);
            spawnedCharacters.Add(character);
            PlaceCharacter(character, placement.Slot);
        }

        if (battleCharacters.Count > 0)
        {
            return;
        }

        Character[] sceneCharacters = FindObjectsByType<Character>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Array.Sort(sceneCharacters, (left, right) => string.CompareOrdinal(left.name, right.name));

        for (int i = 0; i < sceneCharacters.Length && i < 4; i++)
        {
            FormationSlot slot = i == 0 ? FormationSlot.Front1 : (FormationSlot)i;
            PlaceCharacter(sceneCharacters[i], slot);
        }
    }

    private void PlaceCharacter(Character character, FormationSlot slot)
    {
        AttachUnitHpBar(character);
        Vector3 formationPosition = GetCharacterSlotPosition(slot);
        character.transform.position = formationPosition;
        character.StopAction();
        battleCharacters.Add(character);
        characterFormationPositions[character] = formationPosition;
    }

    private Vector3 GetCharacterSlotPosition(FormationSlot slot)
    {
        bool isFrontSlot = slot <= FormationSlot.Front4;
        int slotIndex = isFrontSlot
            ? (int)slot
            : (int)slot - (int)FormationSlot.Rear1;
        Vector2[] offsets = isFrontSlot ? frontSlotOffsets : rearSlotOffsets;
        Vector2 offset = offsets != null && slotIndex >= 0 && slotIndex < offsets.Length
            ? offsets[slotIndex]
            : Vector2.zero;

        Transform origin = characterFormationOrigin != null ? characterFormationOrigin : transform;
        return origin.TransformPoint(offset);
    }

    #endregion

    #region Area 입장 및 퇴장

    private IEnumerator EnterArea()
    {
        areaState = AreaState.EnteringArea;
        Vector3 formationOriginPosition = characterFormationOrigin != null
            ? characterFormationOrigin.position
            : transform.position;
        Vector3 entryOriginPosition = characterEntryPoint != null
            ? characterEntryPoint.position
            : formationOriginPosition + Vector3.left * 5f;
        Vector3 entryOffset = entryOriginPosition - formationOriginPosition;

        for (int i = 0; i < battleCharacters.Count; i++)
        {
            Character character = battleCharacters[i];
            if (character == null || !characterFormationPositions.TryGetValue(character, out Vector3 formationPosition))
            {
                continue;
            }

            character.transform.position = formationPosition + entryOffset;
            character.StartPresentationMove();
        }

        yield return MoveCharactersToTargets(characterFormationPositions);

        areaState = AreaState.Ready;
        Debug.Log($"{currentFloor}층 {currentArea} Area 배치가 완료되었습니다. StartWave를 호출할 수 있습니다.", this);
    }

    private IEnumerator ExitArea()
    {
        areaState = AreaState.ExitingArea;
        Vector3 formationOriginPosition = characterFormationOrigin != null
            ? characterFormationOrigin.position
            : transform.position;
        Vector3 exitOriginPosition = characterExitPoint != null
            ? characterExitPoint.position
            : formationOriginPosition + Vector3.right * 5f;
        Vector3 exitOffset = exitOriginPosition - formationOriginPosition;
        Dictionary<Character, Vector3> exitPositions = new Dictionary<Character, Vector3>();

        for (int i = 0; i < battleCharacters.Count; i++)
        {
            Character character = battleCharacters[i];
            if (character == null || !characterFormationPositions.TryGetValue(character, out Vector3 formationPosition))
            {
                continue;
            }

            exitPositions[character] = formationPosition + exitOffset;
            character.StartPresentationMove();
        }

        yield return MoveCharactersToTargets(exitPositions);

        areaState = AreaState.FloorCleared;
        isAdvancingWave = false;
        Debug.Log($"{currentFloor}층 1 Area 테스트가 완료되었습니다.", this);
    }

    private IEnumerator MoveCharactersToTargets(Dictionary<Character, Vector3> targetPositions)
    {
        bool isMoving;
        do
        {
            isMoving = false;
            foreach (KeyValuePair<Character, Vector3> entry in targetPositions)
            {
                Character character = entry.Key;
                if (character == null)
                {
                    continue;
                }

                character.transform.position = Vector3.MoveTowards(
                    character.transform.position,
                    entry.Value,
                    characterAreaMoveSpeed * Time.deltaTime
                );

                if ((character.transform.position - entry.Value).sqrMagnitude > 0.0001f)
                {
                    isMoving = true;
                }
            }

            yield return null;
        }
        while (isMoving);

        foreach (KeyValuePair<Character, Vector3> entry in targetPositions)
        {
            if (entry.Key == null)
            {
                continue;
            }

            entry.Key.transform.position = entry.Value;
            entry.Key.StopPresentationMove();
        }
    }

    #endregion

    #region 몬스터 풀

    private void PrepareMonsterPool()
    {
        monsterPool.Clear();

        FloorMonsterGroup floorGroup = GetCurrentFloorMonsterGroup();
        if (floorGroup == null)
        {
            Debug.LogError($"{currentFloor}층에 해당하는 몬스터 그룹이 없습니다.", this);
            return;
        }

        CreatePool(floorGroup.MonsterPrefabs, false);
        CreatePool(floorGroup.BossPrefabs, true);
    }

    private FloorMonsterGroup GetCurrentFloorMonsterGroup()
    {
        for (int i = 0; i < floorMonsterGroups.Length; i++)
        {
            FloorMonsterGroup group = floorMonsterGroups[i];
            if (group != null && group.Floor == currentFloor)
            {
                return group;
            }
        }

        return null;
    }

    private void CreatePool(Monster[] prefabs, bool isBossGroup)
    {
        for (int prefabIndex = 0; prefabIndex < prefabs.Length; prefabIndex++)
        {
            Monster prefab = prefabs[prefabIndex];
            if (prefab == null)
            {
                continue;
            }

            MonsterRole role = isBossGroup ? MonsterRole.Boss : prefab.Role;
            if (!monsterPool.TryGetValue(role, out List<Monster> rolePool))
            {
                rolePool = new List<Monster>();
                monsterPool.Add(role, rolePool);
            }

            for (int poolIndex = 0; poolIndex < poolSizePerPrefab; poolIndex++)
            {
                Monster monster = Instantiate(prefab, monsterRoot);
                monster.name = $"{currentFloor}F_{prefab.name}_Pool_{poolIndex + 1}";
                AttachUnitHpBar(monster);
                monster.gameObject.SetActive(false);
                rolePool.Add(monster);
            }
        }
    }

    private bool HasAnyPooledMonster()
    {
        foreach (KeyValuePair<MonsterRole, List<Monster>> entry in monsterPool)
        {
            if (entry.Value.Count > 0)
            {
                return true;
            }
        }

        return false;
    }

    #endregion

    #region 전투 UI

    // 유닛 프리팹에 HPBar가 없을 때 StageManager에 지정된 공용 프리팹을 자식으로 생성합니다.
    private void AttachUnitHpBar(Unit unit)
    {
        if (unit == null)
        {
            return;
        }

        Transform hpBarAnchor = unit.HpBarAnchor;
        UnitHpBar hpBar = unit.GetComponentInChildren<UnitHpBar>(true);

        // 프리팹에 HPBar가 미리 포함되어 있으면 새로 생성하지 않고 Anchor 아래로 재배치합니다.
        if (hpBar != null)
        {
            PlaceHpBarAtAnchor(hpBar, hpBarAnchor);
            return;
        }

        if (unitHpBarPrefab == null)
        {
            Debug.LogWarning("StageManager의 Unit Hp Bar Prefab이 지정되지 않았습니다.", this);
            return;
        }

        Transform hpBarParent = hpBarAnchor != null ? hpBarAnchor : unit.transform;
        hpBar = Instantiate(unitHpBarPrefab, hpBarParent);
        hpBar.name = unitHpBarPrefab.name;

        PlaceHpBarAtAnchor(hpBar, hpBarAnchor);
    }

    // Anchor가 있는 유닛만 HPBar의 부모와 로컬 변환을 기준점에 맞춥니다.
    // Anchor가 없는 기존 유닛은 프리팹에 저장된 기본 위치를 그대로 사용합니다.
    private static void PlaceHpBarAtAnchor(UnitHpBar hpBar, Transform hpBarAnchor)
    {
        // Anchor가 있는 프리팹은 개발자가 지정한 기준점에 정확히 맞추고,
        // Anchor가 없는 기존 프리팹은 HpBar 프리팹의 기본 위치를 유지합니다.
        if (hpBar == null || hpBarAnchor == null)
        {
            return;
        }

        hpBar.transform.SetParent(hpBarAnchor, false);
        hpBar.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        hpBar.transform.localScale = Vector3.one;
    }

    #endregion

    #region 웨이브 진행

    public void StartWave()
    {
        if (!battleInitialized)
        {
            Debug.LogWarning("전투 초기화가 완료되지 않아 웨이브를 시작할 수 없습니다.", this);
            return;
        }

        if (areaState != AreaState.Ready)
        {
            Debug.LogWarning($"현재 Area 상태({areaState})에서는 첫 웨이브를 시작할 수 없습니다.", this);
            return;
        }

        StartNextWave();
    }

    private void StartNextWave()
    {

        if (currentWave >= totalWaves)
        {
            Debug.LogWarning("시작할 수 있는 웨이브가 남아 있지 않습니다.", this);
            return;
        }

        currentWave++;
        UnregisterWaveMonsters();
        activeMonsters.Clear();

        int normalCount = 5;
        int swiftCount = 0;
        int defenseCount = 0;
        GetWaveRoleCounts(currentWave, ref normalCount, ref swiftCount, ref defenseCount);

        SpawnRole(MonsterRole.Normal, normalCount);
        SpawnRole(MonsterRole.Swift, swiftCount);
        SpawnRole(MonsterRole.Defense, defenseCount);

        ArrangeWaveMonsters();

        Debug.Log($"1층 웨이브 {currentWave}/{totalWaves} 시작: 몬스터 {activeMonsters.Count}마리", this);
        WaveStarted?.Invoke(currentWave, totalWaves);

        areaState = AreaState.Fighting;
        isAdvancingWave = false;

        for (int i = 0; i < activeMonsters.Count; i++)
        {
            activeMonsters[i].StartAction();
        }

        for (int i = 0; i < battleCharacters.Count; i++)
        {
            battleCharacters[i].StartAction();
        }

        TryCompleteCurrentWave();
    }

    private static void GetWaveRoleCounts(int waveNumber, ref int normal, ref int swift, ref int defense)
    {
        switch (waveNumber)
        {
            case 2:
                normal = 4;
                swift = 1;
                break;
            case 3:
                normal = 3;
                swift = 1;
                defense = 1;
                break;
            case 4:
                normal = 2;
                swift = 2;
                defense = 1;
                break;
        }
    }

    private void SpawnRole(MonsterRole role, int count)
    {
        for (int i = 0; i < count; i++)
        {
            Monster monster = GetInactiveMonster(role);
            if (monster == null)
            {
                Debug.LogWarning($"{currentFloor}층 {role} 역할의 사용 가능한 몬스터가 부족합니다.", this);
                continue;
            }

            monster.name = $"Wave{currentWave}_{role}_{i + 1}";
            monster.transform.position = monsterSpawnPoint.position;
            monster.gameObject.SetActive(true);
            monster.ResetForReuse();
            activeMonsters.Add(monster);
            RegisterWaveMonster(monster);
        }
    }

    // 현재 웨이브에 투입된 몬스터를 생존 집합에 등록하고 사망 이벤트를 구독합니다.
    private void RegisterWaveMonster(Monster monster)
    {
        if (monster == null || !aliveWaveMonsters.Add(monster))
        {
            return;
        }

        monster.Died -= HandleMonsterDied;
        monster.Died += HandleMonsterDied;
    }

    // 같은 몬스터의 중복 사망 통지는 무시하고 생존 몬스터가 없을 때만 웨이브를 완료합니다.
    private void HandleMonsterDied(Monster monster)
    {
        if (monster == null || !aliveWaveMonsters.Remove(monster))
        {
            return;
        }

        monster.Died -= HandleMonsterDied;
        TryCompleteCurrentWave();
    }

    // 빈 웨이브와 마지막 몬스터 사망을 같은 경로로 처리합니다.
    private void TryCompleteCurrentWave()
    {
        if (areaState != AreaState.Fighting || aliveWaveMonsters.Count > 0 || isAdvancingWave)
        {
            return;
        }

        isAdvancingWave = true;
        StartCoroutine(AdvanceWave());
    }

    // 오브젝트 풀 재사용 시 이전 웨이브의 이벤트 구독이 남지 않도록 정리합니다.
    private void UnregisterWaveMonsters()
    {
        foreach (Monster monster in aliveWaveMonsters)
        {
            if (monster != null)
            {
                monster.Died -= HandleMonsterDied;
            }
        }

        aliveWaveMonsters.Clear();
    }

    private Monster GetInactiveMonster(MonsterRole role)
    {
        if (!monsterPool.TryGetValue(role, out List<Monster> candidates))
        {
            return null;
        }

        List<Monster> inactiveCandidates = new List<Monster>();
        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i] != null && !candidates[i].gameObject.activeSelf)
            {
                inactiveCandidates.Add(candidates[i]);
            }
        }

        return inactiveCandidates.Count > 0
            ? inactiveCandidates[waveRandom.Next(inactiveCandidates.Count)]
            : null;
    }

    private void ArrangeWaveMonsters()
    {
        if (activeMonsters.Count == 0)
        {
            return;
        }

        Vector3 spawnPosition = monsterSpawnPoint.position;
        float currentX = spawnPosition.x;
        activeMonsters[0].transform.position = spawnPosition;

        for (int i = 1; i < activeMonsters.Count; i++)
        {
            float requiredSpacing = GetMonsterHalfWidth(activeMonsters[i - 1]) + GetMonsterHalfWidth(activeMonsters[i]);
            float safeMinimum = Mathf.Max(minimumSpawnSpacing, requiredSpacing);
            if (safeMinimum > maximumSpawnSpacing)
            {
                Debug.LogWarning($"몬스터 크기에 필요한 간격({safeMinimum:F2})이 최대 간격({maximumSpawnSpacing:F2})보다 큽니다.", this);
            }

            float spacing = safeMinimum >= maximumSpawnSpacing
                ? maximumSpawnSpacing
                : Mathf.Lerp(safeMinimum, maximumSpawnSpacing, (float)waveRandom.NextDouble());
            currentX += spacing;
            activeMonsters[i].transform.position = new Vector3(currentX, spawnPosition.y, spawnPosition.z);
        }
    }

    private static float GetMonsterHalfWidth(Monster monster)
    {
        Renderer monsterRenderer = monster.GetComponentInChildren<Renderer>();
        return monsterRenderer != null ? monsterRenderer.bounds.extents.x : 0f;
    }

    private IEnumerator AdvanceWave()
    {
        StopCharacterActions();

        if (currentWave >= totalWaves)
        {
            areaState = AreaState.AreaCleared;
            Debug.Log($"{currentFloor}층 {currentArea} Area의 모든 웨이브를 완료했습니다.", this);

            if (areaClearDelay > 0f)
            {
                yield return new WaitForSeconds(areaClearDelay);
            }

            isAdvancingWave = false;
            areaClearUI?.ShowAreaClear(currentArea);
            yield break;
        }

        yield return new WaitForSeconds(nextWaveDelay);
        StartNextWave();
    }

    #endregion

    #region 전투 종료 및 행동 제어

    // AreaCleared 상태에서 하단 이동 버튼을 눌렀을 때만 캐릭터 퇴장 연출을 시작합니다.
    private void HandleMoveRequested()
    {
        if (areaState != AreaState.AreaCleared || isAdvancingWave)
        {
            return;
        }

        isAdvancingWave = true;
        areaClearUI?.HideAll();
        StartCoroutine(ExitArea());
    }

    private bool IsPartyDefeated()
    {
        for (int i = 0; i < battleCharacters.Count; i++)
        {
            Character character = battleCharacters[i];
            if (character != null && !character.IsDead)
            {
                return false;
            }
        }

        return true;
    }

    private void FinishBattleAsDefeat()
    {
        battleInitialized = false;
        isAdvancingWave = true;
        UnregisterWaveMonsters();

        for (int i = 0; i < activeMonsters.Count; i++)
        {
            if (activeMonsters[i] != null)
            {
                activeMonsters[i].StopAction();
            }
        }

        Debug.Log("파티가 전멸했습니다.", this);
        enabled = false;
    }

    private void StopCharacterActions()
    {
        for (int i = 0; i < battleCharacters.Count; i++)
        {
            if (battleCharacters[i] != null)
            {
                battleCharacters[i].StopAction();
            }
        }
    }

    #endregion

    #region 캐릭터 배치 Gizmo

    private void OnDrawGizmos()
    {
        Transform origin = characterFormationOrigin;
        if (origin == null)
        {
            GameObject characterOriginObject = GameObject.Find("CharPos");
            origin = characterOriginObject != null ? characterOriginObject.transform : transform;
        }

        Matrix4x4 previousMatrix = Gizmos.matrix;
        Gizmos.matrix = origin.localToWorldMatrix;

        bool hasConfiguredPlacement = false;
        for (int i = 0; i < characterPlacements.Length; i++)
        {
            CharacterPlacement placement = characterPlacements[i];
            if (placement == null || placement.CharacterPrefab == null)
            {
                continue;
            }

            DrawFormationSlotGizmo(placement.Slot);
            hasConfiguredPlacement = true;
        }

        if (!hasConfiguredPlacement)
        {
            Character[] sceneCharacters = FindObjectsByType<Character>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Array.Sort(sceneCharacters, (left, right) => string.CompareOrdinal(left.name, right.name));

            for (int i = 0; i < sceneCharacters.Length && i < 4; i++)
            {
                DrawFormationSlotGizmo((FormationSlot)i);
            }
        }

        Gizmos.matrix = previousMatrix;
    }

    private void DrawFormationSlotGizmo(FormationSlot slot)
    {
        bool isFrontSlot = slot <= FormationSlot.Front4;
        int slotIndex = isFrontSlot
            ? (int)slot
            : (int)slot - (int)FormationSlot.Rear1;
        Vector2[] offsets = isFrontSlot ? frontSlotOffsets : rearSlotOffsets;

        if (offsets == null || slotIndex < 0 || slotIndex >= offsets.Length)
        {
            return;
        }

        Gizmos.color = isFrontSlot
            ? new Color(1f, 0.35f, 0.2f, 1f)
            : new Color(0.2f, 0.65f, 1f, 1f);
        Gizmos.DrawWireSphere(offsets[slotIndex], formationGizmoRadius);
    }

    #endregion

    // TODO: 보스 몬스터 생성과 보스전 결과 연결
    // TODO: 던전 페이즈와 정산·정비·탐색·이동 흐름 연결

    #region Floor 환경 관리

    public bool InitializeFloor(int floor)
    {
        if (floor < 1)
        {
            Debug.LogError("Floor 번호는 1 이상이어야 합니다.", this);
            return false;
        }

        FloorStageEnvironment environment = GetFloorEnvironment(floor);
        if (environment == null)
        {
            Debug.LogError($"{floor}층에 해당하는 Floor 환경 설정이 없습니다.", this);
            return false;
        }

        if (environment.BackgroundPrefab == null)
        {
            Debug.LogError($"{floor}층의 배경 프리팹이 지정되지 않았습니다.", this);
            return false;
        }

        CleanupFloorEnvironment();
        DisableAllFloorTilemaps();

        currentFloor = floor;
        activeFloorTilemapRoots.Clear();
        GameObject[] selectedTilemapRoots = environment.TilemapRoots ?? Array.Empty<GameObject>();
        for (int i = 0; i < selectedTilemapRoots.Length; i++)
        {
            GameObject tilemapRoot = selectedTilemapRoots[i];
            if (tilemapRoot == null)
            {
                continue;
            }

            tilemapRoot.SetActive(true);
            activeFloorTilemapRoots.Add(tilemapRoot);
        }

        background1 = Instantiate(environment.BackgroundPrefab, backgroundRoot).transform;
        background1.localPosition = environment.BackgroundStartPosition;

        if (background1.GetComponentInChildren<SpriteRenderer>() == null)
        {
            Debug.LogError($"{floor}층 배경 프리팹에 SpriteRenderer가 없습니다.", background1);
            CleanupFloorEnvironment();
            return false;
        }

        Debug.Log($"{floor}층 환경 초기화가 완료되었습니다.", this);
        return true;
    }

    public void CleanupFloor()
    {
        StopAllCoroutines();
        StopCharacterActions();
        UnregisterWaveMonsters();
        areaClearUI?.HideAll();

        foreach (KeyValuePair<MonsterRole, List<Monster>> entry in monsterPool)
        {
            List<Monster> pooledMonsters = entry.Value;
            for (int i = 0; i < pooledMonsters.Count; i++)
            {
                if (pooledMonsters[i] != null)
                {
                    Destroy(pooledMonsters[i].gameObject);
                }
            }
        }

        monsterPool.Clear();
        activeMonsters.Clear();

        for (int i = 0; i < spawnedCharacters.Count; i++)
        {
            if (spawnedCharacters[i] != null)
            {
                Destroy(spawnedCharacters[i].gameObject);
            }
        }

        spawnedCharacters.Clear();
        battleCharacters.Clear();
        characterFormationPositions.Clear();
        CleanupFloorEnvironment();
        DisableAllFloorTilemaps();

        currentWave = 0;
        areaState = AreaState.EnteringArea;
        battleInitialized = false;
        isAdvancingWave = false;
    }

    private FloorStageEnvironment GetFloorEnvironment(int floor)
    {
        for (int i = 0; i < floorEnvironments.Length; i++)
        {
            FloorStageEnvironment environment = floorEnvironments[i];
            if (environment != null && environment.Floor == floor)
            {
                return environment;
            }
        }

        return null;
    }

    private void CleanupFloorEnvironment()
    {
        if (background1 != null)
        {
            Destroy(background1.gameObject);
            background1 = null;
        }

        for (int i = 0; i < activeFloorTilemapRoots.Count; i++)
        {
            if (activeFloorTilemapRoots[i] != null)
            {
                activeFloorTilemapRoots[i].SetActive(false);
            }
        }

        activeFloorTilemapRoots.Clear();
    }

    private void DisableAllFloorTilemaps()
    {
        for (int i = 0; i < floorEnvironments.Length; i++)
        {
            FloorStageEnvironment environment = floorEnvironments[i];
            if (environment == null)
            {
                continue;
            }

            GameObject[] tilemapRoots = environment.TilemapRoots ?? Array.Empty<GameObject>();
            for (int rootIndex = 0; rootIndex < tilemapRoots.Length; rootIndex++)
            {
                if (tilemapRoots[rootIndex] != null)
                {
                    tilemapRoots[rootIndex].SetActive(false);
                }
            }
        }
    }

    #endregion
}
