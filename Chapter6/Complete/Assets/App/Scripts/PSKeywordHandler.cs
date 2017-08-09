using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Windows.Speech;
using System.Linq;
using System.Text.RegularExpressions; 
using HoloToolkit.Unity; 

public class PSKeywordHandler : PlayStateVoiceHandler
{    
    delegate void KeywordAction(PhraseRecognizedEventArgs args);    

    #region properties and variables 

    public ConfidenceLevel confidenceLevel = ConfidenceLevel.High; 

    public float rotationSpeed = 5.0f;

    public float moveSpeed = 5.0f;

    private string _currentPart = null; 

    public string CurrentPart
    {
        get
        {
            return _currentPart; 
        }
        set
        {
            if(_currentPart != null)
            {
                if (_currentPart.Equals(PART_HANDLE) && 
                    (PlayStateManager.Instance.CurrentInteractible != null && PlayStateManager.Instance.CurrentInteractible.interactionType != Interactible.InteractionTypes.Manipulation))
                {
                    PlayStateManager.Instance.Robot.solverActive = false; 
                }
            }

            _currentPart = value;

            if (_currentPart != null)
            {
                if (_currentPart.Equals(PART_HANDLE))
                {
                    PlayStateManager.Instance.Robot.solverActive = true;
                }
            }
        }
    }

    public string CurrentDirection { get; set; } 

    Dictionary<string, KeywordAction> keywordCollection;

    KeywordRecognizer keywordRecognizer;

    #endregion 

    #region abstract methods

    public override void StartHandler()
    {
        keywordCollection = new Dictionary<string, KeywordAction>();

        // Add keyword to start manipulation.
        keywordCollection.Add("rotate left", StartRotatingLeft);
        keywordCollection.Add("rotate right", StartRotatingRight);

        keywordCollection.Add("rotate up", StartRotatingUp);
        keywordCollection.Add("rotate down", StartRotatingDown);

        keywordCollection.Add("rotate one up", StartRotatingArm1Up);
        keywordCollection.Add("rotate one down", StartRotatingArm1Down);

        keywordCollection.Add("rotate two up", StartRotatingArm2Up);
        keywordCollection.Add("rotate two down", StartRotatingArm2Down);

        keywordCollection.Add("move up", StartMovingIKHandle);
        keywordCollection.Add("move down", StartMovingIKHandle);
        keywordCollection.Add("move left", StartMovingIKHandle);
        keywordCollection.Add("move right", StartMovingIKHandle);
        keywordCollection.Add("move forward", StartMovingIKHandle);
        keywordCollection.Add("move back", StartMovingIKHandle);

        keywordCollection.Add("stop", Stop);

        // Initialize KeywordRecognizer with the previously added keywords.
        keywordRecognizer = new KeywordRecognizer(keywordCollection.Keys.ToArray(), confidenceLevel);
        keywordRecognizer.OnPhraseRecognized += KeywordRecognizer_OnPhraseRecognized;
        keywordRecognizer.Start();
    }

    public override void StopHandler()
    {
        if(keywordRecognizer == null)
        {
            return; 
        }

        keywordRecognizer.OnPhraseRecognized -= KeywordRecognizer_OnPhraseRecognized;
        keywordRecognizer.Stop();
        keywordRecognizer.Dispose();
    }

    #endregion

    #region command handlers 

    void KeywordRecognizer_OnPhraseRecognized(PhraseRecognizedEventArgs args)
    {
        KeywordAction keywordAction;

        if (keywordCollection.TryGetValue(args.text, out keywordAction))
        {
            keywordAction.Invoke(args);
        }
    }

    void StartRotatingLeft(PhraseRecognizedEventArgs args)
    {
        CurrentPart = PART_BASE;
        CurrentDirection = Direction.Left;
    }

    void StartRotatingRight(PhraseRecognizedEventArgs args)
    {
        CurrentPart = PART_BASE;
        CurrentDirection = Direction.Right;
    }

    void StartRotatingUp(PhraseRecognizedEventArgs args)
    {
        if (!GazeManager.Instance.Hit)
        {
            return; 
        }

        if(GazeManager.Instance.FocusedObject.name.Equals("arm 1", StringComparison.OrdinalIgnoreCase))
        {
            CurrentPart = PART_ARM_1;
            CurrentDirection = Direction.Up;
        }
        else if (GazeManager.Instance.FocusedObject.name.Equals("arm 1", StringComparison.OrdinalIgnoreCase))
        {
            CurrentPart = PART_ARM_2;
            CurrentDirection = Direction.Up;
        }
    }

    void StartRotatingDown(PhraseRecognizedEventArgs args)
    {
        if (!GazeManager.Instance.Hit)
        {
            return;
        }

        if (GazeManager.Instance.FocusedObject.name.Equals("arm 1", StringComparison.OrdinalIgnoreCase))
        {
            CurrentPart = PART_ARM_1;
            CurrentDirection = Direction.Down;
        }
        else if (GazeManager.Instance.FocusedObject.name.Equals("arm 1", StringComparison.OrdinalIgnoreCase))
        {
            CurrentPart = PART_ARM_2;
            CurrentDirection = Direction.Down;
        }
    }

    void StartRotatingArm1Up(PhraseRecognizedEventArgs args)
    {
        CurrentPart = PART_ARM_1;
        CurrentDirection = Direction.Up;
    }

    void StartRotatingArm1Down(PhraseRecognizedEventArgs args)
    {
        CurrentPart = PART_ARM_1;
        CurrentDirection = Direction.Down;
    }

    void StartRotatingArm2Up(PhraseRecognizedEventArgs args)
    {
        CurrentPart = PART_ARM_2;
        CurrentDirection = Direction.Up;
    }

    void StartRotatingArm2Down(PhraseRecognizedEventArgs args)
    {
        CurrentPart = PART_ARM_2;
        CurrentDirection = Direction.Down;
    }

    void StartMovingIKHandle(PhraseRecognizedEventArgs args)
    {
        if(Regex.IsMatch(args.text, @"\b(up|higher)\b"))
        {
            CurrentPart = PART_HANDLE;
            CurrentDirection = Direction.Up;
        }
        else if (Regex.IsMatch(args.text, @"\b(down|lower)\b"))
        {
            CurrentPart = PART_HANDLE;
            CurrentDirection = Direction.Down;
        }
        else if (Regex.IsMatch(args.text, @"\b(forwards|away)\b"))
        {
            CurrentPart = PART_HANDLE;
            CurrentDirection = Direction.Forward;
        }
        else if (Regex.IsMatch(args.text, @"\b(back|backwards)\b"))
        {
            CurrentPart = PART_HANDLE;
            CurrentDirection = Direction.Back;
        }
        else if (Regex.IsMatch(args.text, @"\b(left)\b"))
        {
            CurrentPart = PART_HANDLE;
            CurrentDirection = Direction.Left;
        }
        else if (Regex.IsMatch(args.text, @"\b(right)\b"))
        {
            CurrentPart = PART_HANDLE;
            CurrentDirection = Direction.Right;
        }
    }

    void Stop(PhraseRecognizedEventArgs args)
    {
        CurrentPart = null; 
    }

    #endregion     

    void Update()
    {
        if(CurrentPart == null)
        {
            return; 
        }

        if(PlayStateManager.Instance.CurrentInteractible != null)
        {
            CurrentPart = null;
            return; 
        }

        if (CurrentPart.Equals(PART_HANDLE))
        {
            PlayStateManager.Instance.Robot.MoveIKHandle(GetTranslationVector(CurrentDirection, moveSpeed * Time.deltaTime));
        }
        else
        {
            PlayStateManager.Instance.Robot.Rotate(CurrentPart, GetRotationVector(CurrentDirection, rotationSpeed * Time.deltaTime));
        }
    }

    void OnDestroy()
    {
        StopHandler(); 
    }
}
