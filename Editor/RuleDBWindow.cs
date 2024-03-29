using Object = UnityEngine.Object;
using System.Collections.Generic;
using System.Text;
using UnityEditor.UIElements;
using UnityEngine.UIElements;
using UnityEditor;
using UnityEngine;
using System;
using TextAsset = UnityEngine.TextAsset;

public class RuleDBWindow : EditorWindow 
{
    public VisualTreeAsset RuleVisAss;
    public VisualTreeAsset FactItemVisAss;
    public VisualTreeAsset RuleListViewItemAss;
    
    public VisualTreeAsset WindowXML;
    public StyleSheet Style;
    
    [MenuItem("Agens/FactMatcher/RuleDB-Window")] // Menu item to create RuleDB-Window
    public static void ShowMyEditor() // Starts editor window
    {
        EditorWindow wnd = GetWindow<RuleDBWindow>();
        wnd.titleContent = new GUIContent("RuleDB-Window");
    }

    private FactMatcher _factMatcher;
    private Label _lastPickedRule;
    private ListView _factListView;
    private ListView _ruleListView;
    private TextField _factFilterField;
    private TextField _ruleFilterField;
    private TextField _factFileField;
    private DropdownField _ruleScriptSelector;
    private List<RuleDBFactTestEntry> _factTests;
    private List<FactRulesListViewController.Data> _rulesDatas;
    private TextAsset _currentRuleScript;
    private Button _openTextFileButton;
    
    private bool _factMatcherSelfAllocated = false;
    private FactMatcherProvider _factMatcherProvider;

    public void CreateGUI()
    {
        var content = new VisualElement();
        WindowXML.CloneTree(content);
        content.styleSheets.Add(Style);
        rootVisualElement.Add(content);

        _lastPickedRule = content.Q<Label>("RuleLabel");
        var rulesDBField = content.Q<ObjectField>("RulesDBField");
        var factMatcherProvider = content.Q<ObjectField>("FactMatcherProvider");
        _ruleScriptSelector = content.Q<DropdownField>("RuleScriptSelector");
        _openTextFileButton = content.Q<Button>("OpenTextFile");
        _factFileField = content.Q<TextField>("FactFileLocation");
        _ruleFilterField = content.Q<TextField>("RuleFilter");
        _ruleListView = content.Q<ListView>("RuleList");
        _factListView = content.Q<ListView>("FactList");
        
        rulesDBField.objectType = typeof(RulesDB);
        rulesDBField.RegisterCallback<ChangeEvent<Object>>(evt => { OnRuleDBFieldChanged(evt, content); });
        factMatcherProvider.RegisterCallback<ChangeEvent<Object>>(OnFactMatcherProviderChanged(content));
        EditorApplication.playModeStateChanged += change => // When play-mode is updated
        {
            if (change == PlayModeStateChange.ExitingPlayMode)
            {
                if (!_factMatcherSelfAllocated)
                {
                    _factMatcher = null;
                    ClearUI(content);
                }
            }
            if (change == PlayModeStateChange.EnteredPlayMode)
            {
                if (!_factMatcherSelfAllocated)
                {
                    if (_factMatcherProvider.GetFactMatcher().IsInited)
                    {
                        InitUIWithFactMatcher(content,_factMatcherProvider.GetFactMatcher());
                    }
                }
            }
        };
    }

    private EventCallback<ChangeEvent<Object>> OnFactMatcherProviderChanged(VisualElement content)
    {
        return evt =>
        {
            var gob = evt.newValue as GameObject;
            if (gob != null)
            {
                Component[] comps = gob.GetComponents<MonoBehaviour>(); // Creates array whit all monoBehaviours in game
                foreach (var comp in comps)
                {
                    if (comp is FactMatcherProvider provider) // Is there a FactmacherProvider in the current comp
                    {
                        _factMatcherProvider = provider;
                        if (_factMatcher != null && _factMatcher.HasDataToDispose() && _factMatcherSelfAllocated) // If we have a _factMatcher and _factMatcher has data to dispose and _factMatcherSelfAllocated
                        {
                            _factMatcher.DisposeData(); // Dispose all data
                            _factMatcher = null;
                        }
                        _factMatcherSelfAllocated = false;
                        if (provider.GetFactMatcher() != null && provider.GetFactMatcher().IsInited) // If provider.GetFactMatcher != null && provider.GetFactMacher.IsInited
                        {
                            InitUIWithFactMatcher(content, provider.GetFactMatcher()); // Initiate the UI whit the FactMatcher and the visual element
                        }
                        break;
                    }
                }
            }
        };
    }

    private void OnRuleDBFieldChanged(ChangeEvent<Object> evt, VisualElement content)
    {
        
        _factListView.bindItem = null;
        _factListView.itemsSource = new List<RuleDBFactTestEntry>();
        _factListView.Rebuild();
        
        _ruleListView.bindItem = null;
        _ruleListView.itemsSource = new List<FactRulesListViewController.Data>();
        _ruleListView.Rebuild();
        if (_factMatcher != null && _factMatcher.HasDataToDispose() && _factMatcherSelfAllocated) // Dispose date if any
        {
            _factMatcher.DisposeData();
            _factMatcher = null;
        }

        var rulesDB = evt.newValue as RulesDB;
        if (rulesDB != null) // Initiate if we have a ruesDB
        {
            _factMatcherSelfAllocated = true;
            _factMatcher = new FactMatcher(rulesDB);
            _factMatcher.Init();
            InitUIWithFactMatcher(content,_factMatcher);
        }
    }

    private void ClearUI(VisualElement content)
    {
        _ruleScriptSelector.visible = false;
        _openTextFileButton.visible = false;
        _ruleFilterField.visible = false;
        content.Q<Button>("PickRuleButton").visible = false;
        content.Q<ListView>("FactList").visible = false;
        content.Q<TextField>("FactFilter").visible = false;
        content.Q<ListView>("RuleList").visible = false;
        content.Q<Button>("SaveToFile").visible = false;
        content.Q<Button>("LoadFromFile").visible = false;
        content.Q<Button>("ReparseAndReload").visible = false;
        content.Q<Button>("RefreshFactValues").visible = false;
    }

    private void InitUIWithFactMatcher(VisualElement content, FactMatcher factMatcher)
    {
        _factMatcher = factMatcher;
        var rulesDB = _factMatcher.ruleDB;
        var pickRuleButton = content.Q<Button>("PickRuleButton");

        rulesDB.OnRulesParsed += OnRulesParsed;
        _factMatcher.OnRulePicked += OnRulePicked;
        _factMatcher.OnInited += OnInited;

        _ruleScriptSelector.visible = true;
        _openTextFileButton.visible = true;
        _ruleFilterField.visible = true;
        content.Q<Button>("PickRuleButton").visible = true;
        content.Q<ListView>("FactList").visible = true;
        content.Q<TextField>("FactFilter").visible = true;
        content.Q<ListView>("RuleList").visible = true;
        content.Q<Button>("SaveToFile").visible = true;
        content.Q<Button>("LoadFromFile").visible = true;
        content.Q<Button>("ReparseAndReload").visible = true;
        content.Q<Button>("RefreshFactValues").visible = true;

        var ruleScriptChoices = new List<string>();
        foreach (var textAsset in rulesDB.generateRuleFrom)
        {
            ruleScriptChoices.Add(textAsset.name);
            _currentRuleScript = textAsset;
            _ruleScriptSelector.SetValueWithoutNotify(textAsset.name);
        }

        ((INotifyValueChanged<string>)_ruleScriptSelector.labelElement).SetValueWithoutNotify("ScriptFile:");
        _ruleScriptSelector.RegisterCallback<ChangeEvent<string>>(ev => // When the _ruleScriptSelector is updated (changed RuleScript)
        {
            var index = Mathf.Max(0, ruleScriptChoices.IndexOf(ev.newValue)); // index = the largest number.
            _currentRuleScript = rulesDB.generateRuleFrom[index];
        });
        _ruleScriptSelector.choices = ruleScriptChoices;
        _openTextFileButton.RegisterCallback<ClickEvent>(ev =>
        {
            AssetDatabase.OpenAsset(_currentRuleScript);
        });

        var reparseAndReload = content.Q<Button>("ReparseAndReload");
        reparseAndReload.RegisterCallback<ClickEvent>(evt => // When save script and reload including facts button is pressed
        {
            string fileName = "_factMatcher_temp_fact_values" + ".csv";
            ProblemReporting problems = _factMatcher.SaveToCSV(fileName);
            string problemSaving = problems.ToString();
            _factMatcher.Reload();
            problemSaving += '\n' + _factMatcher.LoadFromCSV(fileName);
            _lastPickedRule.text = problemSaving == null || problemSaving.Trim('\n', '\r') == "" ? "No problems found" : "Problems:\n" + problemSaving;
            UpdateListViewRules();
            UpdateListView();
        });


        pickRuleButton.RegisterCallback<ClickEvent>(evt => // When pickRuleButton is pressed
        {
            _factMatcher.PickBestRule();
        });

        var saveButton = content.Q<Button>("SaveToFile");
        saveButton.RegisterCallback<ClickEvent>(evt => { _factMatcher.SaveToCSV(_factFileField.value); });

        var loadButton = content.Q<Button>("LoadFromFile");
        loadButton.RegisterCallback<ClickEvent>(evt =>
        {
            _factMatcher.LoadFromCSV(_factFileField.value);
            UpdateListView();
        });

        content.Q<Button>("RefreshFactValues").RegisterCallback<ClickEvent>(evt =>
        {
            UpdateListView();
        });

        _factFilterField = content.Q<TextField>("FactFilter");
        _factFilterField.value = "";
        _factListView.makeItem = () => FactEntryController.MakeItem(FactItemVisAss);
        _factListView.bindItem = (element, i) => FactEntryController.BindItem(element, i, _factTests, _factMatcher);
        _factFilterField.RegisterCallback<ChangeEvent<string>>(evt => { UpdateListView(); });
        UpdateListView();
        _factListView.fixedItemHeight = 22.0f;

        _ruleListView.makeItem = () => FactRulesListViewController.MakeItem(RuleListViewItemAss);
        _ruleListView.bindItem = (element, i) => FactRulesListViewController.BindItem(element, i, _rulesDatas, _factMatcher);
        _ruleListView.itemsSource = CreateRuleDatas();

        _ruleFilterField.RegisterCallback<ChangeEvent<string>>(evt => { UpdateListViewRules(); });
        UpdateListViewRules();
        _ruleListView.fixedItemHeight = 18.0f;

        FactEntryController.factChanged -= OnFactChangedFromFactsList();
        FactEntryController.factChanged += OnFactChangedFromFactsList();
        FactRulesListViewController.factChanged -= OnFactChangedFromFactsAndRulesList();
        FactRulesListViewController.factChanged += OnFactChangedFromFactsAndRulesList();
        FactRulesListViewController.pingedRuleAction -= OnPingedRule;
        FactRulesListViewController.pingedRuleAction += OnPingedRule;
    }

    private Action<int> OnFactChangedFromFactsAndRulesList()
    {
        return i =>
        {
            if (_factMatcher != null && _factMatcher.ruleDB != null)
            {
                _factListView.RefreshItems();
                _ruleListView.RefreshItems();
            }
        };
    }

    private Action<int> OnFactChangedFromFactsList()
    {
        return i =>
        {     
            if (_factMatcher != null && _factMatcher.ruleDB != null)
            {
                _ruleListView.RefreshItems();
            }
        };
    }

    private void OnRulesParsed()
    {
        if (_factMatcher != null)
        {
            if (_factMatcher.HasDataToDispose())
            {
                _factMatcher.DisposeData();
            }
            _factMatcher.Init();
            if (_factMatcher.ruleDB.problemList != null)
            {
                var problems = _factMatcher.ruleDB.problemList;
                string problemsString = "";
                foreach (var problem in problems.GetList())
                {
                    problemsString += problem;
                }
                _lastPickedRule.text = problemsString;
            } 
        }
    }

    private void OnInited()
    {
        UpdateListView();
    }

    private void OnRulePicked(int noOfBestRules)
    {
        var rule = noOfBestRules >= 1 ? _factMatcher.GetRuleFromMatches(0) : null;
        if (rule!=null)
        {
            StringBuilder strBuilder = new();
            var interpolatedPayload = rule.Interpolate(_factMatcher, ref strBuilder);
            _lastPickedRule.text = $"Last Picked rule: {rule.ruleName}\nPayload: {interpolatedPayload}\nRuleID: {rule.RuleID}";
        }
        else
        {
            _lastPickedRule.text = $"Found no Rule for noBestRules = {noOfBestRules}";
        }
        UpdateListView();
    }

    void UpdateListView()
    {
        if (_factMatcher != null && _factMatcher.ruleDB != null)
        {
            var factTests = _factMatcher.ruleDB.CreateFlattenedFactTestListWithNoDuplicateFactIDS(entry =>
            {
                var splits = _factFilterField.text.Split("|");
                bool include = false;
                for (int i = 0; i < splits.Length; i++)
                {
                    if (entry.factName.ToLower().Contains(splits[i].ToLower()))
                    {
                        include = true;
                        break;
                    }
                }
                
                return include;
            });
            
            _factTests = factTests;
            _factListView.itemsSource = _factTests;
            _factListView.RefreshItems();
        }
    }
    
    void UpdateListViewRules()
    {
        if (_factMatcher != null && _factMatcher.ruleDB != null)
        {
            _ruleListView.itemsSource = CreateRuleDatas();
            _ruleListView.RefreshItems();
        }
    }

    public void OnPingedRule(RuleDBEntry pingedRule, int lineNumber)
    { AssetDatabase.OpenAsset(pingedRule.textFile, lineNumber); }

    private List<FactRulesListViewController.Data> CreateRuleDatas()
    {
        var factTestsFromRulesFlattened = _factMatcher.ruleDB.CreateFlattenedRuleAtomListWithPotentiallyDuplicateFactIDS(entry =>
        {
            if (_ruleFilterField.value.Length == 0) { return true; }
            
            var splits = _ruleFilterField.text.Split(",");
            bool include = false;
            for (int i = 0; i < splits.Length; i++)
            {
                var test = splits[i].Trim();
                var invert = test.StartsWith("!");
                var or = test.StartsWith("|");
                var and = test.StartsWith("&");
                test = test.Trim('!');
                test = test.Trim('&');
                test = test.Trim('|');
                var rule = _factMatcher.GetRuleFromRuleID(entry.ruleOwnerID);
                var includeTest = rule.ruleName.Contains(test);
                if (invert)
                {
                    includeTest= !includeTest;
                }
                if (and)
                {
                    include = includeTest && include;
                }
                if (includeTest && or)
                {
                    include = includeTest || include;
                }

                if (!or && !and)
                {
                    include = includeTest;
                }
            }
            return include;
        });
        _rulesDatas ??= new();

        var lastRuleID = -1;
        _rulesDatas.Clear();
        foreach (var ruleDBFactTest in factTestsFromRulesFlattened)
        {
            if (lastRuleID != ruleDBFactTest.ruleOwnerID)
            {
                FactRulesListViewController.Data ruleData = new()
                {
                    text = _factMatcher.GetRuleFromRuleID(ruleDBFactTest.ruleOwnerID).ruleName,
                    isRule = true,
                    ruleIndex = ruleDBFactTest.ruleOwnerID,
                    factIndex = -1
                };
                _rulesDatas.Add(ruleData);
            }

            FactRulesListViewController.Data data = new()
            {
                ruleIndex = ruleDBFactTest.ruleOwnerID,
                factIndex = ruleDBFactTest.factID,
                factValueText = $"{_factMatcher.PrintableFactValueFromFactTest(ruleDBFactTest)}"
            };

            var strictOrNot = ruleDBFactTest.isStrict ? "" : "?";
            data.text =
                $"{strictOrNot}{ruleDBFactTest.factName} {ruleDBFactTest.CompareMethodPrintable()} {ruleDBFactTest.MatchValuePrintable()}";
            data.isRule = false;
            lastRuleID = ruleDBFactTest.ruleOwnerID;
            _rulesDatas.Add(data);
        }
        return _rulesDatas;
    }

    private void OnDestroy()
    {

        FactEntryController.factChanged -= OnFactChangedFromFactsList();
        FactRulesListViewController.factChanged -= OnFactChangedFromFactsAndRulesList();
        FactRulesListViewController.pingedRuleAction -= OnPingedRule;
        if (_factMatcher != null)
        {
            if (_factMatcher.ruleDB != null)
            {
                _factMatcher.ruleDB.OnRulesParsed -= OnRulesParsed;
            }
            _factMatcher.OnRulePicked -= OnRulePicked;
            if (_factMatcher.HasDataToDispose())
            {
                _factMatcher.DisposeData();
            }
        }
    }
}