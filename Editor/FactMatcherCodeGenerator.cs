#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

public class FactMatcherCodeGenerator
{
    public static void GenerateFactIDS(string fullFilePath,string namespaceName,RulesDB rulesDB)
    {
		List<string> allKnownFacts = ExtractAllKnownFactNames(rulesDB);
		List<string> allKnownFactStrings = ExtractAllKnownFactStrings(rulesDB);
        List<string> allKnownRules= ExtractAllKnownRuleNames(rulesDB);
		string classContents = BuildClassContents(namespaceName, allKnownFacts, allKnownRules, allKnownFactStrings);

		string directoryPath = Path.GetDirectoryName(fullFilePath);
		string fileName = Path.GetFileName(fullFilePath);

		// The following is needed if you are using Windows
		#if UNITY_EDITOR_WIN
		fullFilePath = fullFilePath.Replace("/", "\\");
		#endif

		File.WriteAllText(fullFilePath, classContents);
    }

    private static List<string> ExtractAllKnownRuleNames(RulesDB rulesDB)
	{
		 List<string> allKnownRules = new();
		foreach (var rule in rulesDB.rules)
		{
			string ruleName = GenVarName(rule.ruleName);
			allKnownRules.Add(ruleName);
		}
		return allKnownRules;
	}

	public static string GenVarName(string currentName)
	{
		string resultName = currentName.Replace(".", "_");
		resultName = resultName.Trim('_');
		return resultName;
	}

    private static List<string> ExtractAllKnownFactNames(RulesDB rulesDB)
    {
        List<string> factNames = new();
        foreach (var rule in rulesDB.rules)
        {
            foreach (var fact in rule.factTests)
            {
                string factName = GenVarName(fact.factName);
                factNames.Add(factName);
            }
        }
        return factNames;
    }

	private static List<string> ExtractAllKnownFactStrings(RulesDB rulesDB)
	{
		List<string> factStringNames = new();
		foreach (var rule in rulesDB.rules)
		{
			foreach(var fact in rule.factTests)
			{
				if (fact.compareType == FactValueType.String)
				{
					factStringNames.Add(fact.matchString);
				}
			}
		}
		return factStringNames;
	}

	private static string BuildClassContents(string namespaceName, List<string> facts, List<string> rules, List<string> factStrings)
    {
        string tabs = "\t";
        StringBuilder stringBuilder = new();
        string[] usings = { "FactMatching" };
        stringBuilder.AppendLine(GenerateUsing(usings));
        stringBuilder.AppendLine();
        stringBuilder.AppendLine(GeneratePublicClassStart(namespaceName ?? "GenericClassName"));
        stringBuilder.AppendLine();

        Dictionary<string, List<string>> structs = new()
        {
            { "FactIDS", facts.Distinct().ToList() },
			{ "StringIDS", factStrings.Distinct().ToList() },
			{ "RuleIDS", rules.Distinct().ToList() }
        };

		Dictionary<string, List<string>> variables = new();
        foreach (string structName in structs.Keys)
        {
            variables.Add(structName, new List<string>());
        }

        stringBuilder.AppendLine(GenerateStructs(structs, tabs));
		stringBuilder.AppendLine();

		stringBuilder.AppendLine(GeneratePublicVariables(variables, out List<string> variableNames));
		stringBuilder.AppendLine();

		List<string> assignVariablesTo = new();
		foreach (string variableName in variables.Keys)
		{
			assignVariablesTo.Add($"new {variableName}(factMatcher)");
		}
		List<string> variableAssignments = GenerateVariableAssignments(variableNames, assignVariablesTo);
		stringBuilder.AppendLine(GeneratePublicFunction(namespaceName, "FactMatcher factMatcher", variableAssignments, tabs));

        stringBuilder.AppendLine("}");
        string classContents = stringBuilder.ToString();
        return classContents;
    }

    private static string GenerateUsing(string[] usings)
	{
		string usingString = "";
		foreach (var item in usings)
		{
			usingString += $"using {item};";
		}
		return usingString;
	}

    private static string GeneratePublicClassStart(string className, string tabs = "")
    {
		string result = string.Empty;
        result += tabs + $"public class {className}";
        result += tabs + "\n{";
        return result;
    }

	private static string GenerateStructs(Dictionary<string, List<string>> structs, string tabs = "\t")
    {
		string result = string.Empty;
		foreach(var function in structs)
		{
			string functionName = function.Key;
            List<string> variableNames = structs[functionName].ToList();
            List<string> contains = GeneratePublicInts(variableNames);
			List<string> variableAssignment = GenerateGetGivenFromFactMatcher(variableNames, functionName.Remove(functionName.Length - 1));
			contains.Add("\n" + GeneratePublicFunction(functionName, "FactMatcher factMatcher", variableAssignment, tabs + tabs));
			result += "\n\n" + GeneratePublicStruct(tabs, functionName, contains);
		}
		return result.TrimStart('\n');
    }

    private static string GeneratePublicStruct(string tabs, string structName, List<string> contains)
    {
        string contain = string.Empty;
        foreach (var content in contains)
        {
            contain += '\n' + tabs + "\t" + content;
        }
        contain = contain.TrimStart('\n');

        return
            tabs + $"public struct {structName}\n" +
            tabs + "{\n" +
            contain + '\n' +
            tabs + "}";
    }

	private static List<string> GeneratePublicInts(List<string> intVariableNames)
	{
        List<string> result = new();
        foreach (var name in intVariableNames)
        {
			string resultString;
			if (name == "true" || name == "false")
			{
                resultString = $"public int _{name.Replace(' ', '_')};";
            }
			else
			{
				resultString = $"public int {name.Replace(' ', '_')};"; 
			}
			result.Add(resultString);
        }
        return result;
    }
	
    private static string GeneratePublicFunction(string functionName, string functionParam, List<string> contains, string tabs = "\t")
	{
		string contain = string.Empty;
		foreach (var content in contains)
		{
			contain += '\n' + tabs + "\t" + content;
		}
		contain = contain.TrimStart('\n');

		return
			$"{tabs}public {functionName.Replace(' ', '_')}({functionParam})\n" +
			tabs + "{\n" +
			contain + '\n' +
			tabs + "}";
	}

	private static List<string> GenerateGetGivenFromFactMatcher(List<string> variableNames, string factMatcherFunction)
	{
        List<string> result = new();
        foreach (var name in variableNames)
        {
			if(name == "true" || name == "false")
			{
                result.Add($"_{name.Replace(' ', '_')} = factMatcher.{factMatcherFunction}(\"{name}\");");
            }
			else
			{
				result.Add($"{name.Replace(' ', '_')} = factMatcher.{factMatcherFunction}(\"{name}\");"); 
			}
        }
        return result;
    }
	
	private static List<string> GenerateVariableAssignments(List<string> variableNames, List<string> assignments)
	{
        List<string> result = new();
        for (int i = 0; i < variableNames.Count; i++)
        {
            string name = variableNames[i];
			string assignAs = assignments[i];
			if (assignAs == null || assignAs == "")
			{
				result.Add("// Error while adding assignment");
				break;
			}
			if (name == "true" || name == "false")
			{
                result.Add($"_{name.Replace(' ', '_')} = {assignAs};");
            }
			else
			{
				result.Add($"{name.Replace(' ', '_')} = {assignAs};"); 
			}
        }
        return result;
    }

	private static string GeneratePublicVariables(Dictionary<string, List<string>> variables, out List<string> variableNames, string tabs = "\t")
	{
		string result = string.Empty;
		variableNames = new List<string>();
		foreach (var variable in variables)
		{
			foreach (var variableName in variable.Value)
			{
				string newVariableName = variableName.Replace(' ', '_');
				string variableKey = variable.Key.Replace(' ', '_');
				if (variableName != "")
				{
					variableNames.Add(newVariableName);
                    result += '\n' + tabs + $"public {variableKey} {newVariableName};";
                }
				else
				{
					string theVariableName = char.ToLower(variableKey[0]) + variableKey[1..];
                    variableNames.Add(theVariableName);
                    result += '\n' + tabs + $"public {variableKey} {theVariableName};";
				}
			}
			if (variable.Value == null || variable.Value.Count <= 0)
			{
                string theVariableName = char.ToLower(variable.Key[0]) + variable.Key[1..];
                variableNames.Add(theVariableName);
                result += '\n' + tabs + $"public {variable.Key} {theVariableName};";
			}
		}
		if (result == string.Empty)
		{
			result += tabs + "// Failed to generate publicVariables";
		}
		return result.TrimStart('\n');
	}
}
#endif
