using System.Collections.Generic;
using UnityEngine;
using System.Linq;

namespace Utilities.MeshTools
{
    public static class MeshTriAdder
    {
        // adds a triangle to the mesh
        public static void addTriangle(Vector3Int triangle, List<Vector3> verticesLocal, List<Vector3> vertices, List<int> triangles, bool reordered = true)
        {
            // adds vertices clockwise along x, z plane 
            List<Vector3> temp = new List<Vector3>();
            temp.Add(verticesLocal[triangle.x]);
            temp.Add(verticesLocal[triangle.y]);
            temp.Add(verticesLocal[triangle.z]);

            if (reordered)
            {
                // compute central location
                Vector3 center = (temp[0] + temp[1] + temp[2]) / 3;
                // sort vectors by angle, clockwise so that the normal point outside
                temp = temp.OrderBy(o => (-Mathf.Atan((o.x - center.x) / (o.z - center.z)))).ToList();
            }

            // add triangle
            foreach (Vector3 v in temp)
            {
                vertices.Add(v);
                triangles.Add(triangles.Count);
            }
        }

        private struct VertexData
        {
            public Vector3 v { get; set; }
            public Vector2 uv { get; set; }
            public VertexData(Vector3 v, Vector2 uv)
            {
                this.v = v;
                this.uv = uv;
            }
        }

        // adds a triangle to the mesh
        public static void addTriangle(Vector3Int triangle, List<Vector3> verticesLocal, List<Vector3> vertices, List<int> triangles,
                                            List<Vector3> normals, List<Vector2> uvsLocal, List<Vector2> uvs, int triangleOffset = 0)
        {
            // adds vertices clockwise along x, z plane 
            List<VertexData> verts = new List<VertexData>();
            verts.Add(new VertexData(verticesLocal[triangle.x], uvsLocal[triangle.x]));
            verts.Add(new VertexData(verticesLocal[triangle.y], uvsLocal[triangle.y]));
            verts.Add(new VertexData(verticesLocal[triangle.z], uvsLocal[triangle.z]));

            // compute central location
            Vector3 center = (verts[0].v + verts[1].v + verts[2].v) / 3;

            // sort vectors by angle, clockwise so that the normal points upwards
            verts = verts.OrderBy(o => (-Mathf.Atan((o.v.x - center.x) / (o.v.z - center.z)))).ToList();

            Vector3 normal = Vector3.Cross(verts[1].v - verts[0].v, verts[2].v - verts[0].v).normalized;

            // add triangle
            foreach (VertexData v in verts)
            {
                vertices.Add(v.v);
                normals.Add(normal);
                triangles.Add(triangleOffset + triangles.Count);
                uvs.Add(v.uv);
            }
        }

        // adds a triangle to the mesh
        public static void addTriangle(Vector3Int triangle, List<Vector3> verticesLocal, Dictionary<Vector3, int> vertices, List<int> triangles,
                                            List<Vector3> normals, List<Vector2> uvsLocal, List<Vector2> uvs)
        {
            // adds vertices clockwise along x, z plane 
            List<VertexData> verts = new List<VertexData>();
            verts.Add(new VertexData(verticesLocal[triangle.x], uvsLocal[triangle.x]));
            verts.Add(new VertexData(verticesLocal[triangle.y], uvsLocal[triangle.y]));
            verts.Add(new VertexData(verticesLocal[triangle.z], uvsLocal[triangle.z]));

            // compute central location
            Vector3 center = (verts[0].v + verts[1].v + verts[2].v) / 3;

            // sort vectors by angle, clockwise so that the normal points upwards
            verts = verts.OrderBy(o => (-Mathf.Atan((o.v.x - center.x) / (o.v.z - center.z)))).ToList();

            Vector3 normal = Vector3.Cross(verts[1].v - verts[0].v, verts[2].v - verts[0].v).normalized;

            // add triangle
            foreach (VertexData v in verts)
            {
                int index;
                if (vertices.ContainsKey(v.v))
                {
                    index = vertices[v.v];
                }
                else
                {
                    index = vertices.Keys.Count;

                    vertices.Add(v.v, index);
                    normals.Add(normal);
                    uvs.Add(v.uv);
                }

                triangles.Add(index);
            }
        }
    }

    /* ------------------------------------------------------------------------------------
        | Author:  Evelyn Liu (evelyn.liuqi@gmail.com).  
        | Created: 4/24/2010. (Modified 4/28/2010. Corrected a bug as suggested by Mark.)
        | Usage:   Mesh creation helper. To create a new mesh with one of the submesh only.
        ------------------------------------------------------------------------------------ */
    public static class MeshCreationHelper
    {
        ///------------------------------------------------------------
        /// <summary>
        /// Create a new mesh with one of oldMesh's submesh
        /// </summary>
        ///--------------~~~~------------------------------------------------
        public static Mesh CreateMesh(Mesh oldMesh, int subIndex)
        {
            Mesh newMesh = new Mesh();

            List<int> triangles = new List<int>();
            triangles.AddRange(oldMesh.GetTriangles(subIndex)); // the triangles of the sub mesh

            List<Vector3> newVertices = new List<Vector3>();
            List<Vector2> newUvs = new List<Vector2>();

            // Mark's method. 
            Dictionary<int, int> oldToNewIndices = new Dictionary<int, int>();
            int newIndex = 0;

            // Collect the vertices and uvs
            for (int i = 0; i < oldMesh.vertices.Length; i++)
            {
                if (triangles.Contains(i))
                {
                    newVertices.Add(oldMesh.vertices[i]);
                    newUvs.Add(oldMesh.uv[i]);
                    oldToNewIndices.Add(i, newIndex);
                    ++newIndex;
                }
            }

            int[] newTriangles = new int[triangles.Count];

            // Collect the new triangles indecies
            for (int i = 0; i < newTriangles.Length; i++)
            {
                newTriangles[i] = oldToNewIndices[triangles[i]];
            }
            // Assemble the new mesh with the new vertices/uv/triangles.
            newMesh.vertices = newVertices.ToArray();
            newMesh.uv = newUvs.ToArray();
            newMesh.triangles = newTriangles;

            // Re-calculate bounds and normals for the renderer.
            newMesh.RecalculateBounds();
            newMesh.RecalculateNormals();

            return newMesh;
        }
    }


}
