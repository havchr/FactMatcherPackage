#if UNITY_EDITOR

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using System;

public class FactMatcherCodeGenerator
{
    public static void GenerateFactIDS(string fullFilePath, string namespaceName, RulesDB rulesDB)
    {
		List<string> allKnownFacts = ExtractAllKnownFactNames(rulesDB);
		List<string> allKnownFactStrings = ExtractAllKnownFactStrings(rulesDB);
		List<string> allKnownEnums = ExtractAllKnownEnums(rulesDB);
        List<string> allKnownRules = ExtractAllKnownRuleNames(rulesDB);
		 
		string classContents = BuildClassContents(namespaceName, allKnownFacts, allKnownRules, allKnownFactStrings,allKnownEnums);

		string directoryPath = Path.GetDirectoryName(fullFilePath);
		string fileName = Path.GetFileName(fullFilePath);

		#if UNITY_EDITOR_WIN // The following will run if you are using Windows
        fullFilePath = fullFilePath.Replace("/", "\\");
		#endif

		File.WriteAllText(fullFilePath, classContents);
    }

    private static List<string> ExtractAllKnownEnums(RulesDB rulesDB)
    {
	    
		 List<string> result = new();
		 foreach (var doc in rulesDB.DocumentationList)
		 {
			 foreach (var fact in doc.Facts)
			 {
				 foreach (var enumName in fact.EnumNames)
				 {
					 result.Add(enumName);
				 }
			 }
		 }
		 return result;
    }

    private static List<string> ExtractAllKnownRuleNames(RulesDB rulesDB)
	{
		 List<string> allKnownRules = new();
		foreach (var rule in rulesDB.rules)
		{
			string ruleName = GenVarName(rule.ruleName);
			allKnownRules.Add(ruleName);
		}
		return allKnownRules.SortByInt();
	}

	public static string GenVarName(string currentName)
	{
		string resultName = currentName.ToVariableName();
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
		return factNames.SortByInt();
    }

	private static List<string> ExtractAllKnownFactStrings(RulesDB rulesDB)
	{
		List<string> factListNames = new();
		foreach (var rule in rulesDB.rules)
		{
			foreach(var fact in rule.factTests)
			{
				if (fact.compareType == FactValueType.String)
				{
					factListNames.Add(fact.matchString);
				}
			}
		}
		return factListNames.SortByInt();
	}

	private static string BuildClassContents(string namespaceName, List<string> facts, List<string> rules, List<string> factStrings,List<string> enumNames)
    {
        string tabs = "\t";
        StringBuilder stringBuilder = new();
        string[] usings = { "FactMatching" };
        stringBuilder.AppendLine(GenerateUsing(usings));
        stringBuilder.AppendLine();
        stringBuilder.AppendLine(GeneratePublicClassStart(namespaceName ?? "GenericClassName"));

		Dictionary<string, List<string>> structs = new();
		if (facts != null && facts.Count > 0)
		{
			structs.Add("FactIDS", facts.Distinct().ToList()); 
		}
		if (factStrings != null && factStrings.Count > 0)
		{
			structs.Add("StringIDS", factStrings.Distinct().ToList());
		}
		if (rules != null && rules.Count > 0)
		{
			structs.Add("RuleIDS", rules.Distinct().ToList());
		}

		Dictionary<string, List<string>> variables = new();
        foreach (string structName in structs.Keys)
        {
            variables.Add(structName, new List<string>());
        }

        stringBuilder.Append(GenerateStructs(structs,enumNames,tabs));
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
			usingString += $"\nusing {item};";
		}
		return usingString.Trim('\n');
	}

    private static string GeneratePublicClassStart(string className, string tabs = "")
    {
		string result = string.Empty;
        result += tabs + $"public class {className}";
        result += tabs + "\n{";
        return result;
    }

	private static string GenerateStructs(Dictionary<string, List<string>> structs,List<string> enumNames, string tabs = "\t")
    {
		string result = string.Empty;
		foreach(var function in structs)
		{
			string functionName = function.Key;
            List<string> variableNames = structs[functionName].ToList();
            List<string> contains = GeneratePublicInts(variableNames);
			List<string> variableAssignment = GenerateGetGivenFromFactMatcher(variableNames, functionName.Remove(functionName.Length - 1));
			if (function.Key.Equals("StringIDS"))
			{
				foreach (var enumName in enumNames)
				{
					string varName = $"{enumName}StringIDS";
					contains.Add($"public int[] {varName};");
					variableAssignment.Add($"{varName} = FactMatching.Functions.CreateStringIDSFromEnum(factMatcher, typeof({enumName}));");	
				}
			}
			contains.Add("\n" + GeneratePublicFunction(functionName, "FactMatcher factMatcher", variableAssignment, tabs + tabs));
			result += "\n" + GeneratePublicStruct(tabs, functionName, contains);
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
            tabs + "}\n";
    }

	private static List<string> GeneratePublicInts(List<string> intVariableNames)
	{
        List<string> result = new();
        foreach (var name in intVariableNames)
        {
            result.Add($"public int {name.ToVariableName()};");
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
            result.Add($"{name.ToVariableName()} = factMatcher.{factMatcherFunction}(\"{name}\");");
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
            result.Add($"{name.ToVariableName()} = {assignAs};");
        }
        return result;
    }

	private static string GeneratePublicVariables(Dictionary<string, List<string>> variables, out List<string> variableNames, string tabs = "\t")
	{
		string result = string.Empty;
		variableNames = new List<string>();
		if (variables.Count > 0)
		{
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
        }
		else
		{
			result = tabs + "// There is no variables.";
		}
		return result.TrimStart('\n');
	}
}

public static class Extensions
{
    public static List<string> SortByInt(this List<string> strings)
    {
        IOrderedEnumerable<string> sortedStrings = strings.OrderBy(s => s, new StringComparerByNumberAndAlphabetically());
        return sortedStrings.ToList();
    }

    public static string ToVariableName(this string input)
    {
        input = input.Trim();
        char[] harmfulChars = new[] { '(', ')', '-', '\\', '/', '.', ' ', '!', '"', '@', '#', '£', '¤', '$', '%',
            '&', '/', '{', '[', '(', ')', ']', '}', '=', '?', '+', '`', '´', '|', '§', '¨', '~', '^', '\'', '*' };

        foreach (var item in harmfulChars)
        {
            input = input.Replace(item, '_');
        }

        if (input == "true" || input == "false" || char.IsDigit(input[0]))
        {
            input = $"_{input}";
        }

        return input;
    }
}

/// <summary>
/// Gives a compare that is useful to sort based on ints in string
/// </summary>
public class StringComparerByNumberAndAlphabetically : IComparer<string>
{
    public int Compare(string x, string y)
    {
        if (string.IsNullOrEmpty(x) && string.IsNullOrEmpty(y))
        {
            return 0;
        }
        if (string.IsNullOrEmpty(x))
        {
            return 1;
        }
        if (string.IsNullOrEmpty(y))
        {
            return -1;
        }

        string commonX = ExtractCommonSubstring(x);
        string commonY = ExtractCommonSubstring(y);

        int xNum = ExtractNumber(x);
        int yNum = ExtractNumber(y);

        int commonComparison = string.Compare(commonX, commonY, StringComparison.OrdinalIgnoreCase);
        if (commonComparison != 0)
        {
            return commonComparison;
        }

        if (xNum != -1 && yNum != -1)
        {
            return xNum.CompareTo(yNum);
        }

        if (xNum != -1)
        {
            return -1;
        }
        if (yNum != -1)
        {
            return 1;
        }

        return string.Compare(x, y, StringComparison.Ordinal);
    }

    private int ExtractNumber(string str)
    {
        int numStartIndex = -1;
        int numEndIndex = -1;

        for (int i = 0; i < str.Length; i++)
        {
            if (char.IsDigit(str[i]))
            {
                numStartIndex = i;
                break;
            }
        }

        if (numStartIndex != -1)
        {
            for (int i = numStartIndex; i < str.Length; i++)
            {
                if (!char.IsDigit(str[i]))
                {
                    numEndIndex = i;
                    break;
                }
            }

            if (numEndIndex == -1)
            {
                numEndIndex = str.Length;
            }

            string numStr = str.Substring(numStartIndex, numEndIndex - numStartIndex);
            if (int.TryParse(numStr, out int num))
            {
                return num;
            }
        }

        return -1;
    }

    private string ExtractCommonSubstring(string str)
    {
        int commonEndIndex = 0;
        for (int i = 0; i < str.Length; i++)
        {
            if (!char.IsLetter(str[i]))
            {
                commonEndIndex = i;
                break;
            }
        }

        return str.Substring(0, commonEndIndex);
    }
}
#endif
