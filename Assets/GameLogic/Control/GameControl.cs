using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Regions;
using Utilities.Misc;
using Pathfinding;

[AddComponentMenu("Input-Control")]
public class GameControl : MonoBehaviour
{
    #region Configuration

    [Header("Movement configuration")]
    [Range(0,100)]
    public int maxActionPoints = 50;
    [Range(0, 100)]
    public int actionPoints = 50;

    [Header("GUI configuration")]
    public bool showGUI = true;
    public int guiMenuWidth = 200;
    public int guiMenuHeight = 300;

    [Header("Pathfinding configuration")]
    public InputModifiers.KeyboardControlConfiguration pathfindKeyControl;

    [Header("Indicators")]
    #region SelectionIndicators
    public GameObject mouseOverIndicator;
    public GameObject selectionIndicator;
    #endregion
    #region PathIndicators
    // path related game objects
    public GameObject pathIndicator;
    public GameObject pathExploredIndicator;
    #endregion

    #endregion

    #region PrivateVariables

    private bool selectionOrder = true;
    private bool moveMode = true;

    private Tile mouseOverTile, selectedTile, firstClickedTile, secondClickedTile;
    public Tile SelectedTile { get { return selectedTile; } }

    PathFinder DijsktraPF, AstarPF;
    PathResult pathResult;

    private GameSession gameSession;

    DoubleClickDetector dcd;

    static GUIStyle guiStyle;

    #endregion

    void Start()
    {
        gameSession = (GameSession)GameObject.FindGameObjectWithTag("GameSession").GetComponent(typeof(GameSession));

        AstarPF = new AstarPathFinder(maxDepth: 50, maxCost: 1000, maxIncrementalCost: maxActionPoints);
        DijsktraPF = new DijkstraPathFinder(maxDepth: maxActionPoints,
                                            maxCost: actionPoints,
                                            maxIncrementalCost: maxActionPoints
                                            );

        dcd = gameObject.GetComponent<DoubleClickDetector>();

        // create GUI style
        guiStyle = new GUIStyle();
        guiStyle.alignment = TextAnchor.LowerLeft;
        guiStyle.normal.textColor = Tools.hexToColor("#153870");

        mouseOverIndicator = Instantiate(mouseOverIndicator, transform);
        selectionIndicator = Instantiate(selectionIndicator, transform);
    }

    void Update()
    {
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
                    mouseOverIndicator.transform.position = mouseOverTile.coord.getPos(); // move mouseOverIndicator
                }
                else
                {
                    // do nothing
                }
            }
        }

        // left click
        if (dcd.IsDoubleClick() && mouseOverTile != null)
        {
            selectionIndicator.transform.position = mouseOverTile.coord.getPos(); // move selectionTileIndicator
            selectedTile = mouseOverTile;
        }

        // right click
        bool rightMouseClick = Input.GetMouseButtonDown(1);
        if (pathfindKeyControl.isActivated())
        {
            ChangeMoveMode();
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
                DrawMoveRange();

                if (mouseOverTile != null)
                {
                    DrawPathTo(mouseOverTile);
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
                    if (firstClickedTile.Equals(secondClickedTile))
                    {
                        //pathResult = GameControl.gameSession.playerAttemptMove(firstClickedTile, out attemptedMoveMessage, movePlayer: true);
                        StartCoroutine(displayPath(pathResult));
                        ChangeMoveMode();
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
        }
    }

    public void ChangeMoveMode()
    {
        moveMode = !moveMode;
    }

    public void resetMouseControlView()
    {
        moveMode = false;
    }

    public void DrawMoveRange() {
        StartCoroutine(
            displayPath(
                DijsktraPF.pathFromTo(
                    gameSession.getRegion(),
                    firstClickedTile,
                    new Tile(new Coord(new Vector3(float.MaxValue, float.MaxValue, float.MaxValue)), int.MaxValue, int.MaxValue),
                    playersCanBlockPath: true
                    ),
                writeToGlobalPathResult: false,
                displayTimeInSeconds: 0.01f,
                drawExplored: false));
    }

    public void DrawPathTo(Tile tile)
    {
        StartCoroutine(
            displayPath(
                 AstarPF.pathFromTo(
                     gameSession.getRegion(),
                     firstClickedTile,
                     tile,
                     playersCanBlockPath: true
                     ),
                 writeToGlobalPathResult: false,
                 displayTimeInSeconds: 0.01f,
                 drawExplored: false,
                 drawCost: true
                 ));
    }

    public IEnumerator displayPath(PathResult pr, string tag = "", float displayTimeInSeconds = 2f, bool drawPath = true, bool writeToGlobalPathResult = true, bool drawExplored = false, bool drawCost = false)
    {
        string currentHash = pr.computeHashString();

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

                if (drawPath)
                {
                    // draw path info
                    foreach (Tile tile in pr.getTilesOnPathStartFirst())
                    {
                        GameObject _pathIndicator = Instantiate(pathIndicator, this.transform);
                        _pathIndicator.transform.position = tile.coord.getPos();
                        pathIndicators.Add(_pathIndicator);
                    }
                }

                if (drawExplored)
                {
                    // draw explored info
                    foreach (Tile tile in pr.getExploredTiles())
                    {
                        GameObject exploredIndicator = Instantiate(pathExploredIndicator, transform);
                        exploredIndicator.transform.position = tile.coord.getPos();
                        exploredIndicators.Add(exploredIndicator);
                    }
                }
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
    }

    void OnGUI()
    {
        if (showGUI)
        {
            if (selectedTile != null)
            {
                string currentSelection = "Selected " + selectedTile.coord.getPos();
                GUI.Box(new Rect(Screen.width - guiMenuWidth, Screen.height - guiMenuHeight, guiMenuWidth, guiMenuHeight), currentSelection);
            }
            if (firstClickedTile != null)
            {
                string leftSelection = "First tile\n" + firstClickedTile.coord.getPos();
                GUI.Label(new Rect(0, Screen.height - guiMenuHeight, guiMenuWidth, guiMenuHeight), leftSelection, guiStyle);
            }
            if (secondClickedTile != null)
            {
                string rightSelection = "Second tile\n" + secondClickedTile.coord.getPos();
                GUI.Label(new Rect(0, Screen.height - 2 * guiMenuHeight, guiMenuWidth, guiMenuHeight), rightSelection, guiStyle);
            }
            if (pathResult != null)
            {
                string pathInfo = "Path cost:" + pathResult.pathCost;
                foreach (Tile tile in pathResult.getTilesOnPathStartFirst())
                {
                    pathInfo += "\n" + tile.index;
                }
                GUI.Label(new Rect(guiMenuWidth, Screen.height - guiMenuHeight, guiMenuWidth, guiMenuHeight), pathInfo, guiStyle);
            }
        }
    }
}