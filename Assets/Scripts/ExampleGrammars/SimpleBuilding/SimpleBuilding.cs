// File: Assets/Scripts/SimpleBuilding.cs
using UnityEngine;
using System.Collections.Generic;

namespace CityGen
{
    [System.Serializable]
    public class BlockCompatibility
    {
        [Header("This STOCK block")]
        public GameObject block;

        [Header("Which stocks may follow this block?")]
        public List<GameObject> allowedNextStocks = new List<GameObject>();
        [Tooltip("If empty, allow any next stock when not in strict mode.")]
        public bool allowAnyNextIfEmpty = true;
        
        [Header("Placement")]
        [Tooltip("If true, this block can be used for the ground floor (first stock).")]
        public bool allowAsGround = true;
    }

    public class SimpleBuilding : Shape
    {
        [Header("Compatibility")]
        [Tooltip("If true, ONLY explicitly whitelisted combinations are allowed.")]
        public bool strictCompatibility = false;
        [Tooltip("Declare your stock blocks here, and which stocks/roofs they allow.")]
        public List<BlockCompatibility> compatibility = new List<BlockCompatibility>();

        [Header("Building Settings")]
        public int buildingHeight = -1;      // <0 → random between min/max
        [Min(0.001f)] public float stockHeight = 1f;
        public int maxHeight = 5;
        public int minHeight = 1;
        
        [Header("Rooftop Objects")]
        [Tooltip("Objects that can be placed on building rooftops")]
        public List<GameObject> rooftopObjectPrefabs = new List<GameObject>();
        [Range(0f, 1f)]
        [Tooltip("Probability of spawning rooftop objects (0 = never, 1 = always)")]
        public float rooftopObjectProbability = 0.4f;
        [Range(1, 3)]
        [Tooltip("Maximum number of rooftop objects to spawn per building")]
        public int maxRooftopObjects = 2;
        [Tooltip("Minimum distance between rooftop objects")]
        public float minObjectSpacing = 2f;
        
        // Internal state
        private int stockNumber = 0;
        private GameObject lastStockPrefab = null;

        // Fast lookup + derived pools
        private Dictionary<GameObject, BlockCompatibility> compatLookup;
        private List<GameObject> stockPool;   // all unique stock blocks

        private List<GameObject> startPool;   // stocks with allowAsGround

        public void Initialize(
            int pBuildingHeight,
            float pStockHeight,
            int pStockNumber,
            GameObject pLastStockPrefab,
            List<BlockCompatibility> pCompatibility,
            bool pStrict
        )
        {
            buildingHeight = pBuildingHeight;
            stockHeight = pStockHeight;
            stockNumber = pStockNumber;
            lastStockPrefab = pLastStockPrefab;
            strictCompatibility = pStrict;

            compatibility = pCompatibility ?? compatibility;
            BuildCompatTables();
        }

        [ContextMenu("Regenerate")]
        public void Regenerate()
        {
            StopAllCoroutines(); // avoid double-generation

            // Destroy previous children
            var toDestroy = new List<GameObject>(transform.childCount);
            for (int i = 0; i < transform.childCount; i++)
                toDestroy.Add(transform.GetChild(i).gameObject);
#if UNITY_EDITOR
            if (!Application.isPlaying)
                foreach (var t in toDestroy) if (t) DestroyImmediate(t);
            else
#endif
                foreach (var i in toDestroy) if (i) Destroy(i);

            // Reset
            stockNumber = 0;
            lastStockPrefab = null;

            if (minHeight > maxHeight) { var t = minHeight; minHeight = maxHeight; maxHeight = t; }
            if (stockHeight <= 0f) stockHeight = 1f;

            BuildCompatTables();

            // Validate pools
            if (stockPool == null || stockPool.Count == 0)
            {
                Debug.LogError($"[{name}] No stock blocks found in Compatibility list.");
                return;
            }
            
            Generate(buildDelay);
        }

        // ---------------- Grammar ----------------
        protected override void Execute()
        {
            // Ensure compatibility tables are built
            if (stockPool == null || startPool == null)
            {
                BuildCompatTables();
            }
            
            if (buildingHeight < 0)
            {
                int lo = Mathf.Min(minHeight, maxHeight);
                int hi = Mathf.Max(minHeight, maxHeight);
                buildingHeight = Random.Range(lo, hi + 1);
            }

            if (stockNumber < buildingHeight)
            {
                // Choose a compatible stock block
                GameObject stockPrefab = null;
                
                if (stockNumber == 0)
                {
                    // First floor - must use ground-compatible block
                    stockPrefab = ChooseStartStock();
                    if (stockPrefab == null)
                    {
                        Debug.LogError($"[{name}] No valid ground floor block found! Check that at least one block has 'allowAsGround' enabled.");
                        return;
                    }
                }
                else
                {
                    // Upper floors - use compatible with previous
                    stockPrefab = ChooseCompatibleStock(lastStockPrefab);
                    if (stockPrefab == null)
                    {
                        Debug.LogError($"[{name}] No compatible STOCK prefab at floor {stockNumber} for previous stock '{(lastStockPrefab ? lastStockPrefab.name : "NULL")}' (strict={strictCompatibility}). Aborting.");
                        return;
                    }
                }

                // Apply Y offset for ground floor
                Vector3 spawnPosition = stockNumber == 0 ? new Vector3(0, -0.2f, 0) : Vector3.zero;
                var newStock = SpawnPrefab(stockPrefab, spawnPosition);
                if (newStock == null)
                {
                    Debug.LogError($"[{name}] Failed to spawn stock prefab '{stockPrefab.name}' at floor {stockNumber}.");
                    return;
                }
                
                SetFloorTypeForStock(newStock, stockNumber, buildingHeight);
                
                // Update the last stock reference
                lastStockPrefab = stockPrefab;

                // Continue one floor above
                var remaining = CreateSymbol<SimpleBuilding>("stock", new Vector3(0, stockHeight, -0.1f));
                remaining.Initialize(
                    buildingHeight,
                    stockHeight,
                    stockNumber + 1,
                    stockPrefab,
                    compatibility,
                    strictCompatibility
                );
                
                remaining.Generate(buildDelay);
            }
            else
            {
                // Building is complete - spawn rooftop objects on the last stock
                if (transform.childCount > 0)
                {
                    var lastStock = transform.GetChild(transform.childCount - 1).gameObject;
                    if (lastStock != null)
                    {
                        SpawnRooftopObjects(lastStock);
                    }
                }
                else
                {
                    Debug.LogWarning($"[{name}] Building complete but no child objects found for rooftop objects.");
                }
            }
        }
        
        // Override SpawnPrefab to preserve original prefab scales
        protected new GameObject SpawnPrefab(GameObject prefab, Vector3 localPosition = new Vector3(), Quaternion localRotation = new Quaternion(), Transform parent = null)
        {
            if (parent == null)
            {
                parent = transform; // default: add as child game object
            }
            GameObject copy = Instantiate(prefab, parent);
            copy.transform.localPosition = localPosition;
            copy.transform.localRotation = localRotation;
            // Don't modify scale - keep the original prefab scale
            AddGenerated(copy);
            return copy;
        }

        void SpawnRooftopObjects(GameObject roofObject)
        {
            if (rooftopObjectPrefabs == null || rooftopObjectPrefabs.Count == 0) return;
            if (roofObject == null) return;
            
            // Check if we should spawn rooftop objects
            if (Random.Range(0f, 1f) > rooftopObjectProbability) return;
            
            // Get roof bounds for positioning
            Renderer roofRenderer = roofObject.GetComponent<Renderer>();
            if (roofRenderer == null) roofRenderer = roofObject.GetComponentInChildren<Renderer>();
            if (roofRenderer == null) return;
            
            Bounds roofBounds = roofRenderer.bounds;
            Vector3 roofCenter = roofBounds.center;
            Vector3 roofSize = roofBounds.size;
            
            // Calculate roof surface (top of the roof)
            float roofTopY = roofBounds.max.y;
            
            // Randomly decide how many objects to spawn (0 to maxRooftopObjects)
            int objectCount = Random.Range(0, maxRooftopObjects + 1);
            
            List<Vector3> spawnedPositions = new List<Vector3>();
            
            for (int i = 0; i < objectCount; i++)
            {
                // Try to find a valid position (max 10 attempts per object)
                Vector3 spawnPosition = Vector3.zero;
                bool foundValidPosition = false;
                
                for (int attempt = 0; attempt < 10; attempt++)
                {
                    // Random position on roof surface (with some margin from edges)
                    float margin = 0.3f;
                    float randomX = Random.Range(roofCenter.x - (roofSize.x * 0.5f - margin), 
                                                roofCenter.x + (roofSize.x * 0.5f - margin));
                    float randomZ = Random.Range(roofCenter.z - (roofSize.z * 0.5f - margin), 
                                                roofCenter.z + (roofSize.z * 0.5f - margin));
                    
                    spawnPosition = new Vector3(randomX, roofTopY, randomZ);
                    
                    // Check distance from other spawned objects
                    bool tooClose = false;
                    foreach (Vector3 existingPos in spawnedPositions)
                    {
                        if (Vector3.Distance(spawnPosition, existingPos) < minObjectSpacing)
                        {
                            tooClose = true;
                            break;
                        }
                    }
                    
                    if (!tooClose)
                    {
                        foundValidPosition = true;
                        break;
                    }
                }
                
                if (foundValidPosition)
                {
                    // Choose random rooftop object prefab
                    GameObject selectedPrefab = rooftopObjectPrefabs[Random.Range(0, rooftopObjectPrefabs.Count)];
                    if (selectedPrefab != null)
                    {
                        // Spawn with random Y rotation
                        Quaternion randomRotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);
                        GameObject rooftopObj = SpawnPrefab(selectedPrefab);
                        rooftopObj.transform.position = spawnPosition;
                        rooftopObj.transform.rotation = randomRotation;
                        rooftopObj.name = $"RooftopObject_{i}_{selectedPrefab.name}";
                        
                        spawnedPositions.Add(spawnPosition);
                    }
                }
            }
        }

        void SetFloorTypeForStock(GameObject stockObject, int currentStockNumber, int totalHeight)
        {
            if (!stockObject) return;

            var deformer = stockObject.GetComponent<BuildingDeformer>() ?? stockObject.GetComponentInChildren<BuildingDeformer>();
            if (!deformer) return;

            BuildingDeformer.BuildingFloorType floorType;
            if (currentStockNumber == 0) floorType = BuildingDeformer.BuildingFloorType.GroundFloor;
            else if (currentStockNumber >= totalHeight - 1)
                floorType = (totalHeight <= 2) ? BuildingDeformer.BuildingFloorType.TopFloor
                                               : BuildingDeformer.BuildingFloorType.Penthouse;
            else if (currentStockNumber >= totalHeight - 2)
                floorType = BuildingDeformer.BuildingFloorType.TopFloor;
            else floorType = BuildingDeformer.BuildingFloorType.MiddleFloor;

            deformer.SetFloorType(floorType);
        }

        // ---------------- Compatibility core ----------------

        void BuildCompatTables()
        {
            // Lookup
            compatLookup ??= new Dictionary<GameObject, BlockCompatibility>();
            compatLookup.Clear();

            stockPool ??= new List<GameObject>();
            startPool ??= new List<GameObject>();
            stockPool.Clear();
            startPool.Clear();

            if (compatibility == null) return;

            var stockSet = new HashSet<GameObject>();

            foreach (var bc in compatibility)
            {
                if (bc == null || bc.block == null) continue;

                // Map block → rule
                if (!compatLookup.ContainsKey(bc.block))
                    compatLookup.Add(bc.block, bc);

                // Stock pools
                if (stockSet.Add(bc.block))
                {
                    stockPool.Add(bc.block);
                    if (bc.allowAsGround) startPool.Add(bc.block);
                }
            }

            // If no explicit start flags, default to all stocks as start
            if (startPool.Count == 0)
                startPool.AddRange(stockPool);
        }

        GameObject ChooseStartStock()
        {
            // Ensure pools are built if they're null
            if (startPool == null)
            {
                BuildCompatTables();
            }
            
            if (startPool == null || startPool.Count == 0) 
            {
                Debug.LogError($"[{name}] No start pool available! Make sure at least one block has 'allowAsGround' enabled.");
                return null;
            }
            
            int idx = Random.Range(0, startPool.Count);
            GameObject chosen = startPool[idx];
            
            if (chosen == null)
            {
                Debug.LogError($"[{name}] Chosen start stock at index {idx} is null!");
            }
            
            return chosen;
        }
        
        GameObject ChooseCompatibleStock(GameObject prevStock)
        {
            if (prevStock == null) return ChooseStartStock();

            if (compatLookup != null && compatLookup.TryGetValue(prevStock, out var rule))
            {
                var pool = FilterToKnownStocks(rule.allowedNextStocks);
                if (pool.Count > 0) return pool[Random.Range(0, pool.Count)];

                if (!strictCompatibility && rule.allowAnyNextIfEmpty && stockPool.Count > 0)
                    return stockPool[Random.Range(0, stockPool.Count)];

                return null; // strict + no allowed next
            }

            // No rule for prev
            if (!strictCompatibility && stockPool.Count > 0)
                return stockPool[Random.Range(0, stockPool.Count)];

            return null;
        }
        
        // Keep only entries present in stockPool to avoid stray assets
        List<GameObject> FilterToKnownStocks(List<GameObject> list)
        {
            var result = new List<GameObject>();
            if (list == null || stockPool == null) return result;
            var known = new HashSet<GameObject>(stockPool);
            foreach (var go in list) if (go && known.Contains(go)) result.Add(go);
            return result;
        }
    }
}
