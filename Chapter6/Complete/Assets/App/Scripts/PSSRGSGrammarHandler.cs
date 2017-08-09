using System;
//using System.Collections;
//using System.Collections.Generic;
using UnityEngine;
//using System.Linq;
using UnityEngine.Windows.Speech;
using System.IO; 
//using System.Text.RegularExpressions;
//using HoloToolkit.Unity;

public class PSSRGSGrammarHandler : PlayStateVoiceHandler
{
    #region enums and types 

    public struct Command
    {
        public string action; 
        public string part;
        public string direction;
        public string unit;
        public float? change; 

        public bool IsDiscrete
        {
            get { return change.HasValue && (unit != null && unit != string.Empty); }
        }

        public float ScaledChange
        {
            get
            {
                if (!change.HasValue)
                {
                    return 0; 
                }

                return change.Value * GetMeterScaleForUnit(unit); 
            }
        }

        public float GetMeterScaleForUnit(string unit, float defaultScale=1f) {
            if(unit == null || unit == string.Empty)
            {
                return defaultScale; 
            }

            switch (unit)
            {
                case CommandUnit.Centieters:
                    return change.Value / 100f;
                case CommandUnit.Millimeters:
                    return change.Value / 1000f;  
            }

            return defaultScale;
        }
    }

    #endregion

    #region constants 

    sealed class SemanticKeys
    {
        public const string Action = "Action";
        public const string Part = "Part";
        public const string Direction = "Direction";
        public const string Change = "Change";
        public const string Unit = "Unit";
    }

    sealed class CommandAction
    {
        public const string Stop = "stop";
        public const string Rotate = "rotate";
        public const string Move = "move";
    }

    sealed class CommandUnit
    {
        public const string Degrees = "degrees";
        public const string Meters = "meters";
        public const string Centieters = "centieters";
        public const string Millimeters = "millimeters";
    }    

    #endregion

    #region properties and variables 

    public ConfidenceLevel confidenceLevel = ConfidenceLevel.Medium;

    [Tooltip("Speed of robot part rotating")]
    public float rotationSpeed = 5.0f;

    [Tooltip("Speed of handle moving")]
    public float moveSpeed = 5.0f;

    [Tooltip("The file name of the SRGS file to use for recognition (must reside in the StreamingAssets folder)")]
    public string SRGSFileName = "srgs_robotcommands.xml";

    private Command? _currentCommand; 

    public Command? CurrentCommand
    {
        get
        {
            return _currentCommand; 
        }
        private set
        {
            if (_currentCommand.HasValue)
            {
                if(_currentCommand.Value.part.Equals(PART_HANDLE, StringComparison.OrdinalIgnoreCase))
                {
                    PlayStateManager.Instance.Robot.solverActive = false;
                }

                _currentCommand = value;

                if (_currentCommand.Value.part.Equals(PART_HANDLE, StringComparison.OrdinalIgnoreCase))
                {
                    PlayStateManager.Instance.Robot.solverActive = true;
                }
            }
        } 
    }

    private GrammarRecognizer grammarRecognizer;    

    #endregion

    public override void StartHandler()
    {
        if(grammarRecognizer == null)
        {
            try
            {
                grammarRecognizer = new GrammarRecognizer(Path.Combine(Application.streamingAssetsPath, SRGSFileName));
                grammarRecognizer.OnPhraseRecognized += GrammarRecognizer_OnPhraseRecognized;                
            }
            catch
            {
                throw new Exception(string.Format("Error while trying to load or parse the SRGS file {0}", SRGSFileName));
            }
        }

        grammarRecognizer.Start();
    }

    public override void StopHandler()
    {
        if(grammarRecognizer != null)
        {
            grammarRecognizer.Stop();
        }        
    }

    private void Update()
    {
        if(CurrentCommand.HasValue)
        {
            ProcessCurrentCommand(); 
        }
    }

    private void OnDestroy()
    {
        if (grammarRecognizer != null)
        {
            grammarRecognizer.Stop();
            grammarRecognizer.OnPhraseRecognized -= GrammarRecognizer_OnPhraseRecognized;
            grammarRecognizer.Dispose();
            grammarRecognizer = null; 
        }
    }

    private void GrammarRecognizer_OnPhraseRecognized(PhraseRecognizedEventArgs args)
    {               
        if(args.confidence < confidenceLevel)
        {
            return; 
        }

        Command commandCandidate = CreateCommand(args);

        if (IsCommandValid(commandCandidate))
        {
            CurrentCommand = commandCandidate;
        }
    }

    Command CreateCommand(PhraseRecognizedEventArgs args)
    {
        SemanticMeaning[] meanings = args.semanticMeanings;

        return new Command
        {
            action = meanings.Contains(SemanticKeys.Action) ? meanings.SafeGet(SemanticKeys.Action).Value.values[0] : string.Empty,
            part = meanings.Contains(SemanticKeys.Part) ? meanings.SafeGet(SemanticKeys.Part).Value.values[0] : string.Empty,
            direction = meanings.Contains(SemanticKeys.Direction) ? meanings.SafeGet(SemanticKeys.Direction).Value.values[0] : string.Empty,
            change = meanings.Contains(SemanticKeys.Change) ? int.Parse(meanings.SafeGet(SemanticKeys.Change).Value.values[0]) : 0,
            unit = meanings.Contains(SemanticKeys.Unit) ? meanings.SafeGet(SemanticKeys.Unit).Value.values[0] : string.Empty
        };                
    }

    #region command validation 

    bool IsCommandValid(Command command)
    {
        if (command.action == CommandAction.Stop)
        {
            return true;
        }

        if (command.part == null)
        {
            return false; 
        }

        if (command.action == string.Empty)
        {
            return false; 
        }

        if (command.direction == string.Empty)
        {
            return false; 
        }

        if(command.part.Equals(PART_BASE, StringComparison.OrdinalIgnoreCase)){
            return IsCommandValidForBase(command); 
        }

        if(command.part.Equals(PART_ARM_1, StringComparison.OrdinalIgnoreCase) ||
                command.part.Equals(PART_ARM_2, StringComparison.OrdinalIgnoreCase))
        {
            return IsCommandValidForArm(command);
        }

        if (command.part.Equals(PART_HANDLE, StringComparison.OrdinalIgnoreCase))
        {
            return IsCommandValidForHandle(command); 
        }

        return false; 
    }

    bool IsCommandValidForBase(Command command)
    {
        if (command.action != CommandAction.Rotate)
        {
            return false;
        }

        if (command.direction != Direction.Left || command.direction != Direction.Right)
        {
            return false;
        }

        if (command.unit != string.Empty)
        {
            if (command.unit != CommandUnit.Degrees)
            {
                return false;
            }
        }

        return true; 
    }

    bool IsCommandValidForArm(Command command)
    {
        if (command.action != CommandAction.Rotate)
        {
            return false;
        }

        if (command.direction != Direction.Up || command.direction != Direction.Down)
        {
            return false;
        }

        if (command.unit != string.Empty)
        {
            if (command.unit != CommandUnit.Degrees)
            {
                return false;
            }
        }

        return true; 
    }

    bool IsCommandValidForHandle(Command command)
    {
        if (command.action != CommandAction.Move)
        {
            return false;
        }

        if (command.unit != string.Empty)
        {
            if (command.unit == CommandUnit.Degrees)
            {
                return false;
            }
        }

        return true; 
    }

    #endregion 

    void ProcessCurrentCommand()
    {
        if (!CurrentCommand.HasValue)
        {
            return; 
        }

        Command command = CurrentCommand.Value; 

        switch (command.action)
        {
            case CommandAction.Stop:
                {
                    // terminate command 
                    CurrentCommand = null; 
                    break;
                }
            case CommandAction.Rotate:
                {
                    PlayStateManager.Instance.Robot.solverActive = false;

                    if (command.IsDiscrete)
                    {
                        PlayStateManager.Instance.Robot.Rotate(command.part, GetRotationVector(command.direction, command.ScaledChange));
                        CurrentCommand = null; 
                    }
                    else
                    {
                        PlayStateManager.Instance.Robot.Rotate(command.part, GetRotationVector(command.direction, rotationSpeed * Time.deltaTime));
                    }

                    break; 
                }
            case CommandAction.Move:
                {
                    PlayStateManager.Instance.Robot.solverActive = true;

                    if (command.IsDiscrete)
                    {
                        PlayStateManager.Instance.Robot.MoveIKHandle(GetTranslationVector(command.direction, command.ScaledChange));
                        PlayStateManager.Instance.Robot.solverActive = false;
                        CurrentCommand = null;
                    }
                    else
                    {
                        PlayStateManager.Instance.Robot.MoveIKHandle(GetTranslationVector(command.direction, moveSpeed * Time.deltaTime));
                        PlayStateManager.Instance.Robot.Rotate(command.part, GetRotationVector(command.direction, rotationSpeed * Time.deltaTime));
                    }
                    break; 
                }
        }
    }    
}
