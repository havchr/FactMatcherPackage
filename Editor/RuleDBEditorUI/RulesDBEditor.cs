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
public class RulesDBEditor : Editor
{
    private RulesDB _rulesDB;

    private void OnEnable()
    {
        _rulesDB = (RulesDB)target;
        problems = new(((RulesDB)target).problemList);
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

    private ListView generateDocFrom;
    private ListView generateRuleFrom;

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


    void OnAddItemToObsy(int index)
    {
        Debug.Log($"just added {_rulesDB.generateRuleFrom[index].name}");
    }

    void OnRemoveItemFromObsy(int index)
    {
        Debug.Log($"removing {_rulesDB.generateRuleFrom[index].name}");
    }

    public override VisualElement CreateInspectorGUI()
    {
        if (IsVisualTreeAssetsAssigned())
        {
            mainRoot = new();
            _ruleDBInspectorVisAss.CloneTree(mainRoot);

            Toggle autoParseRuleScript = mainRoot.Q<Toggle>("autoParseRuleScript");
            autoParseRuleScript.bindingPath = nameof(RulesDB.autoParseRuleScript);
            autoParseRuleScript.RegisterCallback<ChangeEvent<bool>>(evt => _rulesDB.SetUpFileWatcher(evt.newValue));

            Toggle ignoreDoc = mainRoot.Q<Toggle>("ignoreDoc");
            ignoreDoc.bindingPath = nameof(_rulesDB.ignoreDocumentationDemand);
            ignoreDoc.RegisterCallback<ChangeEvent<bool>>(evt => SetupRuleListView(ruleListView));

            Toggle debugLogMissingIDS = mainRoot.Q<Toggle>("debugLogMissingIDS");
            debugLogMissingIDS.bindingPath = nameof(_rulesDB.debugLogMissingIDS);


            generateDocFrom = mainRoot.Q<ListView>("generateDocFrom");
            generateDocFrom.bindingPath = nameof(_rulesDB.generateDocumentationFrom);
            generateRuleFrom = mainRoot.Q<ListView>("generateRuleFrom");
            generateRuleFrom.bindingPath = nameof(_rulesDB.generateRuleFrom);
            
            generateRuleFrom.itemsRemoved += index =>
            {
                RuleDBWorkaroundAccess = _rulesDB;
                EditorApplication.update += RefreshFileWatchersForItemsChangedInFileListWorkaround;
            };
            generateRuleFrom.itemsAdded += indices =>
            {
                RuleDBWorkaroundAccess = _rulesDB;
                EditorApplication.update += RefreshFileWatchersForItemsChangedInFileListWorkaround;
            };
            
            generateDocFrom.itemsRemoved += index =>
            {
                RuleDBWorkaroundAccess = _rulesDB;
                EditorApplication.update += RefreshFileWatchersForItemsChangedInFileListWorkaround;
            };
            generateDocFrom.itemsAdded += indices =>
            {
                RuleDBWorkaroundAccess = _rulesDB;
                EditorApplication.update += RefreshFileWatchersForItemsChangedInFileListWorkaround;
            };

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

            CheckProblems(true);

            mainRoot.Add(RenderDefaultUIInFoldout(false));

            _rulesDB.AutoParserTriggerForEditorUI = RunAutoParser;
            RuleDBListViewController.pingedRuleButtonAction = OnPingRuleButton;
            RuleDBListViewController.pingedDocButtonAction = OnPingDocButton;

            return mainRoot;
        }
        else
        { throw new Exception("VisualTreeAssets not assigned"); }
    }

    #region ListView itemsAdded callback workaround
    /*
     * Because , when we add items to the ListView, the UIToolkit gives us the callback prior to the
     * data actually being populated, we have to use this workaround.
     * As an example, if our list has A.txt and we add B.txt , we get a callback when our data contains
     * A.txt [0] and A.txt[1] , if you press +1 on something in the inspector, Unity often duplicates the last element,
     * so we probably get a callback for that event instead of getting it after it has actually written B.txt to the array.
     * annoying. Oh well.
     *
     * Anyhow - after things have changed, we simply remove all file-listeners that we had,
     * and add all new file listeners. Hypothesis , it should then only contain listeners for file assets in the RuleDB
     */
    private static RulesDB RuleDBWorkaroundAccess = null;
    
    private static void RefreshFileWatchersForItemsChangedInFileListWorkaround()
    {
        if (RuleDBWorkaroundAccess != null)
        {
            RuleDBWorkaroundAccess.SetUpFileWatcher();
            RuleDBWorkaroundAccess = null;
        }
        EditorApplication.update -= RefreshFileWatchersForItemsChangedInFileListWorkaround;
    }

    #endregion

    private void RunAutoParser(ProblemReporting problems)
    {
        Debug.Log($"We autoparsed {_rulesDB.name} updating UI");
        problems?.ClearList();
        this.problems = problems; 
        CheckProblems(true);
        SetupRuleListView(ruleListView); 
    }

    public void UpdateRuleListView(ListView listView)
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
        if (ruleFilter != null)
        {
            ruleFilter.UnregisterCallback<ChangeEvent<string>>(evt =>
            {
                if (ruleListView != null)
                {
                    UpdateRuleListView(ruleListView);
                }
            });
        }

        _rulesDB.AutoParserTriggerForEditorUI -= RunAutoParser;
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

        if (errorDetected = problems.ContainsErrors(true, out int errors, out string errorsString))
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

        if (warningDetected = problems.ContainsWarnings(true, out int warnings, out string warningsString))
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
            StringBuilder genName = new();
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
            Debug.LogError($"Failed Stripping Name Into Camel case for {name} with exception {e}");
            return name;
        }
    }
    
    
}
#endif