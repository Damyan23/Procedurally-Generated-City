using UnityEngine;

public class HouseBuilder : MonoBehaviour
{
    [SerializeField] private RoadGrid roadGrid;
    [SerializeField] private GameObject housePrefab;

    private void Start()
    {
        BuildHouses();
    }

    public void BuildHouses()
    {
        if (roadGrid == null || housePrefab == null)
        {
            Debug.LogWarning("Missing roadGrid or housePrefab.");
            return;
        }

        roadGrid.LoadCells();

        foreach (var serializableCell in roadGrid.houseCells)
        {
            Vector2Int cell = serializableCell.ToVector2Int();
            Vector3 position = roadGrid.transform.position + new Vector3
            (
                (cell.x + 0.5f) * roadGrid.cellSize,
                0,
                (cell.y + 0.5f) * roadGrid.cellSize
            );
            //Vector3 position = roadGrid.transform.position + new Vector3(cell.x * roadGrid.cellSize, 0, cell.y * roadGrid.cellSize);
            Instantiate(housePrefab, position, Quaternion.identity, transform);
        }
    }
}
