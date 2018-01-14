//
//Filename: KeyboardCameraControl.cs
//

using System.Collections;
using UnityEngine;
using System;

using Regions;

[AddComponentMenu("Camera-Control/Keyboard")]
public class CameraControl : MonoBehaviour
{
    public static float globalSensitivity = 2F;

    public static Vector3 restrictionCenterPoint, viewCenterPoint;

    static float viewCenterOffset = 200f;

    private static bool toggleCenterPointFocus = false;
    private static bool centeringOnPlayer = false;

    static float viewCenterOnPlayerOffset = 75f; // how far from player position the camera will be set when focusing on player
    static float viewCenterOnPlayerLimiterInertia = 0.5f; // how 

    // distances in Unity units
    static int cameraLimitDistance = 500; // how far camera can move away from the player
    static int minCameraToGroundDistance = 2; // how close to ground the camera can go before limiter will start resisting
    static int maxCameraToGroundDistance = 200; // how high camera can go before limiter will start resisting

    static float limiterInertia = 0.1f;
    // speed limiter must be adjusted given maxCameraToGroundDistance; shorter max dist requires higher limiter
    static float cameraTooHighSpeedLimiter = 1.5f; // lower means less resistance
    static float cameraTooLowSpeedLimiter = 5f; // this one needs to be resistive otherwise camera will dip into objects

    // Keyboard axes buttons in the same order as Unity
    public enum KeyboardAxis { Horizontal = 0, Vertical = 1, None = 3 }

    [System.Serializable]
    // Handles left modifiers keys (Alt, Ctrl, Shift)
    public class Modifiers
    {
        public bool leftAlt;
        public bool leftControl;
        public bool leftShift;

        public bool checkModifiers()
        {
            return (!leftAlt ^ Input.GetKey(KeyCode.LeftAlt)) &&
                (!leftControl ^ Input.GetKey(KeyCode.LeftControl)) &&
                (!leftShift ^ Input.GetKey(KeyCode.LeftShift));
        }
    }

    [System.Serializable]
    // Handles common parameters for translations and rotations
    public class KeyboardControlConfiguration
    {
        public bool activate;
        public KeyboardAxis keyboardAxis;
        public Modifiers modifiers;
        public float sensitivity;

        public bool isActivated()
        {
            return activate && keyboardAxis != KeyboardAxis.None && modifiers.checkModifiers();
        }
    }

    // Yaw default configuration
    public KeyboardControlConfiguration yaw = new KeyboardControlConfiguration { keyboardAxis = KeyboardAxis.Horizontal, modifiers = new Modifiers { leftAlt = true }, sensitivity = globalSensitivity };

    // Pitch default configuration
    public KeyboardControlConfiguration pitch = new KeyboardControlConfiguration { keyboardAxis = KeyboardAxis.Vertical, modifiers = new Modifiers { leftAlt = true }, sensitivity = globalSensitivity };

    // Roll default configuration
    public KeyboardControlConfiguration roll = new KeyboardControlConfiguration { keyboardAxis = KeyboardAxis.Horizontal, modifiers = new Modifiers { leftAlt = true, leftControl = true }, sensitivity = globalSensitivity };

    // Vertical translation default configuration
    public KeyboardControlConfiguration verticalTranslation = new KeyboardControlConfiguration { keyboardAxis = KeyboardAxis.Vertical, modifiers = new Modifiers { leftControl = true }, sensitivity = globalSensitivity };

    // Horizontal translation default configuration
    public KeyboardControlConfiguration horizontalTranslation = new KeyboardControlConfiguration { keyboardAxis = KeyboardAxis.Horizontal, sensitivity = globalSensitivity };

    // Depth (forward/backward) translation default configuration
    public KeyboardControlConfiguration depthTranslation = new KeyboardControlConfiguration { keyboardAxis = KeyboardAxis.Vertical, sensitivity = globalSensitivity };

    // Default unity names for keyboard axes
    public string keyboardHorizontalAxisName = "Horizontal";
    public string keyboardVerticalAxisName = "Vertical";

    private string[] keyboardAxesNames;

    private Region region;

    void Start()
    {
        keyboardAxesNames = new string[] { keyboardHorizontalAxisName, keyboardVerticalAxisName };

        transform.position = getCameraPositionPlayerCentered();

        GameObject go = GameObject.FindGameObjectWithTag("GameSession");
        region = ((GameSession)go.GetComponent(typeof(GameSession))).getRegion();

        restrictionCenterPoint = new Vector3(0, 0, 0); // GameControl.gameSession.humanPlayer.getPos();
        viewCenterPoint = region.getTileAt(restrictionCenterPoint).coord.getPos();
    }

    // LateUpdate  is called once per frame after all Update are done
    void LateUpdate()
    {

        if (toggleCenterPointFocus && !centeringOnPlayer)
        {
            toggleCenterPointFocus = false;
            centeringOnPlayer = true;
            StartCoroutine(startCenteringOnPlayer());
        }

        Vector3 cameraPos = this.transform.position,
            cameraDir = this.transform.forward;

        cameraPos.y = 0;
        cameraDir.y = 0;

        // update view center 
        restrictionCenterPoint = new Vector3(0, 0, 0); // GameControl.gameSession.humanPlayer.getPos();
        viewCenterPoint = cameraPos + cameraDir * viewCenterOffset;

        // COMPUTE MOVEMENT
        if (yaw.isActivated())
        { // camera rotate left/right
            float rotationX = Input.GetAxis(keyboardAxesNames[(int)yaw.keyboardAxis]) * yaw.sensitivity;
            transform.Rotate(0, rotationX, 0, Space.World);
        }
        if (pitch.isActivated())
        { // camera rotate up/down
            float rotationY = Input.GetAxis(keyboardAxesNames[(int)pitch.keyboardAxis]) * pitch.sensitivity;
            transform.Rotate(-rotationY, 0, 0);
        }
        //if (roll.isActivated()) {
        //    float rotationZ = Input.GetAxis(keyboardAxesNames[(int)roll.keyboardAxis]) * roll.sensitivity;
        //    transform.Rotate(0, 0, rotationZ, Space.World);
        //}
        if (verticalTranslation.isActivated())
        {
            float translateY = Input.GetAxis(keyboardAxesNames[(int)verticalTranslation.keyboardAxis]) * verticalTranslation.sensitivity;
            transform.Translate(0, translateY, 0, Space.World);
        }
        if (horizontalTranslation.isActivated())
        {
            Vector3 direction = transform.right;
            direction.y = 0;
            direction.Normalize();
            transform.Translate(Input.GetAxis(keyboardAxesNames[(int)horizontalTranslation.keyboardAxis]) * horizontalTranslation.sensitivity * direction, Space.World);
        }
        if (depthTranslation.isActivated())
        { // camera move forward/backword
            Vector3 direction = transform.forward;
            direction.y = 0;
            direction.Normalize();
            transform.Translate(Input.GetAxis(keyboardAxesNames[(int)depthTranslation.keyboardAxis]) * depthTranslation.sensitivity * direction, Space.World);
        }

        limitCamera();
    }

    public static void toggleCenterOnPlayer()
    {
        toggleCenterPointFocus = !toggleCenterPointFocus;
    }

    private IEnumerator startCenteringOnPlayer(float centeredThreshold = 1f)
    {
        Vector3 vectorToPlayer = getCameraPositionPlayerCentered() - transform.position;
        while (vectorToPlayer.magnitude > centeredThreshold)
        {
            // move cam towards player
            transform.position += vectorToPlayer.normalized * vectorToPlayer.magnitude * viewCenterOnPlayerLimiterInertia;
            // update distance to player
            vectorToPlayer = getCameraPositionPlayerCentered() - transform.position;
            yield return null;
        }
        centeringOnPlayer = false;
    }

    private void limitCamera()
    {
        // check if camera is out of bounds 
        Vector3 posRelative = transform.position - restrictionCenterPoint;
        if (posRelative.x > cameraLimitDistance)
        {
            transform.position -= new Vector3(posRelative.x - cameraLimitDistance, 0, 0);
        }
        else if (posRelative.x < -cameraLimitDistance)
        {
            transform.position -= new Vector3(posRelative.x + cameraLimitDistance, 0, 0);
        }
        if (posRelative.z > cameraLimitDistance)
        {
            transform.position -= new Vector3(0, 0, posRelative.z - cameraLimitDistance);
        }
        else if (posRelative.z < -cameraLimitDistance)
        {
            transform.position -= new Vector3(0, 0, posRelative.z + cameraLimitDistance);
        }
        // adjust camera height based on terrain
        float waterLevel = 0;// GameControl.gameSession.mapGenerator.getRegion().getWaterLevelElevation();
        float offsetAboveWater = transform.position.y - (waterLevel) - minCameraToGroundDistance;
        if (offsetAboveWater < 0)
        { // camera too low based on water elevation
            transform.position -= new Vector3(0, offsetAboveWater, 0) * limiterInertia * cameraTooLowSpeedLimiter;
        }
        try
        {
            Vector3 tileBelow = region.getTileAt(transform.position).coord.getPos();

            float offsetAboveFloor = transform.position.y - (tileBelow.y) - minCameraToGroundDistance;
            float offsetBelowCeiling = tileBelow.y + maxCameraToGroundDistance - (transform.position.y);

            if (offsetAboveFloor < 0)
            { // camera too low based on tile height
                transform.position -= new Vector3(0, offsetAboveFloor, 0) * limiterInertia * cameraTooLowSpeedLimiter;
            }
            else if (offsetBelowCeiling < 0)
            { // camera too high 
                transform.position += new Vector3(0, offsetBelowCeiling, 0) * limiterInertia * cameraTooHighSpeedLimiter;
            }
        }
        catch (NullReferenceException e)
        {
            // do nothing
        }

    }

    public Vector3 getCameraPositionPlayerCentered()
    {
        return /*GameControl.gameSession.humanPlayer.getPos()*/ new Vector3(0, 0, 0) - transform.forward * viewCenterOnPlayerOffset;
    }
}