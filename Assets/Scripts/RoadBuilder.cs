using System.Collections.Generic;
using UnityEngine;

public class RoadBuilder : MonoBehaviour
{
    public RoadGrid roadGrid;

    // Prefabs for different road shapes (unrotated versions)
    public GameObject straightRoadPrefab;
    public GameObject cornerRoadPrefab;
    public GameObject tCrossRoadPrefab;
    public GameObject crossRoadPrefab;
    public List<GameObject> stopLightPrefabs;
    public Vector3 stopLightOffset = Vector3.zero;

    private void Start()
    {
        BuildRoads();
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

            if (connections == 4) // If the connections are four the current road peice is a crossroad so no nead to rotate
            {
                prefabToUse = crossRoadPrefab;
            }
            else if (connections == 3) // If the connections are 3 - a T raod peice so I need to rotate according to where there isnt a neighbour
            {
                prefabToUse = tCrossRoadPrefab;

                if (!top)
                    rotation = Quaternion.identity;
                else if (!right)
                    rotation = Quaternion.Euler(0, 90, 0);
                else if (!bottom)
                    rotation = Quaternion.Euler(0, 180, 0);
                else if (!left)
                    rotation = Quaternion.Euler(0, -90, 0);
            }
            // For a straight road I need to chekc if the previous neighbour is on top (if the raod is building down aka the x is decreasing) or bottom or if it is
            // on the left or right. Depending on that I need to check if there is no neighbour in the opsite direction aka if its on top or bottom I need to know
            // that there isnt one on left or right because then that means there should be a corner peice and not a stright road peice.
            else if (((top || bottom) && !left && !right) || ((left || right) && !top && !bottom))
            {
                prefabToUse = straightRoadPrefab;

                if (top || bottom)
                    rotation = Quaternion.identity; // vertical (Z)
                else
                    rotation = Quaternion.Euler(0, 90, 0); // horizontal (X)
            }
            else if ((top && right) || (right && bottom) || (bottom && left) || (left && top)) // If two connections then its a corner peice
            {
                prefabToUse = cornerRoadPrefab;

                if (bottom && right)
                    rotation = Quaternion.identity;
                else if (left && bottom)
                    rotation = Quaternion.Euler(0, 90, 0);
                else if (top && left)
                    rotation = Quaternion.Euler(0, 180, 0);
                else if (right && top)
                    rotation = Quaternion.Euler(0, 270, 0);
            }
            else
            {
                prefabToUse = straightRoadPrefab;
                rotation = Quaternion.identity;
            }

            if (prefabToUse != null)
            {
                GameObject tile = Instantiate(prefabToUse, position, rotation, transform);
                tile.name = "Cell: " + cell.ToString();

                if (prefabToUse == crossRoadPrefab || prefabToUse == tCrossRoadPrefab)
                {
                    if (stopLightPrefabs != null && stopLightPrefabs.Count > 0)
                    {
                        // Determine how many lights based on intersection type
                        int lightCount = prefabToUse == crossRoadPrefab ? 4 : 3;

                        for (int i = 0; i < lightCount; i++)
                        {
                            GameObject selectedStopLight = stopLightPrefabs[Random.Range(0, stopLightPrefabs.Count)];
                            if (selectedStopLight != null)
                            {
                                Vector3 offset = GetStoplightOffset(i, lightCount, roadGrid.cellSize * 0.5f); // Calculate different positions
                                GameObject stoplight = Instantiate(selectedStopLight, position + offset + stopLightOffset, rotation, tile.transform);
                                stoplight.name = "Stoplight " + (i + 1) + " for " + tile.name;
                            }
                        }
                    }
                }
            }
        }
    }

    private Vector3 GetStoplightOffset(int index, int totalLights, float halfCellSize)
    {
        switch (totalLights)
        {
            case 4: // Crossroad, 4 stoplights
                // Place on 4 corners
                switch (index)
                {
                    case 0: return new Vector3(-halfCellSize, 0, halfCellSize);  // Top Left
                    case 1: return new Vector3(halfCellSize, 0, halfCellSize);   // Top Right
                    case 2: return new Vector3(-halfCellSize, 0, -halfCellSize); // Bottom Left
                    case 3: return new Vector3(halfCellSize, 0, -halfCellSize);  // Bottom Right
                }
                break;
            case 3: // T-cross, 3 stoplights
                // Place on 3 sides (simple example: forward, left, right)
                switch (index)
                {
                    case 0: return new Vector3(0, 0, halfCellSize);             // Front
                    case 1: return new Vector3(-halfCellSize, 0, 0);            // Left
                    case 2: return new Vector3(halfCellSize, 0, 0);             // Right
                }
                break;
        }

        return Vector3.zero;
    }
}
