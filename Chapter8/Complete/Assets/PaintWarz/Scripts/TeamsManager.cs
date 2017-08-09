using System;
using System.Linq; 
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using HoloToolkit.Unity; 

/// <summary>
/// Rather than explicitly manage teams, we observe Players within the scene and assign 
/// them to their team when added. 
/// </summary>
public class TeamsManager : Singleton<TeamsManager> {

    [Tooltip("How frequently the teams are updated")]
    public float teamsRefresh = 10; 

    /// <summary>
    /// Team lookup 
    /// </summary>
    private Dictionary<string, Team> teams = new Dictionary<string, Team>();

    private float lastTeamRefresh = 0; 
	
	void Update () {
        if(lastTeamRefresh == 0 || (Time.timeSinceLevelLoad - lastTeamRefresh) > (teamsRefresh))
        {
            RefreshTeams();             
        }	
	}

    public void SetTeamsTarget(string team, Vector3 position)
    {
        if (!teams.ContainsKey(team))
        {
            teams.Add(team, new Team());
            teams[team].name = team;
        }

        teams[team].TargetPosition = position;
        teams[team].Update();
    }

    public List<Player> GetOpponents(Player player)
    {
        return teams.Where((kvp) =>
        {
            return !kvp.Key.Equals(player.Team, StringComparison.OrdinalIgnoreCase);
        }).SelectMany(kvp =>
        {
            return kvp.Value;
        }).ToList();        
    }

    public List<Player> GetTeammates(Player player)
    {
        return teams.Where((kvp) =>
        {
            return kvp.Key.Equals(player.Team, StringComparison.OrdinalIgnoreCase);
        }).SelectMany(kvp =>
        {
            return kvp.Value;
        }).ToList();
    }

    public Team GetTeam(Player player)
    {
        Team team = null;

        if (!teams.TryGetValue(player.Team, out team))
        {
            team = new Team();
            team.name = player.Team;
            team.Add(player); 
            teams.Add(team.name, team);
            try
            {
                RefreshTeams();
            }
            catch { }
        }

        return team;
    }

    /// <summary>
    /// Ensure that Players are all assigned to a team 
    /// </summary>
    void RefreshTeams()
    {
        var allPlayers = GameObject.FindObjectsOfType<Player>();
       
        foreach(var player in allPlayers)
        {
            if (!teams.ContainsKey(player.Team))
            {
                teams.Add(player.Team, new Team());
                teams[player.Team].name = player.Team; 
            }

            if (!teams[player.Team].Contains(player))
            {
                teams[player.Team].Add(player);
            }            
        }

        lastTeamRefresh = Time.timeSinceLevelLoad;
    }
}
