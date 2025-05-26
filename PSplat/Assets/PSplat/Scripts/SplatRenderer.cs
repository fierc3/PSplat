using UnityEngine;
using UnityEditor;

[ExecuteAlways]
public class SplatRenderer : MonoBehaviour
{
    public Material splatMaterial;
    public Mesh quadMesh;
    public int splatCount = 1000000;
    public float spread = 10f;

    private ComputeBuffer splatBuffer;
    private ComputeBuffer argsBuffer;

    private int previousSplatCount = -1;


    void OnValidate()
    {
        if (splatCount != previousSplatCount)
        {
            Debug.Log($"[Editor] Splat count changed: {previousSplatCount} → {splatCount}");
            ReinitBuffers();
        }
    }

    void ReinitBuffers()
    {
        ReleaseBuffers();
        InitBuffers();
        previousSplatCount = splatCount;
    }


    void OnEnable()
    {
        InitBuffers();
        previousSplatCount = splatCount;
        SceneView.duringSceneGui += RenderScene;
    }

    void OnDisable()
    {
        SceneView.duringSceneGui -= RenderScene;
        ReleaseBuffers();
    }

    void InitBuffers()
    {
        if (quadMesh == null)
            quadMesh = Resources.GetBuiltinResource<Mesh>("Quad.fbx");

        // Fill splat data
        var splats = new SplatData[splatCount];
        for (int i = 0; i < splats.Length; i++)
        {
            splats[i].position = Random.insideUnitSphere * spread;
            splats[i].size = Random.Range(0.01f, 0.05f);
            splats[i].color = Color.Lerp(Color.yellow, Color.red, Random.value);
        }

        // Allocate compute buffer
        int stride = sizeof(float) * (3 + 1 + 4); // pos + size + color
        splatBuffer = new ComputeBuffer(splatCount, stride);
        splatBuffer.SetData(splats);
        splatMaterial.SetBuffer("_Splats", splatBuffer);

        // Setup indirect args buffer
        uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
        args[0] = (quadMesh != null) ? quadMesh.GetIndexCount(0) : 0;
        args[1] = (uint)splatCount;
        args[2] = (quadMesh != null) ? quadMesh.GetIndexStart(0) : 0;
        args[3] = (quadMesh != null) ? quadMesh.GetBaseVertex(0) : 0;
        args[4] = 0;
        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
        argsBuffer.SetData(args);
    }

    void ReleaseBuffers()
    {
        splatBuffer?.Release();
        argsBuffer?.Release();
    }

    void RenderScene(SceneView sceneView)
    {
        if (!splatMaterial || splatBuffer == null || argsBuffer == null) return;

        var bounds = new Bounds(Vector3.zero, Vector3.one * spread * 2f);
        Graphics.DrawMeshInstancedIndirect(quadMesh, 0, splatMaterial, bounds, argsBuffer, 0, null, UnityEngine.Rendering.ShadowCastingMode.Off, false, 0, sceneView.camera);

        SceneView.RepaintAll();
    }
}
