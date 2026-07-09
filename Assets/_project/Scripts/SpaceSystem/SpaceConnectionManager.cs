using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteAlways]
public class SpaceConnectionManager : MonoBehaviour
{
    private const float DefaultSnapLength = 2f;
    private const string DefaultDisplayZoneName = "DisplayZone";
    private const string LegacyDisplayZoneName = "MemoryDisplayZoneRoot";
    private const string SpaceConnectionsRootName = "SpaceConnections";
    private const string PreviewConnectionName = "_PreviewConnection";
    private const string ConnectedBlocksRootName = "ConnectedBlocks";
    private const string WhiteFloorSegmentId = "SM_floor_white_001_1X2";
    private const string WhiteFloorFallbackSegmentId = "SM_floor_white_001_1X1";
    private const string WhiteWallSegmentId = "SM_wall_white_001_1x2.5";

    [Serializable]
    private struct CandidatePose
    {
        public MemorySpaceBlock blockB;
        public Vector3 blockBPosition;
        public Quaternion blockBRotation;
        public Vector3 anchorAPosition;
        public Vector3 anchorBPosition;
        public float connectorWidth;
        public float connectorHeight;
        public float connectorLength;
    }

    [SerializeField] private Transform displayZoneRoot;
    [SerializeField] private Transform spaceConnectionsRoot;
    [SerializeField] private List<SpaceOpeningPort> registeredPorts = new List<SpaceOpeningPort>();
    [SerializeField] private SpaceConnection previewConnection;

    public IReadOnlyList<SpaceOpeningPort> RegisteredPorts
    {
        get { return registeredPorts; }
    }

    private void OnEnable()
    {
        EnsureConnectionRoots();
        RefreshRegisteredPorts();
    }

    private void OnValidate()
    {
        EnsureConnectionRoots();
        RefreshRegisteredPorts();
    }

    public static SpaceConnectionManager FindExistingManager()
    {
        SpaceConnectionManager[] managers = FindObjectsOfType<SpaceConnectionManager>(true);
        return managers != null && managers.Length > 0 ? managers[0] : null;
    }

    public static SpaceConnectionManager GetOrCreateManager()
    {
        SpaceConnectionManager manager = FindExistingManager();
        if (manager != null)
        {
            manager.EnsureConnectionRoots();
            manager.RefreshRegisteredPorts();
            return manager;
        }

        GameObject managerObject = new GameObject("SpaceConnectionManager");
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Undo.RegisterCreatedObjectUndo(managerObject, "Create Space Connection Manager");
            manager = Undo.AddComponent<SpaceConnectionManager>(managerObject);
        }
        else
#endif
        {
            manager = managerObject.AddComponent<SpaceConnectionManager>();
        }

        manager.EnsureConnectionRoots();
        manager.RefreshRegisteredPorts();
        return manager;
    }

    public void RegisterPort(SpaceOpeningPort port)
    {
        if (port == null || registeredPorts.Contains(port))
        {
            return;
        }

        registeredPorts.Add(port);
    }

    public void UnregisterPort(SpaceOpeningPort port)
    {
        if (port == null)
        {
            return;
        }

        registeredPorts.Remove(port);
    }

    [ContextMenu("Refresh Registered Ports")]
    public void RefreshRegisteredPorts()
    {
        registeredPorts.Clear();
        SpaceOpeningPort[] ports = FindObjectsOfType<SpaceOpeningPort>(true);
        Array.Sort(ports, ComparePorts);

        for (int i = 0; i < ports.Length; i++)
        {
            SpaceOpeningPort port = ports[i];
            if (port == null || !port.gameObject.scene.IsValid())
            {
                continue;
            }

            registeredPorts.Add(port);
        }
    }

    public bool TryFindBestMatch(
        SpaceOpeningPort sourcePort,
        bool autoAlignBlockB,
        out SpaceOpeningPort match,
        out string message)
    {
        match = null;
        message = string.Empty;

        if (sourcePort == null)
        {
            message = "Assign Port A first.";
            return false;
        }

        RefreshRegisteredPorts();

        float bestDistance = float.MaxValue;
        bool requireFacing = !autoAlignBlockB;

        for (int i = 0; i < registeredPorts.Count; i++)
        {
            SpaceOpeningPort candidate = registeredPorts[i];
            if (candidate == null || candidate == sourcePort)
            {
                continue;
            }

            if (!sourcePort.CanConnectTo(candidate, requireFacing, out _))
            {
                continue;
            }

            float distance = Vector3.Distance(
                sourcePort.EffectiveAnchor.position,
                candidate.EffectiveAnchor.position);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                match = candidate;
            }
        }

        if (match == null)
        {
            message = "No matching available doorway ports were found.";
            return false;
        }

        message = $"Matched {match.name}.";
        return true;
    }

    public bool ValidatePorts(
        SpaceOpeningPort portA,
        SpaceOpeningPort portB,
        bool autoAlignBlockB,
        bool allowOverlapAnyway,
        out string message)
    {
        message = string.Empty;

        if (!TryBuildCandidatePose(portA, portB, autoAlignBlockB, out CandidatePose pose, out message))
        {
            return false;
        }

        if (TryGetOverlapWarning(portA, pose, out string overlapWarning))
        {
            message = allowOverlapAnyway
                ? $"Ports are compatible, but {overlapWarning}"
                : overlapWarning;
            return allowOverlapAnyway;
        }

        message = "Ports are compatible.";
        return true;
    }

    public bool PreviewConnection(
        SpaceOpeningPort portA,
        SpaceOpeningPort portB,
        bool autoAlignBlockB,
        bool allowOverlapAnyway,
        out string message)
    {
        ClearPreview();

        if (!TryBuildCandidatePose(portA, portB, autoAlignBlockB, out CandidatePose pose, out message))
        {
            return false;
        }

        bool hasOverlapWarning = TryGetOverlapWarning(portA, pose, out string overlapWarning);
        if (hasOverlapWarning && !allowOverlapAnyway)
        {
            message = overlapWarning;
            return false;
        }

        previewConnection = BuildConnectionObject(portA, portB, pose, autoAlignBlockB, true);
        message = hasOverlapWarning
            ? $"Preview created. {overlapWarning}"
            : "Connection preview created.";
        return previewConnection != null;
    }

    public bool ConnectPorts(
        SpaceOpeningPort portA,
        SpaceOpeningPort portB,
        bool autoAlignBlockB,
        bool allowOverlapAnyway,
        out string message)
    {
        ClearPreview();

        if (!TryBuildCandidatePose(portA, portB, autoAlignBlockB, out CandidatePose pose, out message))
        {
            return false;
        }

        bool hasOverlapWarning = TryGetOverlapWarning(portA, pose, out string overlapWarning);
        if (hasOverlapWarning && !allowOverlapAnyway)
        {
            message = overlapWarning;
            return false;
        }

        if (autoAlignBlockB && pose.blockB != null)
        {
            ApplyTransformWithUndo(
                pose.blockB.transform,
                pose.blockBPosition,
                pose.blockBRotation,
                "Auto Align Space Block");
            pose.blockB.GetOrCreateBlockBoundsCollider();
        }

        SpaceConnection connection = BuildConnectionObject(portA, portB, pose, autoAlignBlockB, false);
        if (connection == null)
        {
            message = "Failed to generate the connector module.";
            return false;
        }

        if (autoAlignBlockB && pose.blockB != null && portA != null && portA.OwningBlock != null)
        {
            connection.CaptureMergedBlockState(pose.blockB);
            MergeConnectedContentIntoBlockA(portA.OwningBlock, pose.blockB, connection);
        }

        portA.SetOccupied(portB);
        portB.SetOccupied(portA);

        message = hasOverlapWarning
            ? $"Doorways connected. {overlapWarning}"
            : "Doorways connected.";
        return true;
    }

    public bool ClearConnection(SpaceOpeningPort portA, SpaceOpeningPort portB, out string message)
    {
        bool clearedPreview = ClearPreview();
        SpaceConnection existingConnection = FindConnection(portA, portB);
        if (existingConnection == null)
        {
            message = clearedPreview
                ? "Cleared the connection preview."
                : "No matching connection was found.";
            return clearedPreview;
        }

        if (existingConnection.portA != null)
        {
            existingConnection.portA.ClearOccupied();
        }

        if (existingConnection.portB != null)
        {
            existingConnection.portB.ClearOccupied();
        }

        existingConnection.RestoreMergedBlockParent();
        DestroySceneObject(existingConnection.gameObject);
        message = "Cleared the selected connection.";
        return true;
    }

    public bool ClearPreview()
    {
        if (previewConnection != null)
        {
            DestroySceneObject(previewConnection.gameObject);
            previewConnection = null;
            return true;
        }

        if (spaceConnectionsRoot == null)
        {
            return false;
        }

        Transform previewRoot = spaceConnectionsRoot.Find(PreviewConnectionName);
        if (previewRoot == null)
        {
            return false;
        }

        DestroySceneObject(previewRoot.gameObject);
        return true;
    }

    private bool TryBuildCandidatePose(
        SpaceOpeningPort portA,
        SpaceOpeningPort portB,
        bool autoAlignBlockB,
        out CandidatePose pose,
        out string message)
    {
        pose = default;
        message = string.Empty;

        if (portA == null || portB == null)
        {
            message = "Assign both Port A and Port B.";
            return false;
        }

        if (!portA.CanConnectTo(portB, requireFacing: !autoAlignBlockB, out message))
        {
            return false;
        }

        pose.blockB = portB.OwningBlock;
        pose.anchorAPosition = portA.EffectiveAnchor.position;
        pose.connectorWidth = Mathf.Max(1f, portA.widthUnits);
        pose.connectorHeight = Mathf.Max(1f, portA.height);

        if (autoAlignBlockB)
        {
            if (!TryComputeAutoAlignedPose(portA, portB, out Vector3 alignedBlockPosition, out Quaternion alignedBlockRotation, out Vector3 alignedAnchorBPosition))
            {
                message = "Unable to auto-align Block B from the selected ports.";
                return false;
            }

            pose.blockBPosition = alignedBlockPosition;
            pose.blockBRotation = alignedBlockRotation;
            pose.anchorBPosition = alignedAnchorBPosition;
        }
        else
        {
            pose.blockBPosition = pose.blockB != null ? pose.blockB.transform.position : Vector3.zero;
            pose.blockBRotation = pose.blockB != null ? pose.blockB.transform.rotation : Quaternion.identity;
            pose.anchorBPosition = portB.EffectiveAnchor.position;

            if (!portA.FacesPort(portB))
            {
                message = "Doorways must face each other.";
                return false;
            }
        }

        pose.connectorLength = Vector3.Distance(pose.anchorAPosition, pose.anchorBPosition);
        if (pose.connectorLength < 0.05f)
        {
            message = "Connector anchors are too close together. Move the blocks apart before connecting.";
            return false;
        }

        return true;
    }

    private bool TryComputeAutoAlignedPose(
        SpaceOpeningPort portA,
        SpaceOpeningPort portB,
        out Vector3 alignedBlockPosition,
        out Quaternion alignedBlockRotation,
        out Vector3 alignedAnchorBPosition)
    {
        alignedBlockPosition = Vector3.zero;
        alignedBlockRotation = Quaternion.identity;
        alignedAnchorBPosition = Vector3.zero;

        if (portA == null || portB == null || portB.OwningBlock == null)
        {
            return false;
        }

        MemorySpaceBlock blockB = portB.OwningBlock;
        Transform anchorA = portA.EffectiveAnchor;
        Transform anchorB = portB.EffectiveAnchor;

        Vector3 desiredForward = -anchorA.forward.normalized;
        Quaternion deltaRotation = Quaternion.FromToRotation(anchorB.forward.normalized, desiredForward);
        alignedBlockRotation = deltaRotation * blockB.transform.rotation;

        Vector3 desiredAnchorBPosition = anchorA.position - (anchorA.forward.normalized * DefaultSnapLength);
        Vector3 localAnchorOffset = blockB.transform.InverseTransformPoint(anchorB.position);
        alignedBlockPosition = desiredAnchorBPosition - (alignedBlockRotation * localAnchorOffset);
        alignedAnchorBPosition = alignedBlockPosition + (alignedBlockRotation * localAnchorOffset);
        return true;
    }

    private bool TryGetOverlapWarning(SpaceOpeningPort portA, CandidatePose pose, out string warning)
    {
        warning = string.Empty;
        if (pose.blockB == null)
        {
            return false;
        }

        BoxCollider movingBoundsCollider = pose.blockB.GetOrCreateBlockBoundsCollider();
        if (movingBoundsCollider == null)
        {
            return false;
        }

        Bounds movingBounds = MemorySpaceBlock.GetWorldBoundsFromLocalBox(
            movingBoundsCollider.center,
            movingBoundsCollider.size,
            pose.blockBPosition,
            pose.blockBRotation);

        MemorySpaceBlock[] blocks = FindObjectsOfType<MemorySpaceBlock>(true);
        List<string> overlappingBlocks = new List<string>();
        for (int i = 0; i < blocks.Length; i++)
        {
            MemorySpaceBlock block = blocks[i];
            if (block == null || block == pose.blockB || !block.gameObject.scene.IsValid())
            {
                continue;
            }

            if (!block.TryGetWorldBounds(out Bounds worldBounds))
            {
                continue;
            }

            if (movingBounds.Intersects(worldBounds))
            {
                overlappingBlocks.Add(block.name);
            }
        }

        if (overlappingBlocks.Count == 0)
        {
            return false;
        }

        warning = $"overlap was detected with {string.Join(", ", overlappingBlocks.ToArray())}.";
        return true;
    }

    private SpaceConnection FindConnection(SpaceOpeningPort portA, SpaceOpeningPort portB)
    {
        SpaceConnection[] connections = FindObjectsOfType<SpaceConnection>(true);
        for (int i = 0; i < connections.Length; i++)
        {
            SpaceConnection connection = connections[i];
            if (connection == null || connection.isPreview)
            {
                continue;
            }

            if (connection.Matches(portA, portB))
            {
                return connection;
            }
        }

        return null;
    }

    private SpaceConnection BuildConnectionObject(
        SpaceOpeningPort portA,
        SpaceOpeningPort portB,
        CandidatePose pose,
        bool autoAlignBlockB,
        bool isPreview)
    {
        EnsureConnectionRoots();

        string connectionName = isPreview
            ? PreviewConnectionName
            : $"SpaceConnection_{Guid.NewGuid():N}".Substring(0, 22);

        Vector3 direction = (pose.anchorBPosition - pose.anchorAPosition).normalized;
        Vector3 up = portA != null && portA.OwningBlock != null
            ? portA.OwningBlock.transform.up
            : Vector3.up;
        Vector3 baseOrigin = pose.anchorAPosition - (up * (pose.connectorHeight * 0.5f));

        GameObject rootObject = new GameObject(connectionName);
        RegisterCreatedObject(rootObject, isPreview ? "Preview Space Connection" : "Create Space Connection");
        SetParentWithUndo(
            rootObject.transform,
            spaceConnectionsRoot,
            isPreview ? "Preview Space Connection" : "Create Space Connection",
            false);
        rootObject.transform.SetPositionAndRotation(baseOrigin, Quaternion.LookRotation(direction, up));

        SpaceConnection connection = AddComponentWithUndo<SpaceConnection>(
            rootObject,
            isPreview ? "Preview Space Connection" : "Create Space Connection");
        connection.Bind(
            portA,
            portB,
            isPreview,
            autoAlignBlockB,
            pose.connectorWidth,
            pose.connectorHeight,
            pose.connectorLength);

        BuildConnectorModule(rootObject.transform, connection, isPreview);
        return connection;
    }

    private void BuildConnectorModule(Transform root, SpaceConnection connection, bool isPreview)
    {
        if (TryBuildSegmentConnectorModule(root, connection, isPreview))
        {
            return;
        }

        float width = Mathf.Max(1f, connection.connectorWidth);
        float height = Mathf.Max(1f, connection.connectorHeight);
        float length = Mathf.Max(0.05f, connection.connectorLength);
        float floorThickness = 0.08f;
        float thresholdDepth = Mathf.Min(0.16f, length * 0.25f);
        float thresholdHeight = 0.04f;
        float sideWallThickness = 0.08f;
        float beamThickness = 0.12f;

        CreatePrimitive(
            "ConnectorFloor",
            root,
            new Vector3(0f, floorThickness * 0.5f, length * 0.5f),
            new Vector3(width, floorThickness, length),
            isPreview);

        CreatePrimitive(
            "Threshold_A",
            root,
            new Vector3(0f, thresholdHeight * 0.5f, thresholdDepth * 0.5f),
            new Vector3(width, thresholdHeight, thresholdDepth),
            isPreview);

        CreatePrimitive(
            "Threshold_B",
            root,
            new Vector3(0f, thresholdHeight * 0.5f, length - (thresholdDepth * 0.5f)),
            new Vector3(width, thresholdHeight, thresholdDepth),
            isPreview);

        CreatePrimitive(
            "LeftReturnWall",
            root,
            new Vector3((-width * 0.5f) + (sideWallThickness * 0.5f), height * 0.5f, length * 0.5f),
            new Vector3(sideWallThickness, height, length),
            isPreview);

        CreatePrimitive(
            "RightReturnWall",
            root,
            new Vector3((width * 0.5f) - (sideWallThickness * 0.5f), height * 0.5f, length * 0.5f),
            new Vector3(sideWallThickness, height, length),
            isPreview);

        if (height > beamThickness)
        {
            CreatePrimitive(
                "TopBeam",
                root,
                new Vector3(0f, height - (beamThickness * 0.5f), length * 0.5f),
                new Vector3(width, beamThickness, length),
                isPreview);
        }
    }

    private bool TryBuildSegmentConnectorModule(Transform root, SpaceConnection connection, bool isPreview)
    {
        if (root == null || connection == null)
        {
            return false;
        }

        SpaceSegmentKit kit = connection.portA != null && connection.portA.OwningBlock != null
            ? connection.portA.OwningBlock.segmentKit
            : null;
        if (kit == null && connection.portB != null && connection.portB.OwningBlock != null)
        {
            kit = connection.portB.OwningBlock.segmentKit;
        }

        if (kit == null)
        {
            return false;
        }

        SpaceSegmentDefinition floorDefinition = FindSegmentDefinition(
            kit,
            WhiteFloorSegmentId,
            SegmentCategory.Floor,
            "white_001",
            new Vector2(1f, 2f),
            0f,
            SegmentVariant.Default);
        bool useWideFloorLayout = floorDefinition != null && floorDefinition.prefab != null;
        if (!useWideFloorLayout)
        {
            floorDefinition = FindSegmentDefinition(
                kit,
                WhiteFloorFallbackSegmentId,
                SegmentCategory.Floor,
                "white_001",
                new Vector2(1f, 1f),
                0f,
                SegmentVariant.Default);
        }
        SpaceSegmentDefinition wallDefinition = FindSegmentDefinition(
            kit,
            WhiteWallSegmentId,
            SegmentCategory.Wall,
            "white_001",
            new Vector2(1f, 1f),
            2.5f,
            SegmentVariant.Solid);

        if (floorDefinition == null || floorDefinition.prefab == null || wallDefinition == null || wallDefinition.prefab == null)
        {
            return false;
        }

        float wallCenterOffset = Mathf.Max(0.5f, connection.connectorWidth * 0.5f);
        int columnCount = Mathf.Max(1, Mathf.RoundToInt(connection.connectorWidth));

        if (useWideFloorLayout)
        {
            float floorCenterZ = connection.connectorLength * 0.5f;
            for (int column = 0; column < columnCount; column++)
            {
                float centerX = (-connection.connectorWidth * 0.5f) + 0.5f + column;
                string floorName;
                if (columnCount == 2)
                {
                    floorName = column == 0 ? "LeftFloor" : "RightFloor";
                }
                else
                {
                    floorName = $"Floor_{column + 1:00}";
                }

                CreateFloorSegmentInstance(
                    floorDefinition,
                    root,
                    floorName,
                    new Vector3(centerX, 0f, floorCenterZ),
                    isPreview);
            }
        }
        else
        {
            int segmentCount = Mathf.Max(2, Mathf.RoundToInt(Mathf.Max(DefaultSnapLength, connection.connectorLength)));
            float segmentLength = Mathf.Max(0.5f, connection.connectorLength / segmentCount);
            for (int row = 0; row < segmentCount; row++)
            {
                float centerZ = (row + 0.5f) * segmentLength;
                for (int column = 0; column < columnCount; column++)
                {
                    float centerX = (-connection.connectorWidth * 0.5f) + 0.5f + column;
                    CreateFloorSegmentInstance(
                        floorDefinition,
                        root,
                        $"Floor_{row + 1:00}_{column + 1:00}",
                        new Vector3(centerX, 0f, centerZ),
                        isPreview);
                }
            }
        }

        int wallSegmentCount = Mathf.Max(2, Mathf.RoundToInt(Mathf.Max(DefaultSnapLength, connection.connectorLength)));
        float wallSegmentLength = Mathf.Max(0.5f, connection.connectorLength / wallSegmentCount);
        for (int i = 0; i < wallSegmentCount; i++)
        {
            float centerZ = (i + 0.5f) * wallSegmentLength;

            CreateWallSegmentInstance(
                wallDefinition,
                root,
                $"LeftReturnWall_{i + 1:00}",
                new Vector3(-wallCenterOffset, 0f, centerZ),
                WallSide.West,
                isPreview);

            CreateWallSegmentInstance(
                wallDefinition,
                root,
                $"RightReturnWall_{i + 1:00}",
                new Vector3(wallCenterOffset, 0f, centerZ),
                WallSide.East,
                isPreview);
        }

        return true;
    }

    private static SpaceSegmentDefinition FindSegmentDefinition(
        SpaceSegmentKit kit,
        string segmentId,
        SegmentCategory category,
        string styleId,
        Vector2 sizeXZ,
        float height,
        SegmentVariant variant)
    {
        if (kit == null)
        {
            return null;
        }

        SpaceSegmentDefinition definition = kit.GetSegment(segmentId);
        if (definition != null)
        {
            return definition;
        }

        return kit.FindSegment(category, styleId, sizeXZ, height, variant);
    }

    private static void CreateFloorSegmentInstance(
        SpaceSegmentDefinition definition,
        Transform parent,
        string segmentName,
        Vector3 targetLocalCenter,
        bool isPreview)
    {
        if (definition == null || definition.prefab == null || parent == null)
        {
            return;
        }

        GameObject instance = Instantiate(definition.prefab, parent, false);
        RegisterCreatedObject(instance, "Create Connection Floor");
        SetParentWithUndo(instance.transform, parent, "Create Connection Floor", false);
        instance.name = segmentName;
        instance.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        AlignToLocalBounds(instance.transform, targetLocalCenter.x, 0f, targetLocalCenter.z);
        SetPreviewColliderState(instance, isPreview);
    }

    private static void CreateWallSegmentInstance(
        SpaceSegmentDefinition definition,
        Transform parent,
        string segmentName,
        Vector3 targetLocalCenter,
        WallSide side,
        bool isPreview)
    {
        if (definition == null || definition.prefab == null || parent == null)
        {
            return;
        }

        GameObject instance = Instantiate(definition.prefab, parent, false);
        RegisterCreatedObject(instance, "Create Connection Wall");
        SetParentWithUndo(instance.transform, parent, "Create Connection Wall", false);
        instance.name = segmentName;
        instance.transform.localRotation = GetConnectionWallRotation(side);
        AlignToLocalBounds(instance.transform, targetLocalCenter.x, 0f, targetLocalCenter.z);
        SetPreviewColliderState(instance, isPreview);
    }

    private static Quaternion GetConnectionWallRotation(WallSide side)
    {
        Quaternion sideRotation;
        switch (side)
        {
            case WallSide.East:
                sideRotation = Quaternion.Euler(0f, 90f, 0f);
                break;
            case WallSide.South:
                sideRotation = Quaternion.Euler(0f, 180f, 0f);
                break;
            case WallSide.West:
                sideRotation = Quaternion.Euler(0f, -90f, 0f);
                break;
            default:
                sideRotation = Quaternion.identity;
                break;
        }

        return sideRotation * Quaternion.Euler(90f, 90f, 0f);
    }

    private static void AlignToLocalBounds(Transform instanceTransform, float targetCenterX, float targetMinY, float targetCenterZ)
    {
        if (instanceTransform == null || instanceTransform.parent == null)
        {
            return;
        }

        instanceTransform.localPosition = Vector3.zero;
        if (!TryGetLocalBounds(instanceTransform, out Vector3 localCenter, out Vector3 localSize))
        {
            return;
        }

        float localMinY = localCenter.y - (localSize.y * 0.5f);
        instanceTransform.localPosition = new Vector3(
            targetCenterX - localCenter.x,
            targetMinY - localMinY,
            targetCenterZ - localCenter.z);
    }

    private static bool TryGetLocalBounds(Transform instanceTransform, out Vector3 localCenter, out Vector3 localSize)
    {
        localCenter = Vector3.zero;
        localSize = Vector3.zero;

        Renderer[] renderers = instanceTransform.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            return false;
        }

        Bounds combinedBounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
        {
            combinedBounds.Encapsulate(renderers[i].bounds);
        }

        Transform parent = instanceTransform.parent;
        localCenter = parent.InverseTransformPoint(combinedBounds.center);
        Vector3 localSizeVector = parent.InverseTransformVector(combinedBounds.size);
        localSize = new Vector3(
            Mathf.Abs(localSizeVector.x),
            Mathf.Abs(localSizeVector.y),
            Mathf.Abs(localSizeVector.z));
        return true;
    }

    private static void SetPreviewColliderState(GameObject root, bool isPreview)
    {
        if (root == null || !isPreview)
        {
            return;
        }

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            colliders[i].enabled = false;
        }
    }

    private static void MergeConnectedContentIntoBlockA(
        MemorySpaceBlock blockA,
        MemorySpaceBlock blockB,
        SpaceConnection connection)
    {
        if (blockA == null || blockB == null || blockA == blockB)
        {
            return;
        }

        Transform connectedBlocksRoot = GetOrCreateChildTransform(
            blockA.transform,
            ConnectedBlocksRootName,
            "Merge Connected Blocks");

        SetParentWithUndo(blockB.transform, connectedBlocksRoot, "Merge Connected Blocks", true);

        if (connection != null)
        {
            SetParentWithUndo(connection.transform, connectedBlocksRoot, "Merge Space Connection", true);
        }
    }

    private static void CreatePrimitive(
        string primitiveName,
        Transform parent,
        Vector3 localPosition,
        Vector3 localScale,
        bool isPreview)
    {
        GameObject primitive = GameObject.CreatePrimitive(PrimitiveType.Cube);
        RegisterCreatedObject(primitive, "Create Connection Primitive");
        primitive.name = primitiveName;
        SetParentWithUndo(primitive.transform, parent, "Create Connection Primitive", false);
        primitive.transform.localPosition = localPosition;
        primitive.transform.localRotation = Quaternion.identity;
        primitive.transform.localScale = localScale;

        Collider primitiveCollider = primitive.GetComponent<Collider>();
        if (primitiveCollider != null && isPreview)
        {
            primitiveCollider.enabled = false;
        }
    }

    private void EnsureConnectionRoots()
    {
        if (displayZoneRoot == null)
        {
            displayZoneRoot = FindDisplayZoneRoot();
        }

        if (displayZoneRoot == null)
        {
            GameObject displayZoneObject = new GameObject(DefaultDisplayZoneName);
            RegisterCreatedObject(displayZoneObject, "Create Display Zone");
            displayZoneRoot = displayZoneObject.transform;
        }

        if (spaceConnectionsRoot == null || spaceConnectionsRoot.parent != displayZoneRoot)
        {
            Transform existingConnectionsRoot = displayZoneRoot.Find(SpaceConnectionsRootName);
            if (existingConnectionsRoot == null)
            {
                existingConnectionsRoot = GetOrCreateChildTransform(
                    displayZoneRoot,
                    SpaceConnectionsRootName,
                    "Create Space Connections Root");
            }

            spaceConnectionsRoot = existingConnectionsRoot;
        }
    }

    private static Transform FindDisplayZoneRoot()
    {
        GameObject displayZone = GameObject.Find(DefaultDisplayZoneName);
        if (displayZone != null)
        {
            return displayZone.transform;
        }

        GameObject legacyDisplayZone = GameObject.Find(LegacyDisplayZoneName);
        return legacyDisplayZone != null ? legacyDisplayZone.transform : null;
    }

    private static int ComparePorts(SpaceOpeningPort a, SpaceOpeningPort b)
    {
        string aId = a != null ? a.openingId : string.Empty;
        string bId = b != null ? b.openingId : string.Empty;
        return string.Compare(aId, bId, StringComparison.OrdinalIgnoreCase);
    }

    private static void DestroySceneObject(GameObject target)
    {
        if (target == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(target);
        }
        else
        {
#if UNITY_EDITOR
            Undo.DestroyObjectImmediate(target);
#else
            DestroyImmediate(target);
#endif
        }
    }

    private static void ApplyTransformWithUndo(
        Transform target,
        Vector3 worldPosition,
        Quaternion worldRotation,
        string undoLabel)
    {
        if (target == null)
        {
            return;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Undo.RecordObject(target, undoLabel);
        }
#endif

        target.SetPositionAndRotation(worldPosition, worldRotation);
    }

    private static Transform GetOrCreateChildTransform(Transform parent, string childName, string undoLabel)
    {
        if (parent == null)
        {
            return null;
        }

        Transform existingChild = parent.Find(childName);
        if (existingChild != null)
        {
            existingChild.localPosition = Vector3.zero;
            existingChild.localRotation = Quaternion.identity;
            existingChild.localScale = Vector3.one;
            return existingChild;
        }

        GameObject childObject = new GameObject(childName);
        RegisterCreatedObject(childObject, undoLabel);
        SetParentWithUndo(childObject.transform, parent, undoLabel, false);
        childObject.transform.localPosition = Vector3.zero;
        childObject.transform.localRotation = Quaternion.identity;
        childObject.transform.localScale = Vector3.one;
        return childObject.transform;
    }

    private static void SetParentWithUndo(
        Transform child,
        Transform parent,
        string undoLabel,
        bool worldPositionStays)
    {
        if (child == null)
        {
            return;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Undo.SetTransformParent(child, parent, undoLabel);
            if (!worldPositionStays)
            {
                child.localPosition = Vector3.zero;
                child.localRotation = Quaternion.identity;
                child.localScale = Vector3.one;
            }

            return;
        }
#endif

        child.SetParent(parent, worldPositionStays);
    }

    private static T AddComponentWithUndo<T>(GameObject target, string undoLabel) where T : Component
    {
        if (target == null)
        {
            return null;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            return Undo.AddComponent<T>(target);
        }
#endif

        return target.AddComponent<T>();
    }

    private static void RegisterCreatedObject(GameObject target, string undoLabel)
    {
        if (target == null)
        {
            return;
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Undo.RegisterCreatedObjectUndo(target, undoLabel);
        }
#endif
    }
}
