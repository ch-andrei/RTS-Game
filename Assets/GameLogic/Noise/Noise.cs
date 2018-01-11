using UnityEngine;
using System;
using System.Collections.Generic;

using Regions;

namespace HeightMapGenerators
{
    [System.Serializable]
    public class HeightMapConfig
    {
        public int preset = -1;

        // how uniform the overall terrain should be
        [Range(0, 1)]
        public float flatness = 0.25f;

        // amplifiction parameter for rescaling height values; high amplifiaction pulls small values smaller, and make high values appear higher; exponential stretching
        [Range(0, 10)]
        public float amplification = 10;

        // height map resolution depends on region size; this scaler can be used to increase the resolution
        [Range(0.1f, 10f)]
        public float resolutionScale = 1f;

        // number of discrete values for the height map
        [Range(1, 100)]
        public int heightSteps = 50;

        public HeightMapConfig()
        {
        }
    }

    [System.Serializable]
    public class ErosionConfig {

        public bool applyErosion = true;

        // number of iterations of erosion computations
        [Range(0, 200)]
        public int erosionIterations = 50;

        // scales the influence of erosion linearly
        [Range(0, 1)]
        public float erosionStrength = 1f;

        // amount of water to deposit during erosion simulation: higher means more erosion; acts like a strength parameter; high values smoothen erossion
        [Range(0, 1)]
        public float erosionWaterAmount = 0.1f;

        // amount of water to lose per simulation iteration: water[time k + 1] = erosionWaterLoss * water[time k]
        [Range(0, 1)]
        public float erosionWaterLoss = 0.99f;

        // scales the influence of erosion on terrain movement (1 means terrain wont move, 0 means maximum terrain movement)
        [Range(0, 1)]
        public float earthStability = 0.25f;

        // limits the influence of elevation difference on terrain movement; acts as maximum elevation difference after which terrain movement won't be affected
        [Range(0, 1)]
        public float erosionVelocityElevationCap = 0.2f;

        public ErosionConfig()
        {
        }
    }

    public class HeightMap
    {
        private HeightMapConfig config;
        private FastPerlinNoiseConfig noiseConfig;
        private ErosionConfig erosionConfig;

        private Noise noise;

        public int preset { get { return this.config.preset; } }
        public float flatness { get { return this.config.flatness; } }
        public float amplification { get { return this.config.amplification; } }
        public int heightSteps { get { return this.config.heightSteps; } }

        public int erosionIterations { get { return this.erosionConfig.erosionIterations; } }
        public float erosionStrength { get { return this.erosionConfig.erosionStrength; } }
        public float erosionWaterAmount { get { return this.erosionConfig.erosionWaterAmount; } }
        public float erosionWaterLoss { get { return this.erosionConfig.erosionWaterLoss; } }
        public float earthStability { get { return this.erosionConfig.earthStability; } }
        public float erosionVelocityElevationCap { get { return this.erosionConfig.erosionVelocityElevationCap; } }

        public float read(float u, float v) { return this.noise.lerpNoiseValue(u, v); }

        public HeightMap(int seed, HeightMapConfig config, FastPerlinNoiseConfig noiseConfig, ErosionConfig erosionConfig)
        {
            this.config = config;
            this.noiseConfig = noiseConfig;
            this.erosionConfig = erosionConfig;
            this.noise = new FastPerlinNoise(seed, noiseConfig);
            modifyNoise();
        }

        public void modifyNoise()
        {
            float[,] elevations = this.noise.getNoiseValues();

            switch (preset)
            {
                case 0:
                    elevations = applyNormalizedHalfSphere(elevations, elevations.GetLength(0), 1f);
                    break;
                case 1:
                    applyLogisticsFunctionToElevations(elevations);
                    break;
                case 2:
                    amplifyElevations(elevations, 2);
                    break;
                case 3:
                    break;
                default:
                    Debug.Log("Default noise map.");
                    amplifyElevations(elevations, amplification);
                    elevations = applyNormalizedHalfSphere(elevations, elevations.GetLength(0), 0.5f, 0.75f);
                    logarithmicClamp(elevations, 1f, 1);
                    dampenElevations(elevations);
                    crushToAverage(elevations, flatness);
                    break;
            }

            computeErosion(elevations);

            Utilities.normalize(elevations, maxOnly: true, rescaleSmallMax: false);
            normalizeToNElevationLevels(elevations, heightSteps);

            this.noise.setNoiseValues(elevations);
        }

        private void crushToAverage(float[,] elevations, float strength = 0.5f)
        {
            strength = Mathf.Clamp(strength, 0f, 1f);

            float[] minMaxAvg = Utilities.computeMinMaxAvg(elevations);
            float avg = minMaxAvg[2];

            Debug.Log("pre AVERAGE: " + avg);

            for (int i = 0; i < elevations.GetLength(0); i++)
            {
                for (int j = 0; j < elevations.GetLength(1); j++)
                {
                    elevations[i, j] += (avg - elevations[i, j]) * strength;
                }
            }

            minMaxAvg = Utilities.computeMinMaxAvg(elevations);
            avg = minMaxAvg[2];

            Debug.Log("post AVERAGE: " + avg);
        }

        private void normalizeToNElevationLevels(float[,] elevations, int levels)
        {
            for (int i = 0; i < elevations.GetLength(0); i++)
            {
                for (int j = 0; j < elevations.GetLength(1); j++)
                {
                    elevations[i, j] = ((int)(elevations[i, j] * levels)) / ((float)levels);
                }
            }
        }

        private float logarithmic(float value, float start, float intensity)
        {
            return (float)(start + Mathf.Log(1 - start + value / intensity));
        }

        // flattens terrain
        private void logarithmicClamp(float[,] elevations, float logClampThreshold, float intensity)
        {
            for (int i = 0; i < elevations.GetLength(0); i++)
            {
                for (int j = 0; j < elevations.GetLength(1); j++)
                {
                    if (elevations[i, j] > logClampThreshold)
                    {
                        elevations[i, j] = logarithmic(elevations[i, j], logClampThreshold, intensity);
                    }
                }
            }
        }

        public float logisticsFunction(float value)
        {
            float growth_rate = 5.0f;
            return (float)(1.0 / (1 + Mathf.Exp(growth_rate / 2 + -growth_rate * value)));
        }

        // flattens the terrain
        public void applyLogisticsFunctionToElevations(float[,] elevations)
        {
            for (int i = 0; i < elevations.GetLength(0); i++)
            {
                for (int j = 0; j < elevations.GetLength(1); j++)
                {
                    elevations[i, j] = logisticsFunction(elevations[i, j]);
                }
            }
        }

        // results in elevation = (amplify_factor * elevation) ^ amplify_factor
        public void amplifyElevations(float[,] elevations, float amplify_factor, float scale_factor = 1f)
        {
            for (int i = 0; i < elevations.GetLength(0); i++)
            {
                for (int j = 0; j < elevations.GetLength(1); j++)
                {
                    elevations[i, j] = Mathf.Pow(scale_factor * elevations[i, j], amplify_factor);
                }
            }
            Utilities.normalize(elevations);
        }

        public void dampenElevations(float[,] elevations)
        {
            for (int i = 0; i < elevations.GetLength(0); i++)
            {
                for (int j = 0; j < elevations.GetLength(1); j++)
                {
                    elevations[i, j] = Mathf.Log(1 + elevations[i, j]);
                }
            }
        }

        public void convolutionFilter(float[,] elevations, float[,] weights)
        {
            for (int i = 1; i < elevations.GetLength(0) - 1; i++)
            {
                for (int j = 1; j < elevations.GetLength(1) - 1; j++)
                {
                    for (int ii = -1; ii < 2; ii++)
                    {
                        for (int jj = -1; jj < 2; jj++)
                        {
                            elevations[i, j] += weights[ii + 1, jj + 1] * elevations[i + ii, j + jj];
                        }
                    }
                    if (elevations[i, j] < 0)
                    {
                        elevations[i, j] = 0;
                    }
                    if (elevations[i, j] > 1)
                    {
                        elevations[i, j] = 1;
                    }
                }
            }
        }

        // intensity is how strong the sphere effect is
        // threshold is the maximum value on the sphere that will be applied
        public float[,] applyNormalizedHalfSphere(float[,] elevations, int diameter, float intensity, float sphereMaxValue = 1f)
        {
            Debug.Log("applying sphere of diameter " + diameter);
            float radius = diameter / 2f;
            float[,] sphere = new float[diameter, diameter];
            for (int i = 0; i < diameter; i++)
            {
                for (int j = 0; j < diameter; j++)
                {
                    float val = Mathf.Pow(radius, 2) - Mathf.Pow(i - radius, 2) - Mathf.Pow(j - radius, 2);
                    val = (val > 0) ? val : 0;
                    sphere[i, j] = Mathf.Sqrt(val);
                }
            }
            for (int i = 0; i < diameter; i++)
            {
                for (int j = 0; j < diameter; j++)
                {
                    sphere[i, j] /= radius;
                    sphere[i, j] = Mathf.Clamp(sphere[i, j], 0, sphereMaxValue);
                }
            }
            return Utilities.mergeArrays(sphere, elevations, intensity, 1); ;
        }

        // *** EROSSION COMPUTATIONS *** //

        private void computeErosion(float[,] elevations)
        {
            if (!this.erosionConfig.applyErosion)
                return;

            float[,] waterVolumes = new float[elevations.GetLength(0), elevations.GetLength(1)];

            Debug.Log("Computing Erosion: elevations size " + elevations.GetLength(0) + " by " + elevations.GetLength(1));

            erosionDepositWater(waterVolumes, erosionWaterAmount/*, 1f, 4*/);

            int numIterations = erosionIterations;
            while (numIterations-- > 0)
            {
                computeErosionIteration(elevations, waterVolumes);
                Debug.Log("Erosion iterations left " + numIterations);
            }

            this.noise.setNoiseValues(elevations);
        }

        public struct HeightMapTile
        {
            public int i;
            public int j;
            public float elevation;
            public HeightMapTile(int i, int j, float elevation) {
                this.i = i;
                this.j = j;
                this.elevation = elevation;
            }
        }

        private List<HeightMapTile> getNeighborTiles(int i, int j)
        {
            float[,] elevations = this.noise.getNoiseValues();

            List<HeightMapTile> neighbors = new List<HeightMapTile>();
            foreach (Vector2Int dir in Region.SquareNeighbors) {
                try
                {
                    int ii = i + dir.x;
                    int jj = j + dir.y;
                    neighbors.Add(new HeightMapTile(ii, jj, elevations[ii, jj]));
                }
                catch (IndexOutOfRangeException e)
                {
                    // do nothing
                }
            }

            return neighbors;
        }

        private void computeErosionIteration(float[,] elevations, float[,] waterVolumes)
        {
            float minWaterThreshold = 1e-3f;
            float velocityElevationToProximityRatio = 0.95f;
            float velocityProximityInfluence = 0.25f;

            // deep copy of arrays
            float[,] waterUpdated = new float[waterVolumes.GetLength(0), waterVolumes.GetLength(1)];
            for (int i = 0; i < waterUpdated.GetLength(0); i++)
            {
                for (int j = 0; j < waterUpdated.GetLength(1); j++)
                {
                    waterUpdated[i, j] = waterVolumes[i, j];
                }
            }

            HeightMapTile current;
            for (int i = 0; i < elevations.GetLength(0); i++)
            {
                for (int j = 0; j < elevations.GetLength(1); j++)
                {

                    // do not do anything for small water amounts
                    if (waterVolumes[i, j] < minWaterThreshold) {
                        //Debug.Log("MIN WATER REACHED");
                        continue;
                    }

                    current = new HeightMapTile(i, j, elevations[i,j]);
                    
                    // get tile neighbors
                    List<HeightMapTile> neighbors = getNeighborTiles(i, j);

                    // sort in ascending order: lowest first
                    neighbors.Sort((x, y) => x.elevation.CompareTo(y.elevation));

                    // TEMP
                    // temporary addition: only use the lowest tile neighbor -> remove all but the lowest neighbor
                    if (neighbors.Count > 1)
                        neighbors.RemoveRange(1, neighbors.Count - 1);
                    // TEMP END

                    // compute elevation influenced 'velocity' vectors
                    //List<float> elevationGradientWithWater = new List<float>();
                    List<float> elevationGradient = new List<float>();
                    foreach (HeightMapTile t in neighbors)
                    {
                        //elevationGradientWithWater.Add((current.elevation + waterVolumes[current.i, current.j]) - (t.elevation + waterVolumes[t.i, t.j]));
                        elevationGradient.Add(current.elevation - t.elevation);
                    }

                    for (int k = 0; k < neighbors.Count; k++)
                    {
                        if (elevationGradient[k] > 0)
                        {
                            // get neighbor
                            HeightMapTile neighbor = neighbors[k];

                            // if no water left move on to next tile
                            if (waterUpdated[current.i, current.j] <= 0)
                            {
                                waterUpdated[current.i, current.j] = 0;
                                break;
                            }

                            float waterVelocity;
                            // velocity from elevation
                            waterVelocity = 1f - Mathf.Exp(-Mathf.Abs(elevationGradient[k]) / erosionVelocityElevationCap); // range [0,1]

                            // velocity from proximity
                            // do weighted sum based on constants
                            waterVelocity = waterVelocity * velocityElevationToProximityRatio + (1f - velocityElevationToProximityRatio) * velocityProximityInfluence;

                            float waterLossAmount = erosionStrength * waterVelocity * waterUpdated[current.i, current.j];

                            //Debug.Log("velocity " + waterVelocity + "; waterLossAmount " + waterLossAmount + " from [" +  current.i + ", " + current.j +
                            //    "] to [" + neighbor.i + ", " + neighbor.j + "] using elev diff " + elevationGradient[k]);

                            // check if want to erode more water than currently available
                            if (waterLossAmount > waterUpdated[current.i, current.j])
                                waterLossAmount = waterUpdated[current.i, current.j];

                            // remove water from current
                            waterUpdated[current.i, current.j] -= waterLossAmount;

                            // add water to neighbor
                            waterUpdated[neighbor.j, neighbor.j] += waterLossAmount;

                            // compute terrain elevation adjustment
                            float terrainMovement = waterLossAmount / erosionWaterAmount * (1f - earthStability) * elevationGradient[k];

                            //Debug.Log("terrainMovement " + terrainMovement + " from [" + current.i + ", " + current.j +
                            //    "] to [" + neighbor.i + ", " + neighbor.j + "]");

                            // adjust elevations
                            elevations[current.i, current.j] -= terrainMovement;
                            elevations[neighbor.i, neighbor.j] += terrainMovement;
                        }
                    }
                }
            }

            // write back updated water volumes
            for (int i = 0; i < waterUpdated.GetLength(0); i++)
            {
                for (int j = 0; j < waterUpdated.GetLength(1); j++)
                {
                    waterVolumes[i, j] = waterUpdated[i, j];
                }
            }

            // simulate drying effect
            erosionRemoveWater(waterVolumes, erosionWaterLoss);
        }

        private void erosionRemoveWater(float[,] waterVolumes, float erosionWaterLoss)
        {
            for (int i = 0; i < waterVolumes.GetLength(0); i++)
            {
                for (int j = 0; j < waterVolumes.GetLength(1); j++)
                {
                    waterVolumes[i, j] *= erosionWaterLoss;
                }
            }
        }

        private void erosionDepositWater(float[,] waterVolumes, float waterAmount)
        {
            for (int i = 0; i < waterVolumes.GetLength(0); i++)
            {
                for (int j = 0; j < waterVolumes.GetLength(1); j++)
                {
                    waterVolumes[i, j] = waterAmount;
                }
            }
        }

        private void erosionDepositWaterRandom(float[,] waterVolumes, float waterAmount, float probability, int radius)
        {
            probability = Mathf.Clamp(probability, 0f, 1f);

            for (int i = 0; i < waterVolumes.GetLength(0); i++)
            {
                for (int j = 0; j < waterVolumes.GetLength(1); j++)
                {
                    waterVolumes[i, j] = 0;
                }
            }
            for (int i = 0; i < waterVolumes.GetLength(0); i++)
            {
                for (int j = 0; j < waterVolumes.GetLength(1); j++)
                {
                    if (UnityEngine.Random.Range(0f, 1f) < probability)
                    {
                        for (int ii = i - radius; ii < i + radius; ii++)
                        {
                            for (int jj = j - radius; jj < j + radius; jj++)
                            {
                                try
                                {
                                    waterVolumes[ii, jj] += Mathf.Clamp(UnityEngine.Random.Range(0f, waterAmount * probability), 0, waterAmount);
                                }
                                catch (NullReferenceException e)
                                {
                                    // do nothing
                                }
                                catch (IndexOutOfRangeException e)
                                {
                                    // do nothing
                                }
                            }
                        }
                    }
                }
            }
            for (int i = 0; i < waterVolumes.GetLength(0); i++)
            {
                for (int j = 0; j < waterVolumes.GetLength(1); j++)
                {
                    if (waterVolumes[i, j] > waterAmount)
                    {
                        waterVolumes[i, j] = waterAmount;
                    }
                }
            }
        }
    }

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
