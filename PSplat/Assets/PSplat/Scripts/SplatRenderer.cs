using UnityEngine;
using UnityEditor;

[ExecuteAlways]
public class SplatRenderer : MonoBehaviour
{
    public Material splatMaterial;
    public int splatCount = 100;
    public float spread = 5f;

    private Mesh quadMesh;
    private Matrix4x4[] matrices;

    void OnEnable()
    {
        InitSplats();
        SceneView.duringSceneGui += RenderInSceneView;
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= RenderInSceneView;
    }

    void OnValidate()
    {
        InitSplats();
    }

    void InitSplats()
    {
        quadMesh = CreateQuad();
        matrices = new Matrix4x4[splatCount];

        for (int i = 0; i < splatCount; i++)
        {
            Vector3 pos = new Vector3(
                Random.Range(-spread, spread),
                Random.Range(-spread, spread),
                Random.Range(-spread, spread)
            );
            matrices[i] = Matrix4x4.TRS(pos, Quaternion.identity, Vector3.one * 0.3f);
        }
    }

    void RenderInSceneView(SceneView sceneView)
    {
        if (splatMaterial == null || quadMesh == null || matrices == null) return;

        for (int i = 0; i < matrices.Length; i++)
        {
            Graphics.DrawMesh(quadMesh, matrices[i], splatMaterial, 0, sceneView.camera);
        }

        SceneView.RepaintAll(); // Keep drawing in Edit mode
    }

    Mesh CreateQuad()
    {
        Mesh mesh = new Mesh();
        mesh.vertices = new Vector3[]
        {
            new Vector3(-0.5f, -0.5f, 0),
            new Vector3( 0.5f, -0.5f, 0),
            new Vector3(-0.5f,  0.5f, 0),
            new Vector3( 0.5f,  0.5f, 0),
        };
        mesh.uv = new Vector2[]
        {
            new Vector2(0,0), new Vector2(1,0), new Vector2(0,1), new Vector2(1,1)
        };
        mesh.triangles = new int[] { 0, 2, 1, 2, 3, 1 };
        return mesh;
    }
}
