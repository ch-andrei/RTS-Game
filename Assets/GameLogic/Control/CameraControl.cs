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
    public float globalSensitivity = 2f; // global movement speed

    #region MouseControlConfiguration

    // camera scrolling sensitivity
    [Header("Scrolling")]
    public float scrollingSensitivityModifier = 10f;

    // edge scrolling
    [Header("Edge scrolling")]
    public bool allowEdgeScrolling = false;
    public int edgeScrollDetectBorderThickness = 15;

    // mouse control camera translation
    [Header("Mouse Scrolling")]
    public bool allowMouseTranslation = true;
    public float mouseTranslationSensitivityModifier = 1f; // mouse translation movement speed modifier

    // mouse rotation control
    [Header("Mouse Rotation")]
    public bool allowMouseRotation = true;
    public float mouseRotationSensitivityModifier = 100f; // mouse rotation movement speed modifier

    // zoom with FOV 
    [Header("Camera zoom")]
    public bool allowCameraZoom = true;
    public float cameraZoomSensitivityModifier = 1f;
    public float cameraFovMin = 30f;
    public float cameraFovMax = 120f;

    #endregion

    #region CameraControlConfiguration

    // camera restriction
    [Header("Camera restriction")]

    public float viewCenterOffset = 200f; // camera view center point offset; calculated as this far in front of camera
    public float viewCenterOnPlayerOffset = 75f; // how far from player position the camera will be set when focusing on player
    public float viewCenterOnPlayerLimiterInertia = 0.5f; // how 

    // speed limiter must be adjusted given maxCameraToGroundDistance; shorter max dist requires higher limiter
    public float limiterInertia = 0.1f;

    public float cameraLimitDistance = 500f; // how far camera can move away from the player
    public float minCameraToGroundDistance = 2f; // how close to ground the camera can go before limiter will start resisting
    public float maxCameraToGroundDistance = 200f; // how high camera can go before limiter will start resisting

    public float cameraTooHighSpeedLimiter = 1.5f; // lower means less resistance
    public float cameraTooLowSpeedLimiter = 5f; // this one needs to be resistive otherwise camera will dip into objects

    #endregion

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

    private Vector3 mousePreviousPosition;

    private Vector3 restrictionCenterPoint, viewCenterPoint;

    private bool toggleCenterPointFocus = false;
    private bool centeringOnPlayer = false;

    private Region region;

    void Start()
    {
        transform.position = getCameraPositionPlayerCentered();

        GameObject go = GameObject.FindGameObjectWithTag("GameSession");
        region = ((GameSession)go.GetComponent(typeof(GameSession))).getRegion();

        restrictionCenterPoint = new Vector3(0, 0, 0); // GameControl.gameSession.humanPlayer.getPos();
        viewCenterPoint = region.getTileAt(restrictionCenterPoint).coord.getPos();

        mousePreviousPosition = Input.mousePosition;
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

        viewCenterPoint = cameraPos + cameraDir * viewCenterOffset;

        if (Input.GetMouseButtonDown(1))
        {
            mousePreviousPosition = Input.mousePosition;
            Debug.Log(mousePreviousPosition);
        }

        processCameraMovement();
        processCameraRotation();
        processCameraZoom();

        limitCamera();
    }

    public void toggleCenterOnPlayer()
    {
        toggleCenterPointFocus = !toggleCenterPointFocus;
    }

    private void processCameraMovement() {
        Vector3 movement = Vector3.zero;

        // keyboard and edge scrolling

        Vector3 mouseDelta = Input.mousePosition - mousePreviousPosition;
        Vector3 forward = transform.forward;
        Vector3 right = transform.right;

        forward.y = 0;
        right.y = 0;

        if (Input.GetKey(KeyCode.W) || (allowEdgeScrolling && Input.mousePosition.y >= Screen.height - edgeScrollDetectBorderThickness))
        {
            movement += forward;
        }
        if (Input.GetKey(KeyCode.S) || (allowEdgeScrolling && Input.mousePosition.y <= edgeScrollDetectBorderThickness))
        {
            movement -= forward;
        }
        if (Input.GetKey(KeyCode.A) || (allowEdgeScrolling && Input.mousePosition.x <= edgeScrollDetectBorderThickness))
        {
            movement -= right;
        }
        if (Input.GetKey(KeyCode.D) || (allowEdgeScrolling && Input.mousePosition.x >= Screen.width - edgeScrollDetectBorderThickness))
        {
            movement += right;
        }
        if (Input.GetKey(KeyCode.Q))
        {
            movement += Vector3.down;
        }
        if (Input.GetKey(KeyCode.E))
        {
            movement += Vector3.up;
        }

        // scrolling with mouse
        if (allowMouseTranslation && Input.GetMouseButton(2))
        {
            Vector3 mouseTranslation = Vector3.zero;
            mouseTranslation += forward * -mouseDelta.x / Screen.width;
            mouseTranslation += right * -mouseDelta.y / Screen.height;

            movement += mouseTranslation * mouseTranslationSensitivityModifier;
        }

        movement *= scrollingSensitivityModifier * globalSensitivity * Time.deltaTime;
        transform.Translate(movement, Space.World);
    }

    private void processCameraRotation()
    {
        if (allowMouseRotation && Input.GetMouseButton(1)) // right mouse
        {
            if (mousePreviousPosition.x >= 0 &&
                mousePreviousPosition.y >= 0 &&
                mousePreviousPosition.x <= Screen.width &&
                mousePreviousPosition.y <= Screen.height)
            {
                Vector3 rotation = Vector3.zero;
                Vector3 mouseDelta = Input.mousePosition - mousePreviousPosition;

                rotation += Vector3.up * mouseDelta.x / Screen.width; // horizontal
                rotation += Vector3.left * mouseDelta.y / Screen.height; // vertical
                rotation *= mouseRotationSensitivityModifier * globalSensitivity * Time.deltaTime;

                rotation += transform.localEulerAngles;
                rotation.x = Mathf.Clamp(rotation.x, 0f, 80f);
                rotation.z = 0;

                transform.localEulerAngles = rotation;
            }
        }
    }

    private void processCameraZoom()
    {
        if (allowCameraZoom)
        {
            // camera zoom via FOV change
            Camera.main.fieldOfView -= Input.mouseScrollDelta.y * cameraZoomSensitivityModifier;
            Camera.main.fieldOfView = Mathf.Clamp(Camera.main.fieldOfView, cameraFovMin, cameraFovMax);
        }
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