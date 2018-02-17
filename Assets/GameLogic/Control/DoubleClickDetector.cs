using System;
using UnityEngine;

public class DoubleClickDetector : MonoBehaviour
{
    public float doubleClickDelay = 0.3f; // time in seconds for a double click to register; otherwise it is a single click

    public MouseButton mouseButton = MouseButton.Left;
    public enum MouseButton : byte
    {
        Left = 0,
        Right = 1,
        Middle = 2
    }

    private int numberOfClicks = 0;
    private float timeSinceLastClick = 0.0f;

    bool IsDoubleClick()
    {
        bool isDoubleClick = numberOfClicks == 2;
        if (isDoubleClick)
            numberOfClicks = 0;
        return isDoubleClick;
    }

    void Update()
    {
        timeSinceLastClick += Time.deltaTime;

        if (timeSinceLastClick > doubleClickDelay)
        {
            numberOfClicks = 0;
        }

        if (Input.GetMouseButtonDown((int)mouseButton))
        {
            numberOfClicks++;
            timeSinceLastClick = 0.0f;
        }
    }
}