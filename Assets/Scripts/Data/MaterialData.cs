using UnityEngine;

[CreateAssetMenu(fileName = "MaterialData", menuName = "Dungeon Guidance/Material Data")]
public sealed class MaterialData : ScriptableObject
{
    [SerializeField] private string materialId;
    [SerializeField] private string displayName;
    [SerializeField] private Sprite icon;
    [SerializeField, Min(0)] private int sellPrice;

    public string MaterialId => materialId;
    public string DisplayName => displayName;
    public Sprite Icon => icon;
    public int SellPrice => sellPrice;
}
