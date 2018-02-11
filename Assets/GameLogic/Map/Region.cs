using System;
using System.Collections.Generic;
using UnityEngine;

using HeightMapGenerators;

namespace Regions
{
    public class Coord
    {
        protected Vector3 pos;
        public float x { get { return this.pos.x; } }
        public float y { get { return this.pos.y; } }
        public float z { get { return this.pos.z; } }

        public Coord(Vector3 pos)
        {
            this.pos = pos;
        }

        public Vector3 getPos()
        {
            return pos;
        }
    }

    public class Tile
    {
        public Coord coord;
        public int i;
        public int j;

        public Vector2Int index { get { return new Vector2Int(this.i, this.j); } }
    
        public Tile(Coord coord, int i, int j)
        {
            this.coord = coord;
            this.i = i;
            this.j = j;
        }

        public bool equals(Tile tile) { return this.i == tile.i && this.j == tile.j; }
    }

    [System.Serializable] // for unity editor
    public class RegionGenConfig
    {
        private const int maxNumberOfTiles = 64000; // slightly less than ~2^16 -> unity's mesh vertex count limitation 

        [Range(1, maxNumberOfTiles)]
        public int numberOfTiles;

        [Range(0.001f, 10f)]
        public float tileSize;

        [Range(1, 1000)]
        public int maxElevation;

        public RegionGenConfig()
        {
        }
    }

    public abstract class Region
    {
        protected int seed;

        protected HeightMap heightMap;

        protected float tileSize;
        protected int gridRadius;

        protected float regionSize;
        protected float center;

        protected float waterLevelElevation;
        protected float minElevation, maxElevation, avgElevation;

        public Tile[,] tiles;
        protected abstract int computeGridRadius();

        protected Vector3 regionWorldCoordToIndex(Vector2 pos) { return regionWorldCoordToIndex(pos.x, pos.y); }
        protected Vector3 regionWorldCoordToIndex(Vector3 pos) { return regionWorldCoordToIndex(pos.x, pos.z); }
        protected abstract Vector3 regionWorldCoordToIndex(float x, float y);

        public abstract Tile getTileAt(Vector3 pos);
        public abstract List<Vector2Int> getNeighborDirections();

        public Region(int seed)
        {
            this.seed = seed;
        }

        public Tile[,] getTiles()
        {
            return this.tiles;
        }

        // unity units coordinates
        public List<Tile> getTileNeighbors(Vector3 tilePos)
        {
            return getTileNeighbors(regionWorldCoordToIndex(tilePos));
        }

        // unity units coordinates
        public List<Tile> getTileNeighbors(Vector2Int tileIndex)
        {
            return getTileNeighbors(tileIndex.x, tileIndex.y);
        }

        public float distanceBetweenTiles(Tile tile1, Tile tile2) {
            return (tile1.coord.getPos() - tile2.coord.getPos()).magnitude;
        }

        // array index coordinates
        public List<Tile> getTileNeighbors(int i, int j)
        {
            List<Tile> neighbors = new List<Tile>();
            foreach (Vector2Int dir in this.getNeighborDirections())
            {
                try
                {
                    Tile neighbor = this.tiles[i + dir.x, j + dir.y];
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

        public Vector2 coordUV(Coord coord)
        {
            float u = coord.x / regionSize + 1f / 2f;
            float v = coord.z / regionSize + 1f / 2f;
            return new Vector2(u, v);
        }

        // *** ELEVATION PARAMETERS COMPUTATIONS *** //
        protected void computeElevationParameters()
        {
            this.minElevation = this.computeMinimumElevation();
            this.maxElevation = this.computeMaximumElevation();
            this.avgElevation = this.computeAverageElevation();
        }

        protected float computeAverageElevation()
        {
            double sum = 0;
            List<Coord> coords = getTileVertices();
            foreach (Coord coord in coords)
            {
                sum += coord.y;
            }
            return (float)(sum / (coords.Count));
        }

        protected float computeMaximumElevation()
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

        protected float computeMinimumElevation()
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
    }
}
