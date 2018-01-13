using System.Collections.Generic;
using UnityEngine;

using HeightMapGenerators;

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
        private const int maxNumberOfTiles = 64000; // slightly less than ~2^16 -> unity's mesh vertex cont limitation 

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

        protected float maxElevation, minElevation, averageElevation, waterLevelElevation;

        public Coord[,] tiles;
        public abstract List<Coord> getTileVertices();

        // act as indices for the SquareNeighbors array
        public enum SquareDirections : byte
        {
            Top = 0,
            Right = 1,
            Bottom = 2,
            Left = 3
        }

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
}
