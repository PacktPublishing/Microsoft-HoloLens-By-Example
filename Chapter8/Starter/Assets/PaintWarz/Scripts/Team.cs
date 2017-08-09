using System;
using System.Linq; 
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Extension of a List specifically for a Team of Players with helper methods that could 
/// be used to faciliate team orientated behaviour (such as flocking http://www.red3d.com/cwr/boids/) 
/// </summary>
public class Team : List<Player> {
    /// <summary>
    /// Key that associates a player with a team 
    /// </summary>
    public string name = string.Empty;
    /// <summary>
    /// Center of the team 
    /// </summary>
    public Vector3 teamCenter = Vector3.zero;
    /// <summary>
    /// Average velocity of the team 
    /// </summary>
    public Vector3 teamVelocity = Vector3.zero;

    private bool targetPositionSet = false;

    private Vector3 _targetPosition = Vector3.zero; 

    public Vector3 TargetPosition
    {
        get
        {
            if (!targetPositionSet)
            {
                Update();

                _targetPosition = teamCenter; 

                targetPositionSet = true; 
            }

            return _targetPosition; 
        }
        set
        {
            targetPositionSet = true;

            _targetPosition = value;

            Update(); 

            foreach(var player in this)
            {
                player.TargetPosition = _targetPosition; 
            }
                                 
        }
    }        

    /// <summary>
    /// Calcualte team center and velocity 
    /// </summary>
    public void Update()
    {
        teamCenter = Vector3.zero;
        teamVelocity = Vector3.zero;

        int activePlayerCount = 0; 

        for(int i=Count-1; i>=0; i--)
        {
            var player = this[i];
            if(player == null)
            {
                RemoveAt(i); 
            }
            else
            {
                if (player.IsPlaying)
                {
                    teamCenter += player.transform.position;
                    teamVelocity += player.GetComponent<Rigidbody>().velocity;
                    activePlayerCount += 1; 
                }                
            }
        }

        teamCenter /= activePlayerCount;
        teamVelocity /= activePlayerCount;
    }
	
}
