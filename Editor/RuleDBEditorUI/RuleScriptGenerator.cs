using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;

namespace FactMatching
{
    
    public class RuleScriptTestData{

        public static readonly string[] testValuesStringNames = new string[]{"test","name","personality","favourite_food"};

        public static readonly string[] testValuesStringValues = new string[]{"peter",
            "frog",
            "oslo",
            "los angeles",
            "cat","dog","horse",
            "mouse","video","perfect",
            "true","false",
            "videogame","joystick","mario",
            "milkfroth" ,"single","married","lonely","bachelor"
        };


        public static readonly string[] testValuesFloatNames = new string[]{"milk_amount","water_amount","beer_amount","player.health","player.age","item_strength"};
        public static readonly float testValuesFloatRangeLower = -10.0f;
        public static readonly float testValuesFloatRangeUpper= 20.0f;
    }

    public class RuleScriptGenerator 
    {
    
        [MenuItem("Assets/Create/FactMatcher/DocumentationScript")]
        private static void CreateDocumentationScript(MenuCommand command)
        {
            var fileName = "Documentation_";
            File.WriteAllText(GetCreateAssetPathWithVersioningCounter(fileName, "dfmrs"), RuleScriptDefaultContent.DocumentationContent.TrimStart());
            AssetDatabase.Refresh();
        }

    
        [MenuItem("Assets/Create/FactMatcher/RuleScript")]
        private static void CreateRuleScript(MenuCommand command)
        {
            var fileName = "ruleScript_";
            File.WriteAllText(GetCreateAssetPathWithVersioningCounter(fileName, "fmrs"), RuleScriptDefaultContent.GetDefaultRuleScriptContent());
            AssetDatabase.Refresh();
		
        }
    
        [MenuItem("Assets/Create/FactMatcher/RuleScript Test (1000)")]
        private static void CreateRuleScriptMassiveTest1000(MenuCommand command)
        {
            int rules = 1000;
            var fileName = $"ruleScript_{rules}_rules_test";
            File.WriteAllText(GetCreateAssetPathWithVersioningCounter(fileName, "fmrs"), GetTestRuleScriptContent(rules));
            AssetDatabase.Refresh();
		
        }
    
        [MenuItem("Assets/Create/FactMatcher/RuleScript Test (10 000)")]
        private static void CreateRuleScriptMassiveTest10000(MenuCommand command)
        {
            int rules = 10000;
            var fileName = $"ruleScript_{rules}_rules_test";
            File.WriteAllText(GetCreateAssetPathWithVersioningCounter(fileName, "fmrs"), GetTestRuleScriptContent(rules));
            AssetDatabase.Refresh();
		
        }

        public static void MutateRuleScriptTestData(FactMatcher matcher, int mutations)
        {
            MutateRuleScriptTestData(matcher,mutations,
                RuleScriptTestData.testValuesStringNames,
                RuleScriptTestData.testValuesStringValues,
                RuleScriptTestData.testValuesFloatNames,
                RuleScriptTestData.testValuesFloatRangeLower,
                RuleScriptTestData.testValuesFloatRangeUpper);
        }
        private static void MutateRuleScriptTestData(FactMatcher matcher,int mutations, string[] stringNames, string[] stringVals, string[] floatNames, float rangeLower, float rangeUpper)
        {
            for (int i = 0; i < mutations; i++)
            {
                bool mutateString = Random.Range(0, 100) > 100;
                string mutation = mutateString ? stringNames[Random.Range(0, stringNames.Length)] : floatNames[Random.Range(0, floatNames.Length)];
                matcher[matcher.FactID(mutation)] = mutateString ? matcher.StringID(stringVals[Random.Range(0, stringVals.Length)]) : Random.Range(rangeLower, rangeUpper);
            }
        }

        private static string GetTestRuleScriptContent(int numRules)
        {
            var stringnames = RuleScriptTestData.testValuesStringNames;
            var stringvals = RuleScriptTestData.testValuesStringValues;
            var floatnames = RuleScriptTestData.testValuesFloatNames;
            float factRangeLow = RuleScriptTestData.testValuesFloatRangeLower;
            float factRangeUpper = RuleScriptTestData.testValuesFloatRangeUpper;
        
            var floatTest = new string[]{"<",">","=","<=",">="};
            var stringTest= new string[]{"="};
            StringWriter writer = new();
            for (int i = 0; i < numRules; i++)
            {
                writer.WriteLine($".test_rule_{i}");
                var stringnamesIndices = new int[stringnames.Length];
                var stringnamesAvailable = stringnamesIndices.Length;
                for (int j = 0; j < stringnamesIndices.Length; j++)
                {
                    stringnamesIndices[j] = j;
                }
                var floatnamesIndices = new int[floatnames.Length];
                var floatNamesAvailable = floatnamesIndices.Length;
                for (int j = 0; j < floatnamesIndices.Length; j++)
                {
                    floatnamesIndices[j] = j;
                }

                var numFacts = Random.Range(factRangeLow, factRangeUpper);
                for (int j = 0; j < numFacts; j++)
                {
                    if (Random.Range(0, 100) > 50)
                    {
                        var name = $"string_test_{j}";
                        if (stringnamesAvailable > 0)
                        {
                            var index= Random.Range(0,stringnamesAvailable);
                            stringnamesAvailable--;
                            stringnamesIndices[index] = stringnamesIndices[stringnamesAvailable];
                            name = stringnames[stringnamesIndices[index]];
                        }
                        writer.WriteLine($"    {name} {stringTest[Random.Range(0,stringTest.Length)]} {stringvals[Random.Range(0,stringvals.Length)]}");
                    }
                    else
                    {
                        var name = $"float_test_{j}";
                        if (floatNamesAvailable> 0)
                        {
                            var index= Random.Range(0,floatNamesAvailable);
                            floatNamesAvailable--;
                            floatnamesIndices[index] = floatnamesIndices[floatNamesAvailable];
                            name = floatnames[floatnamesIndices[index]];
                        }
                        writer.WriteLine($"    {name} {floatTest[Random.Range(0,floatTest.Length)]} {Random.Range(-500.0f,500.0f)}");
                    }
                }
                writer.WriteLine($".THEN RESPONSE");
                writer.WriteLine($".Response is rule {i}");
                writer.WriteLine($".END");
                writer.WriteLine();
            }

            return writer.ToString();
        }
    
        private static string GetCreateAssetPathWithVersioningCounter(string fileName, string fileExtension)
        {
            var dirPath = AssetDatabase.GetAssetPath(Selection.activeObject);
            var path = dirPath + "/" + fileName;
            var assets = AssetDatabase.FindAssets(fileName, new[]{dirPath});
            int fileNumber = 1;

            foreach (var assetGUID in assets)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(assetGUID);
                Debug.Log(assetPath);

                var splitted = assetPath.Split(new[] {fileName}, StringSplitOptions.RemoveEmptyEntries);

                for (int i = 0; i < splitted.Length; i++)
                {
                    Debug.Log($"split {i} is {splitted[i]}");
                }
                var number = splitted[1];
                if (splitted[1].Contains("."))
                {
                    number = splitted[1].Split('.')[0];
                    Debug.Log($"Number is {number}");
                }
                if(int.TryParse(number, out int potentialFileNumber))
                {
                    if (potentialFileNumber > fileNumber)
                    {
                        fileNumber = potentialFileNumber;
                    }
                }

            }
            fileNumber++;
            var finalPath = path + $"{fileNumber}.{fileExtension}.txt";
            if(finalPath.StartsWith("Assets"))
            {
                var splitOnAsset = finalPath.Split(new[] { "Assets" }, StringSplitOptions.RemoveEmptyEntries);
                return Application.dataPath + splitOnAsset[0];
            }
            return finalPath;
        }


    }
}