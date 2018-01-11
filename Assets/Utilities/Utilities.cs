using System.Xml;
using System.Collections.Generic;
using UnityEngine;

public class Utilities
{
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

    public static float[,] mergeArrays(float[,] a, float[,] b, float weightA, float weightB)
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
        float[,] c = (choice) ? new float[a.GetLength(0), a.GetLength(0)] : new float[b.GetLength(0), b.GetLength(0)];
        double ratio = (double)a.GetLength(0) / b.GetLength(0);
        for (int i = 0; i < c.GetLength(0); i++)
        {
            for (int j = 0; j < c.GetLength(0); j++)
            {
                // sum weighted values
                if (choice)
                {
                    c[i, j] = weightA * a[i, j] + weightB * b[(int)(i / ratio), (int)(j / ratio)];
                }
                else
                {
                    c[i, j] = weightA * a[(int)(i * ratio), (int)(j * ratio)] + weightB * b[i, j];
                }
                // rescale the values back
                c[i, j] /= (weightA + weightB);
            }
        }
        return c;
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
