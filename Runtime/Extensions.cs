using System.Collections.Generic;
using UnityEngine;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FactMatching
{
    public static class Extensions 
    {
        public static TextAsset FromPathToTextAsset(string filePath)
        {
            #if UNITY_EDITOR
            return AssetDatabase.LoadAssetAtPath<TextAsset>(filePath);
            #endif
            return null;
        }

        public static bool IsNullOrWhitespace(this string str)
        {
            if (!string.IsNullOrEmpty(str))
            {
                for (int index = 0; index < str.Length; ++index)
                {
                    if (!char.IsWhiteSpace(str[index]))
                    {
                        return false;
                    }
                }
            }
            return true;
        }
        
        public static bool IsNullOrEmpty<T>(this IList<T> list) => list == null || list.Count == 0;
    }

    static class KeywordExtensions
    {
        public static string SubstringBetweenIndexes(this string value, int startIndex, int endIndex)
        {
            try
            {
                return value[startIndex..endIndex];
            }
            catch (Exception)
            {
                throw;
            }
        }

        public static string GetBetween(this string strSource, string strStart, string strEnd)
        {
            if (strSource.Contains(strStart) && strSource.Contains(strEnd))
            {
                int start = strSource.IndexOf(strStart, 0) + strStart.Length;
                int end = strSource.IndexOf(strEnd, start);
                return strSource[start..end];
            }

            throw new Exception($"There was no string between ({strStart}) and ({strEnd})");
        }
        
        public static string GetBetween(this string strSource, char charStart, char charEnd)
        {
            if (strSource.Contains(charStart) && strSource.Contains(charEnd))
            {
                int start = strSource.IndexOf(charStart, 0) + 1;
                int end = strSource.IndexOf(charEnd, start);
                return strSource[start..end];
            }

            throw new Exception($"There was no string between ({charStart}) and ({charEnd})");
        }
    }
}
