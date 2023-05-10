using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace FactMatching
{
    public static class Extensions 
    {
        public static bool IsNullOrWhitespace(this string str)
        {
            if (!string.IsNullOrEmpty(str))
            {
                for (int index = 0; index < str.Length; ++index)
                {
                    if (!char.IsWhiteSpace(str[index]))
                        return false;
                }
            }
            return true;
        }
        
        public static bool IsNullOrEmpty<T>(this IList<T> list) => list == null || list.Count == 0;
    }
    
}