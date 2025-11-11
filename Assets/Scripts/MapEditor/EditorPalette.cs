using System.Collections.Generic;
using UnityEngine;

// 팔레트에 표시될 개별 아이템의 정보
[System.Serializable]
public class PaletteItem
{
    public Sprite icon; // UI 버튼에 표시될 아이콘
    public GameObject prefab; // EditorManager가 배치할 실제 프리팹
}

// 프리팹 아이템들의 묶음 (예: "플랫폼", "장애물")
[System.Serializable]
public class PaletteCategory
{
    public List<PaletteItem> items = new List<PaletteItem>();
}