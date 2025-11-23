// ============================================================================
// File: Assets/Scripts/Demo/CASSharpenDemoHotkeys.cs
// Optional: quick hotkeys to prove it works in Play Mode.
// ============================================================================
using UnityEngine;

public sealed class CASSharpenDemoHotkeys : MonoBehaviour
{
    public CASSharpenFullScreenPass pass;
    public KeyCode toggleMode = KeyCode.Tab;   // cycle debug mode
    public KeyCode toggleOverdrive = KeyCode.O;
    public KeyCode slideSplitLeft = KeyCode.LeftBracket;
    public KeyCode slideSplitRight = KeyCode.RightBracket;

    void Update()
    {
        if (pass == null) return;
        if (Input.GetKeyDown(toggleMode))
            pass.debugMode = (pass.debugMode + 1) % 4;

        if (Input.GetKeyDown(toggleOverdrive))
            pass.overdrive = (pass.overdrive < 2f) ? 2f : 1f;

        if (Input.GetKey(slideSplitLeft))
            pass.split = Mathf.Clamp01(pass.split - Time.unscaledDeltaTime * 0.4f);

        if (Input.GetKey(slideSplitRight))
            pass.split = Mathf.Clamp01(pass.split + Time.unscaledDeltaTime * 0.4f);
    }
}
