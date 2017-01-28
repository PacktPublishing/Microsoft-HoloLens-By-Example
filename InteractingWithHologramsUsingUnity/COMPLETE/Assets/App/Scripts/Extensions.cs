using System; 
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class Extensions {

	public static float Length(this Color color)
    {
        return Mathf.Sqrt(color.r * color.r + color.g * color.g + color.b * color.b + color.a * color.a); 
    }

    public static Transform FindTransform(this Transform transform, string name)
    {        
        Queue<Transform> q = new Queue<Transform>();
        q.Enqueue(transform); 

        while(q.Count > 0)
        {
            Transform t = q.Dequeue();
            if (t.name.Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return t;
            }

            foreach(Transform child in t.transform)
            {
                q.Enqueue(child); 
            }
        }

        return null; 
    }
}
