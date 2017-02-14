using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class PlayStateVoiceHandler : MonoBehaviour {

    sealed public class Direction {

        public const string Left = "Left";
        public const string Right = "Right";
        public const string Up = "Up";
        public const string Down = "Down";
        public const string Forward = "Forward";
        public const string Back = "Back";
    }

    #region constants 

    protected const string PART_BASE = "Base";

    protected const string PART_ARM_1 = "Arm 1";

    protected const string PART_ARM_2 = "Arm 2";

    protected const string PART_HANDLE = "Handle";

    #endregion 

    public abstract void StartHandler();

    public abstract void StopHandler();

    #region helper methods 

    protected Vector3 GetRotationVector(string direction, float magnitude = 1f)
    {
        switch (direction)
        {
            case Direction.Up:
                return new Vector3(1f * magnitude, 0, 0);
            case Direction.Down:
                return new Vector3(-1f * magnitude, 0, 0);
            case Direction.Left:
                return new Vector3(0, 0, -1 * magnitude);
            case Direction.Right:
                return new Vector3(0, 0, 1 * magnitude);
        }

        return Vector3.zero;
    }

    protected Vector3 GetTranslationVector(string direction, float magnitude = 1f)
    {
        switch (direction)
        {
            case Direction.Up:
                return new Vector3(0, 1f * magnitude, 0);
            case Direction.Down:
                return new Vector3(0, -1f * magnitude, 0);
            case Direction.Left:
                return new Vector3(-1 * magnitude, 0f, 0);
            case Direction.Right:
                return new Vector3(1 * magnitude, 0f, 0);
            case Direction.Forward:
                return new Vector3(0, 0, 1f * magnitude);
            case Direction.Back:
                return new Vector3(0, 0, -1f * magnitude);
        }

        return Vector3.zero;
    }

    #endregion 
}
