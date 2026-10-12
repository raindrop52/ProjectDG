using UnityEngine;

// 게임 전체 수명 동안 유지되며 공통 시스템의 진입점을 제공하는 매니저입니다.
// 진행 데이터, 저장, 씬 전환 등의 실제 구현은 각 시스템이 준비되는 단계에서 연결합니다.
[DisallowMultipleComponent]
public sealed class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    // TODO: 게임 진행 데이터 관리 연결
    // TODO: 저장 및 불러오기 시스템 연결
    // TODO: 비동기 씬 전환과 로딩 상태 연결
}
