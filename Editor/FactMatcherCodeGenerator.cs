#if UNITY_EDITOR

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

using UnityEditor;
using UnityEngine;

public class FactMatcherCodeGenerator  
{

	//Todo write test cases
    
    public static void GenerateFactIDS(string filename,string namespaceName,RulesDB rulesDB)
    {
	    
		var allKnownFacts = ExtractAllKnownFacts(rulesDB);
		var allKnownRules= ExtractAllKnownRules(rulesDB);
		var classContents = BuildClassContents(namespaceName, allKnownFacts,allKnownRules);

		var assetPathToSave = $"Assets/{filename}";
		
		string filePath = Path.Combine(Directory.GetCurrentDirectory(), assetPathToSave);
		string directoryPath = Path.GetDirectoryName(Path.Combine(Directory.GetCurrentDirectory(), assetPathToSave));
		if (!Directory.Exists(directoryPath))
		{
			Directory.CreateDirectory(directoryPath);
		}
		// The following is needed if you are using Windows
		#if UNITY_EDITOR_WIN
		filePath = filePath.Replace("/", "\\");
#endif
		File.WriteAllText(filePath, classContents);
		AssetDatabase.ImportAsset(assetPathToSave);
    }

	private static List<(string,int)> ExtractAllKnownRules(RulesDB rulesDB)
	{
		 List<(string,int)> allKnownRules = new List<(string,int)>();
		foreach (var rule in rulesDB.rules)
		{
			var ruleName = GenRuleName(rule);
			allKnownRules.Add((ruleName,rule.RuleID));
		}

		return allKnownRules;
	}

	public static string GenRuleName(RuleDBEntry rule)
	{
		var ruleName = rule.ruleName.Replace(".", "_");
		ruleName = ruleName.Trim('_');
		return ruleName;
	}

	private static Dictionary<string, List<(string,int)>> ExtractAllKnownFacts(RulesDB rulesDB)
	{
		Dictionary<string, List<(string,int) >> allKnownFacts = new Dictionary<string, List<(string,int)>>();
		//Todo handle clashing keys , for instance  A.FactDB with global and B.FactDB also with global
		//extract all facts..
		foreach (var rule in rulesDB.rules)
		{
			if (!allKnownFacts.ContainsKey(rule.ruleName))
			{
				allKnownFacts[rule.ruleName] = new List<(string,int)>();
			}


			foreach (var atom in rule.atoms)
			{
				//Parsing out atom.factName which is expected to be in form namespace.fact , for instance player.health is namespace player and fact health.
				if (atom.factName!=null && atom.factName.Length > 1)
				{
					var genNames = CreateFactVariableNameFromFact(atom.factName);
					var key = genNames.Item1;
					var factName  = genNames.Item2;
					if (!allKnownFacts.ContainsKey(key))
					{
						allKnownFacts[ key ] = new List<(string,int)>();
					}

					if (!allKnownFacts[key].Contains((factName,atom.factID)))
					{
						allKnownFacts[ key ].Add((factName,atom.factID));
					}
						
				}
			}

			foreach (var factWrite in rule.factWrites)
			{
				
				if (factWrite.factName !=null && factWrite.factName.Length > 1)
				{
					var genNames = CreateFactVariableNameFromFact(factWrite.factName);

					var key = genNames.Item1;
					var factName  = genNames.Item2;
					
					if (!allKnownFacts.ContainsKey(key))
					{
						allKnownFacts[ key ] = new List<(string,int)>();
					}

					if (!allKnownFacts[key].Contains((factName,factWrite.factID)))
					{
						allKnownFacts[ key ].Add((factName,factWrite.factID));
					}
					
				}
			}
		}

		return allKnownFacts;
	}

	public static (string,string) CreateFactVariableNameFromFact(string factName)
	{
		
		var className = factName;
		var variableName = factName;
		var splitted = factName.Split('.');
		if (splitted.Length <= 2)
		{
			className = splitted[Mathf.Max(splitted.Length - 2, 0)];
			variableName = splitted[Mathf.Max(splitted.Length - 1, 0)];

			if (className.Equals(factName))
			{
				className = "Global";
			}
		}
		else
		{
			Debug.LogError("Format of a fact should be \"namespace.fact\" or just \"fact\"");
		}
		return (className + "Facts", variableName);
	}

	/// <summary>
        /// Creates a string with the contents of the class beginning with namespace and classname
        /// then it iterates over all the enum values and creates a const string for each of them.
        /// Finally, it creates a method containing a huge switch case where you can retreive said const strings easily.
        /// </summary>
        /// <param name="enumType"></param>
        /// <param name="namespaceName"></param>
        /// <param name="enumName"></param>
        /// <returns></returns>
        private static string BuildClassContents(string namespaceName, Dictionary<string, List<(string,int)> > facts, List<(string,int)>  rules)
	{
		//tabs is four spaces
		string tabs = "    ";
            var stringBuilder = new StringBuilder();
            stringBuilder.AppendLine("using Unity.Collections;");
            stringBuilder.AppendLine();
            
            bool isUsingNamespace = !string.IsNullOrEmpty(namespaceName);
            if (isUsingNamespace)
            {
                stringBuilder.AppendLine("namespace " + namespaceName);
                stringBuilder.AppendLine("{");
            }


			stringBuilder.AppendLine((isUsingNamespace ? tabs : "") + "public static class " + "RuleIDs");
			stringBuilder.AppendLine((isUsingNamespace ? tabs : "") + "{");
			
            for (int ruleIndex = 0; ruleIndex < rules.Count; ruleIndex++)
            {
	            var rule = rules[ruleIndex].Item1;
	            stringBuilder.AppendLine((isUsingNamespace ? tabs : "") + "\tpublic const int " + rule + " = " + rules[ruleIndex].Item2+ ";");
            }
				
            stringBuilder.AppendLine((isUsingNamespace ? tabs : "") + "}").AppendLine();
            
            int index = 0;
            foreach (var factContainer in facts)
            {
	            if (factContainer.Value.Count >= 1)
	            {
					stringBuilder.AppendLine((isUsingNamespace ? tabs : "") + "public static class " + factContainer.Key);
					stringBuilder.AppendLine((isUsingNamespace ? tabs : "") + "{");
					foreach (var nameAndID in factContainer.Value)
					{
						stringBuilder.AppendLine((isUsingNamespace ? tabs : "") + $"{tabs}public const int " + nameAndID.Item1 + " = " + nameAndID.Item2 + ";");
						index++;
					}
				
					stringBuilder.AppendLine((isUsingNamespace ? tabs : "") + "}").AppendLine();
	            }
            }
            

            
			stringBuilder.AppendLine((isUsingNamespace ? tabs : "") + "public static class FactMatcherData");
			stringBuilder.AppendLine((isUsingNamespace ? tabs : "") + "{");
			stringBuilder.AppendLine((isUsingNamespace ? tabs : "") + $"{tabs}public static NativeArray<float> CreateFactValues()"); 
			stringBuilder.AppendLine((isUsingNamespace ? tabs : "") + "{"); 
			stringBuilder.AppendLine((isUsingNamespace ? tabs : "") + tabs + $"return new NativeArray<float>({index},Allocator.Persistent);"); 
			stringBuilder.AppendLine((isUsingNamespace ? tabs : "") + "}"); 
			
			stringBuilder.AppendLine((isUsingNamespace ? tabs : "") + "}");
            
            if (isUsingNamespace)
            {
                stringBuilder.AppendLine("}");
            }


            string classContents = stringBuilder.ToString();
            return classContents;
        }
}

#endif