#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FactMatcher;
using UnityEditor;
using UnityEngine;


[CustomEditor(typeof(RulesDB))]
public class RuleDBEditor : Editor
{
    private RulesDB _rulesDB;

    private void OnEnable()
    {
        _rulesDB = (RulesDB)target;
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        if(GUILayout.Button("Generate From Rulescripts")) 
        {
            GenerateFromText();
        }    
    }
    
    [MenuItem("Assets/Create/FactMatcher/RuleScript")]
    private static void CreateRuleScript(MenuCommand command)
    {

        var fileName = "ruleScript_";
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
        var finalPath = path + $"{fileNumber}.txt";
        File.WriteAllText(Application.dataPath + finalPath.Split(new []{"Assets"},StringSplitOptions.RemoveEmptyEntries)[0] , GetDefaultRuleScriptContent());
        AssetDatabase.Refresh();
		
    }

    static string GetDefaultRuleScriptContent()
    {
        var rule1 = ".RuleName\n" +
                    "player.age > 10\n" +
                    "player.name = Johnny Lemon\n" +
                    ":Response:\n" +
                    "rule matches if age is bigger than 10 and name is Johnny Lemon\n" +
                    ":End:\n\n\n";

        var comment = "-- A comment starts with -- , but, remark,it is not handled within the response block..\n" +
                      "-- Variables start with a namespace. If none is given, it will be automatically be given global as the namespace\n" +
                      "-- The variables defined here will end up in generated c# code in FactMatcherCodeGenerator.cs\n" +
                      "-- Everything is case-sensitive..\n" +
                      "-- Deriving another rule allows you to copy all the checks of that rule. See rule below for an example\n\n\n"; 
        
        var rule2 = ".RuleName.Derived\n" +
                    "player.height > 180\n" +
                    "--You can also use Range which expands into to checks when the rulescript is parsed ( for exclusive [ for inclusive ..\n" +
                    "player.health Range(5,25]\n" +
                    ":Response:\n" +
                    "rule matches if base rule .RuleName matches and height is bigger than 180\n" +
                    ":End:\n\n\n";

        return rule1 + comment + rule2;
    }

	
    private void GenerateFromText()
    {
        if (_rulesDB.generateFrom != null)
        {
            _rulesDB.rules.Clear();
            int factID = 0;
            int ruleID = 0;
            Dictionary<string,int> addedFactIDS = new Dictionary<string, int>();
            foreach (var ruleScript in _rulesDB.generateFrom)
            {
                var parser = new RuleScriptParser();
                parser.GenerateFromText(ruleScript.text,_rulesDB.rules,ref factID,ref addedFactIDS, ref ruleID); 
            }
            GenerateFactIDS();
        }
    }

    
    private void GenerateFactIDS()
    {
        EditorUtility.SetDirty(_rulesDB);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        var generatedName = StripNameIntoCamelCase(_rulesDB.name);
        var fileName = $"FactMatcher/Generated/{generatedName}.cs";
        FactMatcherCodeGenerator.GenerateFactIDS(fileName, GetNameSpaceName(), _rulesDB);
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