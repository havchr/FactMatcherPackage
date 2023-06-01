#if UNITY_EDITOR
using TextAsset = UnityEngine.TextAsset;
using Random = UnityEngine.Random;
using System.Collections.Generic;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.Text;
using UnityEngine;
using UnityEditor;
using System.IO;
using System;

[CustomEditor(typeof(RulesDB))]
public class RuleDBEditor : Editor
{
    private RulesDB _rulesDB;

    private void OnEnable()
    {
        _rulesDB = (RulesDB)target;
    }

    [SerializeField] private VisualTreeAsset _ruleDBInspectorVisAss;
    [Space(10)]
    [SerializeField] private VisualTreeAsset _ruleListViewItem;

    private bool IsVisualTreeAssetsAssigned()
    { return (_ruleDBInspectorVisAss && _ruleListViewItem); }

    private VisualElement mainRoot;

    private HelpBox warningBox;
    private HelpBox errorBox;

    private TextField ruleFilter;
    private ListView ruleListView;
    private List<RuleDBListViewController.Data> listOfData;

    enum ListToGenerate
    {
        DocumentsFacts,
        DocumentsFactsCanBe,
        FactsFactWrites,
        FactsFactTests,
        FactsFactWritesAndFactTests,

        None
    }

    public override VisualElement CreateInspectorGUI()
    {
        if (IsVisualTreeAssetsAssigned())
        {
            mainRoot = new();
            _ruleDBInspectorVisAss.CloneTree(mainRoot);

            Toggle autoParseRuleScript = mainRoot.Q<Toggle>("autoParseRuleScript");
            autoParseRuleScript.bindingPath = nameof(_rulesDB.autoParseRuleScript);

            Toggle multipleBestRules = mainRoot.Q<Toggle>("multipleBestRules");
            multipleBestRules.bindingPath = nameof(_rulesDB.PickMultipleBestRules);

            Toggle writeToAllMatches = mainRoot.Q<Toggle>("writeToAllMatches");
            writeToAllMatches.bindingPath = nameof(_rulesDB.FactWriteToAllThatMatches);

            Toggle ignoreDoc = mainRoot.Q<Toggle>("ignoreDoc");
            ignoreDoc.bindingPath = nameof(_rulesDB.ignoreDocumentationDemand);
            ignoreDoc.RegisterCallback<ChangeEvent<bool>>(evt => SetupRuleListView(ruleListView));

            Toggle debugLogMissingIDS = mainRoot.Q<Toggle>("debugLogMissingIDS");
            debugLogMissingIDS.bindingPath = nameof(_rulesDB.debugLogMissingIDS);

            ListView generateDocFrom = mainRoot.Q<ListView>("generateDocFrom");
            generateDocFrom.bindingPath = nameof(_rulesDB.generateDocumentationFrom);

            ListView generateRuleFrom = mainRoot.Q<ListView>("generateRuleFrom");
            generateRuleFrom.bindingPath = nameof(_rulesDB.generateRuleFrom);

            ruleFilter = mainRoot.Q<TextField>("ruleFilter");
            ruleListView = mainRoot.Q<ListView>("rules");
            SetupRuleListView(ruleListView);

            ruleFilter.RegisterCallback<ChangeEvent<string>>(evt => UpdateRuleListView(ruleListView));
            UpdateRuleListView(ruleListView);

            Button parseRuleAndDoc = mainRoot.Q<Button>("parseRuleScripts");
            parseRuleAndDoc.RegisterCallback<ClickEvent>(ParseRuleAndDoc);

            Button parseToCS = mainRoot.Q<Button>("parseToCS");
            parseToCS.RegisterCallback<ClickEvent>(GenerateFactIDS);

            warningBox = mainRoot.Q<HelpBox>("warningBox");
            warningBox.style.display = DisplayStyle.None;
            warningBox.text = string.Empty;

            errorBox = mainRoot.Q<HelpBox>("errorBox");
            errorBox.style.display = DisplayStyle.None;
            errorBox.text = string.Empty;

            mainRoot.Add(RenderDefaultUIInFoldout(false));

            RuleDBListViewController.pingedRuleButtonAction = OnPingRuleButton;
            RuleDBListViewController.pingedDocButtonAction = OnPingDocButton;

            if (_rulesDB.autoParseRuleScript)
            {
                problems?.ClearList();
                problems = ParseRuleScripts();
                EditorUtility.SetDirty(this);
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                CheckProblems(_rulesDB.autoParseRuleScript);
                SetupRuleListView(ruleListView);
            }

            return mainRoot;
        }
        else
        { throw new Exception("VisualTreeAssets not assigned"); }
    }

    void UpdateRuleListView(ListView listView)
    {
        if (_rulesDB.rules != null)
        {
            listOfData = TurnRuleDBRulesToData(_rulesDB.rules, ruleFilter.text);
            listView.itemsSource = listOfData;
            listView.RefreshItems();
        }
    }

    private void SetupRuleListView(ListView listView)
    {
        if (_rulesDB.rules != null && _rulesDB.rules.Count > 0)
        {
            listOfData = TurnRuleDBRulesToData(_rulesDB.rules);

            listView.itemsSource = listOfData;
            listView.makeItem = () => RuleDBListViewController.MakeItem(_ruleListViewItem, _rulesDB.ignoreDocumentationDemand);
            listView.bindItem = (VisualElement e, int i) => RuleDBListViewController.BindItem(e, i, listOfData);
            listView.fixedItemHeight = 20;
        }
        else
        {
            listView.Clear();
        }
    }

    private List<RuleDBListViewController.Data> TurnRuleDBRulesToData(List<RuleDBEntry> rules, string filterBy = null)
    {
        List<RuleDBListViewController.Data> listOfData = new();

        for (int i = 0; i < rules.Count; i++)
        {
            RuleDBEntry currentRule = rules[i];
            if (filterBy == null || currentRule.ruleName.ToLower().Contains(filterBy.ToLower()))
            {
                RuleDBListViewController.Data data = new()
                {
                    type = RuleDBListViewController.Data.Type.Rule,
                    text = currentRule.ruleName,
                    defaultName = currentRule.ruleName,
                    styleName = "rule",
                    innerIndex = -1,
                    ruleIndex = i,
                };
                listOfData.Add(data);
            }
            if (currentRule.factTests.Count > 0)
            {
                RuleDBListViewController.Data title = new()
                {
                    type = RuleDBListViewController.Data.Type.Title,
                    text = RuleDBListViewController.Data.Type.FactTest.ToString(),
                    styleName = "fact-or",
                    innerIndex = -1,
                    ruleIndex = i,
                };

                List<RuleDBListViewController.Data> listOfFactTestsData = new();
                for (int j = 0; j < currentRule.factTests.Count; j++)
                {
                    RuleDBFactTestEntry currentFactTest = currentRule.factTests[j];
                    bool containsFactTest = filterBy == null || currentFactTest.factName.ToLower().Contains(filterBy.ToLower());
                    if (containsFactTest)
                    {
                        RuleDBListViewController.Data factTestData = new()
                        {
                            type = RuleDBListViewController.Data.Type.FactTest,
                            text = '\t' + currentFactTest.ToString(),
                            defaultName = currentFactTest.factName,
                            styleName = "fact-or",
                            innerIndex = j,
                            ruleIndex = i,
                        };
                        listOfFactTestsData.Add(factTestData); 
                    }
                }

                if (listOfFactTestsData.Count > 0)
                {
                    listOfData?.Add(title);
                    listOfData?.AddRange(listOfFactTestsData);
                }
            }

            if (currentRule.factWrites.Count > 0)
            {
                RuleDBListViewController.Data title = new()
                {
                    type = RuleDBListViewController.Data.Type.Title,
                    text = RuleDBListViewController.Data.Type.FactWrite.ToString(),
                    styleName = "fact-or",
                    innerIndex = -1,
                    ruleIndex = i,
                };

                List<RuleDBListViewController.Data> listOfFactWritesData = new();
                for (int j = 0; j < currentRule.factWrites.Count; j++)
                {
                    RuleDBFactWrite currentFactWrites = currentRule.factWrites[j];
                    if (filterBy == null || currentFactWrites.factName.ToLower().Contains(filterBy.ToLower()))
                    {
                        RuleDBListViewController.Data factWriteData = new()
                        {
                            type = RuleDBListViewController.Data.Type.FactWrite,
                            text = '\t' + currentFactWrites.ToString(),
                            defaultName = currentFactWrites.factName,
                            styleName = "fact-or",
                            innerIndex = j,
                            ruleIndex = i,
                        };
                        listOfFactWritesData.Add(factWriteData); 
                    }
                }

                if (listOfFactWritesData.Count > 0)
                {
                    listOfData?.Add(title);
                    listOfData?.AddRange(listOfFactWritesData);
                }
            }
        }

        return listOfData;
    }

    private void OnDestroy()
    {
        RuleDBListViewController.pingedRuleButtonAction -= OnPingRuleButton;
        RuleDBListViewController.pingedDocButtonAction -= OnPingDocButton;
        mainRoot?.Clear();
    }

    private void OnPingRuleButton(RuleDBListViewController.Data data)
    {
        int lineNumber = data.type switch
        {
            RuleDBListViewController.Data.Type.Rule => _rulesDB.rules[data.ruleIndex].startLine,
            RuleDBListViewController.Data.Type.Title => _rulesDB.rules[data.ruleIndex].startLine,
            RuleDBListViewController.Data.Type.FactWrite => _rulesDB.rules[data.ruleIndex].factWrites[data.innerIndex].lineNumber,
            RuleDBListViewController.Data.Type.FactTest => _rulesDB.rules[data.ruleIndex].factTests[data.innerIndex].lineNumber,
            RuleDBListViewController.Data.Type.Payload => _rulesDB.rules[data.ruleIndex].startLine,
            _ => -1,
        };
        AssetDatabase.OpenAsset(_rulesDB.rules[data.ruleIndex].textFile, lineNumber);
    }

    private void OnPingDocButton(RuleDBListViewController.Data data)
    {
        if (!_rulesDB.ignoreDocumentationDemand)
        {
            if (data.type == RuleDBListViewController.Data.Type.Rule || data.type == RuleDBListViewController.Data.Type.Title)
            {

            }
            else if (data.defaultName != "" && data.defaultName != null)
            {
                bool foundMatch = false;
                foreach (var doc in _rulesDB.documentations)
                {
                    FactInDocument foundFactInDocument = doc.GetFactInDocumentByName(data.defaultName);
                    if (foundMatch = foundFactInDocument != null)
                    {
                        Debug.Log(foundFactInDocument.ToString() + '\n');
                        break;
                    }
                }
                if (!foundMatch)
                {
                    Debug.LogWarning("No matching rule inside the documentation was found");
                }
            }
            else if (data.defaultName != "" && data.defaultName != null && _rulesDB.documentations != null)
            {
                DocumentEntry documentEntry = _rulesDB.GetDocumentEntryByName(data.defaultName);
                Debug.Log($"{(documentEntry != null ? $"{documentEntry?.ToString()}" : $"There is no documentation with the name {data.defaultName}")}");
            } 
        }
        else
        {
            Debug.Log($"Ignoring documentation demand is turned {(_rulesDB.ignoreDocumentationDemand ? "on" : "off")}");
        }
    }

    private VisualElement RenderDefaultUIInFoldout(bool startOpen)
    {
        VisualElement defaultRoot = new();
        Button refreshButton = new(() => SetupRuleListView(ruleListView))
        {
            text = "Refresh button",
        };
        Foldout foldout = new() { viewDataKey = "DefaultInspectorFoldout", text = "The default inspector", value = startOpen };
        foldout.Add(refreshButton);
        InspectorElement.FillDefaultInspector(foldout, serializedObject, this);
        defaultRoot.Add(foldout);
        return defaultRoot;
    }

    public void OnPingedRule(TextAsset textFile, int lineNumber)
    { AssetDatabase.OpenAsset(textFile, lineNumber); }

    private ProblemReporting problems = new();

    private void ParseRuleAndDoc(ClickEvent evt)
    {
        problems?.ClearList();
        problems = ParseRuleScripts();
        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        CheckProblems();
        SetupRuleListView(ruleListView);
    }

    private void CheckProblems(bool autoParsed = false)
    {
        bool errorDetected;
        string savedErrorsString;
        int savedErrors;

        bool warningDetected;
        int savedWarnings;
        string savedWarningsString;

        if (errorDetected = problems.ContainsErrors(out int errors, out string errorsString))
        {
            savedErrors = errors;
            savedErrorsString = errorsString;
            errorBox.style.display = errorDetected ? DisplayStyle.Flex : DisplayStyle.None;
            errorBox.messageType = HelpBoxMessageType.Error;
            errorBox.text = $"{(autoParsed ? "Auto parsed:\n\n" : "")}" +
                $"Encounter {savedErrors} error{(savedErrors > 1 ? "s" : "")}.{savedErrorsString}";
            Debug.LogError($"Encounter {savedErrors} error{(savedErrors > 1 ? "s" : "")}.{savedErrorsString}");

            SetupRuleListView(ruleListView);
            UpdateRuleListView(ruleListView);
        }
        else
        {
            errorBox.text = string.Empty;
        }

        if (warningDetected = problems.ContainsWarnings(out int warnings, out string warningsString))
        {
            errorBox.style.display = errorDetected ? DisplayStyle.Flex : DisplayStyle.None;
            savedWarnings = warnings;
            savedWarningsString = warningsString;
            warningBox.style.display = warningDetected ? DisplayStyle.Flex : DisplayStyle.None;
            warningBox.text = $"{(autoParsed ? "Auto parsed:\n\n" : "")}" +
                $"Encounter {savedWarnings} warning{(savedWarnings > 1 ? "s" : "")}.{warningsString}";
            Debug.LogWarning($"{(autoParsed ? "Auto parsed:" : "")} Encounter {savedWarnings} warning{(savedWarnings > 1 ? "s" : "")}.{savedWarningsString}");
        }
        else
        {
            warningBox.style.display = warningDetected ? DisplayStyle.Flex : DisplayStyle.None;
            warningBox.text = string.Empty;
        }

        if (!errorDetected && !warningDetected)
        {
            errorBox.style.display = DisplayStyle.Flex;
            errorBox.messageType = HelpBoxMessageType.Info;
            errorBox.text = $"{(autoParsed ? "Auto parsed:\n\n" : "")}" +
                "No problems detected :D";
        }
    }

    private static readonly string documentationContent = @"
-- .DOCS %label starts a documentation block
.DOCS rule_name
Documentation about all things related to rule_name.
As much text as you want to, until you reach .. on a single line, which indicates the end
of free form text.
..

--.FACT %fact_name starts documentation of a fact , contains a block of text until .. with
--as much text as you want.
.FACT player.age
as much text as you want here.
..

--newlines are ignored

--if we have a .IT CAN BE block after a .FACT block
--we list up the valid names for the previous fact
--see example below
.FACT player.name
this is player.name that can link names into a sequence, like Johnny, Bob, etc.
related to player.name
..

.IT CAN BE
	Johnny Lemon
    Bob Jonson
..
-- Use .. to end IT CAN BE

.FACT player.height
as much text as you want here.
..

.FACT player.health
as much text as you want here.
..

.FACT player.street_smart
as much text as you want here.
..

.FACT player.intelligence_level
as much text as you want here.
..

.END
    -- .END indicates the end of the document.
";

    [MenuItem("Assets/Create/FactMatcher/Documentation")]
    private static void CreateDocumentationScript(MenuCommand command)
    {
        var fileName = "Documentation_";
        File.WriteAllText(GetCreateAssetPathWithVersioningCounter(fileName, "dfmrs"), documentationContent.TrimStart());
        AssetDatabase.Refresh();
    }

    [MenuItem("Assets/Create/FactMatcher/RuleScript")]
    private static void CreateRuleScript(MenuCommand command)
    {
        var fileName = "ruleScript_";
        File.WriteAllText(GetCreateAssetPathWithVersioningCounter(fileName, "fmrs"), GetDefaultRuleScriptContent());
        AssetDatabase.Refresh();
		
    }
    
    [MenuItem("Assets/Create/FactMatcher/Massive RuleScript Test (1000)")]
    private static void CreateRuleScriptMassiveTest1000(MenuCommand command)
    {
        var fileName = "ruleScript_massive_test";
        File.WriteAllText(GetCreateAssetPathWithVersioningCounter(fileName, "fmrs"), GetMassiveTestRuleScriptContent(1000,10,20));
        AssetDatabase.Refresh();
		
    }
    
    [MenuItem("Assets/Create/FactMatcher/Massive RuleScript Test (10 000)")]
    private static void CreateRuleScriptMassiveTest10000(MenuCommand command)
    {
        var fileName = "ruleScript_massive_test";
        File.WriteAllText(GetCreateAssetPathWithVersioningCounter(fileName, "fmrs"), GetMassiveTestRuleScriptContent(10000,10,30));
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
        
        //var commentPayloads= "-- You can use the .THEN PAYLOAD keyword to list locations of Scriptable object resources\n" +
        //                     "-- Unity will then parse and make accessible that payload as a scriptable object when a rule is picked\n" +
        //                     "-- \n\n\n"; 
        
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

    private ProblemReporting ParseRuleScripts()
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
    
    private void GenerateFactIDS(ClickEvent evt)
    {
        if (_rulesDB == null || _rulesDB.rules == null || _rulesDB.rules.Count < 1)
        {
            ParseRuleAndDoc(evt);
        }

        if (!problems.ContainsErrorOrWarning())
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
            AssetDatabase.Refresh();
        }
    }
        else
        {
            CheckProblems();
        }
        UpdateRuleListView(ruleListView);
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
                    genName.Append(s[..1].ToUpper());
                    genName.Append(s[1..]);
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