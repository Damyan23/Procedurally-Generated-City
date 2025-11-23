using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class TrafficLightSettings
{
    [Header("Traffic Light Configuration")]
    public string directionName;
    public Vector3 positionOffset;
    public Vector3 rotationEuler;
    
    [Header("Optional Settings")]
    public bool enabled = true;
    
    public TrafficLightSettings(string name, Vector3 position, Vector3 rotation)
    {
        directionName = name;
        positionOffset = position;
        rotationEuler = rotation;
        enabled = true;
    }
}

public class RoadBuilder : MonoBehaviour
{
    public RoadGrid roadGrid;

    // Prefabs for road shapes
    public GameObject straightRoadPrefab;
    public GameObject intersectionRoadPrefab; // Single prefab for all intersections
    public GameObject crosswalkRoadPrefab; // Road with crosswalk in the middle
    public List<GameObject> stopLightPrefabs;
    
    [Header("Sidewalk Prefabs")]
    public GameObject sidewalkPrefab; // Straight sidewalk prefab
    public GameObject sidewalkCornerPrefab; // Corner sidewalk prefab
    
    [Header("Traffic Light Settings")]
    public Vector3 globalStopLightOffset = Vector3.zero;
    [Range(0f, 1f)]
    public float trafficLightProbability = 0.3f; // 30% chance by default
    
    [Header("Crosswalk Settings")]
    [Range(0f, 1f)]
    public float crosswalkProbability = 0.6f; // 60% chance by default
    
    [Header("Customizable Traffic Light Positions & Rotations")]
    public List<TrafficLightSettings> trafficLightConfigs = new List<TrafficLightSettings>();

    private void Start()
    {
        InitializeDefaultTrafficLightConfigs();
        BuildRoads();
        BuildSidewalks();
    }

    private void InitializeDefaultTrafficLightConfigs()
    {
        // Only initialize if the list is empty
        if (trafficLightConfigs.Count == 0)
        {
            trafficLightConfigs.Add(new TrafficLightSettings("Top", new Vector3(-0.6f, 0.2f, 0.6f), new Vector3(0, 90, 0)));
            trafficLightConfigs.Add(new TrafficLightSettings("Bottom", new Vector3(-0.6f, 0.2f, -0.6f), new Vector3(0, 0, 0)));
            trafficLightConfigs.Add(new TrafficLightSettings("Left", new Vector3(0.6f, 0.2f, 0.6f), new Vector3(0, 180, 0)));
            trafficLightConfigs.Add(new TrafficLightSettings("Right", new Vector3(0.6f, 0.2f, -0.6f), new Vector3(0, -90, 0)));
        }
    }

    private void BuildRoads()
    {
        if (roadGrid == null)
        {
            Debug.LogWarning("Missing RoadGrid reference.");
            return;
        }

        List<RoadCellInfo> cellInfos = roadGrid.GetRoadCellConnections();

        foreach (RoadCellInfo info in cellInfos)
        {
            Vector2Int cell = info.cell;
            GameObject prefabToUse = null;

            Vector3 position = roadGrid.transform.position + new Vector3(
                (cell.x + 0.5f) * roadGrid.cellSize,
                0f,
                (cell.y + 0.5f) * roadGrid.cellSize
            );

            bool top = info.hasTopNeighbor;
            bool bottom = info.hasBottomNeighbor;
            bool left = info.hasLeftNeighbor;
            bool right = info.hasRightNeighbor;

            int connections = (top ? 1 : 0) + (bottom ? 1 : 0) + (left ? 1 : 0) + (right ? 1 : 0);
            Quaternion rotation = Quaternion.identity;

            // Check if this is a real intersection (T-junction or crossroad)
            bool isRealIntersection = false;
            
            if (connections >= 3) // T-junction (3) or crossroad (4)
            {
                isRealIntersection = true;
            }
            else if (connections == 2) // Could be intersection or straight road
            {
                // It's an intersection only if connections are NOT opposite each other
                // Opposite pairs: (top && bottom) or (left && right)
                bool isOppositeConnections = (top && bottom) || (left && right);
                isRealIntersection = !isOppositeConnections;
            }

            if (isRealIntersection)
            {
                prefabToUse = intersectionRoadPrefab;
                // No rotation needed for intersection prefab
            }
            else if (connections >= 1) // Straight road or single connection
            {
                prefabToUse = straightRoadPrefab;
                
                // Rotate based on connection direction
                if (top || bottom || (top && bottom))
                    rotation = Quaternion.identity; // vertical (Z)
                else if (left || right || (left && right))
                    rotation = Quaternion.Euler(0, 90, 0); // horizontal (X)
            }
            else // No connections - isolated road piece
            {
                prefabToUse = straightRoadPrefab;
                rotation = Quaternion.identity;
            }

            if (prefabToUse != null)
            {
                GameObject tile = Instantiate(prefabToUse, position, rotation, transform);
                tile.name = "Cell: " + cell.ToString();
                
                // Position road tile a full height lower
                Renderer tileRenderer = tile.GetComponent<Renderer>();
                if (tileRenderer == null) tileRenderer = tile.GetComponentInChildren<Renderer>();
                
                if (tileRenderer != null)
                {
                    float fullHeight = tileRenderer.bounds.size.y;
                    tile.transform.position += Vector3.down * fullHeight;
                }

                // Only add traffic lights to intersections with random chance
                if (prefabToUse == intersectionRoadPrefab)
                {
                    if (stopLightPrefabs != null && stopLightPrefabs.Count > 0)
                    {
                        // Randomly decide whether to place traffic lights at this intersection
                        if (Random.Range(0f, 1f) <= trafficLightProbability)
                        {
                            SpawnTrafficLights(info, position, tile.transform);
                        }
                    }
                    
                    // Randomly decide whether to place crosswalks at this intersection
                    if (Random.Range(0f, 1f) <= crosswalkProbability)
                    {
                        SpawnCrosswalkRoads(cell.x, cell.y);
                    }
                }
            }
        }
        
        // Replace straight road tiles with crosswalk tiles around intersections
        ReplaceTilesWithCrosswalks();
    }

    private void SpawnTrafficLights(RoadCellInfo roadInfo, Vector3 intersectionPosition, Transform parentTile)
    {
        // Use the customizable traffic light configurations
        foreach (TrafficLightSettings config in trafficLightConfigs)
        {
            if (!config.enabled) continue;

            bool shouldSpawn = false;
            
            // Check if we should spawn based on road connections and configuration name
            switch (config.directionName.ToLower())
            {
                case "top":
                    shouldSpawn = roadInfo.hasTopNeighbor;
                    break;
                case "bottom":
                    shouldSpawn = roadInfo.hasBottomNeighbor;
                    break;
                case "left":
                    shouldSpawn = roadInfo.hasLeftNeighbor;
                    break;
                case "right":
                    shouldSpawn = roadInfo.hasRightNeighbor;
                    break;
            }

            if (shouldSpawn)
            {
                Vector3 finalPosition = intersectionPosition + config.positionOffset + globalStopLightOffset;
                Quaternion finalRotation = Quaternion.Euler(config.rotationEuler);
                SpawnSingleTrafficLight(finalPosition, finalRotation, parentTile, config.directionName);
            }
        }
    }

    private void SpawnSingleTrafficLight(Vector3 position, Quaternion rotation, Transform parent, string side)
    {
        if (stopLightPrefabs.Count > 0)
        {
            GameObject selectedStopLight = stopLightPrefabs[Random.Range(0, stopLightPrefabs.Count)];
            if (selectedStopLight != null)
            {
                GameObject stoplight = Instantiate(selectedStopLight, position, rotation, parent);
                stoplight.name = $"Traffic Light - {side} Side";
            }
        }
    }
    
    private void SpawnCrosswalkRoads(int intersectionX, int intersectionZ)
    {
        if (crosswalkRoadPrefab == null) return;
        
        // Store crosswalk positions to process after all roads are built
        if (crosswalkPositions == null) crosswalkPositions = new List<CrosswalkData>();
        
        // Get all road cell connections to find neighbors
        List<RoadCellInfo> roadInfos = roadGrid.GetRoadCellConnections();
        
        // Check all 4 directions around the intersection
        Vector2Int[] directions = { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        
        foreach (Vector2Int dir in directions)
        {
            Vector2Int neighborPos = new Vector2Int(intersectionX + dir.x, intersectionZ + dir.y);
            
            // Check if this neighbor position has a road
            if (roadGrid.IsRoadCell(neighborPos))
            {
                // Find the road info for this neighbor
                RoadCellInfo neighborInfo = null;
                foreach (RoadCellInfo info in roadInfos)
                {
                    if (info.cell == neighborPos)
                    {
                        neighborInfo = info;
                        break;
                    }
                }
                
                if (neighborInfo != null)
                {
                    int neighborConnections = (neighborInfo.hasTopNeighbor ? 1 : 0) + (neighborInfo.hasBottomNeighbor ? 1 : 0) + 
                                            (neighborInfo.hasLeftNeighbor ? 1 : 0) + (neighborInfo.hasRightNeighbor ? 1 : 0);
                    
                    // Check if this is a straight road that should get a crosswalk
                    bool isOppositeConnections = (neighborInfo.hasTopNeighbor && neighborInfo.hasBottomNeighbor) || 
                                               (neighborInfo.hasLeftNeighbor && neighborInfo.hasRightNeighbor);
                    
                    if (neighborConnections == 2 && isOppositeConnections)
                    {
                        // Determine rotation based on neighbor's connections
                        Quaternion crosswalkRotation = Quaternion.Euler(0, 90, 0);
                        
                        // If neighbor has left/right connections, rotate 90 degrees (crosswalk goes front-back)
                        // If neighbor has front/back connections, no rotation (crosswalk goes left-right)
                        if (neighborInfo.hasLeftNeighbor && neighborInfo.hasRightNeighbor)
                        {
                            crosswalkRotation = Quaternion.identity;
                        }
                        
                        // Store this crosswalk for later replacement
                        crosswalkPositions.Add(new CrosswalkData(neighborPos, crosswalkRotation));
                    }
                }
            }
        }
    }
    
    private List<CrosswalkData> crosswalkPositions;
    
    [System.Serializable]
    private class CrosswalkData
    {
        public Vector2Int position;
        public Quaternion rotation;
        
        public CrosswalkData(Vector2Int pos, Quaternion rot)
        {
            position = pos;
            rotation = rot;
        }
    }
    
    private void ReplaceTilesWithCrosswalks()
    {
        if (crosswalkPositions == null || crosswalkRoadPrefab == null) return;
        
        foreach (CrosswalkData crosswalk in crosswalkPositions)
        {
            // Find and destroy existing road tile at this position
            string roadTileName = "Cell: " + crosswalk.position.ToString();
            Transform existingTile = transform.Find(roadTileName);
            if (existingTile != null)
            {
                DestroyImmediate(existingTile.gameObject);
            }
            
            // Calculate position (same as regular roads - full height below ground level)
            Vector3 position = roadGrid.transform.position + new Vector3(
                (crosswalk.position.x + 0.5f) * roadGrid.cellSize,
                0f,
                (crosswalk.position.y + 0.5f) * roadGrid.cellSize
            );
            
            GameObject crosswalkObj = Instantiate(crosswalkRoadPrefab, position, crosswalk.rotation, transform);
            crosswalkObj.name = "CrosswalkRoad_Cell: " + crosswalk.position.ToString();
            
            // Position crosswalk road tile a full height lower (same as regular roads)
            Renderer crosswalkRenderer = crosswalkObj.GetComponent<Renderer>();
            if (crosswalkRenderer == null) crosswalkRenderer = crosswalkObj.GetComponentInChildren<Renderer>();
            
            if (crosswalkRenderer != null)
            {
                float fullHeight = crosswalkRenderer.bounds.size.y;
                crosswalkObj.transform.position += Vector3.down * fullHeight;
            }
        }
        
        // Clear the list for next time
        crosswalkPositions.Clear();
    }
    
    private void BuildSidewalks()
    {
        if (roadGrid == null)
        {
            Debug.LogWarning("Missing RoadGrid reference for sidewalks.");
            return;
        }

        List<RoadCellInfo> sidewalkInfos = roadGrid.GetSidewalkCellConnections();

        foreach (RoadCellInfo info in sidewalkInfos)
        {
            Vector2Int cell = info.cell;

            Vector3 position = roadGrid.transform.position + new Vector3(
                (cell.x + 0.5f) * roadGrid.cellSize,
                0f,
                (cell.y + 0.5f) * roadGrid.cellSize
            );

            // Count connections and determine sidewalk type
            int connections = (info.hasTopNeighbor ? 1 : 0) + (info.hasBottomNeighbor ? 1 : 0) + 
                             (info.hasLeftNeighbor ? 1 : 0) + (info.hasRightNeighbor ? 1 : 0);
            
            GameObject prefabToUse = sidewalkPrefab;
            Quaternion rotation = Quaternion.identity;
            
            // Check if this is a corner (2 connections that are NOT opposite)
            if (connections == 2)
            {
                bool isOppositeConnections = (info.hasTopNeighbor && info.hasBottomNeighbor) || 
                                           (info.hasLeftNeighbor && info.hasRightNeighbor);
                
                if (!isOppositeConnections && sidewalkCornerPrefab != null)
                {
                    // This is a corner - use corner prefab
                    prefabToUse = sidewalkCornerPrefab;

                    if (info.hasTopNeighbor && info.hasRightNeighbor)
                        rotation = Quaternion.Euler(0, 180, 0); // Top-Right corner
                    else if (info.hasRightNeighbor && info.hasBottomNeighbor)
                        rotation = Quaternion.Euler(0, -90, 0); // Right-Bottom corner  
                    else if (info.hasBottomNeighbor && info.hasLeftNeighbor)
                        rotation = Quaternion.identity; // Bottom-Left corner (no rotation for -180° prefab)
                    else if (info.hasLeftNeighbor && info.hasTopNeighbor)
                        rotation = Quaternion.Euler(0, 270, 0); // Left-Top corner
                }
                else
                {
                    // Straight sidewalk with opposite connections
                    if (info.hasLeftNeighbor && info.hasRightNeighbor)
                        rotation = Quaternion.Euler(0, 90, 0); // Horizontal
                    // Vertical connections use default rotation (0 degrees)
                }
            }
            else if (connections == 1)
            {
                // Single connection - rotate straight piece to face the connection
                if (info.hasLeftNeighbor || info.hasRightNeighbor)
                    rotation = Quaternion.Euler(0, 90, 0); // Horizontal
                // Vertical connections use default rotation (0 degrees)
            }
            // For 0, 3, or 4 connections, use default straight piece at 0 rotation

            if (prefabToUse != null)
            {
                GameObject sidewalkTile = Instantiate(prefabToUse, position, rotation, transform);
                sidewalkTile.name = "Sidewalk_Cell: " + cell.ToString();
                
                // Position sidewalk tile half its height lower
                Renderer sidewalkRenderer = sidewalkTile.GetComponent<Renderer>();
                if (sidewalkRenderer == null) sidewalkRenderer = sidewalkTile.GetComponentInChildren<Renderer>();
                
                if (sidewalkRenderer != null)
                {
                    float halfHeight = sidewalkRenderer.bounds.size.y * 0.5f;
                    sidewalkTile.transform.position += Vector3.down * halfHeight;
                }
            }
        }
        
        Debug.Log($"Built {sidewalkInfos.Count} sidewalk tiles.");
    }
}