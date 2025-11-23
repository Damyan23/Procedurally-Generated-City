using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

public class CreateSimpleLODFromRenderer : EditorWindow
{
    [Header("Optional cheaper materials per LOD")]
    public Material lod0Material; // leave null to reuse existing
    public Material lod1Material; // e.g., simpler PBR (fewer maps)
    public Material lod2Material; // e.g., Unlit / very cheap

    [Header("Screen-relative heights")]
    [Range(0.01f, 1f)] public float lod0Height = 0.6f;
    [Range(0.0f, 1f)] public float lod1Height = 0.25f;
    [Range(0.0f, 1f)] public float lod2Height = 0.05f;

    [MenuItem("Tools/LOD/Create Simple LOD From Renderer")]
    public static void Open() => GetWindow<CreateSimpleLODFromRenderer>("Simple LOD Builder");

    private void OnGUI()
    {
        GUILayout.Label("Assign optional cheaper materials per LOD", EditorStyles.boldLabel);
        lod0Material = (Material)EditorGUILayout.ObjectField("LOD0 Material", lod0Material, typeof(Material), false);
        lod1Material = (Material)EditorGUILayout.ObjectField("LOD1 Material", lod1Material, typeof(Material), false);
        lod2Material = (Material)EditorGUILayout.ObjectField("LOD2 Material", lod2Material, typeof(Material), false);

        GUILayout.Space(8);
        GUILayout.Label("Screen Heights (bigger → stays longer in view)", EditorStyles.boldLabel);
        lod0Height = EditorGUILayout.Slider("LOD0 Height", lod0Height, 0.01f, 1f);
        lod1Height = EditorGUILayout.Slider("LOD1 Height", lod1Height, 0f, 1f);
        lod2Height = EditorGUILayout.Slider("LOD2 Height", lod2Height, 0f, 1f);

        GUILayout.Space(8);
        if (GUILayout.Button("Build LOD Group From Selection"))
        {
            BuildFromSelection(lod0Material, lod1Material, lod2Material, lod0Height, lod1Height, lod2Height);
        }
    }

    private static void BuildFromSelection(Material m0, Material m1, Material m2,
                                           float h0, float h1, float h2)
    {
        var go = Selection.activeGameObject;
        if (go == null)
        {
            EditorUtility.DisplayDialog("Simple LOD", "Select a GameObject with a MeshRenderer or SkinnedMeshRenderer.", "OK");
            return;
        }

        var mr = go.GetComponent<MeshRenderer>();
        var smr = go.GetComponent<SkinnedMeshRenderer>();
        Renderer srcRenderer = (Renderer)mr ?? smr as Renderer;
        if (srcRenderer == null)
        {
            EditorUtility.DisplayDialog("Simple LOD", "Selected object lacks a MeshRenderer/SkinnedMeshRenderer.", "OK");
            return;
        }

        // Parent for LOD children (use the selected GO as the root).
        Undo.RegisterFullObjectHierarchyUndo(go, "Create Simple LOD");

        // Clean up existing child LOD clones if re-running.
        foreach (Transform child in go.transform.Cast<Transform>().ToArray())
        {
            if (child.name.EndsWith("_LOD0") || child.name.EndsWith("_LOD1") || child.name.EndsWith("_LOD2"))
                Undo.DestroyObjectImmediate(child.gameObject);
        }

        // Ensure renderer is on a child so the root can hold the LODGroup cleanly.
        if (go.GetComponent<LODGroup>() != null && go.GetComponent<Renderer>() != null)
        {
            EditorUtility.DisplayDialog("Simple LOD",
                "Root has both LODGroup and Renderer. Move Renderer to a child or let this tool duplicate to children.",
                "OK");
        }

        // Create LOD children by duplicating the source renderer GameObject.
        var lod0 = CreateLodClone(srcRenderer.gameObject, "_LOD0", go.transform);
        var lod1 = CreateLodClone(srcRenderer.gameObject, "_LOD1", go.transform);
        var lod2 = CreateLodClone(srcRenderer.gameObject, "_LOD2", go.transform);

        // Apply cheaper settings for farther LODs.
        ApplyMaterialIfProvided(lod0, m0);
        ApplyMaterialIfProvided(lod1, m1);
        ApplyMaterialIfProvided(lod2, m2);

        // Turn off costly features progressively.
        SetShadows(lod1, cast: ShadowCastingMode.On, receive: false);
        SetShadows(lod2, cast: ShadowCastingMode.Off, receive: false);
        SetMotionVectors(lod2, false);

        // Build LODGroup.
        var group = go.GetComponent<LODGroup>() ?? Undo.AddComponent<LODGroup>(go);
        group.fadeMode = LODFadeMode.CrossFade;
        group.animateCrossFading = true;

        var r0 = lod0.GetComponentsInChildren<Renderer>(true);
        var r1 = lod1.GetComponentsInChildren<Renderer>(true);
        var r2 = lod2.GetComponentsInChildren<Renderer>(true);

        var lods = new[]
        {
            new LOD(Mathf.Clamp01(h0), r0),
            new LOD(Mathf.Clamp01(h1), r1),
            new LOD(Mathf.Clamp01(h2), r2)
        };

        group.SetLODs(lods);
        group.RecalculateBounds();

        // Disable the original renderer if it was on the root.
        if (srcRenderer.gameObject == go)
            srcRenderer.enabled = false;

        EditorUtility.DisplayDialog("Simple LOD", "LODGroup created with 3 levels using the same mesh (cheaper material/features on farther LODs).", "OK");
    }

    private static GameObject CreateLodClone(GameObject src, string suffix, Transform parent)
    {
        var clone = (GameObject)PrefabUtility.InstantiatePrefab(src) ?? Object.Instantiate(src);
        Undo.RegisterCreatedObjectUndo(clone, "Create LOD clone");
        clone.name = src.name + suffix;
        clone.transform.SetParent(parent, worldPositionStays: false);

        // Ensure only one active LOD at a time is managed by LODGroup (it enables/disables renderers).
        clone.SetActive(true);
        return clone;
    }

    private static void ApplyMaterialIfProvided(GameObject go, Material mat)
    {
        if (mat == null) return;
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            var shared = r.sharedMaterials;
            for (int i = 0; i < shared.Length; i++) shared[i] = mat;
            r.sharedMaterials = shared;
        }
    }

    private static void SetShadows(GameObject go, ShadowCastingMode cast, bool receive)
    {
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
            r.shadowCastingMode = cast;
            r.receiveShadows = receive;
        }
    }

    private static void SetMotionVectors(GameObject go, bool enabled)
    {
        foreach (var r in go.GetComponentsInChildren<Renderer>(true))
        {
#if UNITY_2021_2_OR_NEWER
            r.motionVectorGenerationMode = enabled ? MotionVectorGenerationMode.Object : MotionVectorGenerationMode.ForceNoMotion;
#else
            r.motionVectorGenerationMode = enabled ? MotionVectorGenerationMode.Object : MotionVectorGenerationMode.Camera;
            if (!enabled) r.motionVectorGenerationMode = MotionVectorGenerationMode.Camera; // closest fallback
#endif
        }
    }
}
