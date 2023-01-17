using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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

    private FactMatcher factaMatcha;
    private Label _lastPickedRule;
    private ListView _factListView;
    private ListView _ruleListView;
    private TextField _factFilterField;
    private TextField _factFileField;
    private TextField _ruleScriptField;
    private DropdownField _ruleScriptSelector;
    private List<RuleDBFactTestEntry> _factTests;
    private List<FactRulesListViewController.Data> _rulesDatas;
    private TextAsset _currentRuleScript;
    
    public void CreateGUI()
    {
        var content = new VisualElement();
        WindowXML.CloneTree(content);
        content.styleSheets.Add(Style);
        rootVisualElement.Add(content);

        _lastPickedRule = content.Q<Label>("RuleLabel");
        var rulesDBField = content.Q<ObjectField>("RulesDBField");
        _ruleScriptField = content.Q<TextField>("RuleScript");
        _ruleScriptSelector = content.Q<DropdownField>("RuleScriptSelector");
        _factFileField = content.Q<TextField>("FactFileLocation");
        
        rulesDBField.objectType = typeof(RulesDB);
        
        
        rulesDBField.RegisterCallback<ChangeEvent<Object>>(evt =>
        {
            if (factaMatcha != null && factaMatcha.HasDataToDispose())
            {
                factaMatcha.DisposeData();
                factaMatcha = null;
            }
            var rulesDB = evt.newValue as RulesDB;
            if (rulesDB!=null)
            {
                Debug.Log("We are initing things.");
                factaMatcha = new FactMatcher(rulesDB);
                factaMatcha.Init(countAllMatches:true);
                var button = content.Q<Button>("PickRuleButton");
                factaMatcha.SetFact(factaMatcha.FactID("fact_value_example"), 1337.0f);
                    

                rulesDB.OnRulesParsed += OnRulesParsed;
                factaMatcha.OnRulePicked += OnRulePicked;
                factaMatcha.OnInited += OnInited;
                
                var editor = Editor.CreateEditor(rulesDB);
                var taskInspector = new IMGUIContainer(() => { editor.OnInspectorGUI(); });
                var rulesDBDrawer = content.Q<VisualElement>("RulesDBDrawer");
                rulesDBDrawer.Add(taskInspector);

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
                ((INotifyValueChanged<string>)_ruleScriptSelector.labelElement).SetValueWithoutNotify("scriptFile:");
                _ruleScriptSelector.RegisterCallback<ChangeEvent<string>>(ev =>
                {
                    var index = Mathf.Max(0,ruleScriptChoices.IndexOf(ev.newValue));
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
                    factaMatcha.LoadFromCSV(_factFileField.value);
                    UpdateListView();
                });
                saveScriptAndReloadIncludingFacts.RegisterCallback<ClickEvent>(evt =>
                {
                    factaMatcha.SaveToCSV(_factFileField.value);
                    var path = AssetDatabase.GetAssetPath(_currentRuleScript);
                    StreamWriter writer = new StreamWriter(path, false);
                    writer.Write(_ruleScriptField.value);
                    writer.Close();
                    AssetDatabase.ImportAsset(path); 
                    // _currentRuleScript
                    
                    //_currentRuleScript. = _ruleScriptField.value;  
                    
                    //_ruleScriptField.value = rulesDB.generateFrom[_ruleScriptSelector.].text;
                    
                    factaMatcha.Reload();
                    factaMatcha.LoadFromCSV(_factFileField.value);
                    UpdateListView();
                });


                button.RegisterCallback<ClickEvent>(evt =>
                {
                    factaMatcha.PickRules();

                });
                
                var saveButton= content.Q<Button>("SaveToFile");
                saveButton.RegisterCallback<ClickEvent>(evt =>
                {
                    factaMatcha.SaveToCSV(_factFileField.value);

                });
                
                var loadButton = content.Q<Button>("LoadFromFile");
                loadButton.RegisterCallback<ClickEvent>(evt =>
                {
                    factaMatcha.LoadFromCSV(_factFileField.value);
                    UpdateListView();
                });
                
                _factListView = content.Q<ListView>("FactList");
                _factFilterField = content.Q<TextField>("FactFilter");
                _factFilterField.value = "";
                _factListView.makeItem = () => FactEntryController.MakeItem(FactItemVisAss);
                _factListView.bindItem = (element, i) => FactEntryController.BindItem(element, i, _factTests, factaMatcha);
                _factFilterField.RegisterCallback<ChangeEvent<string> >(evt =>
                {
                    UpdateListView();
                });
                _factListView.itemsSource = _factTests;
                UpdateListView();
                _factListView.fixedItemHeight = 16.0f;

                
                _ruleListView = content.Q<ListView>("RuleList");
                _ruleListView.makeItem = () => FactRulesListViewController.MakeItem(RuleListViewItemAss);
                _ruleListView.bindItem = (element,i) => FactRulesListViewController.BindItem(element,i,_rulesDatas,factaMatcha);
                _ruleListView.itemsSource = _rulesDatas;
                UpdateListViewRules();
                _ruleListView.fixedItemHeight = 18.0f;

                FactEntryController.factChanged -= i =>
                {
                    //UpdateListViewRules();
                };
                FactEntryController.factChanged += i =>
                {
                    //UpdateListViewRules();
                    
                    if (factaMatcha != null && factaMatcha.ruleDB != null)
                    {
                        //createRuleDatas();
                        //_ruleListView.itemsSource = _rulesDatas;
                        _ruleListView.RefreshItems();
                    }
                };
            }
        });
    }

    private void OnRulesParsed()
    {
        if (factaMatcha.HasDataToDispose())
        {
            factaMatcha.DisposeData();
        }
        factaMatcha.Init();
    }

    private void OnInited()
    {
        Debug.Log("On Inited");
        UpdateListView();
    }

    private void OnRulePicked(int noOfBestRules)
    {
        var rule = noOfBestRules >= 1 ? factaMatcha.GetRuleFromMatches(0) : null;
        if (rule!=null)
        {
            _lastPickedRule.text = $"Last Picked rule  {rule.ruleName} payload {rule.payload} and ruleID {rule.RuleID}";
        }
        else
        {
            _lastPickedRule.text = $"found no Rule for noBestRules={noOfBestRules}";
        }
        UpdateListView();
    }
    
    
    void UpdateListView()
    {
        if (factaMatcha != null && factaMatcha.ruleDB != null)
        {
            var factTests = factaMatcha.ruleDB.CreateFlattenedFactTestListWithNoDuplicateFactIDS(entry =>
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
        if (factaMatcha != null && factaMatcha.ruleDB != null)
        {
            createRuleDatas();
            _ruleListView.itemsSource = _rulesDatas;
            _ruleListView.RefreshItems();
        }
    }

    private void createRuleDatas()
    {
        var factTestsFromRulesFlattened = factaMatcha.ruleDB.CreateFlattenedRuleAtomListWithPotentiallyDuplicateFactIDS();
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
                ruleData.text = factaMatcha.GetRuleFromRuleID(ruleDBFactTest.ruleOwnerID).ruleName;
                ruleData.isRule = true;
                ruleData.ruleIndex = ruleDBFactTest.ruleOwnerID;
                ruleData.factIndex = -1;
                _rulesDatas.Add(ruleData);
            }

            var data = new FactRulesListViewController.Data();

            data.ruleIndex = ruleDBFactTest.ruleOwnerID;
            data.factIndex = ruleDBFactTest.factID;
            //var matchValue = factaMatcha.PrintableFactValueFromFactTest(ruleDBFactTest);
            //(ruleDBFactTest.compareType == FactValueType.Value) ? $"{ruleDBFactTest.matchValue}" : ruleDBFactTest.matchString;

            data.factValueText = $"{factaMatcha.PrintableFactValueFromFactTest(ruleDBFactTest)}";

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

        if (factaMatcha.ruleDB != null)
        {
            factaMatcha.ruleDB.OnRulesParsed -= OnRulesParsed;
        }
        factaMatcha.OnRulePicked -= OnRulePicked;
        if (factaMatcha != null && factaMatcha.HasDataToDispose())
        {
            factaMatcha.DisposeData();
        }
    }
}