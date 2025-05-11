using UnityEngine;

public class BuildingDeformer : MonoBehaviour
{
    private Mesh mesh;
    private Vector3[] originalVertices;
    private Vector3[] deformedVertices;

    public float indentDepth = 0.5f;
    public float outdentHeight = 0.5f;
    public int extraFloors = 5;
    public float floorHeight = 2f;

    void Start()
    {
        mesh = GetComponent<MeshFilter>().mesh;
        originalVertices = mesh.vertices;
        deformedVertices = new Vector3[originalVertices.Length];

        for (int i = 0; i < originalVertices.Length; i++)
        {
            Vector3 v = originalVertices[i];

            if (Mathf.Abs(v.x) > 0.4f) v.x *= 0.8f; // indent sides inward
            if (Mathf.Abs(v.z) > 0.4f) v.z *= 0.8f; // indent front/back inward

            if (v.y > 0.4f) v.y += outdentHeight; // pull top verts up

            deformedVertices[i] = v;
        }

        mesh.vertices = deformedVertices;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        transform.localScale += new Vector3(0, extraFloors * floorHeight, 0);
    }
}
