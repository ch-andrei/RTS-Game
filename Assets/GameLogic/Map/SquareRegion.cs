using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Regions;
using Noises;
using HeightMapGenerators;

namespace SquareRegions
{
    public class SquareRegion : Region
    {
        public RegionGenConfig regionGenConfig;

        public SquareRegion(int seed,
            RegionGenConfig regionGenConfig,
            HeightMapConfig heightMapConfig,
            FastPerlinNoiseConfig noiseConfig,
            ErosionConfig erosionConfig) : base(seed)
        {
            this.regionGenConfig = regionGenConfig;

            // compute required array dimensions
            this.gridRadius = computeGridRadius();

            noiseConfig.resolution = (int)(this.gridRadius * heightMapConfig.resolutionScale) + 1;
            this.heightMap = new HeightMap(seed, heightMapConfig, noiseConfig, erosionConfig);

            this.tileSize = regionGenConfig.tileSize;

            computeTileCenterCoords();

            computeElevationParameters();

            Debug.Log("Generated square region.");
        }

        private void computeTileCenterCoords()
        {
            Tile[,] coords;

            int arraySize = 2 * gridRadius + 1;
            if (arraySize < 0)
                return;

            coords = new Tile[arraySize, arraySize];

            this.regionSize = tileSize * arraySize * 2;

            // loop over X and Y in hex cube coordinatess
            for (int X = -gridRadius; X <= gridRadius; X++)
            {
                for (int Y = -gridRadius; Y <= gridRadius; Y++)
                {
                    int i = X + gridRadius;
                    int j = Y + gridRadius;

                    // compute tile pos in unity axis coordinates
                    float x = tileSize * X;
                    float z = tileSize * Y;

                    Vector2 uv = coordUV(new Coord(new Vector3(x, 0, z)));

                    float y = this.regionGenConfig.maxElevation * this.heightMap.read(uv.x, uv.y); // get elevation from Noise 

                    // initialize tile
                    coords[i, j] = new Tile(new Coord(new Vector3(x, y, z)), i, j);
                }
            }

            this.tiles = coords;
        }

        // *** TILE POSITION COMPUTATIONS AND GETTERS *** //

        // unity coordinate pos to storage array index
        override
        public Tile getTileAt(Vector3 pos)
        {
            Vector2 index = regionWorldCoordToIndex(new Vector2(pos.x, pos.z));

            int i, j;
            i = (int)index.x + this.gridRadius;
            j = (int)index.y + this.gridRadius;

            if (i < 0 || j < 0 || i >= tiles.GetLength(0) || j >= tiles.GetLength(0))
            {
                return null;
            }

            return this.tiles[i, j];
        }

        // unity units coordinates
        override
        public List<Vector2Int> getNeighborDirections()
        {
            return new List<Vector2Int>(SquareUtilities.SquareNeighbors);
        }

        public static class SquareUtilities {
            // act as indices for the SquareNeighbors array
            public enum SquareDirections : byte
            {
                Top,
                TopRight,
                Right,
                BottomRight,
                Bottom,
                BottomLeft,
                Left,
                TopLeft
            }

            public static Vector2Int[] SquareNeighbors = new Vector2Int[]
            {
            // order: top, top right, right, bottom right, bottom, bottom left, left, top left
            new Vector2Int(-1, 0),
            new Vector2Int(-1, +1),
            new Vector2Int(0, +1),
            new Vector2Int(+1, +1),
            new Vector2Int(+1, 0),
            new Vector2Int(+1, -1),
            new Vector2Int(0, -1),
            new Vector2Int(-1, -1)
            };

            public enum SquareDirectionsNoDiag : byte
            {
                Top,
                Right,
                Bottom,
                Left
            }

            public static Vector2Int[] SquareNeighborsNoDiag = new Vector2Int[]
{
            // order: top, right, bottom, left
            new Vector2Int(-1, 0),
            new Vector2Int(0, +1),
            new Vector2Int(+1, 0),
            new Vector2Int(0, -1),
};
        }

        // unity coordinate system to hexagonal coords\
        override
        protected Vector3 regionWorldCoordToIndex(float x, float y)
        {
            float i = (int)(Mathf.Floor(x / this.tileSize + 0.5f));
            float j = (int)(Mathf.Floor(y / this.tileSize + 0.5f));
            return new Vector3(i, j, 0);
        }

        // *** REGION SIZE COMPUTATIONS *** //
        override
        protected int computeGridRadius()
        {
            return (int)(Mathf.Floor(Mathf.Sqrt(this.regionGenConfig.numberOfTiles)) / 2) - 1;
        }
    }
}
