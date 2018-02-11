using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Regions;
using HexRegions;
using Pathfinding;

[AddComponentMenu("Input-Control")]
public class InputControl : MonoBehaviour
{
    [Range(0,100)]
    public int maxActionPoints = 50;

    [Range(0, 100)]
    public int actionPoints = 50;

    public bool showGUI = true;

    int menuWidth = 200;
    int menuHeight = 300;

    static bool selectionOrder = true;
    static bool moveMode = true;

    public static Tile SelectedTile { get { return selectedTile; } }

    static Tile mouseOverTile, selectedTile, firstClickedTile, secondClickedTile;
    static int[] currentSelectedIndex;
    static Vector3 ySelectionOffset = new Vector3(0, 0.05f, 0);

    static GUIStyle guiStyle;

    // selection game object
    GameObject mouseOverIndicator, selectionIndicator;

    // path related game objects
    GameObject pathIndicator;
    GameObject pathExploredIndicator;

    PathFinder DijsktraPF, AstarPF;
    PathResult pathResult;

    private GameSession gameSession;

    string attemptedMoveMessage;

    void Start()
    {
        // setup all vars

        GameObject go = GameObject.FindGameObjectWithTag("GameSession");
        gameSession = (GameSession)go.GetComponent(typeof(GameSession));

        mouseOverIndicator = GameObject.FindGameObjectWithTag("MouseOverIndicator");
        selectionIndicator = GameObject.FindGameObjectWithTag("SelectionIndicator");
        pathIndicator = GameObject.FindGameObjectWithTag("PathIndicator");
        pathExploredIndicator = GameObject.FindGameObjectWithTag("PathExploredIndicator");

        // create GUI style
        guiStyle = new GUIStyle();
        guiStyle.alignment = TextAnchor.LowerLeft;
        guiStyle.normal.textColor = Utilities.hexToColor("#153870");

        AstarPF = new AstarPathFinder(maxDepth: 50, maxCost: 1000, maxIncrementalCost: maxActionPoints);
        DijsktraPF = new DijkstraPathFinder(maxDepth: maxActionPoints,
                                            maxCost: actionPoints,
                                            maxIncrementalCost: maxActionPoints
                                            );
    }

    void Update()
    {
        /// *** RAYCASTING FOR SELECTING TILES PART *** ///

        // update selection tile
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hitInfo;
        if (Physics.Raycast(ray, out hitInfo))
        {
            GameObject hitObject = hitInfo.collider.transform.gameObject;
            if (hitObject == null)
            {
                // nothing to do
            }
            else
            {
                if ((mouseOverTile = gameSession.getRegion().getTileAt(hitInfo.point)) != null)
                {
                    mouseOverIndicator.transform.position = mouseOverTile.coord.getPos() + ySelectionOffset; // move mouseOverIndicator
                }
                else
                {
                    // do nothing
                }
            }
        }

        // left click
        bool leftMouseClick = Input.GetMouseButtonDown(0);
        if (leftMouseClick && mouseOverTile != null)
        {
            selectionIndicator.transform.position = mouseOverTile.coord.getPos() + ySelectionOffset; // move selectionTileIndicator
            selectedTile = mouseOverTile;
        }

        // right click
        bool rightMouseClick = Input.GetMouseButtonDown(1);
        if (rightMouseClick && !moveMode)
        {
            changeMoveMode();
        }

        /*PATHFINDING PART */
        if (moveMode)
        {
            DijsktraPF.maxDepth = maxActionPoints;
            DijsktraPF.maxCost = actionPoints;

            // draw move range only 
            // TODO optimize this to not recalculate path on every frame
            if (firstClickedTile != null)
            {
                StartCoroutine(
                    displayPath(
                        DijsktraPF.pathFromTo(
                            gameSession.getRegion(),
                            firstClickedTile,
                            new Tile(new Coord(new Vector3(float.MaxValue, float.MaxValue, float.MaxValue)), int.MaxValue, int.MaxValue),
                            playersCanBlockPath: true
                            ),
                        writeToGlobalPathResult: false,
                        displayTimeInSeconds: 0.05f,
                        drawExplored: true));

                if (mouseOverTile != null)
                {
                    // draw path to the selected tile 
                    // TODO optimize this to not recalculate path on every frame
                    if (mouseOverTile != null)
                        StartCoroutine(
                            displayPath(
                                 AstarPF.pathFromTo(
                                     gameSession.getRegion(),
                                     firstClickedTile,
                                     mouseOverTile,
                                     playersCanBlockPath: true
                                     ),
                                 writeToGlobalPathResult: false,
                                 displayTimeInSeconds: 0.05f,
                                 drawExplored: false,
                                 drawCost: true
                                 ));
                }
            }
            
            // *** MOUSE CLICKS CONTROL PART *** //
            if (rightMouseClick && mouseOverTile != null)
            {
                if (selectionOrder)
                {
                    firstClickedTile = mouseOverTile;
                    // draw path using A* pathfinder (not Dijkstra) for faster performance
                }
                else
                {
                    secondClickedTile = mouseOverTile;

                    // check if right clicked same tile twice
                    if (firstClickedTile.equals(secondClickedTile))
                    {
                        //pathResult = GameControl.gameSession.playerAttemptMove(firstClickedTile, out attemptedMoveMessage, movePlayer: true);
                        StartCoroutine(displayPath(pathResult));
                        changeMoveMode();
                    }
                    else
                    {
                        // clicked another tile: overwrite first selection
                        firstClickedTile = mouseOverTile;

                        // flip selection order an extra time
                        selectionOrder = !selectionOrder;
                    }
                }
                // flip selection order
                selectionOrder = !selectionOrder;
            }

            // draw path to selected tile
            // TODO optimize this to not recalculate path every frame
            if (firstClickedTile != null)
            {
                // draw move path
                StartCoroutine(
                    displayPath(
                        DijsktraPF.pathFromTo(
                            gameSession.getRegion(),
                            firstClickedTile,
                            mouseOverTile,
                            playersCanBlockPath: true
                            ),
                        writeToGlobalPathResult: false,
                        displayTimeInSeconds: 0.05f,
                        drawExplored: false));

            }
            
        }
    }

    public static void changeMoveMode()
    {
        moveMode = !moveMode;
    }

    public static void resetMouseControlView()
    {
        moveMode = false;
    }

    // FOR DEBUGGING PURPOSES
    public IEnumerator computePath(PathFinder pathFinder, Tile start, Tile goal, float displayTimeInSeconds = 2f, bool writeToGlobalPathResult = true)
    {
        if (start != null && goal != null)
        {
            // compute path
            PathResult pr = pathFinder.pathFromTo(gameSession.getRegion(), start, goal);
            yield return displayPath(pr, displayTimeInSeconds, writeToGlobalPathResult, drawExplored: true);
        }
    }

    public IEnumerator displayPath(PathResult pr, float displayTimeInSeconds = 2f, bool writeToGlobalPathResult = true, bool drawExplored = false, bool drawCost = false)
    {
        List<GameObject> pathIndicators = null;
        List<GameObject> exploredIndicators = null;
        GameObject costIndicator = null;

        if (pr != null)
        {
            if (writeToGlobalPathResult)
                pathResult = pr;

            // reset indicator lists
            pathIndicators = new List<GameObject>();
            exploredIndicators = new List<GameObject>();

            // draw path info
            foreach (Tile tile in pr.getTilesOnPathStartFirst())
            {
                GameObject _pathIndicator = Instantiate(pathIndicator);
                _pathIndicator.transform.parent = this.transform;
                _pathIndicator.transform.position = tile.coord.getPos() + ySelectionOffset;
                pathIndicators.Add(_pathIndicator);
            }

            if (drawExplored)
            {
                // draw explored info
                foreach (Tile tile in pr.getExploredTiles())
                {
                    GameObject exploredIndicator = Instantiate(pathExploredIndicator);
                    exploredIndicator.transform.parent = this.transform;
                    exploredIndicator.transform.position = tile.coord.getPos() + ySelectionOffset;
                    exploredIndicators.Add(exploredIndicator);
                }
            }

            //if (drawCost)
            //{
            //    if (pr.reachedGoal)
            //    {
            //        costIndicator = Instantiate(Resources.Load("Prefabs/Text/TileCostObject"), this.transform) as GameObject;

            //        // set position
            //        Tile tile = pr.getTilesOnPath()[0];
            //        costIndicator.transform.position = tile.coord.getPos() + ySelectionOffset * 2;

            //        // set text
            //        string pathCost = Mathf.CeilToInt(pr.pathCost) + "AP";
            //        costIndicator.transform.GetChild(0).GetComponent<TextMesh>().text = pathCost;

            //        costIndicator.transform.LookAt(Camera.main.transform);
            //        costIndicator.transform.forward = -costIndicator.transform.forward;
            //    }
            //}
        }

        // wait for some time
        yield return new WaitForSeconds(displayTimeInSeconds);

        // destroy indicators
        if (pathIndicators != null)
            foreach (GameObject go in pathIndicators)
            {
                Destroy(go);
            }
        if (exploredIndicators != null)
            foreach (GameObject go in exploredIndicators)
            {
                Destroy(go);
            }
        if (costIndicator != null)
            Destroy(costIndicator);
    }

    void OnGUI()
    {
        if (showGUI)
        {
            if (selectedTile != null)
            {
                string currentSelection = "Selected " + selectedTile.coord.getPos();
                GUI.Box(new Rect(Screen.width - menuWidth, Screen.height - menuHeight, menuWidth, menuHeight), currentSelection);
            }
            if (firstClickedTile != null)
            {
                string leftSelection = "First tile\n" + firstClickedTile.coord.getPos();
                GUI.Label(new Rect(0, Screen.height - menuHeight, menuWidth, menuHeight), leftSelection, guiStyle);
            }
            if (secondClickedTile != null)
            {
                string rightSelection = "Second tile\n" + secondClickedTile.coord.getPos();
                GUI.Label(new Rect(0, Screen.height - 2 * menuHeight, menuWidth, menuHeight), rightSelection, guiStyle);
            }
            if (pathResult != null)
            {
                string pathInfo = "Path cost:" + pathResult.pathCost;
                foreach (Tile tile in pathResult.getTilesOnPathStartFirst())
                {
                    pathInfo += "\n" + tile.index;
                }
                GUI.Label(new Rect(menuWidth, Screen.height - menuHeight, menuWidth, menuHeight), pathInfo, guiStyle);
            }
        }
    }
}