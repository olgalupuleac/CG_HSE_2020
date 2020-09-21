using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Xml.Serialization;
using UnityEngine;
using UnityEngine.Assertions;
using Debug = System.Diagnostics.Debug;
using Vector3 = UnityEngine.Vector3;
using Unity.Mathematics;
using static Unity.Mathematics.math;

[RequireComponent(typeof(MeshFilter))]
public class MeshGenerator : MonoBehaviour
{
    public MetaBallField Field = new MetaBallField();

    private MeshFilter _filter;
    private Mesh _mesh;

    private readonly List<Vector3> _vertices = new List<Vector3>();
    private readonly List<Vector3> _normals = new List<Vector3>();
    private readonly List<int> _indices = new List<int>();

    private const int NumberOfSteps = 100;
    private const float Left = -4;
    private const float Right = 4;
    private const float Eps = 0.0005f;
    private readonly Vector3 dx = new Vector3(Eps, 0, 0);
    private readonly Vector3 dy = new Vector3(0, Eps, 0);
    private readonly Vector3 dz = new Vector3(0, 0, Eps);
    private static readonly Vector3 Shift = new Vector3(0.5f, 0.5f, 0.5f);

    private static List<Vector3> BasicCube => new List<Vector3>
    {
        new Vector3(0, 0, 0), // 0
        new Vector3(0, 1, 0), // 1
        new Vector3(1, 1, 0), // 2
        new Vector3(1, 0, 0), // 3
        new Vector3(0, 0, 1), // 4
        new Vector3(0, 1, 1), // 5
        new Vector3(1, 1, 1), // 6
        new Vector3(1, 0, 1), // 7
    };

    private readonly List<(int, int)> _edgeToVertexIndices =
        new List<(int, int)>
        {
            (0, 1), (1, 2), (2, 3), (3, 0),
            (4, 5), (5, 6), (6, 7), (7, 4),
            (0, 4), (1, 5), (2, 6), (3, 7)
        };

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
        Field.Update();
        CalculateTriangles();
    }


    private void CalculateTriangles()
    {
        Vector3 GetRealVertex(Vector3 baseCubeVertex, Vector3 cubeShift)
        {
            return ((cubeShift + baseCubeVertex) / NumberOfSteps - Shift) * (Right - Left);
        }

        int GetMask(Vector3 cubeShift)
        {
            var res = 0;
            for (var i = 0; i < BasicCube.Count; i++)
            {
                var v = BasicCube[i];
                var realVertex = GetRealVertex(v, cubeShift);
                Debug.Assert(realVertex.x <= Right && realVertex.x >= Left);
                Debug.Assert(realVertex.y <= Right && realVertex.y >= Left);
                Debug.Assert(realVertex.z <= Right && realVertex.z >= Left);
                var f = Field.F(realVertex) < 0 ? 0 : 1;
                res |= f << i;
            }

            Debug.Assert(res < 256 && res >= 0);
            return res;
        }

        Vector3 GetPoint(Vector3 cubeShift, int edgeId)
        {
            Debug.Assert(edgeId < _edgeToVertexIndices.Count && edgeId >= 0);
            var (leftIndex, rightIndex) = _edgeToVertexIndices[edgeId];
            var a = GetRealVertex(BasicCube[leftIndex], cubeShift);
            var b = GetRealVertex(BasicCube[rightIndex], cubeShift);
            var fa = Field.F(a);
            var fb = Field.F(b);
            Debug.Assert(fa * fb <= 0);
            var t = Math.Abs(fa) / (Math.Abs(fa) + Math.Abs(fb));
            return a * (1 - t) + b * t;
        }

        for (int a = 0; a < NumberOfSteps; a++)
        {
            for (int b = 0; b < NumberOfSteps; b++)
            {
                for (int c = 0; c < NumberOfSteps; c++)
                {
                    var vertex = new Vector3(a, b, c);
                    var mask = GetMask(vertex);
                    var numberOfTriangles = MarchingCubes.Tables.CaseToTrianglesCount[mask];
                    for (int t = 0; t < numberOfTriangles; t++)
                    {
                        var edges = MarchingCubes.Tables.CaseToVertices[mask][t];
                        for (int e = 0; e < 3; e++)
                        {
                            var edge = edges[e];
                            var point = GetPoint(vertex, edge);
                            _vertices.Add(point);
                            var normal = normalize(float3(Field.F(point - dx) - Field.F(point + dx),
                                Field.F(point - dy) - Field.F(point + dy), Field.F(point - dz) - Field.F(point + dz)));
                            _normals.Add(normal);
                        }
                    }
                }
            }
        }

        for (var i = 0; i < _vertices.Count; i++)
        {
            _indices.Add(i);
        }
    }

    /// <summary>
    /// Executed by Unity on every frame <see cref="https://docs.unity3d.com/Manual/ExecutionOrder.html"/>
    /// You can use it to animate something in runtime.
    /// </summary>
    private void Update()
    {
        //_vertices.Clear();
        //_indices.Clear();
        //_normals.Clear();

        //Field.Update();
        //CalculateTriangles();
        // ----------------------------------------------------------------
        // Generate mesh here. Below is a sample code of a cube generation.
        // ----------------------------------------------------------------

        // What is going to happen if we don't split the vertices? Check it out by yourself by passing
        // sourceVertices and _sourceTriangles to the mesh.

        // Here unity automatically assumes that vertices are points and hence (x, y, z) will be represented as (x, y, z, 1) in homogenous coordinates
        _mesh.Clear();
        _mesh.SetVertices(_vertices);
        _mesh.SetTriangles(_indices, 0);
        _mesh.SetNormals(_normals); // Use _mesh.SetNormals(normals) instead when you calculate them

        // Upload mesh data to the GPU
        _mesh.UploadMeshData(false);
    }
}