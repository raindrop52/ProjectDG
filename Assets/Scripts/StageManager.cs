using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.Tilemaps;

public class StageManager : MonoBehaviour
{
    private Transform background1;
    private Transform background2;
    private float background_width;

    [Header("Background")]
    public Transform prefabParent;
    public GameObject[] background_Prefabs;
    public float background_offset;
    public Vector2 startPos = Vector2.zero;

    [Header("TileMap")]
    public Transform map;
    public TilemapRenderer[] tiles;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        InitSystem();
    }

    private void InitSystem()
    {
        if (map != null && map.childCount > 0)
        {
            tiles = new TilemapRenderer[map.childCount];

            ResetTileMap();
        }
    }

    private void ResetTileMap()
    {
        if (map.childCount <= 0)
        {
            Debug.Log("TileMap Count = 0");
            return;
        }

        for (int cnt = 0; cnt < map.childCount; cnt++)
        {
            tiles[cnt] = map.GetChild(cnt).GetComponent<TilemapRenderer>();
            tiles[cnt].enabled = false;
        }
    }

    public void InitGame(int arrayNum)
    {
        int limitNum = background_Prefabs.Length;
        if (arrayNum > limitNum)
            Debug.Log("Error : Background 배열 최대치 초과");
        else
        {
            // Create Prefab Background
            background1 = Instantiate(background_Prefabs[arrayNum], prefabParent).transform;
            background1.localPosition = startPos;

            SpriteRenderer spRender = background1.GetComponentInChildren<SpriteRenderer>();
            background_width = spRender.bounds.size.x;

            background2 = Instantiate(background_Prefabs[arrayNum], prefabParent).transform;
            background2.localPosition = startPos + Vector2.right * background_width;

            // Enabled TileMap
            if (arrayNum > tiles.Length)
                Debug.Log("Error : TileMap 배열 최대치 초과");
            else
                tiles[arrayNum].enabled = true;
        }
    }

    public void EndGame()
    {
        Destroy(background1.gameObject);
        Destroy(background2.gameObject);

        ResetTileMap();
    }
    
    private void OnTriggerExit2D(Collider2D collision)
    {
        if (background1 != null && background2 != null)
        {
            if (collision != null)
            {
                Transform target = collision.transform;

                if(target.IsChildOf(background1))
                {
                    background1.position = background2.position + Vector3.right * (background_width - background_offset);
                }
                else if (target.IsChildOf(background2))
                {
                    background2.position = background1.position + Vector3.right * (background_width - background_offset);
                }
            }
        }
    }
}
