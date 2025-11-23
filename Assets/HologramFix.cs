using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class HologramGlitch : MonoBehaviour
{
    public float maxGlitch = 1.0f;
    public float minGlitch = 0.1f;
    public float glitchLength = 0.1f;
    public float timeBetweenGlitches = 0.5f;

    private Material _material;

    void Start()
    {
        _material = GetComponent<Renderer>().material;
        InvokeRepeating(nameof(StartGlitch), 0f, timeBetweenGlitches);
    }

    void StartGlitch()
    {
        if (_material == null) return;

        float glitchValue = Random.Range(minGlitch, maxGlitch);
        _material.SetFloat("_GlitchStrength", glitchValue);

        Invoke(nameof(ResetGlitch), glitchLength);
    }

    void ResetGlitch()
    {
        if (_material == null) return;
        _material.SetFloat("_GlitchStrength", 0f);
    }
}
