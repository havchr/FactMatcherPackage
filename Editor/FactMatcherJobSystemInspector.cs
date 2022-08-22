using System.Collections;
using System.Collections.Generic;
using FactMatcher;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

public class FactEntryController
{
    private Label _label;

    public float SetVisualElement(TemplateContainer item)
    {
        _label = item.Q<Label>("Label");
        return _label.layout.height;
    }

    public void Publish(RuleDBAtomEntry atom,string value)
    {
        _label.text = atom.factName + " " + value;
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
    private List<RuleDBAtomEntry> _ruleAtoms;

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
        InspectorElement.FillDefaultInspector(defaultInspectorFoldout,serializedObject,this);
        
        _facty = (FactMatcherJobSystem) this.target;

        _lastPickedRule = inspector.Q<Label>("LastPickedRuleLabel");
        _factListView = inspector.Q<ListView>("FactList");
        _factFilterField = inspector.Q<TextField>("FactFilter");
        _factFilterField.value = "";
        _factListView.makeItem = MakeItem;
        _factListView.bindItem = BindItem;
        
        _factFilterField.RegisterCallback<ChangeEvent<string> >(evt =>
        {
            UpdateListView();
        });
        
        _factListView.itemsSource = _ruleAtoms;
        _facty.OnRulePicked -= OnRulePicked;
        _facty.OnRulePicked += OnRulePicked;
        UpdateListView();
        _factListView.fixedItemHeight = 16.0f;
        return inspector;
    }

    private void OnRulePicked(int ruleId)
    {
        var rule = _facty.ruleDB.RuleFromID(ruleId);
        _lastPickedRule.text = $"Last Picked rule  {rule.ruleName} payload {rule.payload}";
        UpdateListView();
    }

    void UpdateListView()
    {
        _ruleAtoms = _facty.ruleDB.CreateFlattenedRuleAtomList(entry =>
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

    private void BindItem(VisualElement visElement, int index)
    {
        var controlla = (FactEntryController)visElement.userData;
        var factValue = "";
        var atom = _ruleAtoms[index];
        if (_facty.IsInited)
        {
            factValue = atom.compareType == FactValueType.String ? _facty.ruleDB.GetStringFromStringID((int)_facty[atom.factID]) : $"{_facty[atom.factID]}";
        }
        controlla.Publish(_ruleAtoms[index],factValue);
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