using System.Xml;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class Utilities
{
    public class MeshGenerator
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
                }

                triangles.Add(index);
                normals.Add(normal);
                uvs.Add(v.uv);
            }
        }
    }

    public static Color hexToColor(string hex)
    {
        Color c = new Color();
        ColorUtility.TryParseHtmlString(hex, out c);
        return c;
    }

    public static float[] computeMinMaxAvg(float[,] values)
    {
        // get average and max elevation values
        float avg = 0, max = 0, min = float.MaxValue;
        for (int i = 0; i < values.GetLength(0); i++)
        {
            for (int j = 0; j < values.GetLength(0); j++)
            {
                avg += values[i, j];
                if ((values[i, j] > max))
                    max = values[i, j];
                if ((values[i, j] < min))
                    min = values[i, j];
            }
        }

        avg /= values.GetLength(0) * values.GetLength(0); // since elevations is 2d array nxn
        Debug.Log("Pre min/max/avg: " + min + "/" + max + "/" + avg);

        return new float[] { min, max, avg };
    }

    public static void normalize(float[,] values, bool maxOnly = false, bool rescaleSmallMax = true)
    {

        float[] minMaxAvg = computeMinMaxAvg(values);
        float min = minMaxAvg[0];
        float max = minMaxAvg[1];
        float avg = minMaxAvg[2];

        // configuration modifiers
        min = maxOnly ? 0 : min;
        max = !rescaleSmallMax && max < 1f ? 1f : max;

        float adjustment = maxOnly ? 1f / max : 1f / (max - min);
        if (Mathf.Abs(adjustment - 1f) > Mathf.Epsilon)
        {
            for (int i = 0; i < values.GetLength(0); i++)
            {
                for (int j = 0; j < values.GetLength(0); j++)
                {
                    values[i, j] = (Mathf.Abs(values[i, j] - min) * adjustment);
                }
            }
        }

        avg = 0;
        max = 0;
        min = float.MaxValue;
        for (int i = 0; i < values.GetLength(0); i++)
        {
            for (int j = 0; j < values.GetLength(0); j++)
            {
                avg += values[i, j];
                if ((values[i, j] > max))
                    max = values[i, j];
                if ((values[i, j] < min))
                    min = values[i, j];
            }
        }
        Debug.Log("Post min/max/avg: " + min + "/" + max + "/" + avg / (values.GetLength(0) * values.GetLength(0)));
    }

    public static float[,] mergeArrays(float[,] a, float[,] b, float weightA, float weightB, bool overwrite = false)
    {
        if (weightA <= 0 && weightB <= 0)
        {
            weightA = 0.5f;
            weightB = 0.5f;
        }

        weightA = weightA / (weightA + weightB);
        weightB = weightB / (weightA + weightB);

        // works with arrays of different size
        bool choice = a.GetLength(0) > b.GetLength(0);

        float[,] dst;
        if (overwrite)
        {
            dst = a;
        }
        else
        {
            dst = (choice) ? new float[a.GetLength(0), a.GetLength(0)] : new float[b.GetLength(0), b.GetLength(0)];
        }

        double ratio = (double)a.GetLength(0) / b.GetLength(0);
        for (int i = 0; i < dst.GetLength(0); i++)
        {
            for (int j = 0; j < dst.GetLength(0); j++)
            {
                // sum weighted values
                if (choice)
                {
                    dst[i, j] = weightA * a[i, j] + weightB * b[(int)(i / ratio), (int)(j / ratio)];
                }
                else
                {
                    dst[i, j] = weightA * a[(int)(i * ratio), (int)(j * ratio)] + weightB * b[i, j];
                }
                // rescale the values back
                dst[i, j] /= (weightA + weightB);
            }
        }

        return dst;
    }

    public class statsXMLreader
    {
        public static XmlDocument doc;

        public static string stats_fileName = System.IO.Directory.GetCurrentDirectory() + "/assets/xml_defs/stats.xml";

        public static string getParameterFromXML(string caller, string field = null)
        {
            if (doc == null)
            { // load the doc if its null
                doc = new XmlDocument();
                doc.Load(stats_fileName);
            }
            XmlNode node;
            if (field == null)
            {
                node = doc.DocumentElement.SelectSingleNode("/stats/" + caller + "[1]");
            }
            else
            {
                node = doc.DocumentElement.SelectSingleNode("/stats/" + caller + "/" + field + "[1]");
            }
            if (node != null)
            {
                return node.InnerText;
            }
            else
                return null;
        }

        public static string[] getParametersFromXML(string caller, string field = null)
        {
            List<string> strings;
            if (doc == null)
            { // load the doc if its null
                doc = new XmlDocument();
                doc.Load(stats_fileName);
            }
            XmlNodeList nodes;
            if (field == null)
            {
                nodes = doc.DocumentElement.SelectNodes("/stats/" + caller);
            }
            else
            {
                nodes = doc.DocumentElement.SelectNodes("/stats/" + caller + "/" + field);
            }
            if (nodes != null)
            {
                strings = new List<string>();
                foreach (XmlNode node in nodes)
                    strings.Add(node.InnerText);
            }
            else
                return null;
            return strings.ToArray();
        }
    }
}
