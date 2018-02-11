using UnityEngine;
using Mono.Simd;
using System;
using System.Collections.Generic;

using SquareRegions;
using Noises;
using Utilities.Misc;

namespace HeightMapGenerators
{
    [System.Serializable]
    public class HeightMapConfig
    {
        private const float configEpsilon = 1e-3f;

        public int preset = -1;

        // how uniform the overall terrain should be
        [Range(0, 1 - configEpsilon)]
        public float flattenLinearStrength = 0.25f;

        [Range(0, 1 - configEpsilon)]
        public float flattenLowsStrength = 0.5f;

        // how much to suppress high elevations
        [Range(0, 1 - configEpsilon)]
        public float flattenDampenStrength = 0.25f;

        // amplifiction parameter for rescaling height values; high amplifiaction pulls small values smaller, and make high values appear higher; exponential stretching
        [Range(1, 10)]
        public float amplification = 10;

        // height map resolution depends on region size; this scaler can be used to increase the resolution
        [Range(configEpsilon, 10f)]
        public float resolutionScale = 1f;

        // number of discrete values for the height map
        [Range(0, 100)]
        public int heightSteps = 50;

        public HeightMapConfig()
        {
        }
    }

    [System.Serializable]
    public class ErosionConfig {

        public bool applyErosion = true;

        // number of iterations of erosion computations
        [Range(0, 2000)]
        public int iterations = 50;

        // scales the influence of erosion linearly
        [Range(0, 1)]
        public float strength = 1f;

        // amount of water to deposit during erosion simulation: higher means more erosion; acts like a strength parameter; high values smoothen erossion
        [Range(0, 1)]
        public float waterAmount = 0.1f;

        // amount of water to lose per simulation iteration: water[time k + 1] = erosionWaterLoss * water[time k]
        [Range(0, 1)]
        public float waterLoss = 0.99f;

        // limits the influence of elevation difference on terrain movement; acts as maximum elevation difference after which terrain movement won't be affected
        [Range(0, 1)]
        public float waterVelocityElevationDiffRegularizer = 0.2f;

        // if elevation is in range [0-1], water will contribute to elevation in waterAmount / waterToElevationProportion
        // ex: elevation = 0.9, waterAmount = 0.2, combined elevation = 0.9 + 0.2 = 1.1 
        [Range(0, 1)]
        public float waterToElevationProportion = 0.05f;

        [Range(0, 1)]
        public float minTerrainMovementProportion = 0.01f;

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
        public float flattenLinearStrength { get { return this.config.flattenLinearStrength; } }
        public float flattenLowsStrength { get { return this.config.flattenLowsStrength; } }
        public float flattenDampenStrength { get { return this.config.flattenDampenStrength; } }
        public float amplification { get { return this.config.amplification; } }
        public int heightSteps { get { return this.config.heightSteps; } }

        public int erosionIterations { get { return this.erosionConfig.iterations; } }
        public float erosionStrength { get { return this.erosionConfig.strength; } }
        public float erosionWaterAmount { get { return this.erosionConfig.waterAmount; } }
        public float erosionWaterLoss { get { return this.erosionConfig.waterLoss; } }
        public float erosionWaterVelocityElevationDiffRegularizer { get { return this.erosionConfig.waterVelocityElevationDiffRegularizer; } }
        public float erosionWaterToElevationProportion { get { return this.erosionConfig.waterToElevationProportion; } }
        public float erosionMinTerrainMovementProportion { get { return this.erosionConfig.minTerrainMovementProportion; } }

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
                    applyNormalizedHalfSphere(elevations);
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
                    //applyNormalizedHalfSphere(elevations, intensity : 0.5f, sphereMaxValue : 0.75f, overwrite: true);
                    amplifyElevations(elevations, amplification);
                    logarithmicClamp(elevations, 1f, 1);
                    dampenElevations(elevations, flattenDampenStrength);
                    flattenLows(elevations);
                    flattenLinearToAverage(elevations);
                    break;
            }

            computeErosion(elevations);

            Tools.normalize(elevations, maxOnly: true, rescaleSmallMax: false);

            normalizeToNElevationLevels(elevations, heightSteps);

            this.noise.setNoiseValues(elevations);
        }

        private void flattenLinearToAverage(float[,] elevations)
        {
            float[] minMaxAvg = Tools.computeMinMaxAvg(elevations);
            float avg = minMaxAvg[2];

            for (int i = 0; i < elevations.GetLength(0); i++)
            {
                for (int j = 0; j < elevations.GetLength(1); j++)
                {
                    elevations[i, j] += flattenLinearStrength * (avg - elevations[i, j]);
                }
            }
        }

        // flattens low elevations stronger than high elevations; crushes elevations to the min value
        private void flattenLows(float[,] elevations)
        {
            float[] minMaxAvg = Tools.computeMinMaxAvg(elevations);
            float min = minMaxAvg[0];
            float max = minMaxAvg[1];

            for (int i = 0; i < elevations.GetLength(0); i++)
            {
                for (int j = 0; j < elevations.GetLength(1); j++)
                {
                    float distToMin = elevations[i, j] - min;
                    float ratio = distToMin / (max - min);
                    elevations[i, j] += flattenLowsStrength * (min - (1f - ratio) * elevations[i, j]);
                }
            }
        }

        private void normalizeToNElevationLevels(float[,] elevations, int levels)
        {
            if (levels <= 0)
                return;

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

        public float logisticsFunction(float value, float growth_rate = 5.0f)
        {
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
        public void amplifyElevations(float[,] elevations, float amplifyFactor, float scaleFactor = 1f)
        {
            Tools.normalize(elevations);

            for (int i = 0; i < elevations.GetLength(0); i++)
            {
                for (int j = 0; j < elevations.GetLength(1); j++)
                {
                    elevations[i, j] = Mathf.Pow(scaleFactor * elevations[i, j], amplifyFactor);
                }
            }

            Tools.normalize(elevations);
        }

        public void dampenElevations(float[,] elevations, float strength = 1f)
        {
            for (int i = 0; i < elevations.GetLength(0); i++)
            {
                for (int j = 0; j < elevations.GetLength(1); j++)
                {
                    elevations[i, j] -= strength * (elevations[i, j] - Mathf.Log(1f + Mathf.Epsilon + elevations[i, j]));
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
        public void applyNormalizedHalfSphere(float[,] elevations, float intensity = 1f, int diameter = -1, float sphereMaxValue = 1f, bool overwrite = false)
        {
            diameter = (diameter< 0) ? elevations.GetLength(0) : diameter;
            if (diameter <= 0)
                return;

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

            Tools.mergeArrays(elevations, sphere, 1f, intensity, overwrite);
        }

        // *** EROSSION COMPUTATIONS *** //

        private void computeErosion(float[,] elevations)
        {
            if (!this.erosionConfig.applyErosion)
                return;

            float[,] waterVolumes = new float[elevations.GetLength(0), elevations.GetLength(1)];

            Debug.Log("Computing Erosion: elevations size " + elevations.GetLength(0) + " by " + elevations.GetLength(1));

            erosionDepositWaterRandom(waterVolumes, erosionWaterAmount, 0.2f, 4);

            int numIterations = erosionIterations;
            float terrainMovement = float.MaxValue;
            while (numIterations-- > 0)
            {
                terrainMovement = computeErosionIteration(elevations, waterVolumes);
                Debug.Log("EROSION: Moved " + terrainMovement + ". Iterations left " + numIterations);

                if (terrainMovement < erosionMinTerrainMovementProportion) {
                    Debug.Log("Erosion loop finished after " + (erosionIterations - numIterations) + " iterations.");
                }
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

        private List<HeightMapTile> mapGetNeighborTiles(int i, int j)
        {
            float[,] elevations = this.noise.getNoiseValues();

            List<HeightMapTile> neighbors = new List<HeightMapTile>();
            foreach (Vector2Int dir in SquareRegion.SquareUtilities.SquareNeighborsNoDiag) {
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

        private void checkWater(float[,] waterVolumes) {
            float sum = 0;
            for (int i = 0; i < waterVolumes.GetLength(0); i++)
            {
                for (int j = 0; j < waterVolumes.GetLength(1); j++)
                {
                    sum += waterVolumes[i, j];
                }
            }

            Tools.computeMinMaxAvg(waterVolumes);
            Debug.Log("Total water amount = " + sum);
        }

        private float computeErosionIteration(float[,] elevations, float[,] waterVolumes)
        {
            float minWaterThreshold = 1e-3f;
            float velocityElevationToProximityRatio = 0.95f;
            float velocityProximityInfluence = 0.25f;

            checkWater(waterVolumes);

            // deep copy of arrays
            float[,] waterUpdated = new float[waterVolumes.GetLength(0), waterVolumes.GetLength(1)];
            for (int i = 0; i < waterUpdated.GetLength(0); i++)
            {
                for (int j = 0; j < waterUpdated.GetLength(1); j++)
                {
                    waterUpdated[i, j] = waterVolumes[i, j];
                }
            }

            float totalTerrainMovement = 0;
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
                    List<HeightMapTile> neighbors = mapGetNeighborTiles(i, j);

                    // sort in ascending order: lowest first
                    neighbors.Sort((x, y) => x.elevation.CompareTo(y.elevation));

                    // compute elevation influenced 'velocity' vectors
                    List<float> elevationGradientWithWater = new List<float>();
                    //List<float> elevationGradient = new List<float>();
                    foreach (HeightMapTile neighbor in neighbors)
                    {
                        elevationGradientWithWater.Add((current.elevation + waterVolumes[current.i, current.j] * erosionWaterToElevationProportion) - 
                            (neighbor.elevation + waterVolumes[neighbor.i, neighbor.j] * erosionWaterToElevationProportion));
                        //elevationGradient.Add(current.elevation - neighbor.elevation);
                    }

                    float[] waterMovement = new float[neighbors.Count];
                    for (int k = 0; k < neighbors.Count; k++)
                    {
                        if (elevationGradientWithWater[k] > 0)
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
                            waterVelocity = 1f - Mathf.Exp(-Mathf.Abs(elevationGradientWithWater[k]) / erosionWaterVelocityElevationDiffRegularizer); // range [0,1]

                            // velocity from proximity
                            // do weighted sum based on constants
                            waterVelocity = waterVelocity * velocityElevationToProximityRatio + (1f - velocityElevationToProximityRatio) * velocityProximityInfluence;

                            float waterLossAmount = waterVelocity * waterUpdated[current.i, current.j];

                            //Debug.Log("velocity " + waterVelocity + "; waterLossAmount " + waterLossAmount + " from [" +  current.i + ", " + current.j +
                            //    "] to [" + neighbor.i + ", " + neighbor.j + "] using elev diff " + elevationGradient[k]);

                            waterMovement[k] = waterLossAmount;
                        }
                    }

                    // rescale water movement to account for movement to all neighbors
                    float waterMovementTotal = 0;
                    foreach (float f in waterMovement)
                    {
                        waterMovementTotal += f;
                    }
                    if (waterMovementTotal > 0) {
                        // rescale
                        for (int k = 0; k < neighbors.Count; k++)
                        {
                            waterMovement[k] *= waterMovement[k] / waterMovementTotal;
                        }

                        // check if want to move more water than is available
                        waterMovementTotal = 0;
                        foreach (float f in waterMovement)
                        {
                            waterMovementTotal += f;
                        }
                        if (waterMovementTotal > waterUpdated[current.i, current.j])
                        {
                            float modifier = waterUpdated[current.i, current.j] / waterMovementTotal;
                            for (int k = 0; k < neighbors.Count; k++)
                            {
                                waterMovement[k] *= modifier;
                            }
                        }
                    }

                    // update water amount and elevations
                    for (int k = 0; k < neighbors.Count; k++) {
                        HeightMapTile neighbor = neighbors[k];

                        // remove water from current
                        waterUpdated[current.i, current.j] -= waterMovement[k];

                        // add water to neighbor
                        waterUpdated[neighbor.i, neighbor.j] += waterMovement[k];

                        // compute terrain elevation adjustment
                        float terrainMovement = erosionStrength * elevationGradientWithWater[k] * (waterMovement[k] / erosionWaterAmount);

                        //Debug.Log("terrainMovement " + terrainMovement + " from [" + current.i + ", " + current.j +
                        //    "] to [" + neighbor.i + ", " + neighbor.j + "]");

                        // adjust elevations
                        elevations[current.i, current.j] -= terrainMovement;
                        elevations[neighbor.i, neighbor.j] += terrainMovement;

                        totalTerrainMovement += terrainMovement;
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

            return totalTerrainMovement;
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
                                    waterVolumes[ii, jj] += UnityEngine.Random.Range(waterAmount * probability, waterAmount);
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
}
