using System; 
using System.Collections;
using System.Collections.Generic;
using System.Linq; 

public static class Utils {

    public static IEnumerable<int> SteppedRange(int start, int count, int step)
    {
        for(int i=start; i < start + count; i+= step)
        {
            yield return i; 
        }
    }
}
