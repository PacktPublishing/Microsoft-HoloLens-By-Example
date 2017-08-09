using System; 
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Windows.Speech;

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

    public static bool Contains(this Array array, string key)
    {
        foreach(SemanticMeaning semanticMeaning in array)
        {
            if (semanticMeaning.key.Equals(key))
            {
                return true; 
            }
        }

        return false; 
    }

    public static SemanticMeaning? SafeGet(this Array array, string key)
    {
        foreach (SemanticMeaning semanticMeaning in array)
        {
            if (semanticMeaning.key.Equals(key))
            {
                return semanticMeaning;
            }
        }

        return null;
    }
}
