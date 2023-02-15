using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

public class RuleDBWindow : EditorWindow 
{
    
    public VisualTreeAsset RuleVisAss;
    public VisualTreeAsset FactItemVisAss;
    public VisualTreeAsset RuleListViewItemAss;
    
    public VisualTreeAsset WindowXML;
    public StyleSheet Style;
    
    [MenuItem("Agens/FactMatcher/RuleDB-Window")]
    public static void ShowMyEditor()
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
    private TextField _ruleScriptField;
    private DropdownField _ruleScriptSelector;
    private List<RuleDBFactTestEntry> _factTests;
    private List<FactRulesListViewController.Data> _rulesDatas;
    private TextAsset _currentRuleScript;
    
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
        _ruleScriptField = content.Q<TextField>("RuleScript");
        _ruleScriptSelector = content.Q<DropdownField>("RuleScriptSelector");
        _factFileField = content.Q<TextField>("FactFileLocation");
        _ruleFilterField = content.Q<TextField>("RuleFilter");
        
        rulesDBField.objectType = typeof(RulesDB);
        rulesDBField.RegisterCallback<ChangeEvent<Object>>(evt => { onRuleDBFieldChanged(evt, content); });
        factMatcherProvider.RegisterCallback<ChangeEvent<Object>>(onFactMatcherProviderChanged(content));
        EditorApplication.playModeStateChanged += change =>
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


    private EventCallback<ChangeEvent<Object>> onFactMatcherProviderChanged(VisualElement content)
    {
        return evt =>
        {
            var gob = evt.newValue as GameObject;
            if (gob != null)
            {
                Component[] comps = gob.GetComponents<MonoBehaviour>();
                foreach (var comp in comps)
                {
                    FactMatcherProvider provider = comp as FactMatcherProvider;
                    if (provider != null)
                    {
                        _factMatcherProvider = provider;
                        if (_factMatcher != null && _factMatcher.HasDataToDispose() && _factMatcherSelfAllocated)
                        {
                            _factMatcher.DisposeData();
                            _factMatcher = null;
                        }
                        _factMatcherSelfAllocated = false;
                        if (provider.GetFactMatcher()!=null && provider.GetFactMatcher().IsInited)
                        {
                            InitUIWithFactMatcher(content,provider.GetFactMatcher());
                        }
                        break;
                    }
                }
            }
        };
    }

    private void onRuleDBFieldChanged(ChangeEvent<Object> evt, VisualElement content)
    {
        if (_factMatcher != null && _factMatcher.HasDataToDispose() && _factMatcherSelfAllocated)
        {
            _factMatcher.DisposeData();
            _factMatcher = null;
        }

        var rulesDB = evt.newValue as RulesDB;
        if (rulesDB != null)
        {
            Debug.Log("We are initing things.");
            _factMatcherSelfAllocated = true;
            _factMatcher = new FactMatcher(rulesDB);
            _factMatcher.Init(countAllMatches: true);
            InitUIWithFactMatcher(content,_factMatcher);
        }
    }

    
    private void ClearUI(VisualElement content)
    {
        _ruleScriptSelector.visible = false;
        _ruleFilterField.visible = false;
        content.Q<Button>("SaveRuleScript").visible = false;
        content.Q<Button>("PickRuleButton").visible = false;
        content.Q<ListView>("FactList").visible = false;
        content.Q<TextField>("FactFilter").visible = false;
        content.Q<ListView>("RuleList").visible = false;
        content.Q<Button>("SaveToFile").visible = false;
        content.Q<Button>("LoadFromFile").visible = false;
        content.Q<Button>("SaveRuleScript").visible = false;
        content.Q<Button>("SaveRuleScriptReload").visible = false;
        content.Q<Button>("RefreshFactValues").visible = false;
        
    }

    private void InitUIWithFactMatcher(VisualElement content, FactMatcher factMatcher)
    {
        _factMatcher = factMatcher;
        var rulesDB = _factMatcher.ruleDB;
        var button = content.Q<Button>("PickRuleButton");

        rulesDB.OnRulesParsed += OnRulesParsed;
        _factMatcher.OnRulePicked += OnRulePicked;
        _factMatcher.OnInited += OnInited;
        
        _ruleScriptSelector.visible = true;
        _ruleFilterField.visible = true;
        content.Q<Button>("SaveRuleScript").visible = true;
        content.Q<Button>("PickRuleButton").visible = true;
        content.Q<ListView>("FactList").visible = true;
        content.Q<TextField>("FactFilter").visible = true;
        content.Q<ListView>("RuleList").visible = true;
        content.Q<Button>("SaveToFile").visible = true;
        content.Q<Button>("LoadFromFile").visible = true;
        content.Q<Button>("SaveRuleScript").visible = true;
        content.Q<Button>("SaveRuleScriptReload").visible = true;
        content.Q<Button>("RefreshFactValues").visible = true;

        /*
        var editor = Editor.CreateEditor(rulesDB);
        var taskInspector = new IMGUIContainer(() => { editor.OnInspectorGUI(); });
        var rulesDBDrawer = content.Q<VisualElement>("RulesDBDrawer");
        rulesDBDrawer.Add(taskInspector);
        */

        var ruleScriptChoices = new List<string>();
        foreach (var textAsset in rulesDB.generateFrom)
        {
            ruleScriptChoices.Add(textAsset.name);
            if (textAsset.text.Length < 4000)
            {
                _ruleScriptField.value = textAsset.text;
            }

            _currentRuleScript = textAsset;
            _ruleScriptSelector.SetValueWithoutNotify(textAsset.name);
        }

        ((INotifyValueChanged<string>) _ruleScriptSelector.labelElement).SetValueWithoutNotify("scriptFile:");
        _ruleScriptSelector.RegisterCallback<ChangeEvent<string>>(ev =>
        {
            var index = Mathf.Max(0, ruleScriptChoices.IndexOf(ev.newValue));
            if (index > 0 && index < rulesDB.generateFrom.Count)
            {
                _currentRuleScript = rulesDB.generateFrom[index];
                _ruleScriptField.value = rulesDB.generateFrom[index].text;
            }
        });
        _ruleScriptSelector.choices = ruleScriptChoices;


        var saveScriptAndReload = content.Q<Button>("SaveRuleScript");
        var saveScriptAndReloadIncludingFacts = content.Q<Button>("SaveRuleScriptReload");

        saveScriptAndReload.RegisterCallback<ClickEvent>(evt =>
        {
            _factMatcher.LoadFromCSV(_factFileField.value);
            UpdateListView();
        });
        saveScriptAndReloadIncludingFacts.RegisterCallback<ClickEvent>(evt =>
        {
            _factMatcher.SaveToCSV(_factFileField.value);
            var path = AssetDatabase.GetAssetPath(_currentRuleScript);
            StreamWriter writer = new StreamWriter(path, false);
            writer.Write(_ruleScriptField.value);
            writer.Close();
            AssetDatabase.ImportAsset(path);

            _factMatcher.Reload();
            _factMatcher.LoadFromCSV(_factFileField.value);
            UpdateListView();
        });


        button.RegisterCallback<ClickEvent>(evt =>
        {
            _factMatcher.PickRules();
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

        _factListView = content.Q<ListView>("FactList");
        _factFilterField = content.Q<TextField>("FactFilter");
        _factFilterField.value = "";
        _factListView.makeItem = () => FactEntryController.MakeItem(FactItemVisAss);
        _factListView.bindItem = (element, i) => FactEntryController.BindItem(element, i, _factTests, _factMatcher);
        _factFilterField.RegisterCallback<ChangeEvent<string>>(evt => { UpdateListView(); });
        _factListView.itemsSource = _factTests;
        UpdateListView();
        _factListView.fixedItemHeight = 16.0f;


        _ruleListView = content.Q<ListView>("RuleList");
        _ruleListView.makeItem = () => FactRulesListViewController.MakeItem(RuleListViewItemAss);
        _ruleListView.bindItem = (element, i) => FactRulesListViewController.BindItem(element, i, _rulesDatas, _factMatcher);
        _ruleListView.itemsSource = _rulesDatas;

        _ruleFilterField.RegisterCallback<ChangeEvent<string>>(evt => { UpdateListViewRules(); });
        UpdateListViewRules();
        _ruleListView.fixedItemHeight = 18.0f;

        FactEntryController.factChanged -= OnFactChangedFromFactsList();
        FactEntryController.factChanged += OnFactChangedFromFactsList();
        FactRulesListViewController.factChanged -= OnFactChangedFromFactsAndRulesList();
        FactRulesListViewController.factChanged += OnFactChangedFromFactsAndRulesList();
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
        if (_factMatcher.HasDataToDispose())
        {
            _factMatcher.DisposeData();
        }
        _factMatcher.Init();
    }

    private void OnInited()
    {
        Debug.Log("On Inited");
        UpdateListView();
    }

    private void OnRulePicked(int noOfBestRules)
    {
        var rule = noOfBestRules >= 1 ? _factMatcher.GetRuleFromMatches(0) : null;
        if (rule!=null)
        {
            _lastPickedRule.text = $"Last Picked rule  {rule.ruleName} payload {rule.payload} and ruleID {rule.RuleID}";
            StringBuilder strBuilder = new StringBuilder();
            Debug.Log($"interpolated {rule.Interpolate(_factMatcher,ref strBuilder)}");
        }
        else
        {
            _lastPickedRule.text = $"found no Rule for noBestRules={noOfBestRules}";
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
                    if (entry.factName.Contains(splits[i]))
                    {
                        include = true;
                        break;
                    }
                }
                
                return include;
            });
            
            _factListView.itemsSource = factTests;
            _factTests = factTests;
            _factListView.RefreshItems();
        }
    }
    
    void UpdateListViewRules()
    {
        if (_factMatcher != null && _factMatcher.ruleDB != null)
        {
            createRuleDatas();
            _ruleListView.itemsSource = _rulesDatas;
            _ruleListView.RefreshItems();
        }
    }

    private void createRuleDatas()
    {
        var factTestsFromRulesFlattened = _factMatcher.ruleDB.CreateFlattenedRuleAtomListWithPotentiallyDuplicateFactIDS(entry =>
        {
            if (_ruleFilterField.value.Length == 0) return true;
            
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
        if (_rulesDatas == null)
        {
            _rulesDatas = new List<FactRulesListViewController.Data>();
        }

        var lastRuleID = -1;
        _rulesDatas.Clear();
        foreach (var ruleDBFactTest in factTestsFromRulesFlattened)
        {
            if (lastRuleID != ruleDBFactTest.ruleOwnerID)
            {
                var ruleData = new FactRulesListViewController.Data();
                ruleData.text = _factMatcher.GetRuleFromRuleID(ruleDBFactTest.ruleOwnerID).ruleName;
                ruleData.isRule = true;
                ruleData.ruleIndex = ruleDBFactTest.ruleOwnerID;
                ruleData.factIndex = -1;
                _rulesDatas.Add(ruleData);
            }

            var data = new FactRulesListViewController.Data();

            data.ruleIndex = ruleDBFactTest.ruleOwnerID;
            data.factIndex = ruleDBFactTest.factID;
            data.factValueText = $"{_factMatcher.PrintableFactValueFromFactTest(ruleDBFactTest)}";

            var strictOrNot = ruleDBFactTest.isStrict ? "" : "?";
            data.text =
                $"{strictOrNot}{ruleDBFactTest.factName}  {ruleDBFactTest.CompareMethodPrintable()} {ruleDBFactTest.MatchValuePrintable()}";
            data.isRule = false;
            lastRuleID = ruleDBFactTest.ruleOwnerID;
            _rulesDatas.Add(data);
        }
    }

    private void OnDestroy()
    {

        FactEntryController.factChanged -= OnFactChangedFromFactsList();
        FactRulesListViewController.factChanged -= OnFactChangedFromFactsAndRulesList();
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