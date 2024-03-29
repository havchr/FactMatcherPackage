using System.Collections;
using System.Collections.Generic;
using FactMatching;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;


public class RuleEntryController
{
    private TextField _label;

    public float SetVisualElement(TemplateContainer item)
    {
        _label = item.Q<TextField>("TextField");
        return _label.layout.height;
    }

    public void Publish(FactMatcher Facty,RuleDBFactTestEntry factTest,string value)
    {
        
        var callback = (EventCallback<ChangeEvent<string>>) _label.userData;
        if (callback != null)
        {
            _label.UnregisterCallback(callback);
        }

        _label.labelElement.SetEnabled(false);
        var label = _label.labelElement as INotifyValueChanged<string>;
        label.SetValueWithoutNotify(factTest.factName);
        _label.SetValueWithoutNotify(value);
        
        callback = CreateCallback(Facty, factTest);
        _label.RegisterCallback(callback,TrickleDown.TrickleDown);
    }

    private static EventCallback<ChangeEvent<string>> CreateCallback(FactMatcher Facty, RuleDBFactTestEntry factTest)
    {
        return evt =>
        {
            if (Facty.IsInited)
            {
                if (float.TryParse(evt.newValue, out float result))
                {
                    Facty[factTest.factID] = result;
                }
                else
                {
                    Facty[factTest.factID] = Facty.StringID(evt.newValue);
                }
            }
        };
    }
}

[CustomEditor(typeof(FactMatcherJobSystem))]
public class FactMatcherJobSystemInspector : Editor
{

    public VisualTreeAsset FactItemVisAss;
    public VisualTreeAsset InspectorXML;
    public StyleSheet InspectorStyle;

    private ListView _factListView;
    private TextField _factFilterField;
    private Label _lastPickedRule;
    private FactMatcherJobSystem _facty;
    private List<RuleDBFactTestEntry> _ruleAtoms;

    public override VisualElement CreateInspectorGUI()
    {
        if (InspectorXML == null)
        {
            return base.CreateInspectorGUI();
        }

        _facty = (FactMatcherJobSystem) target;
        var inspector = new VisualElement();
        InspectorXML.CloneTree(inspector);
        inspector.styleSheets.Add(InspectorStyle);
        var defaultInspectorFoldout = inspector.Q("DefaultInspector");
        InspectorElement.FillDefaultInspector(defaultInspectorFoldout, serializedObject, this);

        _facty = (FactMatcherJobSystem) this.target;

        _lastPickedRule = inspector.Q<Label>("LastPickedRuleLabel");
        _factListView = inspector.Q<ListView>("FactList");
        _factFilterField = inspector.Q<TextField>("FactFilter");
        _factFilterField.value = "";
        
        /*
         * 
    private void BindItem(VisualElement visElement, int index)
    {
        var controlla = (FactEntryController) visElement.userData;
        var factValue = "";
        var atom = _ruleAtoms[index];
        if (_facty.IsInited)
        {
            factValue = atom.compareType == FactValueType.String ? _facty.ruleDB.GetStringFromStringID((int) _facty[atom.factID]) : $"{_facty[atom.factID]}";
        }

        controlla.Publish(_facty.GetFactMatcherData(), _ruleAtoms[index], factValue);
    }
         */
        
        _factListView.makeItem = () => FactEntryController.MakeItem(FactItemVisAss);
        _factListView.bindItem = (element, i) => FactEntryController.BindItem(element, i, _ruleAtoms, _facty.GetFactMatcher());
        _factFilterField.RegisterCallback<ChangeEvent<string>>(evt => { UpdateListView(); });
        
        //_factListView.makeItem = MakeItem;
        //_factListView.bindItem = BindItem;

        inspector.Q<Button>("PickRuleButton").RegisterCallback<ClickEvent>(evt =>
        {
            if (!_facty.IsInited)
            {
                _facty.Init();
            }

            _facty.PickRules();
        });
        _factFilterField.RegisterCallback<ChangeEvent<string>>(evt => { UpdateListView(); });

        _factListView.itemsSource = _ruleAtoms;
        _facty.OnRulePicked -= OnRulePicked;
        _facty.OnRulePicked += OnRulePicked;
        UpdateListView();
        _factListView.fixedItemHeight = 16.0f;
        return inspector;
    }

    private void OnRulePicked(int noOfBestRules)
    {
        var rule = noOfBestRules >= 1 ? _facty.GetRule(0) : null;
        if (rule != null)
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
        if (_facty != null && _facty.ruleDB != null)
        {
            _ruleAtoms = _facty.ruleDB.CreateFlattenedFactTestListWithNoDuplicateFactIDS(entry =>
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
            _factListView.itemsSource = _ruleAtoms;
            _factListView.RefreshItems();
        }
    }



    TemplateContainer MakeItem()
    {
        var item = FactItemVisAss.Instantiate();
        var controller = new FactEntryController();
        controller.SetVisualElement(item);
        item.userData = controller;
        return item;
    }

    public override void OnInspectorGUI()
    {
        /*
        var facty = (FactMatcherJobSystem) this.target;
        if (facty.IsInited)
        {
                var numFacts = facty.ruleDB.CountNumberOfFacts();
                for (int i = 0; i < numFacts; i++)
                {
                        GUILayout.Label($"{facty.ruleDB.GetFactVariabelNameFromFactID(i) } = {facty[i]}");
                }
        }
        //facty.ruleDB.GetFactVariabelNameFromFactID()
        */
        base.OnInspectorGUI();
    }
}