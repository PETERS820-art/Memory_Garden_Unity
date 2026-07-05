using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[DisallowMultipleComponent]
public class MemoryGardenPlacementManager : MonoBehaviour
{
    [Serializable]
    private class RuntimePlacementLayoutData
    {
        public string layoutId;
        public List<MemoryItemPlacementRecord> records = new List<MemoryItemPlacementRecord>();
    }

    private const float AutoSavePollInterval = 0.5f;
    private const string DefaultRuntimeSaveFileName = "memory_garden_placement.json";

    [Header("Layout Source")]
    [SerializeField] private MemoryGardenPlacementLayout defaultLayout;
    [SerializeField] private bool applyDefaultLayoutOnStart = true;
    [SerializeField] private bool preferRuntimeSaveIfExists = true;
    [SerializeField] private bool autoSaveOnSnap = false;
    [SerializeField] private bool validateOnStart = true;
    [SerializeField] private string runtimeSaveFileName = DefaultRuntimeSaveFileName;

    [Header("Runtime Registry")]
    [SerializeField] private List<MemoryObject> registeredItems = new List<MemoryObject>();
    [SerializeField] private List<MemoryDisplayFurniture> registeredFurniture = new List<MemoryDisplayFurniture>();
    [SerializeField] private List<MemoryDisplaySlot> registeredSlots = new List<MemoryDisplaySlot>();

    public MemoryGardenPlacementLayout DefaultLayout => defaultLayout;

    private readonly Dictionary<string, MemoryObject> itemsById =
        new Dictionary<string, MemoryObject>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, MemoryDisplayFurniture> furnitureById =
        new Dictionary<string, MemoryDisplayFurniture>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, MemoryDisplaySlot> slotsByCompositeId =
        new Dictionary<string, MemoryDisplaySlot>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<MemoryObject, string> resolvedItemIdsByObject =
        new Dictionary<MemoryObject, string>();
    private readonly Dictionary<MemoryDisplayFurniture, string> resolvedFurnitureIdsByObject =
        new Dictionary<MemoryDisplayFurniture, string>();
    private readonly Dictionary<MemoryDisplaySlot, string> resolvedSlotIdsByObject =
        new Dictionary<MemoryDisplaySlot, string>();
    private readonly Dictionary<MemoryDisplaySlot, MemoryDisplayFurniture> slotOwners =
        new Dictionary<MemoryDisplaySlot, MemoryDisplayFurniture>();

    private float nextAutoSaveCheckTime;
    private string lastCapturedLayoutJson;

    private void Start()
    {
        RegisterSceneObjects();

        if (validateOnStart)
        {
            ValidateSceneIds();
        }
        else
        {
            BuildLookupTables(logWarnings: false);
        }

        bool appliedLayout = false;

        if (preferRuntimeSaveIfExists && RuntimeSaveExists())
        {
            appliedLayout = LoadLayoutFromJson();
        }

        if (!appliedLayout && applyDefaultLayoutOnStart && defaultLayout != null)
        {
            appliedLayout = ApplyLayout(defaultLayout);
        }

        if (autoSaveOnSnap)
        {
            lastCapturedLayoutJson = CreateRuntimeLayoutJson();
            nextAutoSaveCheckTime = Time.unscaledTime + AutoSavePollInterval;
        }
        else if (appliedLayout)
        {
            lastCapturedLayoutJson = CreateRuntimeLayoutJson();
        }
    }

    private void Update()
    {
        if (!autoSaveOnSnap || Time.unscaledTime < nextAutoSaveCheckTime)
        {
            return;
        }

        nextAutoSaveCheckTime = Time.unscaledTime + AutoSavePollInterval;

        string currentLayoutJson = CreateRuntimeLayoutJson();
        if (string.IsNullOrWhiteSpace(currentLayoutJson) || string.Equals(currentLayoutJson, lastCapturedLayoutJson, StringComparison.Ordinal))
        {
            return;
        }

        WriteRuntimeLayoutJson(currentLayoutJson);
        lastCapturedLayoutJson = currentLayoutJson;
    }

    public void RegisterSceneObjects()
    {
        registeredItems.Clear();
        registeredFurniture.Clear();
        registeredSlots.Clear();

        MemoryObject[] items =
            FindObjectsByType<MemoryObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        MemoryDisplayFurniture[] furniture =
            FindObjectsByType<MemoryDisplayFurniture>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        MemoryDisplaySlot[] slots =
            FindObjectsByType<MemoryDisplaySlot>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        registeredItems.AddRange(items);
        registeredFurniture.AddRange(furniture);
        registeredSlots.AddRange(slots);

        for (int i = 0; i < registeredFurniture.Count; i++)
        {
            if (registeredFurniture[i] != null)
            {
                registeredFurniture[i].AutoCollectFeatures();
            }
        }
    }

    public bool ValidateSceneIds()
    {
        RegisterSceneObjects();
        return BuildLookupTables(logWarnings: true);
    }

    public bool ValidateLayout(MemoryGardenPlacementLayout layout)
    {
        if (layout == null)
        {
            Debug.LogWarning("[MemoryGardenPlacementManager] No placement layout was provided for validation.", this);
            return false;
        }

        EnsureLookupTablesAvailable(logWarnings: validateOnStart);
        return ValidateLayoutRecords(layout.layoutId, layout.records, layout.name);
    }

    public bool ApplyLayout(MemoryGardenPlacementLayout layout)
    {
        if (layout == null)
        {
            Debug.LogWarning("[MemoryGardenPlacementManager] No default placement layout is assigned.", this);
            return false;
        }

        EnsureLookupTablesAvailable(logWarnings: validateOnStart);
        ValidateLayoutRecords(layout.layoutId, layout.records, layout.name);

        bool applied = ApplyLayoutRecords(layout.layoutId, layout.records, layout.name);
        if (applied)
        {
            lastCapturedLayoutJson = CreateRuntimeLayoutJson();
        }

        return applied;
    }

    public string CaptureCurrentLayoutToJson()
    {
        string json = CreateRuntimeLayoutJson();
        if (!string.IsNullOrWhiteSpace(json))
        {
            WriteRuntimeLayoutJson(json);
            lastCapturedLayoutJson = json;
        }

        return json;
    }

    private string CreateRuntimeLayoutJson()
    {
        EnsureLookupTablesAvailable(logWarnings: false);

        RuntimePlacementLayoutData data = new RuntimePlacementLayoutData
        {
            layoutId = defaultLayout != null && !string.IsNullOrWhiteSpace(defaultLayout.layoutId)
                ? defaultLayout.layoutId
                : "runtime_layout",
            records = CaptureCurrentLayoutRecords()
        };

        return JsonUtility.ToJson(data, prettyPrint: true);
    }

    public bool LoadLayoutFromJson()
    {
        EnsureLookupTablesAvailable(logWarnings: validateOnStart);

        string runtimeSavePath = GetRuntimeSavePath();
        if (!File.Exists(runtimeSavePath))
        {
            return false;
        }

        try
        {
            string json = File.ReadAllText(runtimeSavePath);
            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogWarning($"[MemoryGardenPlacementManager] Runtime placement file is empty: {runtimeSavePath}", this);
                return false;
            }

            RuntimePlacementLayoutData data = JsonUtility.FromJson<RuntimePlacementLayoutData>(json);
            if (data == null || data.records == null)
            {
                Debug.LogWarning($"[MemoryGardenPlacementManager] Runtime placement file could not be deserialized: {runtimeSavePath}", this);
                return false;
            }

            ValidateLayoutRecords(data.layoutId, data.records, "runtime save");
            bool applied = ApplyLayoutRecords(data.layoutId, data.records, "runtime save");
            if (applied)
            {
                lastCapturedLayoutJson = json;
            }

            return applied;
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"[MemoryGardenPlacementManager] Failed to load runtime placement JSON at {runtimeSavePath}: {exception.Message}",
                this);
            return false;
        }
    }

    public void ClearAllOccupancy()
    {
        if (registeredSlots.Count == 0 && registeredItems.Count == 0)
        {
            RegisterSceneObjects();
        }

        for (int i = 0; i < registeredSlots.Count; i++)
        {
            MemoryDisplaySlot slot = registeredSlots[i];
            if (slot != null)
            {
                slot.ClearOccupied();
            }
        }

        for (int i = 0; i < registeredItems.Count; i++)
        {
            MemoryObject item = registeredItems[i];
            if (item != null)
            {
                item.ClearCurrentSlotAssignment();
            }
        }
    }

    public List<MemoryItemPlacementRecord> CaptureCurrentLayoutRecords()
    {
        EnsureLookupTablesAvailable(logWarnings: false);

        List<MemoryItemPlacementRecord> records = new List<MemoryItemPlacementRecord>();
        List<MemoryObject> sortedItems = new List<MemoryObject>(registeredItems);
        sortedItems.Sort(CompareItemsByResolvedId);

        for (int i = 0; i < sortedItems.Count; i++)
        {
            MemoryObject item = sortedItems[i];
            if (item == null || item.CurrentSlot == null)
            {
                continue;
            }

            if (!slotOwners.TryGetValue(item.CurrentSlot, out MemoryDisplayFurniture furniture) || furniture == null)
            {
                continue;
            }

            string itemId = GetResolvedItemId(item);
            string furnitureId = GetResolvedFurnitureId(furniture);
            string slotId = GetResolvedSlotId(item.CurrentSlot);

            if (string.IsNullOrWhiteSpace(itemId)
                || string.IsNullOrWhiteSpace(furnitureId)
                || string.IsNullOrWhiteSpace(slotId))
            {
                continue;
            }

            records.Add(new MemoryItemPlacementRecord
            {
                itemId = itemId,
                furnitureId = furnitureId,
                slotId = slotId
            });
        }

        return records;
    }

    private bool ApplyLayoutRecords(
        string layoutId,
        List<MemoryItemPlacementRecord> records,
        string sourceLabel)
    {
        EnsureLookupTablesAvailable(logWarnings: false);
        ClearAllOccupancy();

        if (records == null || records.Count == 0)
        {
            Debug.Log(
                $"[MemoryGardenPlacementManager] No placement records were found for {sourceLabel}.",
                this);
            return false;
        }

        bool appliedAtLeastOne = false;
        HashSet<string> claimedSlotKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> claimedItemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < records.Count; i++)
        {
            MemoryItemPlacementRecord record = records[i];
            if (record == null)
            {
                continue;
            }

            string itemId = NormalizeId(record.itemId);
            string furnitureId = NormalizeId(record.furnitureId);
            string slotId = NormalizeId(record.slotId);

            if (string.IsNullOrWhiteSpace(itemId)
                || string.IsNullOrWhiteSpace(furnitureId)
                || string.IsNullOrWhiteSpace(slotId))
            {
                continue;
            }

            if (!claimedItemIds.Add(itemId))
            {
                Debug.LogWarning(
                    $"[MemoryGardenPlacementManager] Skipping duplicate item assignment for '{itemId}' in {sourceLabel}.",
                    this);
                continue;
            }

            if (!itemsById.TryGetValue(itemId, out MemoryObject item) || item == null)
            {
                Debug.LogWarning(
                    $"[MemoryGardenPlacementManager] Could not find item '{itemId}' while applying {sourceLabel}.",
                    this);
                continue;
            }

            if (!furnitureById.TryGetValue(furnitureId, out MemoryDisplayFurniture furniture) || furniture == null)
            {
                Debug.LogWarning(
                    $"[MemoryGardenPlacementManager] Could not find furniture '{furnitureId}' while applying {sourceLabel}.",
                    this);
                continue;
            }

            string slotKey = CreateSlotKey(furnitureId, slotId);
            if (!claimedSlotKeys.Add(slotKey))
            {
                Debug.LogWarning(
                    $"[MemoryGardenPlacementManager] Slot '{slotId}' on furniture '{furnitureId}' is assigned more than once in {sourceLabel}.",
                    this);
                continue;
            }

            if (!slotsByCompositeId.TryGetValue(slotKey, out MemoryDisplaySlot slot) || slot == null)
            {
                Debug.LogWarning(
                    $"[MemoryGardenPlacementManager] Could not find slot '{slotId}' on furniture '{furnitureId}' while applying {sourceLabel}.",
                    this);
                continue;
            }

            if (!IsItemCompatibleWithSlot(item, slot))
            {
                Debug.LogWarning(
                    $"[MemoryGardenPlacementManager] Item '{itemId}' is incompatible with slot '{slotId}' on '{furnitureId}'.",
                    this);
                continue;
            }

            if (!item.TryPlaceOnSlot(slot))
            {
                Debug.LogWarning(
                    $"[MemoryGardenPlacementManager] Failed to place item '{itemId}' onto slot '{slotId}' on '{furnitureId}'.",
                    this);
                continue;
            }

            UpdateRespawnPose(item);
            appliedAtLeastOne = true;
        }

        if (appliedAtLeastOne)
        {
            Debug.Log(
                $"[MemoryGardenPlacementManager] Applied layout '{layoutId}' from {sourceLabel}.",
                this);
        }

        return appliedAtLeastOne;
    }

    private bool BuildLookupTables(bool logWarnings)
    {
        itemsById.Clear();
        furnitureById.Clear();
        slotsByCompositeId.Clear();
        resolvedItemIdsByObject.Clear();
        resolvedFurnitureIdsByObject.Clear();
        resolvedSlotIdsByObject.Clear();
        slotOwners.Clear();

        bool isValid = true;

        for (int i = 0; i < registeredItems.Count; i++)
        {
            MemoryObject item = registeredItems[i];
            if (item == null)
            {
                continue;
            }

            string itemId = ResolveItemId(item, logWarnings);
            if (string.IsNullOrWhiteSpace(itemId))
            {
                isValid = false;
                continue;
            }

            resolvedItemIdsByObject[item] = itemId;

            if (itemsById.ContainsKey(itemId))
            {
                if (logWarnings)
                {
                    Debug.LogWarning(
                        $"[MemoryGardenPlacementManager] Duplicate itemId '{itemId}' detected on {item.name}.",
                        item);
                }

                isValid = false;
                continue;
            }

            itemsById.Add(itemId, item);
        }

        for (int i = 0; i < registeredFurniture.Count; i++)
        {
            MemoryDisplayFurniture furniture = registeredFurniture[i];
            if (furniture == null)
            {
                continue;
            }

            string furnitureId = ResolveFurnitureId(furniture, logWarnings);
            if (string.IsNullOrWhiteSpace(furnitureId))
            {
                isValid = false;
                continue;
            }

            resolvedFurnitureIdsByObject[furniture] = furnitureId;

            if (furnitureById.ContainsKey(furnitureId))
            {
                if (logWarnings)
                {
                    Debug.LogWarning(
                        $"[MemoryGardenPlacementManager] Duplicate furnitureId '{furnitureId}' detected on {furniture.name}.",
                        furniture);
                }

                isValid = false;
                continue;
            }

            furnitureById.Add(furnitureId, furniture);
        }

        for (int i = 0; i < registeredSlots.Count; i++)
        {
            MemoryDisplaySlot slot = registeredSlots[i];
            if (slot == null)
            {
                continue;
            }

            MemoryDisplayFurniture owner = FindOwningFurniture(slot);
            if (owner == null)
            {
                if (logWarnings)
                {
                    Debug.LogWarning(
                        $"[MemoryGardenPlacementManager] Slot '{slot.name}' is not parented under a MemoryDisplayFurniture.",
                        slot);
                }

                isValid = false;
                continue;
            }

            string furnitureId = GetResolvedFurnitureId(owner);
            if (string.IsNullOrWhiteSpace(furnitureId))
            {
                furnitureId = ResolveFurnitureId(owner, logWarnings);
                if (!string.IsNullOrWhiteSpace(furnitureId))
                {
                    resolvedFurnitureIdsByObject[owner] = furnitureId;
                }
            }

            string slotId = ResolveSlotId(slot, logWarnings);
            if (string.IsNullOrWhiteSpace(furnitureId) || string.IsNullOrWhiteSpace(slotId))
            {
                isValid = false;
                continue;
            }

            slotOwners[slot] = owner;
            resolvedSlotIdsByObject[slot] = slotId;

            string slotKey = CreateSlotKey(furnitureId, slotId);
            if (slotsByCompositeId.ContainsKey(slotKey))
            {
                if (logWarnings)
                {
                    Debug.LogWarning(
                        $"[MemoryGardenPlacementManager] Duplicate slotId '{slotId}' detected within furniture '{furnitureId}'.",
                        slot);
                }

                isValid = false;
                continue;
            }

            slotsByCompositeId.Add(slotKey, slot);
        }

        return isValid;
    }

    private bool ValidateLayoutRecords(
        string layoutId,
        List<MemoryItemPlacementRecord> records,
        string sourceLabel)
    {
        bool isValid = true;

        if (records == null)
        {
            Debug.LogWarning(
                $"[MemoryGardenPlacementManager] Layout '{layoutId}' from {sourceLabel} has no records list.",
                this);
            return false;
        }

        HashSet<string> claimedSlotKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        HashSet<string> claimedItemIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < records.Count; i++)
        {
            MemoryItemPlacementRecord record = records[i];
            if (record == null)
            {
                Debug.LogWarning(
                    $"[MemoryGardenPlacementManager] Layout '{layoutId}' from {sourceLabel} contains a null record at index {i}.",
                    this);
                isValid = false;
                continue;
            }

            string itemId = NormalizeId(record.itemId);
            string furnitureId = NormalizeId(record.furnitureId);
            string slotId = NormalizeId(record.slotId);

            if (string.IsNullOrWhiteSpace(itemId))
            {
                Debug.LogWarning(
                    $"[MemoryGardenPlacementManager] Layout '{layoutId}' has an empty itemId at record {i}.",
                    this);
                isValid = false;
                continue;
            }

            if (!claimedItemIds.Add(itemId))
            {
                Debug.LogWarning(
                    $"[MemoryGardenPlacementManager] Layout '{layoutId}' assigns item '{itemId}' more than once.",
                    this);
                isValid = false;
            }

            if (!itemsById.TryGetValue(itemId, out MemoryObject item) || item == null)
            {
                Debug.LogWarning(
                    $"[MemoryGardenPlacementManager] Layout '{layoutId}' references missing itemId '{itemId}'.",
                    this);
                isValid = false;
            }

            if (string.IsNullOrWhiteSpace(furnitureId))
            {
                Debug.LogWarning(
                    $"[MemoryGardenPlacementManager] Layout '{layoutId}' has an empty furnitureId for item '{itemId}'.",
                    this);
                isValid = false;
                continue;
            }

            if (!furnitureById.TryGetValue(furnitureId, out MemoryDisplayFurniture furniture) || furniture == null)
            {
                Debug.LogWarning(
                    $"[MemoryGardenPlacementManager] Layout '{layoutId}' references missing furnitureId '{furnitureId}'.",
                    this);
                isValid = false;
            }

            if (string.IsNullOrWhiteSpace(slotId))
            {
                Debug.LogWarning(
                    $"[MemoryGardenPlacementManager] Layout '{layoutId}' has an empty slotId for item '{itemId}'.",
                    this);
                isValid = false;
                continue;
            }

            string slotKey = CreateSlotKey(furnitureId, slotId);
            if (!claimedSlotKeys.Add(slotKey))
            {
                Debug.LogWarning(
                    $"[MemoryGardenPlacementManager] Layout '{layoutId}' assigns multiple items to slot '{slotId}' on furniture '{furnitureId}'.",
                    this);
                isValid = false;
            }

            if (!slotsByCompositeId.TryGetValue(slotKey, out MemoryDisplaySlot slot) || slot == null)
            {
                Debug.LogWarning(
                    $"[MemoryGardenPlacementManager] Layout '{layoutId}' references missing slot '{slotId}' on furniture '{furnitureId}'.",
                    this);
                isValid = false;
                continue;
            }

            if (item != null && !IsItemCompatibleWithSlot(item, slot))
            {
                Debug.LogWarning(
                    $"[MemoryGardenPlacementManager] Item '{itemId}' is incompatible with slot '{slotId}' on '{furnitureId}' in layout '{layoutId}'.",
                    this);
                isValid = false;
            }
        }

        return isValid;
    }

    private void EnsureLookupTablesAvailable(bool logWarnings)
    {
        if (registeredItems.Count == 0 && registeredFurniture.Count == 0 && registeredSlots.Count == 0)
        {
            RegisterSceneObjects();
        }

        if (itemsById.Count == 0 && furnitureById.Count == 0 && slotsByCompositeId.Count == 0)
        {
            BuildLookupTables(logWarnings);
        }
    }

    private void UpdateRespawnPose(MemoryObject item)
    {
        if (item == null)
        {
            return;
        }

        MemoryItemRespawn respawn = item.GetComponent<MemoryItemRespawn>();
        if (respawn != null)
        {
            respawn.SetRespawnPose(item.transform.position, item.transform.rotation);
        }
    }

    private bool IsItemCompatibleWithSlot(MemoryObject item, MemoryDisplaySlot slot)
    {
        if (item == null || slot == null || !item.EnablePlacement)
        {
            return false;
        }

        if (!item.CanUseSlotType(slot.Type))
        {
            return false;
        }

        IReadOnlyList<ItemSize> acceptedSizes = slot.AcceptedItemSizes;
        if (acceptedSizes == null || acceptedSizes.Count == 0)
        {
            return true;
        }

        ItemSize itemSize = item.GetPlacementItemSize();
        for (int i = 0; i < acceptedSizes.Count; i++)
        {
            if (acceptedSizes[i] == itemSize)
            {
                return true;
            }
        }

        return false;
    }

    private MemoryDisplayFurniture FindOwningFurniture(MemoryDisplaySlot slot)
    {
        return slot != null ? slot.GetComponentInParent<MemoryDisplayFurniture>() : null;
    }

    private string ResolveItemId(MemoryObject item, bool logFallbackWarning)
    {
        if (item == null)
        {
            return string.Empty;
        }

        string itemId = NormalizeId(item.ItemId);
        if (!string.IsNullOrWhiteSpace(itemId))
        {
            return itemId;
        }

        string fallbackId = NormalizeId(item.gameObject.name);
        if (logFallbackWarning)
        {
            Debug.LogWarning(
                $"[MemoryGardenPlacementManager] MemoryObject '{item.name}' has an empty itemId. Falling back to GameObject name '{fallbackId}'.",
                item);
        }

        return fallbackId;
    }

    private string ResolveFurnitureId(MemoryDisplayFurniture furniture, bool logFallbackWarning)
    {
        if (furniture == null)
        {
            return string.Empty;
        }

        string furnitureId = NormalizeId(furniture.FurnitureId);
        if (!string.IsNullOrWhiteSpace(furnitureId))
        {
            return furnitureId;
        }

        string fallbackId = NormalizeId(furniture.gameObject.name);
        if (logFallbackWarning)
        {
            Debug.LogWarning(
                $"[MemoryGardenPlacementManager] Furniture '{furniture.name}' has an empty furnitureId. Falling back to GameObject name '{fallbackId}'.",
                furniture);
        }

        return fallbackId;
    }

    private string ResolveSlotId(MemoryDisplaySlot slot, bool logFallbackWarning)
    {
        if (slot == null)
        {
            return string.Empty;
        }

        string slotId = NormalizeId(slot.SlotId);
        if (!string.IsNullOrWhiteSpace(slotId))
        {
            return slotId;
        }

        string fallbackId = NormalizeId(slot.gameObject.name);
        if (logFallbackWarning)
        {
            Debug.LogWarning(
                $"[MemoryGardenPlacementManager] Slot '{slot.name}' has an empty slotId. Falling back to GameObject name '{fallbackId}'.",
                slot);
        }

        return fallbackId;
    }

    private string GetResolvedItemId(MemoryObject item)
    {
        if (item != null && resolvedItemIdsByObject.TryGetValue(item, out string itemId))
        {
            return itemId;
        }

        return ResolveItemId(item, logFallbackWarning: false);
    }

    private string GetResolvedFurnitureId(MemoryDisplayFurniture furniture)
    {
        if (furniture != null && resolvedFurnitureIdsByObject.TryGetValue(furniture, out string furnitureId))
        {
            return furnitureId;
        }

        return ResolveFurnitureId(furniture, logFallbackWarning: false);
    }

    private string GetResolvedSlotId(MemoryDisplaySlot slot)
    {
        if (slot != null && resolvedSlotIdsByObject.TryGetValue(slot, out string slotId))
        {
            return slotId;
        }

        return ResolveSlotId(slot, logFallbackWarning: false);
    }

    private int CompareItemsByResolvedId(MemoryObject left, MemoryObject right)
    {
        string leftId = GetResolvedItemId(left);
        string rightId = GetResolvedItemId(right);
        return string.Compare(leftId, rightId, StringComparison.OrdinalIgnoreCase);
    }

    private string GetRuntimeSavePath()
    {
        string fileName = string.IsNullOrWhiteSpace(runtimeSaveFileName)
            ? DefaultRuntimeSaveFileName
            : runtimeSaveFileName.Trim();
        return Path.Combine(Application.persistentDataPath, fileName);
    }

    private bool RuntimeSaveExists()
    {
        return File.Exists(GetRuntimeSavePath());
    }

    private void WriteRuntimeLayoutJson(string json)
    {
        try
        {
            File.WriteAllText(GetRuntimeSavePath(), json);
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                $"[MemoryGardenPlacementManager] Failed to write runtime placement JSON: {exception.Message}",
                this);
        }
    }

    private static string NormalizeId(string value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private static string CreateSlotKey(string furnitureId, string slotId)
    {
        return $"{NormalizeId(furnitureId)}::{NormalizeId(slotId)}";
    }
}
