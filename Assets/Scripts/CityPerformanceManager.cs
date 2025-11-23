// File: Assets/Scripts/CityPerformanceManager.cs
using UnityEngine;
using System.Collections;
using System.Linq;
using CityGen; // optional; safe if unused

public class CityPerformanceManager : MonoBehaviour
{
    [Header("Batching")]
    [Tooltip("If empty, all materials are eligible for batching. If set, only these are batched.")]
    public Material[] sharedMaterials;
    [Min(1)] public int maxVerticesPerBatch = 65000;
    public bool enableMeshBatching = true;

    [Header("Runtime")]
    public bool optimizeOnGeneration = true;
    [Tooltip("Delay before optimizing; lets generation finish. We also wait 2 extra frames.")]
    public float optimizationDelay = 2f;

    private MeshBatcher meshBatcher;

    void Start()
    {
        if (optimizeOnGeneration)
            StartCoroutine(OptimizeAfterGeneration());
    }

    private IEnumerator OptimizeAfterGeneration()
    {
        yield return new WaitForSeconds(optimizationDelay);
        yield return null; // allow late objects/materials to settle
        yield return null;

        if (!enableMeshBatching)
        {
            Debug.Log("[CityPerformance] Batching disabled.");
            yield break;
        }

        Debug.Log("[CityPerformance] Starting mesh batching...");
        BatchSimilarMeshes();
        Debug.Log("[CityPerformance] Mesh batching complete.");
    }

    private void BatchSimilarMeshes()
    {
        // Remove previous batches if any
        if (meshBatcher != null)
        {
            meshBatcher.RestoreOriginalMeshes();
            DestroyImmediate(meshBatcher.gameObject);
            meshBatcher = null;
        }

        var batchParent = new GameObject("BatchedMeshes");
        batchParent.transform.SetParent(transform, false);

        meshBatcher = batchParent.AddComponent<MeshBatcher>();
        meshBatcher.maxVerticesPerBatch = maxVerticesPerBatch;
        meshBatcher.batchOnStart = false;

        if (sharedMaterials != null && sharedMaterials.Length > 0)
        {
            meshBatcher.batchAllMaterials = false;
            meshBatcher.materialsToGroup = sharedMaterials.ToList();
        }
        else
        {
            meshBatcher.batchAllMaterials = true;
            meshBatcher.materialsToGroup.Clear();
        }

        meshBatcher.BatchMeshes();
    }

    [ContextMenu("Optimize City Now")]
    public void OptimizeCityNow()
    {
        StartCoroutine(OptimizeAfterGeneration());
    }

    [ContextMenu("Restore Original Meshes")]
    public void RestoreOriginalMeshes()
    {
        if (meshBatcher != null)
        {
            meshBatcher.RestoreOriginalMeshes();
            DestroyImmediate(meshBatcher.gameObject);
            meshBatcher = null;
        }

        Debug.Log("[CityPerformance] Restored original state (no batching).");
    }
}
