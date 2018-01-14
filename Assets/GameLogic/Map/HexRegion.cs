﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Regions;
using Noises;
using HeightMapGenerators;

namespace HexRegions {

    public class HexRegion : Region
    {
        public RegionGenConfig regionGenConfig;

        public int gridRadius;
        public float regionSize;
        public float center;
        public float water;
        public float hexHeight;
        public float hexSize;

        private float minElevation, maxElevation, avgElevation;

        public HexRegion(int seed,
            RegionGenConfig regionGenConfig,
            HeightMapConfig heightMapConfig,
            FastPerlinNoiseConfig noiseConfig,
            ErosionConfig erosionConfig) : base(seed)
        {
            this.regionGenConfig = regionGenConfig;

            // compute required array dimensions
            this.gridRadius = getGridSizeForHexagonalGridWithNHexes(this.regionGenConfig.numberOfTiles);

            noiseConfig.resolution = (int)(this.gridRadius * heightMapConfig.resolutionScale) + 1;
            this.heightMap = new HeightMap(seed, heightMapConfig, noiseConfig, erosionConfig);

            this.hexSize = regionGenConfig.tileSize;
            this.hexHeight = this.hexSize * Mathf.Sqrt(3f) / 2f;

            computeHexTileCenterCoords();

            computeElevationParameters();

            Debug.Log("Generated hex region.");
        }

        public Vector2 coordUV(Coord coord) {
            float u = coord.x / regionSize + 1f / 2f;
            float v = coord.z / regionSize + 1f / 2f;
            return new Vector2(u, v);
        }

        private void computeHexTileCenterCoords()
        {
            Tile[,] coords;

            int arraySize = 2 * gridRadius + 1;
            if (arraySize < 0)
                return;

            coords = new Tile[arraySize, arraySize];

            this.regionSize = hexSize * arraySize * 2;
            float centerOffset = regionSize / 2;

            // loop over X and Y in hex cube coordinatess
            for (int X = -gridRadius; X <= gridRadius; X++)
            {
                for (int Y = -gridRadius; Y <= gridRadius; Y++)
                {
                    int i = X + gridRadius;
                    int j = Y + gridRadius;

                    // if outside of the hexagonal region
                    if (Mathf.Abs(X + Y) > gridRadius)
                    {
                        coords[i, j] = null;
                        continue;
                    }

                    // compute hex Z coord
                    int Z = (int)HexUtilities.axialToCubeCoord(new Vector2(X, Y)).z;

                    // compute tile pos in unity axis coordinates
                    float x = hexSize * X * 3f / 2f;
                    float z = hexHeight * (Y - Z);

                    Vector2 uv = coordUV(new Coord(new Vector3(x, 0, z)));

                    float y = this.regionGenConfig.maxElevation * this.heightMap.read(uv.x, uv.y); // get elevation from Noise 

                    // initialize tile
                    coords[i, j] = new Tile(new Coord (new Vector3(x, y, z)), i , j);
                }
            }

            this.tiles = coords;
        }

        public Coord[,] computeHexVertexCoords(Coord[,] tiles)
        {
            int length = tiles.GetLength(0);
            int size = length * 2 + 1;
            Coord[,] vertices = new Coord[size, size];

            for (int i = 0; i < length; i++)
            {
                for (int j = 0; j < length; j++)
                {
                    Coord tile = tiles[i, j];

                    if (tile != null)
                    {
                        int ii = 2 * i + 1, jj = 2 * j + 1;

                        Vector3[] hexVertices = (new Hexagon(tile.getPos(), this.hexSize, this.hexHeight)).getVertices();

                        vertices[ii, jj] = new Coord(hexVertices[0]);

                        for (int k = 0; k < HexUtilities.HexNeighbors.Length; k++)
                        {
                            Vector2Int dir = HexUtilities.HexNeighbors[k];
                            Vector3 pos = hexVertices[k + 1];

                            try
                            {
                                vertices[ii + dir.x, jj + dir.y] = new Coord(pos);
                            }
                            catch (IndexOutOfRangeException e)
                            {
                                // do nothing
                            }
                        }
                    }
                }
            }

            return vertices;
        }

        // *** TILE POSITION COMPUTATIONS AND GETTERS *** //

        // unity coordinate pos to storage array index
        override
        public Tile getTileAt(Vector3 pos)
        {
            Vector2 index = worldCoordToIndex(new Vector2(pos.x, pos.z));

            int i, j;
            i = (int)index.x + this.gridRadius;
            j = (int)index.y + this.gridRadius;
            //Debug.Log(i + ", " + j);

            if (i < 0 || j < 0 || i >= tiles.GetLength(0) || j >= tiles.GetLength(0))
            {
                return null;
            }

            return this.tiles[i, j];
        }

        // unity units coordinates
        public List<Tile> getTileNeighbors(Vector3 tilePos)
        {
            return getTileNeighbors(worldCoordToIndex(tilePos));
        }

        // array index coordinates
        public List<Tile> getTileNeighbors(Vector2Int tileIndex)
        {
            List<Tile> neighbors = new List<Tile>();
            foreach (Vector2Int dir in HexUtilities.HexNeighbors)
            {
                try
                {
                    Vector2Int index = tileIndex + dir;
                    Tile neighbor = this.tiles[index.x, index.y];
                    if (neighbor != null)
                        neighbors.Add(neighbor);
                }
                catch (IndexOutOfRangeException e)
                {
                    // nothing to do
                }
            }
            return neighbors;
        }

        // unity coordinate system to hexagonal coords
        private Vector2 worldCoordToIndex(Vector2 pos)
        {
            return worldCoordToIndex(pos.x, pos.y);
        }
        private Vector2 worldCoordToIndex(Vector3 pos)
        {
            return worldCoordToIndex(pos.x, pos.z);
        }
        private Vector2 worldCoordToIndex(float x, float y)
        {
            float q = (x) * 2f / 3f / this.hexSize;
            float r = (float)(-(x) / 3f + Math.Sqrt(3f) / 3f * (y)) / this.hexSize;
            return HexUtilities.roundAxialToCube(new Vector2(q, r));
        }

        // *** REGION SIZE COMPUTATIONS *** //

        private int getGridSizeForHexagonalGridWithNHexes(int n)
        {
            int numberOfHexes = 1;
            int size = 1;
            while (numberOfHexes <= n)
            {
                numberOfHexes += (size++) * 6;
            }
            return size - 2;
        }

        // *** ELEVATION PARAMETERS COMPUTATIONS *** //

        private void computeElevationParameters()
        {
            this.minElevation = this.computeMinimumElevation();
            this.maxElevation = this.computeMaximumElevation();
            this.avgElevation = this.computeAverageElevation();
        }

        public float computeAverageElevation()
        {
            double sum = 0;
            List<Coord> coords = getTileVertices();
            foreach (Coord coord in coords)
            {
                sum += coord.y;
            }
            return (float)(sum / (coords.Count));
        }

        public float computeMaximumElevation()
        {
            float max = -float.MaxValue;
            List<Coord> coords = getTileVertices();
            foreach (Coord coord in coords)
            {
                if (max < coord.y)
                {
                    max = coord.y;
                }
            }
            return max;
        }

        public float computeMinimumElevation()
        {
            float min = float.MaxValue;

            List<Coord> coords = getTileVertices();
            foreach (Coord coord in coords)
            {
                if (min > coord.y)
                {
                    min = coord.y;
                }
            }

            return min;
        }

        // *** GETTERS AND SETTERS *** //

        override
        public List<Coord> getTileVertices()
        {
            List<Coord> _tiles = new List<Coord>();

            int length = tiles.GetLength(0);
            for (int i = 0; i < length; i++)
            {
                for (int j = 0; j < length; j++)
                {
                    if (tiles[i, j] != null)
                        _tiles.Add(tiles[i, j].coord);
                }
            }

            return _tiles;
        }

        public Tile[,] getTiles()
        {
            return this.tiles;
        }

        //public int getMinimumElevation()
        //{
        //    return this.minElevation;
        //}
        //public int getMaximumElevation()
        //{
        //    return this.maxElevation;
        //}
        //public int getAverageElevation()
        //{
        //    return this.averageElevation;
        //}
        //public int getWaterLevelElevation()
        //{
        //    return this.waterLevelElevation;
        //}
        //public int getViewableSize()
        //{
        //    return this.regionConfig.regionSize;
        //}
        //public long getViewableSeed()
        //{
        //    return this.regionConfig.regionGenConfig.seed;
        //}
        //public int getMaxTileIndex()
        //{
        //    return this.tiles.GetLength(0);
        //}
        // convenience for storing tile data
        public class HexUtilities
        {
            // act as indices for the Neighbors array
            public enum HexDirections : byte
            {
                TopRight = 0,
                Right = 1,
                BottomRight = 2,
                BottomLeft = 3,
                Left = 4,
                TopLeft = 5
            }

            // right, 
            public static Vector2Int[] HexNeighbors = new Vector2Int[]
            {
                // order: top right, right, bottom right, bottom left, left, top left
                new Vector2Int(+1, -1),
                new Vector2Int(+1, 0),
                new Vector2Int(0, +1),
                new Vector2Int(-1, +1),
                new Vector2Int(-1, 0),
                new Vector2Int(0, -1)
            };

            public static Vector3 axialToCubeCoord(Vector2 axial)
            {
                return new Vector3(axial.x, axial.y, -axial.x - axial.y);
            }

            public static Vector3 roundAxialToCube(Vector2 xy)
            {
                return roundCubeCoord(axialToCubeCoord(xy));
            }

            public static Vector3 roundCubeCoord(Vector3 cubeCoord)
            {
                float rx = Mathf.Round(cubeCoord.x);
                float ry = Mathf.Round(cubeCoord.y);
                float rz = Mathf.Round(cubeCoord.z);
                float x_diff = Mathf.Abs(rx - cubeCoord.x);
                float y_diff = Mathf.Abs(ry - cubeCoord.y);
                float z_diff = Mathf.Abs(rz - cubeCoord.z);
                if (x_diff > y_diff && x_diff > z_diff)
                    rx = -ry - rz;
                else if (y_diff > z_diff)
                    ry = -rx - rz;
                else
                    rz = -rx - ry;
                return new Vector3(rx, ry, rz);
            }

            public static float distanceBetweenHexCoords(Vector2 a, Vector2 b)
            {
                return distanceBetweenHexCoords(axialToCubeCoord(a), axialToCubeCoord(b));
            }

            public static float distanceBetweenHexCoords(Vector3 a, Vector3 b)
            {
                return Mathf.Max(Mathf.Abs(a.x - b.x), Mathf.Abs(a.y - b.y), Mathf.Abs(a.z - b.z));
            }
        }

        public class Hexagon
        {
            // controls the scaling of the hexagon
            public static float hexScaleFactor = 1f;

            /* flat topped hexagon.
            /* 3d hexagon vertices:

              6   1
            5   0   2
              4   3

            hardcoded triangle indices
            */
            public static readonly Vector3[] surfaceTris = new Vector3[]
            {
                new Vector3(0, 6, 1),
                new Vector3(0, 1, 2),
                new Vector3(0, 2, 3),
                new Vector3(0, 3, 4),
                new Vector3(0, 4, 5),
                new Vector3(0, 5, 6)
            };

            public static readonly Vector2[] uvSurface = new Vector2[]
            {
                new Vector2(0.5f , 0.5f),
                new Vector2(0.75f, 1f  ),
                new Vector2(1f   , 0.5f),
                new Vector2(0.75f, 0   ),
                new Vector2(0.25f, 0   ),
                new Vector2(0    , 0.5f),
                new Vector2(0.25f, 1   )
            };

            static int numVertices = 13;
            private Vector3[] vertices;

            public Hexagon(Vector3 central, float size, float height)
            {
                vertices = new Vector3[numVertices];
                initializeVertices(central, hexScaleFactor * height, hexScaleFactor * size);
            }

            public Vector3[] getVertices()
            {
                return this.vertices;
            }

            private void initializeVertices(Vector3 central, float height, float size)
            {
                vertices[0] = central;
                float halfSize = size / 2f;
                // surface
                vertices[1] = new Vector3(central.x + halfSize, central.y, central.z + height); // top right
                vertices[2] = new Vector3(central.x + size, central.y, central.z); // right;
                vertices[3] = new Vector3(central.x + halfSize, central.y, central.z - height); // bottom right;
                vertices[4] = new Vector3(central.x - halfSize, central.y, central.z - height); // bottom left;
                vertices[5] = new Vector3(central.x - size, central.y, central.z); // left;
                vertices[6] = new Vector3(central.x - halfSize, central.y, central.z + height); // top left;
            }
        }
    }
}
