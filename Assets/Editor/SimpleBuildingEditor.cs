#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(CityGen.SimpleBuilding))]
public class SimpleBuildingEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space();
        if (GUILayout.Button("Regenerate"))
        {
            var b = (CityGen.SimpleBuilding)target;
            b.Regenerate();
        }
    }
}
#endif
