using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace FactMatching
{
    public static class Extensions 
    {
        #if UNITY_EDITOR
        public static TextAsset FromPathToTextAsset(string filePath)
        {
            return AssetDatabase.LoadAssetAtPath<TextAsset>(filePath);
        }

        public static string GetFullPathFromObject(UnityEngine.Object @object)
        {
            #if UNITY_EDITOR_WIN
            char pathSeparator = '\\';
            #else
            char pathSeparator = '/';
            #endif

            string workingDirectory = Environment.CurrentDirectory;
            string[] splittedPath = AssetDatabase.GetAssetPath(@object).Split('/');

            string assetFolderPath = $"{workingDirectory}/{string.Join(pathSeparator, splittedPath[..^1])}";
            #if UNITY_EDITOR_WIN
            assetFolderPath = assetFolderPath.Replace("/", "\\");
            #endif

            return $"{assetFolderPath}{pathSeparator}{splittedPath[^1]}";
        }
        
        public static string GetFullPathFromObject(UnityEngine.Object @object, out string folderPath)
        {
            #if UNITY_EDITOR_WIN
            char pathSeparator = '\\';
            #else
            char pathSeparator = '/';
            #endif

            string workingDirectory = Environment.CurrentDirectory;
            string[] splittedPath = AssetDatabase.GetAssetPath(@object).Split('/');

            folderPath = $"{workingDirectory}/{string.Join(pathSeparator, splittedPath[..^1])}";
            #if UNITY_EDITOR_WIN
            folderPath = folderPath.Replace("/", "\\");
            #endif

            return $"{folderPath}{pathSeparator}{splittedPath[^1]}";
        }

        public static string GetFullPathFromObject(UnityEngine.Object @object, out string folderPath, out string fileName)
        {
            string workingDirectory = Environment.CurrentDirectory;
            string path = AssetDatabase.GetAssetPath(@object);
            string fullPath = workingDirectory + "/" + path;

            #if UNITY_EDITOR_WIN
            fullPath = fullPath.Replace("/", "\\");
            #endif

            folderPath = Path.GetDirectoryName(fullPath);
            fileName = Path.GetFileName(fullPath);
            return fullPath;
        }

        public static List<T> GetAllInstances<T>() where T : ScriptableObject
        {
            string filter = "t:" + typeof(T).Name;
            string[] guids = AssetDatabase.FindAssets(filter);
            
            List<T> instances = new(guids.Length);
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                T instance = AssetDatabase.LoadAssetAtPath<T>(path);

                instances.Add(instance);
            }
            return instances;
        }
        #endif

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
