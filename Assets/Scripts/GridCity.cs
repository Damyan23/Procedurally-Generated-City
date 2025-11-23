// Version 2023 - Modified to work with RoadGrid System
//  (Updates: now uses RoadGrid to determine building placement locations)
using UnityEngine;

namespace CityGen {
	public class GridCity : MonoBehaviour {
		[Header("RoadGrid Integration")]
		public RoadGrid roadGrid; // Reference to the RoadGrid component
		
		[Header("Building Prefabs")]
		public GameObject[] buildingPrefabs;

		[Header("Generation Settings")]
		public float buildDelaySeconds = 0.1f;
		public bool useDelayBetweenBuildings = true; // Option to add delay between each building

		void Start() {
			// Ensure RoadGrid is loaded
			if (roadGrid != null) {
				roadGrid.LoadCells();
			}
			Generate();
		}

		void Update() {
			if (Input.GetKeyDown(KeyCode.G)) {
				DestroyChildren();
				Generate();
			}
		}

		void DestroyChildren() {
			// Destroy all existing buildings
			for (int i = 0; i < transform.childCount; i++) {
				Destroy(transform.GetChild(i).gameObject);
			}
		}

		void Generate() {
			if (roadGrid == null) {
				Debug.LogError("GridCity: No RoadGrid assigned! Please assign a RoadGrid in the inspector.");
				return;
			}

			if (buildingPrefabs == null || buildingPrefabs.Length == 0) {
				Debug.LogError("GridCity: No building prefabs assigned! Please assign building prefabs in the inspector.");
				return;
			}

			// Make sure the road grid cells are loaded
			roadGrid.LoadCells();

			// Generate buildings only on house cells (purple squares)
			foreach (var serializableCell in roadGrid.houseCells) {
				Vector2Int cell = serializableCell.ToVector2Int();
				
				// Calculate world position for this cell
				Vector3 position = roadGrid.transform.position + new Vector3(
					(cell.x + 0.5f) * roadGrid.cellSize,
					0,
					(cell.y + 0.5f) * roadGrid.cellSize
				);

				// Create a new building, chosen randomly from the prefabs
				int buildingIndex = Random.Range(0, buildingPrefabs.Length);
				GameObject newBuilding = Instantiate(buildingPrefabs[buildingIndex], transform);

				// Position the building at the calculated world position
				newBuilding.transform.position = position;
				
				// Name the building for easier identification in the hierarchy
				newBuilding.name = $"Building_Cell({cell.x},{cell.y})";

				// If the building has a Shape (grammar) component, launch the grammar
				Shape shape = newBuilding.GetComponent<Shape>();
				if (shape != null) {
					if (useDelayBetweenBuildings) {
						// Add a small random delay to make the building generation look more organic
						float delay = buildDelaySeconds + Random.Range(0f, buildDelaySeconds * 0.5f);
						shape.Generate(delay);
					} else {
						shape.Generate(buildDelaySeconds);
					}
				}
			}

			Debug.Log($"GridCity: Generated {roadGrid.houseCells.Count} buildings on house cells.");
		}

		// Method to regenerate buildings - useful for calling from other scripts
		public void RegenerateBuildings() {
			DestroyChildren();
			Generate();
		}

		// Method to get the number of buildings that will be generated
		public int GetBuildingCount() {
			if (roadGrid == null) return 0;
			return roadGrid.houseCells.Count;
		}

		// Utility method to check if a specific cell has a building
		public bool HasBuildingAtCell(Vector2Int cell) {
			if (roadGrid == null) return false;
			return roadGrid.IsHouseCell(cell);
		}

		// Method to generate a single building at a specific cell (useful for dynamic generation)
		public GameObject GenerateBuildingAtCell(Vector2Int cell) {
			if (roadGrid == null || !roadGrid.IsHouseCell(cell)) {
				Debug.LogWarning($"GridCity: Cannot generate building at cell {cell} - not a house cell or no RoadGrid assigned.");
				return null;
			}

			if (buildingPrefabs == null || buildingPrefabs.Length == 0) {
				Debug.LogError("GridCity: No building prefabs assigned!");
				return null;
			}

			// Calculate world position for this cell
			Vector3 position = roadGrid.transform.position + new Vector3(
				(cell.x + 0.5f) * roadGrid.cellSize,
				0,
				(cell.y + 0.5f) * roadGrid.cellSize
			);

			// Create a new building, chosen randomly from the prefabs
			int buildingIndex = Random.Range(0, buildingPrefabs.Length);
			GameObject currentBuildingPrefab = buildingPrefabs [buildingIndex];
			GameObject newBuilding = Instantiate(currentBuildingPrefab, transform);

			// Position the building at the calculated world position
			newBuilding.transform.position = position;
			newBuilding.name = $"Building_Cell({cell.x},{cell.y})";

			// If the building has a Shape (grammar) component, launch the grammar
			Shape shape = newBuilding.GetComponent<Shape>();
			if (shape != null) {
				shape.Generate(buildDelaySeconds);
			}

			return newBuilding;
		}
	}
}