#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;


[CustomEditor(typeof(RulesDB))]
public class RuleDBEditor : Editor
{
    private RulesDB _rulesDB;

    private void OnEnable()
    {
        _rulesDB = (RulesDB)target;
    }

    List<ProblemEntry> problems = new();
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        if(GUILayout.Button("Parse Rulescripts")) 
        {
            problems = ParseRuleScripts();
            EditorUtility.SetDirty(this);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            
        }
        if (GUILayout.Button("Parse to C#") && _rulesDB.generateFrom != null)
        {
            GenerateFactIDS();
        }

        int errors = 0;
        int warnings = 0;
        foreach (var problem in problems)
        {
            if (problem.ProblemType == RuleScriptParsingProblems.ProblemType.Warning.ToString()) { warnings++; }
            else if (problem.ProblemType == RuleScriptParsingProblems.ProblemType.Error.ToString()) { errors++; }
        }
        if (errors > 0)
        {
            EditorGUILayout.HelpBox($"Encounter {errors} error{(errors > 1 ? "s" : "")}", MessageType.Error);
        }
        if (warnings > 0)
        {
            EditorGUILayout.HelpBox($"Encounter {warnings} warning{(warnings > 1 ? "s" : "")}", MessageType.Warning);
        }
        else if (!(errors > 0))
        {
            EditorGUILayout.HelpBox($"Encountered no warnings or errors", MessageType.Info);
        }
    }

    [MenuItem("Assets/Create/FactMatcher/RuleScript")]
    private static void CreateRuleScript(MenuCommand command)
    {
        var fileName = "ruleScript_";
        File.WriteAllText(GetCreateAssetPathWithVersioningCounter(fileName), GetDefaultRuleScriptContent());
        AssetDatabase.Refresh();
		
    }
    
    [MenuItem("Assets/Create/FactMatcher/Massive RuleScript Test (1000)")]
    private static void CreateRuleScriptMassiveTest1000(MenuCommand command)
    {
        var fileName = "ruleScript_massive_test";
        File.WriteAllText(GetCreateAssetPathWithVersioningCounter(fileName), GetMassiveTestRuleScriptContent(1000,10,20));
        AssetDatabase.Refresh();
		
    }
    
    [MenuItem("Assets/Create/FactMatcher/Massive RuleScript Test (10 000)")]
    private static void CreateRuleScriptMassiveTest10000(MenuCommand command)
    {
        var fileName = "ruleScript_massive_test";
        File.WriteAllText(GetCreateAssetPathWithVersioningCounter(fileName), GetMassiveTestRuleScriptContent(10000,10,30));
        AssetDatabase.Refresh();
		
    }

    private static string GetMassiveTestRuleScriptContent(int numRules,int factRangeLow,int factRangeUpper)
    {
        var stringnames = new string[]{"test","name","personality","favourite_food"};
        var stringvals = new string[]{"peter",
            "frog",
            "oslo",
            "los angeles",
            "cat","dog","horse",
            "mouse","video","perfect",
            "true","false",
            "videogame","joystick","mario",
            "milkfroth" ,"single","married","lonely","bachelor"
        };
        var floatnames = new string[]{"milk_amount","water_amount","beer_amount","player.health","player.age","item_strength"};
        
        var floatTest = new string[]{"<",">","=","<=",">="};
        var stringTest= new string[]{"="};
        StringWriter writer = new StringWriter();
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
        }

        return writer.ToString();
    }

    private static string GetCreateAssetPathWithVersioningCounter(string fileName)
    {
        var dirPath = AssetDatabase.GetAssetPath(Selection.activeObject);
        var path = dirPath +"/" + fileName ;
        var assets = AssetDatabase.FindAssets(fileName,new []{dirPath});
        int fileNumber = 1;
        foreach (var assetGUID in assets)
        {
            var assetPath = AssetDatabase.GUIDToAssetPath(assetGUID);
            Debug.Log(assetPath);

            var splitted = assetPath.Split(new []{fileName},StringSplitOptions.RemoveEmptyEntries);

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
        var finalPath = path + $"{fileNumber}.fmrs.txt";
        return Application.dataPath + finalPath.Split(new[] {"Assets"}, StringSplitOptions.RemoveEmptyEntries)[0];
    }

    static string GetDefaultRuleScriptContent()
    {
        var rule1 = ".rule_name IF\n" +
                    "    player.age > 10\n" +
                    "    player.name = Johnny Lemon\n" +
                    ".THEN WRITE\n" +
                    "    --exmple of writing back to the fact system\n" +
                    "    player.age = 9\n" +
                    ".THEN RESPONSE\n" +
                    "rule matches if age is bigger than 10 and name is Johnny Lemon\n" +
                    ".END\n\n\n";

        var comment = "-- A comment starts with -- , but, remark,it is not handled within the response block..\n" +
                      "-- Variables start with a namespace. If none is given, it will be automatically be given global as the namespace\n" +
                      "-- The variables defined here will end up in generated c# code in FactMatcherCodeGenerator.cs\n" +
                      "-- Everything is case-sensitive..\n" +
                      "-- Deriving another rule allows you to copy all the checks of that rule. See rule below for an example\n\n\n"; 
        
        var comment_convections = "-- the prefered naming convection is snake_case , divide by under_score\n" +
                                  "-- Keywords are preferably written in upper caps\n" +
                                  "-- .THEN WRITE\n" +
                                  "-- .THEN RESPONSE\n" +
                                  "-- .THEN PAYLOAD\n" +
                                  "-- .IF\n" +
                                  "-- .OR\n" +
                                  "-- Deriving another rule allows you to copy all the checks of that rule. See rule below for an example\n\n\n"; 
        
        var rule2 = ".rule_name.derived IF\n" +
                    "   player.height > 180\n" +
                    "--You can also use Range which expands into to checks when the rulescript is parsed ( for exclusive [ for inclusive ..\n" +
                    "   player.health Range(5,25]\n" +
                    ".THEN RESPONSE\n" +
                    "rule matches if base rule .RuleName matches and height is bigger than 180\n" +
                    ".END\n\n\n";
        
        var commentOrGroups = "-- You can use OR by starting a test with IF and including OR statements further down\n" +
                              "-- a group of IF OR, is evaluated as matching if one of the elements in the OR group is true.\n" +
                              "-- for judging which rule has the most matches, a match in an OR group counts as 1, even though multiple ORS match\n" +
                              "-- \n\n\n"; 
        
        var rule3 = ".rule_or_group_example IF\n" +
                    "   IF player.height > 180\n" +
                    "      OR player.street_smart > 15\n" +
                    ".THEN RESPONSE\n" +
                    "you matched the rule , player.height is above 150 and/or street_smart is above 15\n" +
                    ".END\n\n\n";
        
        var commentPayloads= "-- You can use the .THEN PAYLOAD keyword to list locations of Scriptable object resources\n" +
                             "-- Unity will then parse and make accessible that payload as a scriptable object when a rule is picked\n" +
                             "-- \n\n\n"; 
        
        var rule4 = ".rule_payload_example IF\n" +
                    "   player.height > 180\n" +
                    "   player.intelligence_level >= 10\n" +
                    ".THEN PAYLOAD\n" +
                    "    payload_example.asset\n" +
                    ".THEN RESPONSE\n" +
                    "Rule with payload example\n" +
                    ".END\n\n\n";

        return rule1 + comment + rule2 + commentOrGroups + rule3 + comment_convections + rule4;
    }

	
    private List<ProblemEntry> ParseRuleScripts()
    {
        var rulesProperty = serializedObject.FindProperty("rules");
        rulesProperty.ClearArray();

        problems = _rulesDB.CreateRulesFromRulescripts();
        
        EditorUtility.SetDirty(_rulesDB);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        foreach (var rule in _rulesDB.rules)
        {
            foreach (var factTest in rule.factTests)
            {
                factTest.ruleOwnerID = rule.RuleID;
            }
        }

        return problems;
    }

    
    private void GenerateFactIDS()
    {
        EditorUtility.SetDirty(_rulesDB);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        string generatedName = StripNameIntoCamelCase(_rulesDB.name);
        string defaultFilePath = "/Assets/FactMatcher/Generated";
        #if UNITY_EDITOR_WIN
        defaultFilePath = defaultFilePath.Replace("/", "\\");
        #endif
        defaultFilePath = Directory.GetCurrentDirectory() + defaultFilePath;
        string fullFilePath = EditorUtility.SaveFilePanel("Auto generate C# script", defaultFilePath, generatedName, "cs");
        string fileName = Path.GetFileName(fullFilePath);
        if (fileName != "")
        {
            FactMatcherCodeGenerator.GenerateFactIDS(fullFilePath, GetNameSpaceName(), _rulesDB);
        }
    }

    public string GetNameSpaceName()
    {
        return $"FactMatcher_{StripNameIntoCamelCase(_rulesDB.name)}_Gen";
    }

        
    //turns a string like "John Apple banana.power" into "JohnAppleBananaPower"
    public static string StripNameIntoCamelCase(string name)
    {

        try
        {
            var stripEm = new[] {' ', '.'};
            StringBuilder genName = new StringBuilder();
            foreach (var strippy in stripEm)
            {
                genName.Clear();
                var splitted = name.Split(strippy);
                foreach (var s in splitted)
                {
                    genName.Append(s.Substring(0, 1).ToUpper());
                    genName.Append(s.Substring(1, s.Length - 1));
                }

                name = genName.ToString();
            }

            return name;

        }
        catch (Exception e)
        {
            Debug.LogError($"Failed Stripping Name Into Camel case for {name} with exception {e.ToString()}");
            return name;
        }
    }
    
}
#endif