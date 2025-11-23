// File: Assets/Editor/MeshWelderEditor.cs
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MeshWelder))]
public class MeshWelderEditor : Editor
{
    private const string PreviewChildName = "__WeldPreview__";
    private string _lastReport = "";

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var welder = (MeshWelder)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Welding Tools", EditorStyles.boldLabel);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Analyze"))
                _lastReport = Analyze(welder);

            if (GUILayout.Button("Preview Weld"))
                PreviewWeld(welder);
        }

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Apply Weld (In Place)"))
            {
                welder.WeldThisMeshInPlace();
                RemovePreview(welder);
                _lastReport = Analyze(welder);
            }

            if (GUILayout.Button("Clear Preview"))
                RemovePreview(welder);
        }

        if (!string.IsNullOrEmpty(_lastReport))
            EditorGUILayout.HelpBox(_lastReport, MessageType.Info);

        EditorGUILayout.Space();
        EditorGUILayout.HelpBox(
            "Tip: You can put MeshWelder on a parent (city root). The tools will auto-target the first MeshFilter or SkinnedMeshRenderer found on this object or its children.",
            MessageType.None);
    }

    private string Analyze(MeshWelder welder)
    {
        var (src, v0, t0, s0, sim, v1, t1, s1) = welder.AnalyzeCurrentMesh();
        if (!src) return "No MeshFilter/SkinnedMeshRenderer with a sharedMesh found on this object or its children.";
        if (sim) DestroyImmediate(sim);
        return $"Original:  verts {v0:N0}, tris {t0:N0}, submeshes {s0}\n" +
               $"Simulated: verts {v1:N0}, tris {t1:N0}, submeshes {s1}\n" +
               $"Delta:     verts {(v1 - v0):+#,#;-#,#;0}, tris {(t1 - t0):+#,#;-#,#;0}";
    }

    private void PreviewWeld(MeshWelder welder)
    {
        if (!TryFindTarget(welder, out var baseTransform, out var srcMesh, out var srcMaterials))
        {
            EditorUtility.DisplayDialog("MeshWelder", "No MeshFilter/SkinnedMeshRenderer with a sharedMesh found on this object or its children.", "OK");
            return;
        }

        var welded = MeshWelder.WeldVertices(srcMesh,
                                             welder.weldDistance,
                                             welder.uvTolerance,
                                             welder.normalAngleTolerance,
                                             welder.keepSubmeshesSeparated,
                                             welder.recalcNormalsAfter,
                                             welder.recalcTangentsAfter);
        welded.name = srcMesh.name + "_WeldPreview";

        var existing = baseTransform.Find(PreviewChildName);
        GameObject go = existing ? existing.gameObject : new GameObject(PreviewChildName);
        if (!existing)
        {
            go.hideFlags = HideFlags.DontSave | HideFlags.NotEditable;
            go.transform.SetParent(baseTransform, false);
            go.AddComponent<MeshFilter>();
            go.AddComponent<MeshRenderer>();
        }

        var pmf = go.GetComponent<MeshFilter>();
        var pmr = go.GetComponent<MeshRenderer>();
        pmf.sharedMesh = welded;

        var mats = new Material[srcMaterials.Length];
        for (int i = 0; i < srcMaterials.Length; i++)
        {
            var m = srcMaterials[i];
            if (m && m.HasProperty("_Color"))
            {
                var inst = new Material(m);
                var c = inst.color;
                inst.color = new Color(c.r * 0.85f, c.g * 1.05f, c.b * 0.85f, c.a);
                inst.name = m.name + " (Preview)";
                mats[i] = inst;
            }
            else mats[i] = m;
        }
        pmr.sharedMaterials = mats;

        Selection.activeGameObject = go;
        SceneView.RepaintAll();
    }

    private void RemovePreview(MeshWelder welder)
    {
        if (!TryFindTarget(welder, out var baseTransform, out _, out _)) return;
        var t = baseTransform.Find(PreviewChildName);
        if (!t) return;

        var pmr = t.GetComponent<MeshRenderer>();
        if (pmr)
        {
            foreach (var m in pmr.sharedMaterials)
                if (m && m.name.EndsWith("(Preview)")) DestroyImmediate(m);
        }
        DestroyImmediate(t.gameObject);
        SceneView.RepaintAll();
    }

    private static bool TryFindTarget(MeshWelder welder, out Transform baseTransform, out Mesh mesh, out Material[] materials)
    {
        baseTransform = welder.transform;
        mesh = null;
        materials = System.Array.Empty<Material>();

        var mf = welder.GetComponent<MeshFilter>();
        if (!mf) mf = welder.GetComponentInChildren<MeshFilter>(true);

        if (mf && mf.sharedMesh && mf.GetComponent<MeshRenderer>())
        {
            baseTransform = mf.transform;
            mesh = mf.sharedMesh;
            materials = mf.GetComponent<MeshRenderer>().sharedMaterials;
            return true;
        }

        var smr = welder.GetComponent<SkinnedMeshRenderer>();
        if (!smr) smr = welder.GetComponentInChildren<SkinnedMeshRenderer>(true);

        if (smr && smr.sharedMesh)
        {
            baseTransform = smr.transform;
            mesh = smr.sharedMesh;
            materials = smr.sharedMaterials;
            return true;
        }

        return false;
    }
}
#endif
