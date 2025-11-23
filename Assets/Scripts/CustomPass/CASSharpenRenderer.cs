// File: Assets/Scripts/CustomPass/CASSharpenRenderer.cs
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.Experimental.Rendering;

[System.Serializable]
public class CASSharpenFullScreenPass : CustomPass
{
    [Header("Sharpen")]
    [Range(0f, 1.2f)] public float sharpness = 1.0f;
    [Range(0f, 1f)]   public float antiRinging = 0.3f;
    [Range(0f, 4f)]   public float overdrive = 2.0f;
    [Range(0f, 0.5f)] public float vibrance = 0.25f;
    [Range(0f, 2f)]   public float saturation = 1.5f;
    [Range(0f, 0.4f)] public float microContrast = 0.3f;

    [Header("Debug / Visibility")]
    // 0=Off,1=Split,2=Edges,3=Outline (ensure your shader supports these if used)
    [Range(0,3)] public int debugMode = 1;
    [Range(0f,1f)] public float split = 0.5f;
    [Min(0)] public int posterizeSteps = 0;

    [Header("Enable")]
    public bool enable = true;

    Material _mat;
    RTHandle _tmpColor;

    static readonly int _Sharpness       = Shader.PropertyToID("_Sharpness");
    static readonly int _AntiRinging     = Shader.PropertyToID("_AntiRinging");
    static readonly int _Vibrance        = Shader.PropertyToID("_Vibrance");
    static readonly int _Saturation      = Shader.PropertyToID("_Saturation");
    static readonly int _MicroContrast   = Shader.PropertyToID("_MicroContrast");
    static readonly int _Overdrive       = Shader.PropertyToID("_Overdrive");
    static readonly int _Split           = Shader.PropertyToID("_Split");
    static readonly int _DebugMode       = Shader.PropertyToID("_DebugMode");
    static readonly int _PosterizeSteps  = Shader.PropertyToID("_PosterizeSteps");
    static readonly int _SourceTex       = Shader.PropertyToID("_SourceTex");

    protected override void Setup(ScriptableRenderContext _, CommandBuffer __)
    {
        _mat = CoreUtils.CreateEngineMaterial("Hidden/Custom/CASSharpen");
        if (_mat == null)
            Debug.LogError("[CAS] Shader 'Hidden/Custom/CASSharpen' not found or failed to compile.");

        targetColorBuffer = TargetBuffer.Camera;
        targetDepthBuffer = TargetBuffer.None;
        name = "CAS Sharpen (Loud)";
    }

    protected override void Execute(CustomPassContext ctx)
    {
        if (!enable) return;
        if (_mat == null) return;

        var camColor = ctx.cameraColorBuffer;
        if (camColor == null) return; // injection point/frame settings may not have color

        // Pick camera color graphicsFormat (fallback to safe HDR)
        GraphicsFormat fmt = GraphicsFormat.R16G16B16A16_SFloat;
        if (camColor.rt != null && camColor.rt.graphicsFormat != GraphicsFormat.None)
            fmt = camColor.rt.graphicsFormat;

        // (Re)allocate temp color once; no rtHandleProperties needed
        if (_tmpColor == null || (_tmpColor.rt != null && _tmpColor.rt.graphicsFormat != fmt))
        {
            RTHandles.Release(_tmpColor);
            _tmpColor = RTHandles.Alloc(
                scaleFactor: Vector2.one,             // follows dynamic resolution automatically
                colorFormat: fmt,
                dimension: TextureXR.dimension,
                slices: TextureXR.slices,
                useDynamicScale: true,
                name: "CASSharpen_TempColor"
            );
        }

        // Copy camera -> temp (read from temp, write to camera)
        HDUtils.BlitCameraTexture(ctx.cmd, camColor, _tmpColor);

        // Push params
        _mat.SetFloat(_Sharpness,     sharpness);
        _mat.SetFloat(_AntiRinging,   antiRinging);
        _mat.SetFloat(_Vibrance,      vibrance);
        _mat.SetFloat(_Saturation,    saturation);
        _mat.SetFloat(_MicroContrast, microContrast);
        _mat.SetFloat(_Overdrive,     overdrive);
        _mat.SetFloat(_Split,         split);
        _mat.SetInt  (_DebugMode,     Mathf.Clamp(debugMode, 0, 3));
        _mat.SetInt  (_PosterizeSteps, Mathf.Max(0, posterizeSteps));
        _mat.SetTexture(_SourceTex,   _tmpColor);

        // Draw result back to camera color
        HDUtils.DrawFullScreen(ctx.cmd, _mat, camColor, shaderPassId: 0);
    }

    protected override void Cleanup()
    {
        CoreUtils.Destroy(_mat); _mat = null;
        RTHandles.Release(_tmpColor); _tmpColor = null;
    }
}
