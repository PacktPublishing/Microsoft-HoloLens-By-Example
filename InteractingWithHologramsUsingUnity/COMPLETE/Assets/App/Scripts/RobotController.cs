using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class RobotController : MonoBehaviour
{
    #region Nested Classes 
    
    public enum BoundsType
    {
        Default, // bounds that encapsualtes everything  
        Base, // bounds that encapsulates only the transforms that touch the surface 
        Placement // bounds used for placement 
    } 

    public enum Axis : uint
    {
        X = 2,
        Y = 4,
        Z = 8
    }    

    public enum JointType
    {
        Spherical
    }

    [Serializable]
    public class Constraint
    {
        /// <summary>
        /// enforce property, otherwise ignore 
        /// </summary>
        public bool enabled = true;
        public Axis axis = Axis.X;
        public float min = 0;
        public float max = 0;
        public JointType jointType = JointType.Spherical;

        public Constraint()
        {
            enabled = true;             
        }        
    }

    [Serializable]
    public class Joint
    {
        [Tooltip("Associated Transform where the constraint(s) will be applied")]
        public Transform transform;

        [Tooltip("When true, axis will be locked if not in the constraints list")]
        public bool lockAxisByDefault = true; 

        public List<Constraint> constraints = new List<Constraint>();
        internal Quaternion initialRotation;

        public uint AxisMask
        {
            get
            {
                uint mask = 0;

                foreach (var constraint in constraints)
                {
                    mask |= (uint)constraint.axis;
                }

                return mask; 
            }
        }

        public void SaveState()
        {
            initialRotation = transform.localRotation;
        }

        public void RestoreState()
        {
            transform.localEulerAngles = initialRotation.eulerAngles; 
        }
    }

    [Serializable]
    public class HologramFriendlyName
    {
        public string gameObjectName;
        public string friendlyName;

        public override string ToString()
        {
            return string.Format("{0} -> {1}", gameObjectName, friendlyName);
        }
    }

    #endregion

    #region properties and variables 

    [Tooltip("Distance threshold from the target we will try to obtain")]
    public float ikTargetDistanceThreshold = 0.005f; 

    public bool solverActive = true;

    [Tooltip("Used as a fallback target and manipulated by the user")]
    public Transform ikHandle; 

    [Tooltip("Target for the IK chain")]
    public Transform _target;

    public Transform Target
    {
        get { return _target; }
        set
        {
            _target = value;
        }
    }

    public Transform CurrentTarget
    {
        get
        {
            if(Target != null)
            {
                return Target;
            }

            return ikHandle; 
        }
    }

    [Tooltip("Applying damping when moving the robot towards the CurrentTarget")]
    public bool dampingEnabled = true;

    [Tooltip("Damping applied, when enabled, to the robot arm")]
    public float damping = 0.5f;

    [Tooltip("Transform that will be rotated towards the user/camera")]
    public Joint baseJoint; 

    [Tooltip("Joints of the robot arm")]
    public List<Joint> joints = new List<Joint>();

    /// <summary>
    /// World position of the last joint 
    /// </summary>
    public Vector3 EffectorPosition
    {
        get
        {
            if (joints == null || joints.Count == 0)
            {
                return Vector3.zero;
            }

            return joints.Last().transform.position;
        }
    }

    [Tooltip("List containing object name and friendly name, displayed via the text component")]
    public List<HologramFriendlyName> friendlyNameLookup = new List<HologramFriendlyName>();

    #endregion

    private void OnDrawGizmos()
    {
        //Gizmos.color = Color.yellow;
        //Gizmos.DrawWireCube(GetDefaultBounds().center, GetDefaultBounds().size);

        //Gizmos.color = Color.cyan;
        //Gizmos.DrawWireCube(GetBaseBounds().center, GetBaseBounds().size);

        //Gizmos.color = Color.magenta;
        //Gizmos.DrawWireCube(GetPlacementBounds().center, GetPlacementBounds().size);
    }

    void Start()
    {
        baseJoint.SaveState(); 

        foreach (var joint in joints)
        {
            joint.SaveState();
        }
    }

    void LateUpdate()
    {
        if (solverActive)
        {
            UpdateBaseJoint(); 

            CalculateCCDForJointChain();
        }
    }

    /// <summary>
    /// Called when placed, will face the user and position the ikHandle at the position of the EffectorPosition
    /// </summary>
    public void OnPlaced()
    {        
        Vector3 position = transform.position;
        Vector3 target = Camera.main.transform.position;

        target.y = position.y; // ignore y 

        Vector3 dir = (target - position).normalized;

        transform.forward = dir;

        if (ikHandle != null)
        {
            ikHandle.position = EffectorPosition;
        }
    }

    public bool MoveIKHandle(Vector3 displacement)
    {
        if(ikHandle == null)
        {
            return false; 
        }

        ikHandle.position += displacement;

        return true; 
    }

    /// <summary>
    /// Rotate transform 
    /// </summary>
    /// <param name="name"></param>
    /// <param name="eulerAngles"></param>
    /// <param name="space"></param>
    /// <returns></returns>
    public bool Rotate(string name, Vector3 eulerAngles, Space space = Space.Self)
    {
        Transform targetTransform = transform.FindTransform(name);

        if(targetTransform == null)
        {
            Debug.LogWarningFormat("Couldn't find transform with the name {0}", name);
            return false; 
        }

        targetTransform.Rotate(eulerAngles, space);

        // update corresponding joint (if exist) 
        foreach(var joint in joints)
        {
            if(joint.transform == targetTransform)
            {
                joint.SaveState(); 
            }
        }

        if (ikHandle != null)
        {
            if (!solverActive)
            {
                ikHandle.position = EffectorPosition;
            }
        }

        return true; 
    }

    /// <summary>
    /// Rotate the base towards the current target 
    /// </summary>
    void UpdateBaseJoint()
    {
        if (baseJoint == null || baseJoint.transform == null)
        {
            return;
        }

        Vector3 jointPosition = baseJoint.transform.position;
        Vector3 targetPosition = CurrentTarget.position;

        Vector3 direction = (targetPosition - jointPosition);

        Quaternion targetRotation = Quaternion.Euler(
            baseJoint.transform.rotation.eulerAngles.x,
            Quaternion.LookRotation(direction).eulerAngles.y,
            baseJoint.transform.rotation.eulerAngles.z);

        if (dampingEnabled)
        {
            targetRotation = Quaternion.Lerp(baseJoint.transform.rotation, targetRotation, damping);
        }

        baseJoint.transform.rotation = targetRotation;
    }

    /// <summary>
    /// Using inverse kinematics to rotate rotates the current target 
    /// </summary>
    void CalculateCCDForJointChain()
    {
        if(joints == null || joints.Count == 0)
        {
            return; 
        }        

        int index = joints.Count - 1;

        while(index >= 0 && (EffectorPosition - CurrentTarget.position).sqrMagnitude > ikTargetDistanceThreshold)
        {
            //getting the vector between the new destination and the joint's world position
            Vector3 jointPosition = joints[index].transform.position;
            Vector3 jointWorldPositionToDestination = (CurrentTarget.position - jointPosition);

            //getting the vector between the end effector and the joint's world position
            Vector3 effectorPosition = joints.Last().transform.position;
            Vector3 boneWorldToEndEffector = (effectorPosition - jointPosition);

            jointWorldPositionToDestination.Normalize();
            boneWorldToEndEffector.Normalize();

            // calculate the angle between jointWorldPositionToDestination and boneWorldToEndEffector
            float angle = Vector3.Dot(boneWorldToEndEffector, jointWorldPositionToDestination);

            if (angle < 0.99999f)
            {
                // use cross product to get the axis we are rotating around 
                Vector3 cross = Vector3.Cross(boneWorldToEndEffector, jointWorldPositionToDestination);
                cross.Normalize();

                angle = Mathf.Acos(angle);

                angle = angle * Mathf.Rad2Deg;

                Quaternion currentRotation = joints[index].transform.rotation;
                Quaternion targetRotation = Quaternion.AngleAxis(angle, cross) * currentRotation;

                if (dampingEnabled)
                {
                    targetRotation = Quaternion.Lerp(currentRotation, targetRotation, damping);
                }

                joints[index].transform.rotation = targetRotation;

                ConstraintJoint(joints[index]);
            }

            index--;
        } 
    }

    /// <summary>
    /// Limit the rotation based on the given constraints and, if locking axis by default, remove changes on 
    /// axis that have no constraints assigned to them 
    /// </summary>
    /// <param name="joint"></param>
    void ConstraintJoint(Joint joint)
    {
        Vector3 eulerAngles = joint.transform.localRotation.eulerAngles;

        eulerAngles.x %= 360;
        eulerAngles.y %= 360;
        eulerAngles.z %= 360;

        int appliedAxis = 0; 

        foreach (var constraint in joint.constraints)
        {
            appliedAxis |= (int)constraint.axis;
            ApplyConstraint(constraint, ref eulerAngles);
        }

        if (joint.lockAxisByDefault)
        {
            if((appliedAxis & (int)Axis.X) == 0)
            {
                RestraintAxisToOriginal(Axis.X, joint, ref eulerAngles); 
            }

            if ((appliedAxis & (int)Axis.Y) == 0)
            {
                RestraintAxisToOriginal(Axis.Y, joint, ref eulerAngles);
            }

            if ((appliedAxis & (int)Axis.Z) == 0)
            {
                RestraintAxisToOriginal(Axis.Z, joint, ref eulerAngles);
            }
        }

        joint.transform.localEulerAngles = eulerAngles;
    }

    /// <summary>
    /// Apply the rotation limits to the given eulerAngles
    /// </summary>
    /// <param name="constraint"></param>
    /// <param name="eulerAngles"></param>
    void ApplyConstraint(Constraint constraint, ref Vector3 eulerAngles)
    {
        if (!constraint.enabled)
        {
            return; 
        }

        if (constraint.axis == Axis.X)
        {
            eulerAngles.x = Mathf.Clamp(eulerAngles.x, constraint.min, constraint.max);
        }
        else if (constraint.axis == Axis.Y)
        {
            eulerAngles.y = Mathf.Clamp(eulerAngles.y, constraint.min, constraint.max);
        }
        else if (constraint.axis == Axis.Z)
        {
            eulerAngles.z = Mathf.Clamp(eulerAngles.z, constraint.min, constraint.max);
        }
    }

    /// <summary>
    /// Remove changes on axis that have no constraints assigned to them 
    /// </summary>
    /// <param name="axis"></param>
    /// <param name="joint"></param>
    /// <param name="eulerAngles"></param>
    void RestraintAxisToOriginal(Axis axis, Joint joint, ref Vector3 eulerAngles)
    {
        if (axis == Axis.X)
        {
            eulerAngles.x = joint.initialRotation.eulerAngles.x;
        }
        else if (axis == Axis.Y)
        {
            eulerAngles.y = joint.initialRotation.eulerAngles.y;
        }
        else if (axis == Axis.Z)
        {
            eulerAngles.z = joint.initialRotation.eulerAngles.z;
        }
    }

    /// <summary>
    /// Return to oroginal pose 
    /// </summary>
    public void Reset()
    {
        foreach (var joint in joints)
        {
            joint.RestoreState(); 
        }
    }

    #region BoundingBox methods 

    public Bounds GetBounds(BoundsType boundsType = BoundsType.Default)
    {
        switch (boundsType)
        {
            case BoundsType.Base:
                return GetBaseBounds();
            case BoundsType.Placement:
                return GetPlacementBounds(); 
        }

        return GetDefaultBounds();
    }

    Bounds GetDefaultBounds()
    {
        BoxCollider bc = GetComponent<BoxCollider>();

        if (bc != null)
        {
            return bc.bounds;
        }

        Bounds bounds = new Bounds(transform.position, Vector3.zero);

        {
            Renderer renderer = GetComponent<Renderer>();
            if (renderer != null)
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers != null && renderers.Length > 0)
        {
            foreach (var renderer in renderers)
            {
                bounds.Encapsulate(renderer.bounds);
            }
        }

        return bounds;
    }

    Bounds GetBaseBounds()
    {
        List<Bounds> allBounds = new List<Bounds>();

        float lowestPoint = float.MaxValue;

        {
            Renderer renderer = GetComponent<Renderer>();
            if (renderer != null)
            {
                allBounds.Add(renderer.bounds);
                lowestPoint = Mathf.Min(lowestPoint, renderer.bounds.min.y);
            }
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers != null && renderers.Length > 0)
        {
            foreach (var renderer in renderers)
            {
                allBounds.Add(renderer.bounds);
                lowestPoint = Mathf.Min(lowestPoint, renderer.bounds.min.y);
            }
        }

        List<Bounds> lowestBounds = allBounds.Where(b => Mathf.Abs(b.min.y - lowestPoint) < 0.01f).ToList();

        Bounds bounds = new Bounds(transform.position, Vector3.zero);

        foreach (var b in lowestBounds)
        {
            bounds.Encapsulate(b);
        }

        return bounds;
    }

    Bounds GetPlacementBounds()
    {
        List<Bounds> allBounds = new List<Bounds>();

        float lowestPoint = float.MaxValue;
        float highestPoint = float.MinValue;

        {
            Renderer renderer = GetComponent<Renderer>();
            if (renderer != null)
            {
                allBounds.Add(renderer.bounds);
                lowestPoint = Mathf.Min(lowestPoint, renderer.bounds.min.y);
                highestPoint = Mathf.Max(highestPoint, renderer.bounds.max.y);
            }
        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        if (renderers != null && renderers.Length > 0)
        {
            foreach (var renderer in renderers)
            {
                allBounds.Add(renderer.bounds);
                lowestPoint = Mathf.Min(lowestPoint, renderer.bounds.min.y);
                highestPoint = Mathf.Max(highestPoint, renderer.bounds.max.y);
            }
        }

        List<Bounds> lowestBounds = allBounds.Where(b => Mathf.Abs(b.min.y - lowestPoint) < 0.01f).ToList();

        Bounds bounds = new Bounds(transform.position, Vector3.zero);

        foreach (var b in lowestBounds)
        {
            bounds.Encapsulate(b);
        }

        // extend to it's max height and a little half of the robot arms posed length 
        const float paddingXZ = 1.5f;
        float maxXZ = Mathf.Max(bounds.size.x, bounds.size.z); 
        Vector3 size = new Vector3(maxXZ * paddingXZ, (highestPoint - lowestPoint), maxXZ * paddingXZ);
        Vector3 center = new Vector3(transform.position.x, bounds.center.y + ((size.y - bounds.size.y) * 0.5f), bounds.center.z + (size.z - maxXZ) * 0.5f);

        return new Bounds(center, size); 
    }

    #endregion 

    #region GameObject / FriendlyName Lookup 

    public string GetFriendlyNameForGameObject(string gameObjectName)
    {
        foreach (var hfn in friendlyNameLookup)
        {
            if (hfn.gameObjectName.Equals(gameObjectName, StringComparison.OrdinalIgnoreCase))
            {
                return hfn.friendlyName;
            }
        }

        return string.Empty;
    }

    public string GetGameObjectNameForFriendlyName(string friendlyName)
    {
        foreach (var hfn in friendlyNameLookup)
        {
            if (hfn.friendlyName.Equals(friendlyName, StringComparison.OrdinalIgnoreCase))
            {
                return hfn.gameObjectName;
            }
        }

        return string.Empty;
    }

    public GameObject GetGameObjectWithName(string gameObjectName)
    {
        if(gameObject.name.Equals(gameObjectName, StringComparison.OrdinalIgnoreCase))
        {
            return gameObject; 
        }

        var childTransform = transform.FindChild(gameObjectName);
        if(childTransform != null)
        {
            return childTransform.gameObject; 
        }

        return null; 
    }

    #endregion 
}
