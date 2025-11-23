// File: Assets/Scripts/MeshWelder.cs
using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MeshWelder : MonoBehaviour
{
    [Header("Welding Settings")]
    [Range(0.0001f, 0.2f)] public float weldDistance = 0.01f;
    [Range(0f, 0.05f)] public float uvTolerance = 0.0005f;
    [Range(0f, 180f)] public float normalAngleTolerance = 5f;
    public bool keepSubmeshesSeparated = true;
    public bool recalcNormalsAfter = false;
    public bool recalcTangentsAfter = false;

    [Header("Run Options")]
    public bool weldOnStart = false;

    [Header("Gizmos (debug)")]
    public bool drawBoundsGizmo = true;
    [Range(0, 2000)] public int drawVertexDots = 0;
    [Range(0.001f, 0.05f)] public float vertexDotSize = 0.01f;

    void Start()
    {
        if (weldOnStart) WeldThisMeshInPlace();
    }

    [ContextMenu("Weld This Mesh (in place)")]
    public void WeldThisMeshInPlace()
    {
        if (!TryGetTargetMesh(out var kind, out var mf, out var smr, out var src))
        {
            Debug.LogError("[MeshWelder] No MeshFilter/SkinnedMeshRenderer with a sharedMesh found on this object or its children.");
            return;
        }

        var dst = WeldVertices(src, weldDistance, uvTolerance, normalAngleTolerance,
                               keepSubmeshesSeparated, recalcNormalsAfter, recalcTangentsAfter);
                               
        dst.name = src.name + "_Welded";

        if (kind == TargetKind.MeshFilter)
            mf.sharedMesh = dst;
        else
            smr.sharedMesh = dst;

        Debug.Log($"[MeshWelder] Welded '{src.name}': {src.vertexCount} → {dst.vertexCount} verts, submeshes={dst.subMeshCount}");
    }

    [ContextMenu("Weld All Children (in place)")]
    public void WeldAllChildrenInPlace()
    {
        int changed = 0;

        foreach (var mf in GetComponentsInChildren<MeshFilter>(true))
        {
            if (!mf || !mf.sharedMesh) continue;
            var src = mf.sharedMesh;
            var dst = WeldVertices(src, weldDistance, uvTolerance, normalAngleTolerance,
                                   keepSubmeshesSeparated, recalcNormalsAfter, recalcTangentsAfter);
            dst.name = src.name + "_Welded";
            mf.sharedMesh = dst;
            changed++;
        }

        foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            if (!smr || !smr.sharedMesh) continue;
            var src = smr.sharedMesh;
            var dst = WeldVertices(src, weldDistance, uvTolerance, normalAngleTolerance,
                                   keepSubmeshesSeparated, recalcNormalsAfter, recalcTangentsAfter);
            dst.name = src.name + "_Welded";
            smr.sharedMesh = dst;
            changed++;
        }

        Debug.Log($"[MeshWelder] Welded {changed} meshes under '{name}'.");
    }

    /// Analyze current target (self or first child with a mesh) and a simulated weld.
    public (Mesh src, int srcVerts, int srcTris, int srcSubs,
            Mesh sim, int simVerts, int simTris, int simSubs) AnalyzeCurrentMesh()
    {
        if (!TryGetTargetMesh(out _, out var mf, out var smr, out var src))
            return (null, 0, 0, 0, null, 0, 0, 0);

        int srcVerts = src.vertexCount;
        int srcTris = 0;
        int sCount = Mathf.Max(1, src.subMeshCount);
        for (int s = 0; s < sCount; s++) srcTris += src.GetIndices(Mathf.Min(s, src.subMeshCount - 1)).Length / 3;

        var sim = WeldVertices(src, weldDistance, uvTolerance, normalAngleTolerance,
                               keepSubmeshesSeparated, recalcNormalsAfter, recalcTangentsAfter);

        int simVerts = sim.vertexCount;
        int simTris = 0;
        int simS = Mathf.Max(1, sim.subMeshCount);
        for (int s = 0; s < simS; s++) simTris += sim.GetIndices(Mathf.Min(s, sim.subMeshCount - 1)).Length / 3;

        return (src, srcVerts, srcTris, sCount, sim, simVerts, simTris, simS);
    }

    void OnDrawGizmosSelected()
    {
        if (!TryGetTargetMesh(out _, out var mf, out var smr, out var mesh)) return;
        var t = (mf ? mf.transform : smr.transform);

        if (drawBoundsGizmo)
        {
            Gizmos.color = Color.cyan;
            var old = Gizmos.matrix;
            Gizmos.matrix = t.localToWorldMatrix;
            Gizmos.DrawWireCube(mesh.bounds.center, mesh.bounds.size);
            Gizmos.matrix = old;
        }

        if (drawVertexDots > 0)
        {
            Gizmos.color = Color.yellow;
            var verts = mesh.vertices;
            int step = Mathf.Max(1, verts.Length / Mathf.Max(1, drawVertexDots));
            for (int i = 0; i < verts.Length; i += step)
            {
                var wp = t.TransformPoint(verts[i]);
                Gizmos.DrawSphere(wp, vertexDotSize);
            }
        }
    }

    private enum TargetKind { MeshFilter, Skinned }
    private bool TryGetTargetMesh(out TargetKind kind, out MeshFilter mf, out SkinnedMeshRenderer smr, out Mesh mesh)
    {
        mf = GetComponent<MeshFilter>();
        smr = null;
        mesh = mf && mf.sharedMesh ? mf.sharedMesh : null;
        if (mesh != null) { kind = TargetKind.MeshFilter; return true; }

        smr = GetComponent<SkinnedMeshRenderer>();
        mesh = smr && smr.sharedMesh ? smr.sharedMesh : null;
        if (mesh != null) { kind = TargetKind.Skinned; return true; }

        mf = GetComponentInChildren<MeshFilter>(true);
        mesh = mf && mf.sharedMesh ? mf.sharedMesh : null;
        if (mesh != null) { kind = TargetKind.MeshFilter; return true; }

        smr = GetComponentInChildren<SkinnedMeshRenderer>(true);
        mesh = smr && smr.sharedMesh ? smr.sharedMesh : null;
        if (mesh != null) { kind = TargetKind.Skinned; return true; }

        kind = default;
        return false;
    }

    // ================= Core welding (attribute-aware) =================
    public static Mesh WeldVertices(
        Mesh mesh,
        float positionEps = 0.01f,
        float uvEps = 0.0005f,
        float normalAngleEps = 5f,
        bool keepSubmeshSeparate = true,
        bool recalcNormals = false,
        bool recalcTangents = false)
    {
        if (!mesh) throw new ArgumentNullException(nameof(mesh));

        mesh.GetVertices(_tmpVerts);
        mesh.GetNormals(_tmpNormals);
        mesh.GetTangents(_tmpTangents);
        mesh.GetColors(_tmpColors);
        mesh.GetUVs(0, _tmpUV0);
        mesh.GetUVs(1, _tmpUV1);
        mesh.GetUVs(2, _tmpUV2);
        mesh.GetUVs(3, _tmpUV3);

        var hasNormals  = _tmpNormals.Count  == _tmpVerts.Count;
        var hasTangents = _tmpTangents.Count == _tmpVerts.Count;
        var hasColors   = _tmpColors.Count   == _tmpVerts.Count;
        var hasUV0 = _tmpUV0.Count == _tmpVerts.Count;
        var hasUV1 = _tmpUV1.Count == _tmpVerts.Count;
        var hasUV2 = _tmpUV2.Count == _tmpVerts.Count;
        var hasUV3 = _tmpUV3.Count == _tmpVerts.Count;

        int srcSubCount = mesh.subMeshCount > 0 ? mesh.subMeshCount : 1;
        _tmpSubTriangles.Clear();
        for (int s = 0; s < srcSubCount; s++)
        {
            _tmpSubTriangles.Add(new List<int>());
            var indices = mesh.GetIndices(Mathf.Min(s, mesh.subMeshCount - 1));
            _tmpSubTriangles[s].AddRange(indices);
        }

        _outVerts.Clear(); _outNormals.Clear(); _outTangents.Clear(); _outColors.Clear();
        _outUV0.Clear(); _outUV1.Clear(); _outUV2.Clear(); _outUV3.Clear();

        _outSubTriangles.Clear();
        for (int s = 0; s < srcSubCount; s++) _outSubTriangles.Add(new List<int>(_tmpSubTriangles[s].Count));

        _spatial.Clear();
        _mapOldToNew.Clear();

        float invCell = 1f / Mathf.Max(0.000001f, positionEps);
        float cosLimit = Mathf.Cos(normalAngleEps * Mathf.Deg2Rad);

        int vCount = _tmpVerts.Count;
        for (int oldIndex = 0; oldIndex < vCount; oldIndex++)
        {
            var p = _tmpVerts[oldIndex];
            var key = new Int3(
                Mathf.RoundToInt(p.x * invCell),
                Mathf.RoundToInt(p.y * invCell),
                Mathf.RoundToInt(p.z * invCell)
            );

            if (!_spatial.TryGetValue(key, out var bucket))
            {
                bucket = _bucketCache.Count > 0 ? _bucketCache.Pop() : new List<int>(8);
                bucket.Clear();
                _spatial.Add(key, bucket);
            }

            int newIndex = -1;
            for (int i = 0; i < bucket.Count; i++)
            {
                int cand = bucket[i];
                if (!PosClose(_outVerts[cand], p, positionEps)) continue;
                if (hasUV0 && !UVClose(_outUV0[cand], _tmpUV0[oldIndex], uvEps)) continue;
                if (hasUV1 && !UVClose(_outUV1[cand], _tmpUV1[oldIndex], uvEps)) continue;
                if (hasUV2 && !UVClose(_outUV2[cand], _tmpUV2[oldIndex], uvEps)) continue;
                if (hasUV3 && !UVClose(_outUV3[cand], _tmpUV3[oldIndex], uvEps)) continue;

                if (hasNormals)
                {
                    var nA = _outNormals[cand].normalized;
                    var nB = _tmpNormals[oldIndex].normalized;
                    if (Vector3.Dot(nA, nB) < cosLimit) continue;
                }

                if (hasTangents && !TangentClose(_outTangents[cand], _tmpTangents[oldIndex])) continue;
                if (hasColors   && !ColorClose(_outColors[cand], _tmpColors[oldIndex])) continue;

                newIndex = cand; break;
            }

            if (newIndex == -1)
            {
                newIndex = _outVerts.Count;
                _outVerts.Add(p);
                if (hasNormals)  _outNormals.Add(_tmpNormals[oldIndex]);
                if (hasTangents) _outTangents.Add(_tmpTangents[oldIndex]);
                if (hasColors)   _outColors.Add(_tmpColors[oldIndex]);
                if (hasUV0) _outUV0.Add(_tmpUV0[oldIndex]);
                if (hasUV1) _outUV1.Add(_tmpUV1[oldIndex]);
                if (hasUV2) _outUV2.Add(_tmpUV2[oldIndex]);
                if (hasUV3) _outUV3.Add(_tmpUV3[oldIndex]);
                bucket.Add(newIndex);
            }

            _mapOldToNew[oldIndex] = newIndex;
        }

        for (int s = 0; s < srcSubCount; s++)
        {
            var srcTris = _tmpSubTriangles[s];
            var dstTris = _outSubTriangles[s];
            dstTris.Clear();

            for (int i = 0; i < srcTris.Count; i += 3)
            {
                int a = _mapOldToNew[srcTris[i]];
                int b = _mapOldToNew[srcTris[i + 1]];
                int c = _mapOldToNew[srcTris[i + 2]];
                if (a == b || b == c || c == a) continue;
                dstTris.Add(a); dstTris.Add(b); dstTris.Add(c);
            }
        }

        var outMesh = new Mesh
        {
            indexFormat = (_outVerts.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32
                                                   : UnityEngine.Rendering.IndexFormat.UInt16)
        };
        outMesh.SetVertices(_outVerts);
        if (_outNormals.Count  == _outVerts.Count) outMesh.SetNormals(_outNormals);
        if (_outTangents.Count == _outVerts.Count) outMesh.SetTangents(_outTangents);
        if (_outColors.Count   == _outVerts.Count) outMesh.SetColors(_outColors);
        if (_outUV0.Count == _outVerts.Count) outMesh.SetUVs(0, _outUV0);
        if (_outUV1.Count == _outVerts.Count) outMesh.SetUVs(1, _outUV1);
        if (_outUV2.Count == _outVerts.Count) outMesh.SetUVs(2, _outUV2);
        if (_outUV3.Count == _outVerts.Count) outMesh.SetUVs(3, _outUV3);

        outMesh.subMeshCount = srcSubCount;
        for (int s = 0; s < srcSubCount; s++) outMesh.SetTriangles(_outSubTriangles[s], s, true);

        outMesh.RecalculateBounds();
        if (recalcNormals)  outMesh.RecalculateNormals();
        if (recalcTangents) { try { outMesh.RecalculateTangents(); } catch {} }

        ClearTemps();
        return outMesh;
    }

    // ===== helpers & shared temp buffers =====
    private static readonly List<Vector3> _tmpVerts   = new List<Vector3>(65536);
    private static readonly List<Vector3> _tmpNormals = new List<Vector3>(65536);
    private static readonly List<Vector4> _tmpTangents= new List<Vector4>(65536);
    private static readonly List<Color>   _tmpColors  = new List<Color>(65536);
    private static readonly List<Vector2> _tmpUV0     = new List<Vector2>(65536);
    private static readonly List<Vector2> _tmpUV1     = new List<Vector2>(65536);
    private static readonly List<Vector2> _tmpUV2     = new List<Vector2>(65536);
    private static readonly List<Vector2> _tmpUV3     = new List<Vector2>(65536);

    private static readonly List<Vector3> _outVerts   = new List<Vector3>(65536);
    private static readonly List<Vector3> _outNormals = new List<Vector3>(65536);
    private static readonly List<Vector4> _outTangents= new List<Vector4>(65536);
    private static readonly List<Color>   _outColors  = new List<Color>(65536);
    private static readonly List<Vector2> _outUV0     = new List<Vector2>(65536);
    private static readonly List<Vector2> _outUV1     = new List<Vector2>(65536);
    private static readonly List<Vector2> _outUV2     = new List<Vector2>(65536);
    private static readonly List<Vector2> _outUV3     = new List<Vector2>(65536);

    private static readonly List<List<int>> _tmpSubTriangles = new List<List<int>>(8);
    private static readonly List<List<int>> _outSubTriangles = new List<List<int>>(8);

    private static readonly Dictionary<int, int> _mapOldToNew = new Dictionary<int, int>(65536);
    private static readonly Dictionary<Int3, List<int>> _spatial = new Dictionary<Int3, List<int>>(65536);
    private static readonly Stack<List<int>> _bucketCache = new Stack<List<int>>();

    private static bool PosClose(in Vector3 a, in Vector3 b, float eps) => (a - b).sqrMagnitude <= eps * eps;
    private static bool UVClose(in Vector2 a, in Vector2 b, float eps) => Mathf.Abs(a.x - b.x) <= eps && Mathf.Abs(a.y - b.y) <= eps;
    private static bool TangentClose(in Vector4 a, in Vector4 b)
        => Mathf.Abs(a.x - b.x) <= 1e-4f && Mathf.Abs(a.y - b.y) <= 1e-4f && Mathf.Abs(a.z - b.z) <= 1e-4f && Mathf.Abs(a.w - b.w) <= 1e-4f;
    private static bool ColorClose(in Color a, in Color b)
        => Mathf.Abs(a.r - b.r) <= 1e-3f && Mathf.Abs(a.g - b.g) <= 1e-3f &&
           Mathf.Abs(a.b - b.b) <= 1e-3f && Mathf.Abs(a.a - b.a) <= 1e-3f;

    private static void ClearTemps()
    {
        _tmpVerts.Clear(); _tmpNormals.Clear(); _tmpTangents.Clear(); _tmpColors.Clear();
        _tmpUV0.Clear(); _tmpUV1.Clear(); _tmpUV2.Clear(); _tmpUV3.Clear();

        _outVerts.Clear(); _outNormals.Clear(); _outTangents.Clear(); _outColors.Clear();
        _outUV0.Clear(); _outUV1.Clear(); _outUV2.Clear(); _outUV3.Clear();

        foreach (var kv in _spatial)
        {
            kv.Value.Clear();
            _bucketCache.Push(kv.Value);
        }
        _spatial.Clear();
        _mapOldToNew.Clear();

        _tmpSubTriangles.Clear();
        _outSubTriangles.Clear();
    }

    private readonly struct Int3 : IEquatable<Int3>
    {
        public readonly int x, y, z;
        public Int3(int x, int y, int z) { this.x = x; this.y = y; this.z = z; }
        public bool Equals(Int3 other) => x == other.x && y == other.y && z == other.z;
        public override bool Equals(object obj) => obj is Int3 o && Equals(o);
        public override int GetHashCode() => (x * 73856093) ^ (y * 19349663) ^ (z * 83492791);
    }
}
