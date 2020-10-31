using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class MeshGenerator : MonoBehaviour
{
    public MetaBallField Field = new MetaBallField();
    private MeshFilter _filter;
    private Mesh _mesh;
    private List<Vector3> vertices = new List<Vector3>();
    private List<Vector3> normals = new List<Vector3>();
    private List<int> indices = new List<int>();

    private static readonly  Vector3 x_delta = new Vector3(0.1f, 0, 0);
    private static readonly Vector3 y_delta = new Vector3(0, 0, 0.1f);
    private static readonly Vector3 z_delta = new Vector3(0, 0.1f, 0);

    private const float cubeSize = 0.3f;
    private const int spaceSize = 100;

    private float[,,] space = new float[spaceSize, spaceSize, spaceSize];
    private float[] currentCube = new float[8];


    private Vector3 getCenterOfBalls()
    {
        var result = new Vector3(0, 0, 0);
        result = Field._ballPositions.Aggregate(result, (current, ball) => current + ball);
        return result / 3;
    }
    
    private void sampling(Vector3 pos)
    {
        for (var i = 0; i < spaceSize; ++i)
        for (var j = 0; j < spaceSize; ++j)
        for (var k = 0; k < spaceSize; ++k)
            space[i, j, k] = Field.F(new Vector3(i, j, k) * cubeSize + pos);
    }
    
    // Slide 15
    private Vector3 getNormal(Vector3 x)
    {
        return Vector3.Normalize(
            new Vector3(
                Field.F(x + x_delta) - Field.F(x - x_delta),
                Field.F(x + y_delta) - Field.F(x - y_delta),
                Field.F(x + z_delta) - Field.F(x - z_delta)
            )
        );
    }
    
    // slide 16
    private int getMaskOfCurrCube()
    {
        var mask = 0;
        for (var i = 0; i < 8; ++i)
        {
            mask |= (currentCube[i] > 0 ? 1 : 0) * (1 << i);
        }

        return mask;
    }
    
    private void addIVN(int i, Vector3 offset)
    {
        indices.Add(vertices.Count);

        var a = MarchingCubes.Tables._cubeEdges[i][0];
        var b = MarchingCubes.Tables._cubeEdges[i][1];
        var point = (
                        MarchingCubes.Tables._cubeVertices[a] * currentCube[b]
                        - MarchingCubes.Tables._cubeVertices[b] * currentCube[a]
                    )
                    / (currentCube[b] - currentCube[a]);

        vertices.Add(offset + point * cubeSize);
        normals.Add(getNormal(vertices.Last()));
    }
    
    /// <summary>
    /// Executed by Unity upon object initialization. <see cref="https://docs.unity3d.com/Manual/ExecutionOrder.html"/>
    /// </summary>
    private void Awake()
    {
        // Getting a component, responsible for storing the mesh
        _filter = GetComponent<MeshFilter>();
        // instantiating the mesh
        _mesh = _filter.mesh = new Mesh();
        // Just a little optimization, telling unity that the mesh is going to be updated frequently
        _mesh.MarkDynamic();
    }

    /// <summary>
    /// Executed by Unity on every frame <see cref="https://docs.unity3d.com/Manual/ExecutionOrder.html"/>
    /// You can use it to animate something in runtime.
    /// </summary>
    private void Update()
    {
        vertices.Clear();
        indices.Clear();
        normals.Clear();
        Field.Update();

        var pos = getCenterOfBalls() - new Vector3(spaceSize, spaceSize, spaceSize) * cubeSize / 2;
        sampling(pos);

        for (var i = 0; i < spaceSize - 1; ++i)
        for (var j = 0; j < spaceSize - 1; ++j)
        for (var k = 0; k < spaceSize - 1; ++k)
        {
            currentCube[0] = space[i, j, k];
            currentCube[1] = space[i, j + 1, k];
            currentCube[2] = space[i + 1, j + 1, k];
            currentCube[3] = space[i + 1, j, k];
            currentCube[4] = space[i, j, k + 1];
            currentCube[5] = space[i, j + 1, k + 1];
            currentCube[6] = space[i + 1, j + 1, k + 1];
            currentCube[7] = space[i + 1, j, k + 1];

            var offset = new Vector3(i, j, k) * cubeSize + pos;
            var mask = getMaskOfCurrCube();
            for (var z = 0; z < MarchingCubes.Tables.CaseToTrianglesCount[mask]; ++z)
            {
                var triangle = MarchingCubes.Tables.CaseToVertices[mask][z];
                addIVN(triangle.x, offset);
                addIVN(triangle.y, offset);
                addIVN(triangle.z, offset);
            }
        }

        // Here unity automatically assumes that vertices are points and hence (x, y, z) will be represented as (x, y, z, 1) in homogenous coordinates
        _mesh.Clear();
        _mesh.SetVertices(vertices);
        _mesh.SetTriangles(indices, 0);
        _mesh.SetNormals(normals);

        // Upload mesh data to the GPU
        _mesh.UploadMeshData(false);
    }
}