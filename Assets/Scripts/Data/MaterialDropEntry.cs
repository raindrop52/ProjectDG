using System;
using UnityEngine;

[Serializable]
public sealed class MaterialDropEntry
{
    [SerializeField] private MaterialData material;
    [SerializeField, Range(0f, 100f)] private float dropChancePercent = 100f;
    [SerializeField, Min(1)] private int minAmount = 1;
    [SerializeField, Min(1)] private int maxAmount = 1;

    public MaterialData Material => material;
    public float DropChancePercent => dropChancePercent;
    public int MinAmount => minAmount;
    public int MaxAmount => Mathf.Max(minAmount, maxAmount);
}
