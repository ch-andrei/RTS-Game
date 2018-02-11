using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using Regions;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer), typeof(MeshCollider))]
public class RegionView : MonoBehaviour
{
    Region region;

    public void Awake()
    {

    }

    public void Start()
    {
        Debug.Log("Starting regionview.");
        GameObject go = GameObject.FindGameObjectWithTag("GameSession");
        region = ((GameSession)go.GetComponent(typeof(GameSession))).getRegion();
        InitializeMesh();
    }

    public void Update()
    {

    }

    public void InitializeMesh()
    {
        Debug.Log("Initializing region Mesh...");

        MeshFilter meshFilter = GetComponent<MeshFilter>();
        MeshCollider meshCollider = GetComponent<MeshCollider>();

        // compute mesh parameters
        Dictionary<Vector3, int> verticesDict = new Dictionary<Vector3, int>();
        List<Vector3> normals = new List<Vector3>();

        List<Vector2> uvs = new List<Vector2>();

        List<int> tris = new List<int>();

        // copy vertice vectors
        Tile[,] tiles = region.getTiles();

        Dictionary<string, bool> trisDict = new Dictionary<string, bool>();

        float yTotal = 0;
        int trisCount = 0;
        int length = tiles.GetLength(0);
        List<Vector2Int> neighbors = region.getNeighborDirections();
        int numNeighbors = neighbors.Count;
        for (int i = 0; i < length; i++)
        {
            for (int j = 0; j < length; j++)
            {
                if (tiles[i, j] != null)
                {
                    // for every neighbor
                    for (int s = 0; s < numNeighbors; s++)
                    {
                        int t = (s + 1) % numNeighbors;

                        try
                        {
                            Vector2Int ind1 = new Vector2Int(i, j) + neighbors[s];
                            Vector2Int ind2 = new Vector2Int(i, j) + neighbors[t];

                            // compute string key for the triangle
                            int[] triInds = new int[] { (i * length + j), (ind1.x * length + ind1.y), (ind2.x * length + ind2.y) };
                            Array.Sort(triInds);
                            string triStringKey = "";
                            foreach (float f in triInds)
                            {
                                triStringKey += f + "-";
                            }

                            // add triangle only if it wasnt added already
                            if (!trisDict.ContainsKey(triStringKey))
                            {
                                trisDict.Add(triStringKey, true);

                                yTotal += tiles[i, j].coord.y + tiles[ind1.x, ind1.y].coord.y + tiles[ind2.x, ind2.y].coord.y;
                                List<Vector3> verticesLocal = new List<Vector3>();
                                verticesLocal.Add(tiles[i, j].coord.getPos());
                                verticesLocal.Add(tiles[ind1.x, ind1.y].coord.getPos());
                                verticesLocal.Add(tiles[ind2.x, ind2.y].coord.getPos());

                                List<Vector2> uvsLocal = new List<Vector2>();
                                uvsLocal.Add(region.coordUV(tiles[i, j].coord));
                                uvsLocal.Add(region.coordUV(tiles[ind1.x, ind1.y].coord));
                                uvsLocal.Add(region.coordUV(tiles[ind2.x, ind2.y].coord));

                                Utilities.MeshGenerator.addTriangle(new Vector3Int(0, 1, 2), verticesLocal, verticesDict, tris, normals, uvsLocal, uvs);
                                trisCount++;
                            }
                        }
                        catch (IndexOutOfRangeException e) { }
                        catch (NullReferenceException e) { }
                    }
                }
            }
        }

        Debug.Log("Tiles with size " + length);
        Debug.Log("Built a mesh with " + verticesDict.Keys.Count + " vertices and " + trisCount + " triangles; total height " + yTotal + ".");

        Mesh mesh = new Mesh();

        mesh.Clear();
        mesh.subMeshCount = 2;

        mesh.SetVertices(verticesDict.Keys.ToList<Vector3>());

        mesh.SetTriangles(tris.ToArray(), 0);

        mesh.SetUVs(0, uvs);

        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        // assign back to meshFilter
        meshFilter.mesh = mesh;
        meshCollider.sharedMesh = meshFilter.sharedMesh;
    }
}