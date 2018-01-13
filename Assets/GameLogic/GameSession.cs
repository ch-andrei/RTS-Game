using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Regions;
using HexRegions;
using HeightMapGenerators;

public class GameSession : MonoBehaviour
{
    [Range(1, 100000000)]
    public int seed = 0;

    public bool useRandomSeed;

    public bool drawGizmos = true;
    [Range(1, 100)]
    public int gizmoSize = 5;
    [Range(0, 100)]
    public int gizmoSkip = 0;

    public bool regenerate = false;

    public RegionGenConfig regionGenConfig;
    public HeightMapConfig heightMapConfig;
    public FastPerlinNoiseConfig noiseConfig;
    public ErosionConfig erosionConfig;

    private Region region;

    void Awake()
    {
        Debug.Log("Awakening game session.");
        Application.targetFrameRate = 30;

        int seed = useRandomSeed ? UnityEngine.Random.Range(int.MinValue, int.MaxValue) : this.seed;

        this.region = new HexRegion(seed, regionGenConfig, heightMapConfig, noiseConfig, erosionConfig);
    }

    public Region getRegion() {
        return this.region;
    }

    // Use this for initialization
    void Start()
    {
        Debug.Log("Starting game session.");
        Awake();
    }

    public void Update()
    {
        if (region == null)
        {
            Awake();
        }
    }

    public Color hexToColor(string hex)
    {
        return Utilities.hexToColor(hex);
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos)
        {
            return;
        }

        if (regenerate)
        {
            regenerate = false;
            Awake();
        }

        if (region != null)
        {
            Gizmos.color = hexToColor("#000000"); // black
            Gizmos.DrawCube(new Vector3(0,0,0), new Vector3(10000,0,10000));

            // set color and draw gizmos
            //int water_level = gameSession.mapGenerator.getRegion().getWaterLevelElevation();
            int order = 0;
            Color c;
            foreach (Coord tile in region.getTileVertices())
            {
                if (gizmoSkip + 1 != 0)
                {
                    order = ++order % (gizmoSkip + 1);
                    if (order != 0)
                    {
                        continue;
                    }
                }
                //if (tile.getTileType() != null)
                {
                    int elevation = (int)tile.y /*- water_level*/;
                    //if (tile.getTileType().GetType() == typeof(WaterTileType))
                    //{
                    //    //Debug.Log("water: elevation " + elevation);
                    //    if (elevation > -5)
                    //    {
                    //        c = hexToColor("#C2D2E7");
                    //    }
                    //    else if (elevation > -10)
                    //    {
                    //        c = hexToColor("#54B3F0");
                    //    }
                    //    else if (elevation > -25)
                    //    {
                    //        c = hexToColor("#067DED");
                    //    }
                    //    else if (elevation > -50)
                    //    {
                    //        c = hexToColor("#005F95");
                    //    }
                    //    else
                    //        c = hexToColor("#004176");
                    //}
                    //else if (tile.getTileType().GetType() == typeof(LandTileType))
                    {
                        //Debug.Log("water: elevation " + elevation);
                        if (elevation < 0)
                            c = hexToColor("#696300");
                        else if (elevation < 10)
                            c = hexToColor("#00C103");
                        else if (elevation < 20)
                            c = hexToColor("#59FF00");
                        else if (elevation < 30)
                            c = hexToColor("#F2FF00");
                        else if (elevation < 40)
                            c = hexToColor("#FFBE00");
                        else if (elevation < 50)
                            c = hexToColor("#FF8C00");
                        else if (elevation < 60)
                            c = hexToColor("#FF6900");
                        else if (elevation < 70)
                            c = hexToColor("#E74900");
                        else if (elevation < 80)
                            c = hexToColor("#E10C00");
                        else if (elevation < 90)
                            c = hexToColor("#971C00");
                        else if (elevation < 100)
                            c = hexToColor("#C24340");
                        else if (elevation < 115)
                            c = hexToColor("#B9818A");
                        else if (elevation < 130)
                            c = hexToColor("#988E8B");
                        else if (elevation < 160)
                            c = hexToColor("#AEB5BD");
                        else // default
                            c = hexToColor("#FFFFFF");
                    }
                    //else
                    //    c = new Color(0, 0, 0, 0);
                    Gizmos.color = c;
                    Vector3 pos = tile.getPos(); ;
                    //if (elevation < 0) {
                    //    pos.y = water_level; // if it's water, draw elevation as equal to water_level
                    //}
                    Gizmos.DrawSphere(pos, gizmoSize);
                }
            }
        }
    }
}
