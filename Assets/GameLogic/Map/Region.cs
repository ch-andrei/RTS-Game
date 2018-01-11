using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

using HeightMapGenerators;
using Tiles;

namespace Regions
{

    public class Coord
    {
        protected Vector3 coord { get; set; }
        public float x { get { return this.coord.x; } }
        public float y { get { return this.coord.y; } }
        public float z { get { return this.coord.z; } }

        public Coord(Vector3 coord)
        {
            this.coord = coord;
        }

        public Vector3 getPos()
        {
            return this.coord;
        }
    }

    [System.Serializable] // for unity editor
    public class RegionGenConfig
    {
        [Range(1, 200000)]
        public int numberOfTiles;

        [Range(0.001f, 50f)]
        public float tileSize;

        [Range(1, 25000)]
        public int maxElevation;

        public RegionGenConfig()
        {
        }
    }

    public abstract class Region
    {
        protected int seed;

        protected HeightMap heightMap;

        protected float maxElevation, minElevation, averageElevation, waterLevelElevation;

        public Coord[,] tiles;
        public abstract List<Coord> getTileVertices();

        // act as indices for the Neighbors array
        public enum SquareDirections : byte
        {
            Top = 0,
            Right = 1,
            Bottom = 2,
            Left = 3
        }

        // right, 
        public static Vector2Int[] SquareNeighbors = new Vector2Int[]
        {
                // order: top, right, bottom, left
                new Vector2Int(-1, 0),
                new Vector2Int(0, +1),
                new Vector2Int(+1, 0),
                new Vector2Int(0, -1)
        };

        public Region(int seed)
        {
            this.seed = seed;
        }
    }

    public class HexRegion : Region
    {
        public RegionGenConfig regionGenConfig;

        public int gridRadius;
        public float regionSize;
        public float center;
        public float water;
        public float hexHeight;
        public float hexSize;

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

            //computeElevationParameters();
        }

        private void computeHexTileCenterCoords()
        {
            Coord[,] coords;

            int arraySize = 2 * gridRadius + 1;
            if (arraySize < 0)
                return;

            coords = new Coord[arraySize, arraySize];

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
                    if (Math.Abs(X + Y) > gridRadius)
                    {
                        coords[i, j] = null;
                        continue;
                    }

                    // compute hex Z coord
                    int Z = (int)HexUtilities.AxialToCubeCoord(new Vector2(X, Y)).z;

                    // compute tile pos in unity axis coordinates
                    float x = hexSize * X * 3f / 2f;
                    float z = hexHeight * (Y - Z);

                    float uNoise = (x + centerOffset) / regionSize;
                    float vNoise = (z + centerOffset) / regionSize;

                    float y = this.regionGenConfig.maxElevation * this.heightMap.read(uNoise, vNoise); // get elevation from Noise 

                    // initialize tile
                    coords[i, j] = new Coord(new Vector3(x, y, z));
                }
            }

            this.tiles = coords;
        }

        //public Coord[,] computeTileVertices(Coord[,] tiles)
        //{
        //    int length = tiles.GetLength(0);
        //    int size = length * 2 + 1;
        //    Coord[,] vertices = new Coord[size, size];

        //    for (int i = 0; i < length; i++)
        //    {
        //        for (int j = 0; j < length; j++)
        //        {
        //            Coord tile = tiles[i, j];

        //            if (tile != null)
        //            {
        //                int ii = 2 * i + 1, jj = 2 * j + 1;

        //                Vector3[] hexVertices = (new Hexagon(tile.getPos(), this.hexSize, this.hexHeight)).getVertices();

        //                vertices[ii, jj] = new Coord(hexVertices[0]);

        //                for (int k = 0; k < HexUtilities.Neighbors.Length; k++) {
        //                    Vector2Int dir = HexUtilities.Neighbors[k];
        //                    Vector3 pos = hexVertices[k + 1];

        //                    try {
        //                        vertices[ii + dir.x, jj + dir.y] = new Coord(pos);
        //                    }
        //                    catch (IndexOutOfRangeException e) {
        //                        // do nothing
        //                    }
        //                }
        //            }                    
        //        }
        //    }

        //    return vertices;
        //}

        //// *** TILE POSITION COMPUTATIONS AND GETTERS *** //

        //public HexTilePos getTileAt(Vector3 pos) {
        //    Vector2 index = worldCoordToIndex(new Vector2(pos.x, pos.z));
        //    int i, j;
        //    i = (int)index.x + this.gridRadius;
        //    j = (int)index.y + this.gridRadius;
        //    //Debug.Log(i + ", " + j);
        //    if (i < 0 || j < 0 || i >= tiles.GetLength(0) || j >= tiles.GetLength(0))
        //    {
        //        return null;
        //    }
        //    return this.tiles[i, j];
        //}

        //// writes index of the tile to the 'out' parameter
        //public HexTilePos getTileAt(Vector3 pos, out int[] index) {
        //    HexTilePos tile = getTileAt(pos);
        //    if (tile != null)
        //        index = new int[] { (int)tile.index.x, (int)tile.index.y };
        //    else
        //        index = null;
        //    return tile;
        //}

        //// unity units coordinates
        //public List<HexTilePos> getTileNeighbors(Vector3 tilePos) {
        //    return getTileNeighbors(worldCoordToIndex(tilePos));
        //}

        //// array index coordinates
        //public List<HexTilePos> getTileNeighbors(Vector2 tileIndex) {
        //    List<HexTilePos> neighbors = new List<HexTilePos>();
        //    foreach (Vector2 dir in HexTilePos.Neighbors) {
        //        try {
        //            Vector2 index = tileIndex + dir;
        //            HexTilePos neighbor = this.tiles[(int)index.x, (int)index.y];
        //            if (neighbor != null)
        //                neighbors.Add(neighbor);
        //        }
        //        catch (IndexOutOfRangeException e)
        //        {
        //            // nothing to do
        //        }
        //    }
        //    return neighbors;
        //}

        //private Vector2 worldCoordToIndex(Vector2 pos) {
        //    return worldCoordToIndex(pos.x, pos.y);
        //}
        //private Vector2 worldCoordToIndex(Vector3 pos)
        //{
        //    return worldCoordToIndex(pos.x, pos.z);
        //}
        //private Vector2 worldCoordToIndex(float x, float y)
        //{
        //    float q = (x) * 2f / 3f / this.regionGenConfig.tileSize;
        //    float r = (float)(-(x) / 3f + Math.Sqrt(3f) / 3f * (y)) / this.regionGenConfig.hexSize;
        //    return roundCubeCoord(q, r);
        //}

        //// code refactored from http://www.redblobgames.com/grids/hexagons/
        //private Vector3 roundCubeCoord(float X, float Y)
        //{
        //    return roundCubeCoord(new Vector3(X, Y, -X - Y));
        //}
        //private Vector3 roundCubeCoord(Vector3 cubeCoord)
        //{
        //    float rx = (float)Math.Round(cubeCoord.x);
        //    float ry = (float)Math.Round(cubeCoord.y);
        //    float rz = (float)Math.Round(cubeCoord.z);
        //    float x_diff = (float)Math.Abs(rx - cubeCoord.x);
        //    float y_diff = (float)Math.Abs(ry - cubeCoord.y);
        //    float z_diff = (float)Math.Abs(rz - cubeCoord.z);
        //    if (x_diff > y_diff && x_diff > z_diff)
        //        rx = -ry - rz;
        //    else if (y_diff > z_diff)
        //        ry = -rx - rz;
        //    else
        //        rz = -rx - ry;
        //    return new Vector3(rx, ry, rz);
        //}

        //// *** REGION SIZE COMPUTATIONS *** //

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

        //private int numberOfHexesForGridSize(int gridSize)
        //{
        //    if (gridSize <= 0) return 1;
        //    else
        //    {
        //        return 6 * gridSize + numberOfHexesForGridSize(gridSize - 1);
        //    }
        //}

        //// *** ELEVATION PARAMETERS COMPUTATIONS *** //

        //private void computeElevationParameters()
        //{
        //    this.minElevation = this.computeMinimumElevation();
        //    this.maxElevation = this.computeMaximumElevation();
        //    this.averageElevation = this.computeAverageElevation();
        //}

        //public int computeAverageElevation()
        //{
        //    long sum = 0;
        //    List<TilePos> tiles = getViewableTiles();
        //    foreach (TilePos tile in tiles)
        //    {
        //        sum += (int)tile.getY();
        //    }
        //    return (int)(sum / (tiles.Count));
        //}

        //public int computeMaximumElevation()
        //{
        //    int max = 0;
        //    List<TilePos> tiles = getViewableTiles();
        //    foreach (TilePos tile in tiles)
        //    {
        //        if (max < tile.getY())
        //        {
        //            max = (int)tile.getY();
        //        }
        //    }
        //    return max;
        //}

        //public int computeMinimumElevation()
        //{
        //    int min = int.MaxValue;
        //    List<TilePos> tiles = getViewableTiles();
        //    foreach (TilePos tile in tiles)
        //    {
        //        if (min > tile.getY())
        //        {
        //            min = (int)tile.getY();
        //        }
        //    }
        //    if (min == int.MaxValue) min = -1;
        //    return min;
        //}

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
                        _tiles.Add(tiles[i, j]);
                }
            }

            return _tiles;
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

            public static Vector3 AxialToCubeCoord(Vector2 axial)
            {
                return new Vector3(axial.x, axial.y, -axial.x - axial.y);
            }

            public static float distanceBetweenHexCoords(Vector2 a, Vector2 b)
            {
                return distanceBetweenHexCoords(AxialToCubeCoord(a), AxialToCubeCoord(b));
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
