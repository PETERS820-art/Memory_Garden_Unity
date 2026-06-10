using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(
    fileName = "MemoryGardenPlacementLayout",
    menuName = "Memory Garden/Placement Layout")]
public class MemoryGardenPlacementLayout : ScriptableObject
{
    public string layoutId = "default_layout";
    public List<MemoryItemPlacementRecord> records = new List<MemoryItemPlacementRecord>();
}

[Serializable]
public class MemoryItemPlacementRecord
{
    public string itemId;
    public string furnitureId;
    public string slotId;
}
