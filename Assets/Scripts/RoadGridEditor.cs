using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

[CustomEditor(typeof(RoadGrid))]
public class RoadGridEditor : Editor
{
    private RoadGrid grid;
    private bool drawRoadMode = false;
    private bool drawHouseMode = false;
    private bool drawSidewalkMode = false;
    private HashSet<Vector2Int> processedCells = new();
    private bool? currentDrawState = null;

    private void OnEnable()
    {
        grid = (RoadGrid)target;
        SceneView.duringSceneGui += OnSceneGUI;
        grid?.LoadCells();
    }

    private void OnDisable()
    {
        SceneView.duringSceneGui -= OnSceneGUI;
    }

    public override void OnInspectorGUI()
    {
        GUILayout.Label("Houses", EditorStyles.boldLabel);
        GUI.backgroundColor = new Color(0.6f, 0.2f, 0.8f); // Purple
        drawHouseMode = GUILayout.Toggle(drawHouseMode, "Draw Houses", "Button");
        if (GUILayout.Button("Clear Houses"))
        {
            Undo.RecordObject(grid, "Clear Houses");
            grid.houseCells.Clear();
            grid.LoadCells();
            EditorUtility.SetDirty(grid);
        }

        GUILayout.Space(10);

        GUILayout.Label("Roads", EditorStyles.boldLabel);
        GUI.backgroundColor = Color.yellow;
        drawRoadMode = GUILayout.Toggle(drawRoadMode, "Draw Roads", "Button");
        if (GUILayout.Button("Clear Roads"))
        {
            Undo.RecordObject(grid, "Clear Roads");
            grid.roadCells.Clear();
            grid.LoadCells();
            EditorUtility.SetDirty(grid);
        }

        GUILayout.Space(10);

        GUILayout.Label("Sidewalks", EditorStyles.boldLabel);
        GUI.backgroundColor = Color.cyan;
        drawSidewalkMode = GUILayout.Toggle(drawSidewalkMode, "Draw Sidewalks", "Button");
        if (GUILayout.Button("Clear Sidewalks"))
        {
            Undo.RecordObject(grid, "Clear Sidewalks");
            grid.sidewalkCells.Clear();
            grid.LoadCells();
            EditorUtility.SetDirty(grid);
        }

        GUILayout.Space(10);

        GUI.backgroundColor = Color.red;
        if (GUILayout.Button("CLEAR ALL"))
        {
            Undo.RecordObject(grid, "Clear All Cells");
            grid.ClearAllCells();
            EditorUtility.SetDirty(grid);
        }

        GUI.backgroundColor = Color.white;
        DrawDefaultInspector();
    }


    private void OnSceneGUI(SceneView sceneView)
    {
        if (grid == null) return;

        Handles.zTest = UnityEngine.Rendering.CompareFunction.LessEqual;

        for (int x = 0; x < grid.width; x++)
        {
            for (int y = 0; y < grid.height; y++)
            {
                Vector3 pos = grid.transform.position + new Vector3(x * grid.cellSize, 0, y * grid.cellSize);
                Vector2Int cell = new(x, y);
                Color fill = grid.IsRoadCell(cell) ? Color.yellow :
                             grid.IsHouseCell(cell) ? new Color(0.6f, 0.2f, 0.8f) :
                             grid.IsSidewalkCell(cell) ? Color.cyan :
                             Color.gray;

                Handles.DrawSolidRectangleWithOutline(new Vector3[] {
                    pos,
                    pos + new Vector3(grid.cellSize, 0, 0),
                    pos + new Vector3(grid.cellSize, 0, grid.cellSize),
                    pos + new Vector3(0, 0, grid.cellSize),
                }, fill, Color.black);
            }
        }

        Event e = Event.current;
        if ((drawRoadMode || drawHouseMode || drawSidewalkMode) && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0)
        {
            Ray ray = HandleUtility.GUIPointToWorldRay(e.mousePosition);
            Plane plane = new Plane(Vector3.up, grid.transform.position);
            if (plane.Raycast(ray, out float enter))
            {
                Vector3 local = ray.GetPoint(enter) - grid.transform.position;
                int x = Mathf.FloorToInt(local.x / grid.cellSize);
                int y = Mathf.FloorToInt(local.z / grid.cellSize);
                Vector2Int cell = new(x, y);

                if (x >= 0 && x < grid.width && y >= 0 && y < grid.height && !processedCells.Contains(cell))
                {
                    if (drawRoadMode)
                    {
                        bool isRoad = grid.IsRoadCell(cell);
                        if (currentDrawState == null) currentDrawState = !isRoad;

                        if (currentDrawState == true && !isRoad)
                        {
                            if (grid.IsHouseCell(cell)) grid.ToggleHouseCell(cell);
                            if (grid.IsSidewalkCell(cell)) grid.ToggleSidewalkCell(cell);
                            grid.ToggleRoadCell(cell);
                        }
                        else if (currentDrawState == false && isRoad)
                        {
                            grid.ToggleRoadCell(cell);
                        }
                    }
                    else if (drawHouseMode)
                    {
                        bool isHouse = grid.IsHouseCell(cell);
                        if (currentDrawState == null) currentDrawState = !isHouse;

                        if (currentDrawState == true && !isHouse)
                        {
                            if (grid.IsRoadCell(cell)) grid.ToggleRoadCell(cell);
                            if (grid.IsSidewalkCell(cell)) grid.ToggleSidewalkCell(cell);
                            grid.ToggleHouseCell(cell);
                        }
                        else if (currentDrawState == false && isHouse)
                        {
                            grid.ToggleHouseCell(cell);
                        }
                    }
                    else if (drawSidewalkMode)
                    {
                        bool isSidewalk = grid.IsSidewalkCell(cell);
                        if (currentDrawState == null) currentDrawState = !isSidewalk;

                        // NEW: Clear it from road/house before setting sidewalk
                        if (currentDrawState == true && !isSidewalk)
                        {
                            if (grid.IsRoadCell(cell)) grid.ToggleRoadCell(cell);
                            if (grid.IsHouseCell(cell)) grid.ToggleHouseCell(cell);
                            grid.ToggleSidewalkCell(cell);
                        }
                        else if (currentDrawState == false && isSidewalk)
                        {
                            grid.ToggleSidewalkCell(cell);
                        }
                    }

                    processedCells.Add(cell);
                    Undo.RecordObject(grid, "Modify Grid Cell");
                    EditorUtility.SetDirty(grid);
                }

                e.Use();
            }
        }

        if (e.type == EventType.MouseUp)
        {
            processedCells.Clear();
            currentDrawState = null;
        }
    }
}
