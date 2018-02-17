using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Regions;
using Utilities.PriorityQueue;

namespace Pathfinding
{
    public class PathResult
    {
        protected List<PathTile> pathTiles; // goal tile is first in this list
        protected List<PathTile> exploredPathTiles;

        public bool reachedGoal { get; set; }
        public float pathCost { get; set; }

        public PathResult()
        {
            pathTiles = new List<PathTile>();
            exploredPathTiles = new List<PathTile>();
            reachedGoal = false;
        }

        public List<Tile> getTilesOnPath()
        {
            List<Tile> tilesOnPath = new List<Tile>();
            foreach (PathTile pt in pathTiles)
            {
                tilesOnPath.Add(pt.tile);
            }
            return tilesOnPath;
        }

        public List<Tile> getTilesOnPathStartFirst()
        {
            List<Tile> tilesOnPath = getTilesOnPath();
            // reverse the order to be start tile firsts
            tilesOnPath.Reverse();
            return tilesOnPath;
        }

        public List<Tile> getExploredTiles()
        {
            List<Tile> exploredTiles = new List<Tile>();
            foreach (PathTile pt in exploredPathTiles)
            {
                exploredTiles.Add(pt.tile);
            }
            return exploredTiles;
        }

        public void addPathtile(PathTile pt)
        {
            this.pathTiles.Add(pt);
        }

        public void addExploredPathtile(PathTile pt)
        {
            this.exploredPathTiles.Add(pt);
        }

        public string computeHashString()
        {
            string hash = "";
            foreach (Tile tile in getExploredTiles())
            {
                hash += tile.index;
            }
            return hash;
        }
    }

    // to be used as key in a dictionary
    public interface IKeyable<T1> { T1 getKey(); }

    public class PathTile : IKeyable<Vector2Int>
    {
        public Tile tile;
        public int depth;

        public PathTile(Tile tile) { this.tile = tile; }

        public bool equals(PathTile pt) { return this.tile.Equals(pt.tile); }

        public Vector2Int getKey() { return this.tile.index; }
    }

    public abstract class PathFinder
    {
        public static float upElevatonPerPoint = 0.6f;
        public static float downElevatonPerPoint = 0.7f;

        public int maxDepth { get; set; }
        public float maxCost { get; set; }
        public float maxIncrementalCost { get; set; }

        public PathFinder(int maxDepth, float maxCost, float maxIncrementalCost)
        {
            this.maxDepth = maxDepth;
            this.maxCost = maxCost;
            this.maxIncrementalCost = maxIncrementalCost;
        }

        // TODO: refactor this to be handled by region
        // assumes the tiles are adjacent to each other
        public virtual float costBetween(PathTile t1, PathTile t2)
        {
            float cost = ((t1.tile.index - t2.tile.index).magnitude > 1f) ? Mathf.Sqrt(2f) : 1f; // base cost between tiles

            // cost due to elevation
            float elevationDelta = (t2.tile.coord.y - t1.tile.coord.y);
            if (elevationDelta < 0)
                cost -= elevationDelta / downElevatonPerPoint;
            else
                cost += elevationDelta / upElevatonPerPoint;

            //Debug.Log("PathFinder cost between " + t1.tile.index + " and " + t2.tile.index + ": " + cost + " elevation influence " + (elevationDelta / downElevatonPerPoint));

            // cost due to tile attributes
            //cost += t2.tile.moveCostPenalty;

            if (cost > this.maxIncrementalCost)
                return float.PositiveInfinity;
            return cost;
        }

        public abstract PathResult pathFromTo(Region region, Tile start, Tile goal, bool playersCanBlockPath = false);
    }

    public class LongDistancePathFinder : PathFinder
    {
        private static int _maxDepth = 50;
        private static float _maxCost = 500;

        DijkstraPathFinder DijsktraPF;
        AstarPathFinder AstarPF;

        public LongDistancePathFinder(int maxDepth, float maxCost, float maxIncrementalCost) : base(maxDepth, maxCost, maxIncrementalCost)
        {
            DijsktraPF = new DijkstraPathFinder(maxDepth, maxCost, maxIncrementalCost);
            AstarPF = new AstarPathFinder(_maxDepth, _maxCost, maxIncrementalCost);
        }

        override
        public PathResult pathFromTo(Region region, Tile start, Tile goal, bool playersCanBlockPath = false)
        {
            // attempt normal Dijsktra pathfinder first
            PathResult pr = DijsktraPF.pathFromTo(
                            region,
                            start,
                            goal,
                            playersCanBlockPath
                            );

            if (pr.reachedGoal)
            {
                return pr;
            }

            // get full path to tile even if its out of range
            PathResult prA = AstarPF.pathFromTo(
                            region,
                            start,
                            goal,
                            playersCanBlockPath
                            );

            // get move range
            PathResult prD = DijsktraPF.pathFromTo(
                            region,
                            start,
                            new Tile(new Coord(new Vector3(float.MaxValue, float.MaxValue, float.MaxValue)), int.MaxValue, int.MaxValue),
                            playersCanBlockPath
                            );

            // get the last tile given by astar pathfinder to goal that is still within move range
            Tile _goal = null;
            if (prA.reachedGoal)
            {
                foreach (Tile t in prA.getTilesOnPathStartFirst())
                {
                    bool outOfRange = true;
                    foreach (Tile explored in prD.getExploredTiles())
                    {
                        if (t.coord.getPos() == explored.coord.getPos())
                        {
                            _goal = t;
                            outOfRange = false;
                            break;
                        }
                    }
                    if (outOfRange)
                        break;
                }
            }

            if (_goal != null)
            {
                return DijsktraPF.pathFromTo(
                            region,
                            start,
                            _goal,
                            playersCanBlockPath
                            );
            }
            else
            {
                return prD;
            }
        }
    }

    public abstract class HeuristicPathFinder : PathFinder
    {
        public static float heuristicDepthInfluence = 1e-3f; // nudges priorities for tie breaking

        public HeuristicPathFinder(int maxDepth, float maxCost, float maxIncrementalCost) : base(maxDepth, maxCost, maxIncrementalCost)
        {
        }

        override
        public PathResult pathFromTo(Region region, Tile start, Tile goal, bool playersCanBlockPath = false)
        {
            PathResult pathResult = new PathResult();

            PathTile goalPt = new PathTile(goal);

            // set up lists 
            PriorityQueue<PathTile> frontier = new PriorityQueue<PathTile>();
            Dictionary<Vector2Int, PathTile> explored = new Dictionary<Vector2Int, PathTile>();
            Dictionary<Vector2Int, PathTile> previous = new Dictionary<Vector2Int, PathTile>();
            Dictionary<Vector2Int, float> costs = new Dictionary<Vector2Int, float>();

            PathTile crt;

            crt = new PathTile(start);
            crt.depth = 0;

            frontier.Enqueue(crt, 0);
            previous[crt.tile.index] = null;
            costs[crt.tile.index] = 0;

            // start pathfinding
            while (!frontier.IsEmpty())
            {
                // get current 
                crt = frontier.Dequeue();

                // record that the tile was explored
                explored[crt.tile.index]= crt;

                if (crt.equals(goalPt))
                {
                    // reached goal; search complete
                    pathResult.reachedGoal = true;
                    pathResult.pathCost = costs[crt.tile.index];
                    break;
                }

                // get neighbor tiles
                List<PathTile> neighbors = new List<PathTile>();
                foreach (Tile neighborTile in region.getTileNeighbors(crt.tile.index))
                {
                    PathTile neighbor = new PathTile(neighborTile);
                    //neighborPt.cost = crt.cost + costBetween(crt, neighborPt);
                    neighbor.depth = crt.depth + 1;
                    neighbors.Add(neighbor);
                }

                // add neighbor tiles to search
                float cost, priority;
                foreach (PathTile neighbor in neighbors)
                {

                    // check if exceeding max depth
                    if (neighbor.depth > maxDepth)
                    {
                        break;
                    }

                    // compute cost
                    float _cost = costBetween(crt, neighbor);

                    //// check if path is blocked by another player
                    //if (playersCanBlockPath && GameControl.gameSession.checkForPlayersAt(neighbor.tile) != null)
                    //{
                    //    if (!neighbor.CompareTo(goalPt))  // ensures that you can move to a tile with an enemy
                    //        _cost = float.PositiveInfinity; // set highest cost to signify that the tile is unreachable
                    //}

                    cost = costs[crt.tile.index] + _cost;

                    if (cost <= maxCost)
                    {
                        if (!costs.ContainsKey(neighbor.tile.index) || cost < costs[neighbor.tile.index])
                        {
                            costs[neighbor.tile.index] = cost;

                            // compute heuristic priority
                            priority = cost + heuristic(region, neighbor, goalPt);
                            priority -= neighbor.depth * heuristicDepthInfluence; // makes so that tiles closest to goal are more eagerly explored

                            frontier.Enqueue(neighbor, priority);

                            previous[neighbor.tile.index] = crt;
                        }
                    }
                }
            }

            // build list of tiles on path if goal was reached
            if (pathResult.reachedGoal)
            {
                pathResult.addPathtile(goalPt);

                crt = previous[goal.index];

                while (crt != null)
                {
                    pathResult.addPathtile(crt);
                    crt = previous[crt.tile.index];
                }
            }

            foreach (PathTile pt in explored.Values)
            {
                pathResult.addExploredPathtile(pt);
            }

            return pathResult;
        }

        // *** HEURISTIC COMPUTATIONS *** ///

        public abstract float heuristic(Region region, PathTile start, PathTile goal);
    }

    public class AstarPathFinder : HeuristicPathFinder
    {
        public AstarPathFinder(int maxDepth, float maxCost, float maxIncrementalCost) : base(maxDepth, maxCost, maxIncrementalCost)
        {
        }

        override
        public float heuristic(Region region, PathTile start, PathTile goal)
        {
            float cost = region.distanceBetweenTiles(start.tile, goal.tile);
            float elevationDelta = start.tile.coord.y - goal.tile.coord.y;
            if (elevationDelta < 0)
                cost += -elevationDelta / downElevatonPerPoint;
            else
                cost += elevationDelta / upElevatonPerPoint;
            return cost;
        }
    }

    public class DijkstraPathFinder : HeuristicPathFinder
    {
        public DijkstraPathFinder(int maxDepth, float maxCost, float maxIncrementalCost) : base(maxDepth, maxCost, maxIncrementalCost)
        {
        }

        override
        public float heuristic(Region region, PathTile start, PathTile goal)
        {
            // Dijkstra can be considered as a special case of A* where heuristic is always equal zero
            return 0;
        }
    }

    public class DijkstraUniformCostPathFinder : DijkstraPathFinder
    {
        private float uniformCost;

        public DijkstraUniformCostPathFinder(float uniformCost, int maxDepth, float maxCost, float maxIncrementalCost = 0) : base(maxDepth, maxCost, maxIncrementalCost)
        {
            this.uniformCost = uniformCost;
        }

        override
        public float costBetween(PathTile t1, PathTile t2)
        {
            return uniformCost;
        }
    }
}