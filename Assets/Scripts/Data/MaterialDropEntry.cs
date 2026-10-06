using System;
using UnityEngine;

[Serializable]
// 몬스터 사망 시 확률에 따라 소재 1개를 지급하기 위한 드롭 항목입니다.
public sealed class MaterialDropEntry
{
    [SerializeField] private MaterialData material;
    [SerializeField, Range(0f, 100f)] private float dropChancePercent = 100f;

    public MaterialData Material => material;
    public float DropChancePercent => dropChancePercent;
}
