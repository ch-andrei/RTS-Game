using UnityEngine;

namespace Utilities.Misc
{
    public class Tools
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
    }
}
