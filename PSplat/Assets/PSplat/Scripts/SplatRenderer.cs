using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Globalization;
using System;
using Random = UnityEngine.Random;
using System.IO;

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

    private string plyFile = "V:\\temp\\ply\\philipp-cropped.ply";


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

        SplatData[] splats;

        if (plyFile != null)
        {
            splats = LoadBinaryPlySplats(plyFile);
            splatCount = splats.Length;
            Debug.Log($"Loaded {splatCount} splats from PLY file: {plyFile}");
        }
        else
        {
            splats = new SplatData[splatCount];
            for (int i = 0; i < splats.Length; i++)
            {
                splats[i].position = Random.insideUnitSphere * spread;
                splats[i].size = Random.Range(0.01f, 0.05f);
                splats[i].color = Color.Lerp(Color.yellow, Color.red, Random.value);
            }
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

    public static SplatData[] LoadBinaryPlySplats(string path)
    {
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
        using var br = new BinaryReader(fs);

        // 1. Read header
        string line;
        int vertexCount = 0;

        while ((line = ReadAsciiLine(br)) != null)
        {
            if (line.StartsWith("element vertex"))
            {
                var parts = line.Split(' ');
                vertexCount = int.Parse(parts[2]);
            }
            else if (line.StartsWith("end_header"))
            {
                break;
            }
        }

        // 2. Allocate splat data
        var splats = new SplatData[vertexCount];

        for (int i = 0; i < vertexCount; i++)
        {
            float x = br.ReadSingle();
            float y = br.ReadSingle();
            float z = br.ReadSingle();
            br.BaseStream.Position += 12; // skip normal (nx, ny, nz)

            float r = br.ReadSingle();
            float g = br.ReadSingle();
            float b = br.ReadSingle();

            br.BaseStream.Position += 40 * 4; // skip f_rest_0 to f_rest_39

            float opacity = br.ReadSingle();

            float s0 = br.ReadSingle();
            float s1 = br.ReadSingle();
            float s2 = br.ReadSingle();
            float size = (s0 + s1 + s2) / 3f;

            br.BaseStream.Position += 4 * 4; // skip rot_0 to rot_3

            splats[i] = new SplatData
            {
                position = new Vector3(x, y, z),
                size = size,
                color = new Color(r, g, b, opacity)
            };
        }

        return splats;
    }

    private static string ReadAsciiLine(BinaryReader br)
    {
        var chars = new List<char>();
        char c;
        while ((c = br.ReadChar()) != '\n')
        {
            chars.Add(c);
        }
        return new string(chars.ToArray());
    }


    public static SplatData[] LoadPlySplats(string path)
    {
        var lines = File.ReadAllLines(path);
        int headerEnd = 0;
        List<SplatData> splats = new();

        // Skip header
        for (int i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("end_header"))
            {
                headerEnd = i + 1;
                break;
            }
        }

        // Parse body
        for (int i = headerEnd; i < lines.Length; i++)
        {
            var parts = lines[i].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 15) continue;

            var pos = new Vector3(
                float.Parse(parts[0], CultureInfo.InvariantCulture),
                float.Parse(parts[1], CultureInfo.InvariantCulture),
                float.Parse(parts[2], CultureInfo.InvariantCulture));

            var color = new Color(
                float.Parse(parts[6], CultureInfo.InvariantCulture),
                float.Parse(parts[7], CultureInfo.InvariantCulture),
                float.Parse(parts[8], CultureInfo.InvariantCulture),
                1.0f);

            var scale0 = float.Parse(parts[9], CultureInfo.InvariantCulture);
            var scale1 = float.Parse(parts[10], CultureInfo.InvariantCulture);
            var scale2 = float.Parse(parts[11], CultureInfo.InvariantCulture);
            float size = (scale0 + scale1 + scale2) / 3f; // simple fallback

            splats.Add(new SplatData { position = pos, size = size, color = color });
        }

        return splats.ToArray();
    }



}
