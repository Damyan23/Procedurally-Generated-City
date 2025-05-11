using UnityEngine;

public class RoadCellInfo
{
    public Vector2Int cell;
    public bool hasTopNeighbor;
    public bool hasBottomNeighbor;
    public bool hasLeftNeighbor;
    public bool hasRightNeighbor;

    public RoadCellInfo(Vector2Int cell)
    {
        this.cell = cell;
    }
}
