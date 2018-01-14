using System;
using UnityEngine;

namespace Noises {

    public abstract class Noise
    {
        private int noiseResolution = 500;
        private float[,] values;
        private bool generated = false;
        private int seed;

        public Noise(int noise_resolution, int seed)
        {
            this.noiseResolution = noise_resolution;
            this.seed = seed;
            UnityEngine.Random.InitState(seed);
        }

        public void generateNoise()
        {
            if (generated)
            {
                Debug.Log("Noise already generated.");
                return;
            }
            values = generateNoiseValues();
            Debug.Log("Noise was generated.");
            generated = true;
        }

        // extending noise classes must implement this method
        public abstract float[,] generateNoiseValues();

        public void regenerate()
        {
            generated = false;
            generateNoise();
        }

        public bool getGenerated()
        {
            return generated;
        }

        public float[,] getNoiseValues()
        {
            return this.values;
        }

        public void setNoiseValues(float[,] noiseValues)
        {
            this.values = noiseValues;
        }

        // returns linearly interpolated weighted average of a local area of 4 pixels
        // the noise is usually a lower resolution array than the map which uses it, thus we need to interpolate
        public float lerpNoiseValue(float baseU, float baseV)
        {
            float noiseIndex = getNoiseRes() - 1;
            float uInd = noiseIndex * baseU;
            float vInd = noiseIndex * baseV;

            int uF = (int)Mathf.Floor(uInd);
            int uC = (int)Mathf.Ceil(uInd);
            int vF = (int)Mathf.Floor(vInd);
            int vC = (int)Mathf.Ceil(vInd);

            float valFF = values[uF, vF];
            float valFC = values[uF, vC];
            float valCF = values[uC, vF];
            float valCC = values[uC, vC];

            float u = uInd - uF;
            float v = vInd - vF;

            float val1 = Mathf.Lerp(valFF, valCF, u);
            float val2 = Mathf.Lerp(valFC, valCC, u);
            return Mathf.Lerp(val1, val2, v);
        }

        public int getNoiseRes()
        {
            return noiseResolution;
        }
    }

    public class ZeroNoiseMap : Noise
    {
        public ZeroNoiseMap(int noiseResolution, int seed) : base(noiseResolution, seed)
        {
            this.generateNoise();
        }

        override
        public float[,] generateNoiseValues()
        {
            float[,] zeros = new float[this.getNoiseRes(), this.getNoiseRes()];
            for (int i = 0; i < this.getNoiseRes(); i++)
            {
                for (int j = 0; j < this.getNoiseRes(); j++)
                {
                    zeros[i, j] = 0;
                }
            }
            return zeros;
        }
    }

    [System.Serializable]
    public class FastPerlinNoiseConfig
    {
        // noise inputs
        [Range(0.001f, 500f)]
        public float amplitude = 5f;

        [Range(0.001f, 1f)]
        public float persistance = 0.25f;

        [Range(1, 16)]
        public int octaves = 8;

        [Range(1, 10)]
        public int levels = 5;

        public int resolution { get; set; }

        // initialize the region parameters
        public FastPerlinNoiseConfig()
        {
        }
    }

    public class FastPerlinNoise : Noise
    {
        private FastPerlinNoiseConfig noiseConfig;

        public FastPerlinNoiseConfig config { get { return this.noiseConfig; } }
        public int resolution { get { return this.noiseConfig.resolution; } }
        public float amplitude { get { return this.noiseConfig.amplitude; } }
        public float persistance { get { return this.noiseConfig.persistance; } }
        public int octaves { get { return this.noiseConfig.octaves; } }
        public int levels { get { return this.noiseConfig.levels; } }

        public FastPerlinNoise(int seed, FastPerlinNoiseConfig config) : base(config.resolution, seed)
        {
            this.noiseConfig = config;
            this.generateNoise();
        }

        override
        public float[,] generateNoiseValues()
        {
            return generateMultipleLevelPerlinNoise(octaves, levels);
        }

        private float[,] generateMultipleLevelPerlinNoise(int octaveCount, int levels)
        {
            float[,] perlinNoiseCombined = new float[getNoiseRes(), getNoiseRes()];
            // generate 0,1,...,levels of perlin noise patterns and merge these
            for (int i = 1; i <= levels; i++)
            {
                float[,] baseNoise = generateWhiteNoise(getNoiseRes());
                float[,] perlinNoise = generatePerlinNoise(baseNoise, octaveCount);
                // merge results of new perlin level with previous perlinNoise
                perlinNoiseCombined = Utilities.mergeArrays(perlinNoise, perlinNoiseCombined, 1f / levels, (float)i / levels);
            }
            return perlinNoiseCombined;
        }

        private float[,] generateWhiteNoise(int size)
        {
            float[,] noise = new float[size, size];
            for (int i = 0; i < size; i++)
            {
                for (int j = 0; j < size; j++)
                {
                    noise[i, j] = (float)UnityEngine.Random.value;
                }
            }
            return noise;
        }

        private float[,] generateSmoothNoise(float[,] baseNoise, int octave)
        {
            int length = baseNoise.GetLength(0);
            float[,] smoothNoise = new float[length, length];

            int samplePeriod = (int)(2 * octave + 1); // calculates 2 ^ k
            float sampleFrequency = 1.0f / samplePeriod;

            for (int i = 0; i < length; i++)
            {
                //calculate the horizontal sampling indices
                int sample_i0 = (i / samplePeriod) * samplePeriod;
                int sample_i1 = (sample_i0 + samplePeriod) % length; //wrap around
                float horizontal_blend = (i - sample_i0) * sampleFrequency;

                for (int j = 0; j < length; j++)
                {
                    //calculate the vertical sampling indices
                    int sample_j0 = (j / samplePeriod) * samplePeriod;
                    int sample_j1 = (sample_j0 + samplePeriod) % length; //wrap around
                    float vertical_blend = (j - sample_j0) * sampleFrequency;

                    //blend the top two corners
                    float top = Mathf.Lerp(baseNoise[sample_i0, sample_j0],
                            baseNoise[sample_i1, sample_j0], horizontal_blend);

                    //blend the bottom two corners
                    float bottom = Mathf.Lerp(baseNoise[sample_i0, sample_j1],
                            baseNoise[sample_i1, sample_j1], horizontal_blend);

                    //final blend
                    smoothNoise[i, j] = Mathf.Lerp(top, bottom, vertical_blend);
                }
            }
            return smoothNoise;
        }

        private float[,] generatePerlinNoise(float[,] baseNoise, int octaveCount)
        {
            int length = baseNoise.GetLength(0);
            float[][,] smoothNoise = new float[octaveCount][,]; //an array of 2D arrays

            //generate smooth noise
            for (int i = 0; i < octaveCount; i++)
            {
                smoothNoise[i] = generateSmoothNoise(baseNoise, i);
            }

            float[,] perlinNoise = new float[length, length]; //an array of floats initialized to 0

            float totalAmplitude = 0.0f;

            float _amplitude = amplitude;

            //blend noise together
            for (int octave = octaveCount - 1; octave >= 0; octave--)
            {
                _amplitude *= persistance;
                totalAmplitude += _amplitude;

                for (int i = 0; i < length; i++)
                {
                    for (int j = 0; j < length; j++)
                    {
                        perlinNoise[i, j] += smoothNoise[octave][i, j] * _amplitude;
                    }
                }
            }

            //normalisation
            for (int i = 0; i < length; i++)
            {
                for (int j = 0; j < length; j++)
                {
                    perlinNoise[i, j] /= totalAmplitude;
                }
            }

            return perlinNoise;
        }
    }
}