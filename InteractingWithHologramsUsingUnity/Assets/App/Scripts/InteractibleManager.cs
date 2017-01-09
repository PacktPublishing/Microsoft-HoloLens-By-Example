using System; 
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HoloToolkit.Unity;

public class InteractibleManager : Singleton<InteractibleManager> {

    private Interactible _currentInteractible; 

    public Interactible CurrentInteractible
    {
        get { return _currentInteractible; }
        set
        {
            if(_currentInteractible == value)
            {
                return; 
            }

            if(_currentInteractible != null)
            {
                _currentInteractible.GazeExited(); 
            }

            _currentInteractible = value;

            if (_currentInteractible != null)
            {
                _currentInteractible.GazeEntered(); 
            }
        }
    }

    private bool _locked = false; 

    public bool Locked
    {
        get { return _locked; }
        set
        {
            _locked = value;

            if (!_locked)
            {
                GameObject currentHitObject = GazeManager.Instance.FocusedObject; 
                if(currentHitObject == null)
                {
                    CurrentInteractible = null; 
                }
                else
                {
                    var interactible = currentHitObject.GetComponent<Interactible>();
                    CurrentInteractible = interactible;
                }
                
            }
        }
    }

    private GameObject _foucsedObject = null; 

    public GameObject FoucsedObject
    {
        get { return _foucsedObject; }
        set
        {
            if(_foucsedObject == value)
            {
                return; 
            }

            OnFocusedObjectChanged(_foucsedObject, value);

            _foucsedObject = value; 
        }
    }


    void LateUpdate()
    {
        FoucsedObject = GazeManager.Instance.FocusedObject;
    }

    void OnFocusedObjectChanged(GameObject previousObject, GameObject newObject)
    {
        if (Locked)
        {
            return; 
        }

        if(newObject == null)
        {
            CurrentInteractible = null;
        }
        else
        {
            var interactible = newObject.GetComponent<Interactible>();
            CurrentInteractible = interactible;
        }
    }
}
