// File: Assets/Scripts/MeshBatcher.cs
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class MeshBatcher : MonoBehaviour
{
    [Header("Batching")]
    [Tooltip("If true, batch all materials. If false, only batch materials listed below.")]
    public bool batchAllMaterials = true;

    [Tooltip("Only used when 'batchAllMaterials' is false.")]
    public List<Material> materialsToGroup = new List<Material>();

    [Tooltip("Split batches to stay under Unity's 16-bit index limit.")]
    [Min(1)] public int maxVerticesPerBatch = 65000;

    [Tooltip("Run batching on Start(). Otherwise call BatchMeshes() manually.")]
    public bool batchOnStart = false;

    // Runtime state
    private readonly List<GameObject> batchedObjects = new List<GameObject>();
    private readonly List<MeshRenderer> disabledRenderers = new List<MeshRenderer>();
    private bool hasBatchedOnce;

    void Start()
    {
        if (batchOnStart) BatchMeshes();
    }

    [ContextMenu("Batch Meshes")]
    public void BatchMeshes()
    {
        if (hasBatchedOnce && batchedObjects.Count > 0) return;

        var renderers = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
            .Where(r => r.enabled
                        && r.sharedMaterial != null
                        && r.GetComponent<MeshFilter>()?.sharedMesh != null
                        && !r.transform.IsChildOf(transform))
            .ToList();

        if (!batchAllMaterials && materialsToGroup.Count > 0)
            renderers = renderers.Where(r => materialsToGroup.Contains(r.sharedMaterial)).ToList();

        if (renderers.Count == 0) return;

        // Group by material
        var groups = renderers.GroupBy(r => r.sharedMaterial);

        int groupIndex = 0;
        foreach (var g in groups)
        {
            var items = g.Select(r => (mesh: r.GetComponent<MeshFilter>().sharedMesh,
                                       matrix: r.transform.localToWorldMatrix,
                                       src: r))
                         .ToList();

            // Split by vertex budget
            var current = new List<CombineInstance>();
            var currentSrc = new List<MeshRenderer>();
            int currentVerts = 0;
            int subIndex = 0;

            for (int i = 0; i < items.Count; i++)
            {
                int verts = items[i].mesh.vertexCount;

                if (currentVerts + verts > maxVerticesPerBatch && current.Count > 0)
                {
                    CreateBatchGO(g.Key, current, currentSrc, groupIndex, subIndex++);
                    current.Clear();
                    currentSrc.Clear();
                    currentVerts = 0;
                }

                current.Add(new CombineInstance { mesh = items[i].mesh, transform = items[i].matrix });
                currentSrc.Add(items[i].src);
                currentVerts += verts;
            }

            if (current.Count > 0)
                CreateBatchGO(g.Key, current, currentSrc, groupIndex, subIndex);

            groupIndex++;
        }

        // Disable originals after success
        foreach (var r in renderers)
        {
            if (r && r.enabled)
            {
                r.enabled = false;
                disabledRenderers.Add(r);
            }
        }

        hasBatchedOnce = true;
    }

    [ContextMenu("Restore Original Meshes")]
    public void RestoreOriginalMeshes()
    {
        foreach (var r in disabledRenderers) if (r) r.enabled = true;
        disabledRenderers.Clear();

        foreach (var go in batchedObjects) if (go) DestroyImmediate(go);
        batchedObjects.Clear();

        hasBatchedOnce = false;
    }

    // Helpers
    private void CreateBatchGO(Material mat, List<CombineInstance> combines, List<MeshRenderer> sources, int groupIndex, int subIndex)
    {
        var go = new GameObject($"Batched_{groupIndex}_{subIndex}");
        go.transform.SetParent(transform, false);
        go.layer = sources[0].gameObject.layer;

        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();

        var mesh = new Mesh { name = $"Combined_{groupIndex}_{subIndex}", indexFormat = UnityEngine.Rendering.IndexFormat.UInt16 };
        mesh.CombineMeshes(combines.ToArray(), mergeSubMeshes: true, useMatrices: true, hasLightmapData: false);
        mesh.RecalculateBounds();

        mf.sharedMesh = mesh;
        mr.sharedMaterial = mat;

        batchedObjects.Add(go);
    }
}
