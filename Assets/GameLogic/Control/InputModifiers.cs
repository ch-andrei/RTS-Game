using UnityEngine;

public class InputModifiers
{
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
        public Modifiers modifiers;
        public char inputKey;

        public bool isActivated()
        {
            return Input.GetKeyDown(inputKey.ToString()) && modifiers.checkModifiers();
        }

        public bool isHeld()
        {
            return Input.GetKey(inputKey.ToString()) && modifiers.checkModifiers();
        }
    }
}
