// ============================================================================
// File: Assets/Scripts/CustomPass/CASSharpen.cs
// Volume component (unchanged except namespace organization).
// ============================================================================
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

[System.Serializable]
[VolumeComponentMenu("Custom/CAS Sharpen")]
public sealed class CASSharpen : VolumeComponent, IPostProcessComponent
{
    public ClampedFloatParameter sharpness     = new ClampedFloatParameter(0.7f, 0f, 1.2f);
    public ClampedFloatParameter antiRinging   = new ClampedFloatParameter(0.2f, 0f, 1f);
    public ClampedFloatParameter vibrance      = new ClampedFloatParameter(0.08f, 0f, 0.5f);
    public ClampedFloatParameter saturation    = new ClampedFloatParameter(1.0f, 0f, 2f);
    public ClampedFloatParameter microContrast = new ClampedFloatParameter(0.0f, 0f, 0.4f);
    public BoolParameter enable                = new BoolParameter(true);

    public bool IsActive() =>
        enable.value &&
        (sharpness.value > 0.001f ||
         vibrance.value > 0.001f ||
         Mathf.Abs(saturation.value - 1f) > 0.001f ||
         microContrast.value > 0.001f);

    public bool IsTileCompatible() => false;
}
