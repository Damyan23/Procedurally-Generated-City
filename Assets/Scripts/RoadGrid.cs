using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public class RoadGrid : MonoBehaviour
{
    public int width = 10;
    public int height = 10;
    public float cellSize = 1f;

    public List<SerializableVector2Int> roadCells = new();
    public List<SerializableVector2Int> houseCells = new();
    public List<SerializableVector2Int> sidewalkCells = new();

    private HashSet<Vector2Int> roadCellsSet = new();
    private HashSet<Vector2Int> houseCellsSet = new();
    private HashSet<Vector2Int> sidewalkCellsSet = new();

    private void Awake() => LoadCells();
    private void OnEnable() => LoadCells();

    public void LoadCells()
    {
        roadCellsSet.Clear();
        foreach (var cell in roadCells) roadCellsSet.Add(cell.ToVector2Int());

        houseCellsSet.Clear();
        foreach (var cell in houseCells) houseCellsSet.Add(cell.ToVector2Int());

        sidewalkCellsSet.Clear();
        foreach (var cell in sidewalkCells) sidewalkCellsSet.Add(cell.ToVector2Int());
    }

    public bool IsRoadCell(Vector2Int cell) => roadCellsSet.Contains(cell);
    public bool IsHouseCell(Vector2Int cell) => houseCellsSet.Contains(cell);
    public bool IsSidewalkCell(Vector2Int cell) => sidewalkCellsSet.Contains(cell);

    public void ToggleRoadCell(Vector2Int cell)
    {
        if (roadCellsSet.Contains(cell))
        {
            roadCellsSet.Remove(cell);
            RemoveFromSerializedList(roadCells, cell);
        }
        else
        {
            roadCellsSet.Add(cell);
            AddToSerializedList(roadCells, cell);
        }
    }

    public void ToggleHouseCell(Vector2Int cell)
    {
        if (houseCellsSet.Contains(cell))
        {
            houseCellsSet.Remove(cell);
            RemoveFromSerializedList(houseCells, cell);
        }
        else
        {
            houseCellsSet.Add(cell);
            AddToSerializedList(houseCells, cell);
        }
    }

    public void ToggleSidewalkCell(Vector2Int cell)
    {
        if (sidewalkCellsSet.Contains(cell))
        {
            sidewalkCellsSet.Remove(cell);
            RemoveFromSerializedList(sidewalkCells, cell);
        }
        else
        {
            sidewalkCellsSet.Add(cell);
            AddToSerializedList(sidewalkCells, cell);
        }
    }

    private void AddToSerializedList(List<SerializableVector2Int> list, Vector2Int cell)
    {
        foreach (var v in list)
            if (v.x == cell.x && v.y == cell.y) return;

        list.Add(new SerializableVector2Int(cell));
    }

    private void RemoveFromSerializedList(List<SerializableVector2Int> list, Vector2Int cell)
    {
        for (int i = list.Count - 1; i >= 0; i--)
            if (list[i].x == cell.x && list[i].y == cell.y)
            {
                list.RemoveAt(i);
                break;
            }
    }

    public void ClearRoadCells()
    {
        roadCellsSet.Clear();
        roadCells.Clear();
    }

    public void ClearHouseCells()
    {
        houseCellsSet.Clear();
        houseCells.Clear();
    }

    public void ClearSidewalkCells()
    {
        sidewalkCellsSet.Clear();
        sidewalkCells.Clear();
    }

    public void ClearAllCells()
    {
        ClearRoadCells();
        ClearHouseCells();
        ClearSidewalkCells();
    }

    [System.Serializable]
    public class SerializableVector2Int
    {
        public int x, y;
        public SerializableVector2Int(int x, int y) { this.x = x; this.y = y; }
        public SerializableVector2Int(Vector2Int v) { x = v.x; y = v.y; }
        public Vector2Int ToVector2Int() => new(x, y);
    }

    public List<RoadCellInfo> GetRoadCellConnections()
    {
        List<RoadCellInfo> result = new List<RoadCellInfo>();

        foreach (Vector2Int cell in roadCellsSet)
        {
            RoadCellInfo info = new RoadCellInfo(cell);
            Vector2Int top = new Vector2Int(cell.x + 1, cell.y);
            Vector2Int bottom = new Vector2Int(cell.x - 1, cell.y);
            Vector2Int left = new Vector2Int(cell.x, cell.y + 1);
            Vector2Int right = new Vector2Int(cell.x, cell.y - 1);

            info.hasTopNeighbor = roadCellsSet.Contains(top);
            info.hasBottomNeighbor = roadCellsSet.Contains(bottom);
            info.hasLeftNeighbor = roadCellsSet.Contains(left);
            info.hasRightNeighbor = roadCellsSet.Contains(right);

            result.Add(info);
        }

        return result;
    }
    
    public List<RoadCellInfo> GetSidewalkCellConnections()
    {
        List<RoadCellInfo> result = new List<RoadCellInfo>();

        foreach (Vector2Int cell in sidewalkCellsSet)
        {
            RoadCellInfo info = new RoadCellInfo(cell);
            Vector2Int top = new Vector2Int(cell.x + 1, cell.y);
            Vector2Int bottom = new Vector2Int(cell.x - 1, cell.y);
            Vector2Int left = new Vector2Int(cell.x, cell.y + 1);
            Vector2Int right = new Vector2Int(cell.x, cell.y - 1);

            info.hasTopNeighbor = sidewalkCellsSet.Contains(top);
            info.hasBottomNeighbor = sidewalkCellsSet.Contains(bottom);
            info.hasLeftNeighbor = sidewalkCellsSet.Contains(left);
            info.hasRightNeighbor = sidewalkCellsSet.Contains(right);

            result.Add(info);
        }

        return result;
    }
}
