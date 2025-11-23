// File: Assets/Scripts/BuildingDeformer.cs
using UnityEngine;

[ExecuteAlways]
[DisallowMultipleComponent]
public class BuildingDeformer : MonoBehaviour
{
    [Header("Building Type")]
    public BuildingFloorType floorType = BuildingFloorType.MiddleFloor;

    [Header("Global Style")]
    [Range(0.1f, 2.5f)] public float styleIntensity = 1.0f;          // master scalar
    public bool subtleMode = true;                                    // tight caps
    [Range(0f, 1f)] public float randomness = 0.5f;                   // noise weight 0..1
    public int seedOffset = 0;                                        // per-building variation

    [Header("Macro Massing")]
    [Range(0f, 0.25f)] public float shearAmount = 0.04f;              // small asymmetry shear
    [Range(0f, 0.2f)]  public float stepDepth = 0.06f;                // band recession depth (% of half-extent)
    [Range(1, 6)]      public int   stepCount = 3;                    // # horizontal bands
    [Range(0f, 1f)]    public float stepChance = 0.5f;

    [Header("Surface Detail")]
    [Range(0f, 0.08f)] public float recessDepth = 0.025f;             // window/panel recess along normal
    [Range(0f, 0.08f)] public float panelExtrude = 0.02f;             // small normal extrude
    [Range(0f, 1f)]    public float detailDensity = 0.6f;

    [Header("Top Overhangs")]
    [Range(0f, 0.18f)] public float overhangMax = 0.08f;              // % of half-extent
    [Range(0f, 1f)]    public float overhangCurve = 0.75f;            // growth near top
    public bool allowMultiSides = true;
    [Range(0f, 1f)]    public float multiSideChance = 0.25f;

    [Header("Performance")]
    public bool recalcNormals = true;

    // --- Internal
    private Mesh _mesh;
    private MeshFilter _mf;
    private Vector3[] _baseVerts;
    private Vector3[] _workVerts;
    private Vector3[] _baseNormals;
    private Bounds _baseBounds;
    private int _seed;
    private System.Random _rng;
    // cached noise offsets
    private float _nx, _ny, _nz;

    // computed per-building options
    private bool _useSteps;
    private Vector4 _overhangMask; // +X,-X,+Z,-Z {0/1}

    public enum BuildingFloorType { GroundFloor, MiddleFloor, TopFloor, Penthouse }

    void OnEnable()  { EnsureMesh(); Apply(); }
    void Start()     { EnsureMesh(); Apply(); }
    void OnValidate(){ EnsureMesh(); Apply(); }

    void EnsureMesh()
    {
        if (!_mf) _mf = GetComponent<MeshFilter>() ?? GetComponentInChildren<MeshFilter>();
        if (!_mf || !_mf.sharedMesh) return;

        if (_mesh == null || _mf.sharedMesh == _mesh)
        {
            // Duplicate the shared mesh once to keep original asset clean
            var src = _mf.sharedMesh;
            _mesh = new Mesh { name = "Deformed_" + src.name, indexFormat = src.indexFormat };
            _mesh.vertices  = src.vertices;
            _mesh.triangles = src.triangles;
            _mesh.normals   = src.normals;
            _mesh.uv        = src.uv;
            if (src.colors != null && src.colors.Length > 0) _mesh.colors = src.colors;
            if (src.uv2    != null && src.uv2.Length > 0)    _mesh.uv2   = src.uv2;
            _mf.sharedMesh = _mesh;

            _baseVerts   = _mesh.vertices;
            _baseNormals = (_mesh.normals != null && _mesh.normals.Length == _baseVerts.Length)
                           ? _mesh.normals
                           : RecalcNormalsTemp(_mesh);
            _baseBounds  = _mesh.bounds;
            _workVerts   = new Vector3[_baseVerts.Length];
        }
    }

    void InitRandom()
    {
        // Stable, position-based seed + user offset
        var p = transform.position;
        _seed = seedOffset ^ Mathf.RoundToInt(p.x * 73856093f) ^ Mathf.RoundToInt(p.z * 19349663f) ^ Mathf.RoundToInt(p.y * 83492791f);
        _rng = new System.Random(_seed);
        _nx = (float)_rng.NextDouble() * 100f;
        _ny = (float)_rng.NextDouble() * 100f;
        _nz = (float)_rng.NextDouble() * 100f;

        _useSteps = _rng.NextDouble() < stepChance;

        // Decide overhang sides
        int sides = 1;
        if (allowMultiSides && _rng.NextDouble() < multiSideChance) sides = 2 + _rng.Next(0, 2); // 2-3
        _overhangMask = Vector4.zero;
        for (int i = 0; i < sides; i++)
        {
            int s = _rng.Next(0, 4); // 0:+X,1:-X,2:+Z,3:-Z
            _overhangMask[s] = 1f;
        }
    }

    public void Apply()
    {
        if (_mesh == null || _baseVerts == null) return;

        InitRandom();

        var size = _baseBounds.size;
        // Safety: avoid div by zero on flat meshes
        float hx = Mathf.Max(size.x * 0.5f, 0.0001f);
        float hy = Mathf.Max(size.y, 0.0001f);
        float hz = Mathf.Max(size.z * 0.5f, 0.0001f);

        float master = styleIntensity;
        if (subtleMode) master *= 0.6f;

        // Pre-scale feature caps by bounds
        float capStep    = stepDepth    * Mathf.Min(hx, hz) * master;
        float capRecess  = recessDepth  * master;
        float capExtrude = panelExtrude * master;
        float capOver    = overhangMax  * Mathf.Min(hx, hz) * master;
        float capShear   = shearAmount  * master;

        // Tighten in subtle
        if (subtleMode)
        {
            capStep    *= 0.7f;
            capRecess  *= 0.8f;
            capExtrude *= 0.8f;
            capOver    *= 0.65f;
            capShear   *= 0.6f;
        }

        var center = _baseBounds.center;

        for (int i = 0; i < _baseVerts.Length; i++)
        {
            Vector3 v = _baseVerts[i];
            Vector3 n = _baseNormals[i];

            // Normalize within bounds (local space)
            float h = Mathf.Clamp01((v.y - (_baseBounds.min.y)) / hy);          // 0..1 bottom→top
            Vector2 xz = new Vector2(v.x - center.x, v.z - center.z);
            float edge = Mathf.Clamp01(Mathf.Max(Mathf.Abs(xz.x)/hx, Mathf.Abs(xz.y)/hz)); // 0 center, 1 edge

            // 1) Tiny shear/asymmetry (height-scaled)
            {
                float s = capShear * (h * h);
                v.x += s * (xz.y / Mathf.Max(hz, 0.0001f));
                v.z += s * (xz.x / Mathf.Max(hx, 0.0001f)) * 0.5f;
            }

            // 2) Optional stepped banding (recess inward on some bands)
            if (_useSteps && stepCount > 0 && capStep > 0f)
            {
                float stepSize = 1f / stepCount;
                float band = Mathf.Floor(h / stepSize);
                bool recessed = ((int)band & 1) == 1;
                // soft transition within band
                float t = Mathf.InverseLerp(0.15f, 0.85f, (h % stepSize) / stepSize);
                float bandAmt = recessed ? capStep * t : capStep * 0.25f * (1f - t);

                // move slightly toward center (preserves silhouette)
                Vector3 towardCenter = new Vector3(-Mathf.Sign(xz.x), 0f, -Mathf.Sign(xz.y));
                v += towardCenter * bandAmt;
            }

            // 3) Surface detail (normal space, bounded)
            if (capRecess > 0f || capExtrude > 0f)
            {
                float k = detailDensity;
                float nn = Mathf.PerlinNoise(_nx + v.x * 4f, _ny + v.y * 6f) * 0.6f
                         + Mathf.PerlinNoise(_ny + v.z * 4f, _nz + v.y * 3f) * 0.4f;
                nn = Mathf.Clamp01(nn);
                // Prefer recess near edges, extrude slightly near mid
                float recess = (nn > (1f - k)) ? capRecess * (edge * 0.8f + 0.2f) : 0f;
                float extr   = (nn < k * 0.5f) ? capExtrude * (1f - edge) * 0.6f : 0f;

                float dir = extr - recess; // negative = recess
                v += n * dir;
            }

            // 4) Top overhangs (only for Top/Penthouse, height-weighted)
            if ((floorType == BuildingFloorType.TopFloor || floorType == BuildingFloorType.Penthouse) && capOver > 0f)
            {
                float th = Mathf.Pow(Mathf.Clamp01(h), Mathf.Lerp(1.5f, 3.0f, overhangCurve));
                // side masks based on position sign
                float mxP = (_overhangMask.x > 0f && xz.x > 0f) ? 1f : 0f;
                float mxN = (_overhangMask.y > 0f && xz.x < 0f) ? 1f : 0f;
                float mzP = (_overhangMask.z > 0f && xz.y > 0f) ? 1f : 0f;
                float mzN = (_overhangMask.w > 0f && xz.y < 0f) ? 1f : 0f;

                float jitter = Mathf.Lerp(0f, 0.4f, randomness);
                float jx = (Mathf.PerlinNoise(_nx + v.y * 10f, _ny) - 0.5f) * jitter;
                float jz = (Mathf.PerlinNoise(_ny + v.y * 10f, _nz) - 0.5f) * jitter;

                float pushX = (mxP - mxN) * capOver * th * (1f + jx);
                float pushZ = (mzP - mzN) * capOver * th * (1f + jz);

                v.x += pushX;
                v.z += pushZ;

                // Clamp to silhouette + cap; never exceed 20% of half extent in any mode
                float capSide = subtleMode ? 0.12f : 0.2f;
                v.x = Mathf.Clamp(v.x, center.x - hx * (1f + capSide), center.x + hx * (1f + capSide));
                v.z = Mathf.Clamp(v.z, center.z - hz * (1f + capSide), center.z + hz * (1f + capSide));
            }

            _workVerts[i] = v;
        }

        _mesh.vertices = _workVerts;
        if (recalcNormals) _mesh.RecalculateNormals();
        _mesh.RecalculateBounds();
    }

    // Only used if source had no normals
    static Vector3[] RecalcNormalsTemp(Mesh m)
    {
        m.RecalculateNormals();
        return m.normals;
    }

    // Public API to change type at runtime
    public void SetFloorType(BuildingFloorType newType)
    {
        floorType = newType;
        Apply();
    }
}
