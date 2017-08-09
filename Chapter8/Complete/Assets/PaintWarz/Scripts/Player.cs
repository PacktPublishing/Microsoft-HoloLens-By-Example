using System;
using System.Linq; 
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Networking;

public class Player : NetworkBehaviour {

    const float MinSpeedThreadhold = 0.0005f; 

    public enum PlayerState
    {
        Playing, 
        GameWon, 
        GameLost
    }

    #region Team 

    [SyncVar]
    public string _team;

    public string Team
    {
        get { return _team; }
        set
        {
            if (isServer)
            {
                _team = value; 
            }
            else
            {
                CmdSetTeam(value);
            }
        }
    }

    [Command]
    void CmdSetTeam(string value)
    {
        _team = value; 
    }

    #endregion

    #region PlayerName

    [SyncVar]
    public string _playerName = string.Empty;

    public string PlayerName
    {
        get { return _playerName; }
        set
        {
            if (isServer)
            {
                _playerName = value; 
            }
            else
            {
                CmdSetPlayerName(value);
            }
        }
    }

    [Command]
    void CmdSetPlayerName(string value)
    {
        _playerName = value; 
    }

    #endregion 

    public GameObject bulletPrefab;

    private Image healthUIImage;    

    [Tooltip("How fast the player turns around")]
    public float maxTurnSpeed = 10f;

    /// <summary>
    /// Radius the Player will select around a given target position 
    /// </summary>
    private float targetOffsetRadius = 0.01f;

    /// <summary>
    /// Steer to avoid crowding local flockmates
    /// </summary>
    private float separationWeight = 0.05f;

    [Tooltip("Max range the player can see to either side of themselves (in deg)")]
    public float fieldOfView = 45f;

    [Tooltip("Sight distance")]
    public float maxVisibilityDistance = 1.5f;

    [Tooltip("Max range the player can shoot from")]
    public float maxShootingdistance = 1.5f;

    [Tooltip("How quickly this player can fire the gun")]
    public float shotFrequency = 2.5f;     

    /// <summary>
    /// Keep track of the last time we shot a paint ball 
    /// </summary>
    private float lastShotTimestamp = 0;

    /// <summary>
    /// Used to calcualte displacement / speed 
    /// </summary>
    Vector3 previousPosition = Vector3.zero;

    public float MaxSpeed
    {
        get
        {
            const float maxHealth = 100;

            float healthAsFraction = (float)Health / maxHealth;

            float speed = 0.8f; 

            if(healthAsFraction < 2f)
            {
                speed *= 0.5f;
            } 
            else if(healthAsFraction < 0.5)
            {
                speed *= 0.8f;
            }

            return 0.8f;
        }
    }

    public float RotationSpeed
    {
        get
        {
            return 2f; 
        }
    }

    public bool IsPlaying
    {
        get { return Health > 0; }
    }

    private bool detectedSomethingInFront = false; 

    /// <summary>
    /// Team target position 
    /// </summary>
    private Vector3 _targetPosition = Vector3.zero;
    /// <summary>
    /// Offset around teams target position 
    /// </summary>
    private Vector3 _targetPositionOffset = Vector3.zero;

    /// <summary>
    /// _targetPosition + _targetPositionOffset 
    /// </summary>
    public Vector3 TargetPosition
    {
        get
        {
            return _targetPosition + _targetPositionOffset;
        }
        set
        {
            detectedSomethingInFront = false;

            _targetPosition = value;
            _targetPositionOffset = UnityEngine.Random.insideUnitSphere * targetOffsetRadius;
        }
    }

    /// <summary>
    /// Current target candidate 
    /// </summary>
    public GameObject TargetOpponent
    {
        get; private set;
    }

    /// <summary>
    /// splineTransform used to target the opponent 
    /// </summary>
    private Transform splineTransform;

    private float splineTransformYAngleOverride = 0f; 

    private PlayerState _state = PlayerState.Playing; 

    public PlayerState State
    {
        get
        {
            return _state; 
        }
        set
        {
            _state = value; 
        }
    }

    #region Health 

    [SyncVar(hook = "OnHealthUpdated")]
    public int _health = 100;

    public int Health
    {
        get
        {
            return _health;
        }
        set
        {
            if(_health == value)
            {
                return; 
            }

            if (isServer)
            {
                _health = value;
                OnHealthUpdated(value);
            }
            else
            {
                CmdSetHealth(value);
            }
        }
    }

    [Command]
    void CmdSetHealth(int value)
    {
        _health = value;
        OnHealthUpdated(value);
    }

    void OnHealthUpdated(int value)
    {
        _health = Mathf.Max(0, value);

        if (_health <= 0)
        {
            IsShooting = false;
            Speed = 0f; 
            TargetOpponent = null;

            GetComponent<Animator>().SetBool("playerOut", true);
        }

        const float maxHealthBarWdith = 100f;
        const float maxHealth = 100;
        float healthAsFraction = (float)_health / maxHealth;

        float healthBarWidth = maxHealthBarWdith * healthAsFraction;

        healthUIImage.rectTransform.sizeDelta = new Vector2(healthBarWidth, healthUIImage.rectTransform.sizeDelta.y);

        if (healthAsFraction > 0.6f)
        {
            healthUIImage.color = Color.green;
        }
        else if (healthAsFraction >= 0.2)
        {
            healthUIImage.color = Color.yellow;
        }
        else
        {
            healthUIImage.color = Color.red;
        }
    }

    #endregion 

    #region Speed         

    [SyncVar(hook = "OnSpeedChanged")]
    public float _speed = 0f;

    public float Speed
    {
        get { return _speed; }
        private set
        {
            if(Mathf.Approximately(Mathf.Abs(_speed - value), 0))
            {
                return; 
            }

            if (isServer)
            {
                _speed = value;

                OnSpeedChanged(_speed); 
            }
            else
            {
                CmdSetSpeed(value); 
            }
        }
    }

    [Command]
    void CmdSetSpeed(float value)
    {
        _speed = value; 

        OnSpeedChanged(_speed);
    }

    void OnSpeedChanged(float value)
    {
        if (value > MinSpeedThreadhold)
        {
            GetComponent<Animator>().SetBool("moving", true);
            GetComponent<Animator>().SetFloat("speed", value * 10.0f);
        }
        else
        {
            GetComponent<Animator>().SetBool("moving", false);
            GetComponent<Animator>().SetFloat("speed", 0);            
        }
    }

    public bool IsMoving
    {
        get
        {
            return Speed > MinSpeedThreadhold;
        }
    }

    #endregion

    #region IsShooting 

    private bool _isShootingUpdating = false; 

    [SyncVar(hook = "OnIsShootingUpdated")]
    private bool _isShooting = false; 

    public bool IsShooting
    {
        get
        {
            return _isShooting; 
        }
        set
        {
            if(_isShooting == value)
            {
                return; 
            }

            if (isServer)
            {
                _isShooting = value;
                OnIsShootingUpdated(value);
            }
            else
            {
                if (!_isShootingUpdating)
                {
                    _isShootingUpdating = true; 

                    CmdSetIsShooting(value);
                }                
            }            
        }
    }

    [Command]
    void CmdSetIsShooting(bool value)
    {
        _isShooting = value;
        OnIsShootingUpdated(value);
    }

    void OnIsShootingUpdated(bool value)
    {
        Debug.LogFormat("OnIsShootingUpdated {0} {1}", name, value);
        _isShootingUpdating = false; 
        GetComponent<Animator>().SetBool("shooting", value);
    }

    #endregion     

	void Start () {
        gameObject.name = PlayerName;        

        splineTransform = transform.Find("Player/Base/Hips/Spline");

        healthUIImage = transform.Find("HealthBar/Image").GetComponent<Image>();        

        _targetPosition = transform.position;
        _targetPositionOffset = Vector3.zero;

        if(PlaySpaceManager.Instance.Floor != null)
        {
            transform.parent = PlaySpaceManager.Instance.Floor.transform;
        }   
    }

    public void Init(string team, string name)
    {
        _team = team; 
        _playerName = name; 
    }    

    void Update () {      
        if (!IsPlaying)
        {
            return;
        }

        if (hasAuthority)
        {
            var team = TeamsManager.Instance.GetTeam(this);

            UpdateSpeed();

            if (!detectedSomethingInFront)
            {
                MoveTowardsTarget(team);
            }            

            var targetOpponent = SearchForClosestOpponent(team);

            if (targetOpponent != null)
            {
                if (TakeAim(targetOpponent))
                {
                    IsShooting = ShootAtWill(targetOpponent);
                }
                else
                {
                    targetOpponent = null;
                }
            }
            else
            {
                IsShooting = false;
            }

            TargetOpponent = targetOpponent;

            if (CheckForObstacles())
            {
                // stop moving if we detect something in front of us 
                detectedSomethingInFront = true; 

                _targetPosition = transform.position; 
                _targetPositionOffset = Vector3.zero; 
            }         
        }
    }    

    private void LateUpdate()
    {
        if (!IsPlaying)
        {
            return;
        }

        if (TargetOpponent == null)
        {
            if(!Mathf.Approximately(splineTransformYAngleOverride, 0f))
            {
                splineTransformYAngleOverride = Mathf.Lerp(splineTransformYAngleOverride, 0f, Time.deltaTime);
            }
        }
        else
        {
            var target = TargetOpponent.transform.position - transform.position;
            var angleToTarget = Vector3.Angle(target.normalized, transform.forward);
            var crossProduct = Vector3.Cross(target, transform.forward);
            if (crossProduct.y > 0)
            {
                angleToTarget = -angleToTarget;
            }

            if(Mathf.Abs(angleToTarget) < fieldOfView)
            {
                splineTransformYAngleOverride = Mathf.Lerp(splineTransformYAngleOverride, angleToTarget, Time.deltaTime * 10f);
            }                         
        }

        if (!Mathf.Approximately(splineTransformYAngleOverride, 0f))
        {
            splineTransform.localEulerAngles += new Vector3(splineTransformYAngleOverride, 0f, 0f); 
        }
    }

    #region Methods related to moving 

    bool CheckForObstacles()
    {
        if (!IsMoving)
        {
            return false; 
        }

        const float maxDistance = 0.11f;

        RaycastHit hitInfo; 
        if(Physics.Raycast(transform.position + transform.up * GetComponent<Collider>().bounds.size.y * 0.5f,
            transform.forward, out hitInfo, maxDistance))
        {
            return true; 
        }

        return false; 
    }

    void UpdateSpeed()
    {
        var displacement = (transform.position - previousPosition);
        previousPosition = transform.position;
        displacement.y = 0;
        Speed = displacement.magnitude;
    }

    void MoveTowardsTarget(Team team)
    {
        var myTeam = TeamsManager.Instance.GetTeam(this);

        Vector3 velocity = GetVelocity(myTeam);

        if (velocity.magnitude > 0.1f)
        {
            var targetRotation = Quaternion.LookRotation(velocity.normalized);
            transform.localRotation = Quaternion.Lerp(transform.localRotation, targetRotation, maxTurnSpeed * Time.deltaTime);

            velocity = transform.forward * velocity.magnitude;

            // throttle speed     
            if (velocity.magnitude > MaxSpeed)
            {
                velocity = velocity.normalized * MaxSpeed;
            }

            // remove the y value 
            velocity.y = 0f;
        }
        else
        {
            velocity = Vector3.zero;            
        }

        transform.position += velocity * Time.deltaTime;
    }

    Vector3 GetVelocity(Team team)
    {                       
        var velocity = Vector3.zero;

        var target = (TargetPosition - transform.position);
        target.y = 0f; 

        if (target.magnitude > 0.1f)
        {
            velocity += target;
        }    

        Vector3 separation = Vector3.zero;
        foreach(var teammate in team)
        {
            if (teammate == this || !teammate.IsPlaying) continue;

            Vector3 relativePos = transform.position - teammate.transform.position;
            separation += relativePos / Mathf.Max((relativePos.sqrMagnitude), float.Epsilon) * separationWeight;
        }

        velocity += separation;

        return velocity;  
    }    

    #endregion

    #region Methods related to searching for opponents 

    GameObject SearchForClosestOpponent(Team team)
    {
        Player closestOpponent = null;
        float closestOpponentsDistance = float.MaxValue; 

        var opponents = TeamsManager.Instance.GetOpponents(this);
        foreach(var opponent in opponents)
        {
            if (opponent == null || !opponent.IsPlaying || opponent.hasAuthority)
            {
                continue; 
            }

            var target = (opponent.transform.position - transform.position);

            if(target.magnitude > maxVisibilityDistance)
            {
                continue; 
            }            

            if(target.magnitude < closestOpponentsDistance)
            {
                closestOpponentsDistance = target.magnitude;
                closestOpponent = opponent; 
            }
        }

        return closestOpponent != null ? closestOpponent.gameObject : null;
    }

    bool TakeAim(GameObject targetOpponent)
    {
        var target = (targetOpponent.transform.position - transform.position);
        var angle = Vector3.Angle(target.normalized, transform.forward);

        if (Mathf.Abs(angle) > fieldOfView)
        {
            return false; 
        }

        if (!IsMoving)
        {
            var targetRotation = Quaternion.LookRotation(target.normalized);
            transform.localRotation = Quaternion.Lerp(transform.localRotation, targetRotation, maxTurnSpeed * Time.deltaTime);
        }        

        return true; 
    }

    bool ShootAtWill(GameObject targetOpponent)
    {
        if(targetOpponent == null)
        {
            return false; 
        }        

        var target = targetOpponent.transform.position - transform.position; 
        
        if(target.magnitude > maxShootingdistance)
        {
            return false; 
        }

        var angleToTarget = Vector3.Angle(target.normalized, transform.forward);
        var crossProduct = Vector3.Cross(target, transform.forward);
        if (crossProduct.y > 0)
        {
            angleToTarget = -angleToTarget;
        }

        angleToTarget -= splineTransformYAngleOverride;

        if (Mathf.Abs(angleToTarget) > 5f)
        {
            return false; 
        }

        if (Time.timeSinceLevelLoad - lastShotTimestamp < shotFrequency)
        {
            return true;
        }

        // shoot!
        var position = transform.position + transform.forward * 0.2f + Vector3.up * GetComponent<Collider>().bounds.size.y * 0.5f;
        var direction = transform.forward;

        if (isServer)
        {
            Shoot(position, direction);
        }
        else
        {
            CmdShoot(position, direction);
        }         

        lastShotTimestamp = Time.timeSinceLevelLoad; 

        return true; 
    }

    [Command]
    void CmdShoot(Vector3 position, Vector3 direction)
    {
        Shoot(position, direction);

    }

    void Shoot(Vector3 position, Vector3 direction)
    {
        var bullet = Instantiate(bulletPrefab);
        bullet.transform.position = transform.position + transform.forward * 0.2f + Vector3.up * GetComponent<Collider>().bounds.size.y * 0.5f;
        bullet.transform.forward = transform.forward;

        // Spawn the bullet on the Clients
        NetworkServer.Spawn(bullet);
    } 

    #endregion 

    void GetDistanceAndDirection(Player opponent, out float distance, out Vector3 direction)
    {
        Vector3 diff = (opponent.transform.position - transform.position);

        direction = diff.normalized;
        distance = diff.magnitude;
    }

    public void OnHit(Bullet bullet)
    {       
        if (bullet != null)
        {
            // update health 
            Health -= bullet.damage;
        }
    }
}
