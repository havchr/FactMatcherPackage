using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.Graphs;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;

public class FactRulesListViewController  
{
    public class Data
    {
        public bool isRule;
        public string text;
        public string factValueText;
        public int ruleIndex;
        public int factIndex;
    }
    
    public static TemplateContainer MakeItem(VisualTreeAsset visualAsset)
    {
        var item = visualAsset.Instantiate();
        var controller = new FactRulesListViewController();
        controller.SetVisualElement(item);
        item.userData = controller;
        return item;
    }
    
    public static void BindItem(VisualElement visElement, int index,List<Data> datas,FactMatcher factMatcher)
    {
        var controller = (FactRulesListViewController)visElement.userData;
        controller.Publish(datas[index],factMatcher);
    }
    
    private Label _label;
    private VisualElement _element;

    public float SetVisualElement(TemplateContainer item)
    {
        _element = item;
        _label = item.Q<Label>("Label");
        return _label.layout.height;
    }

    private static int orGroup = -1;
    private static bool lastOrFactResult = false;

    public void Publish(Data data,FactMatcher factMatcher)
    {
        _element.RemoveFromClassList("fact-false");
        _element.RemoveFromClassList("fact-true");
        _element.RemoveFromClassList("fact-or");
        _element.RemoveFromClassList("rule");
        if (data.isRule)
        {
            var matches = factMatcher.GetNumberOfMatchesForRuleID(data.ruleIndex, out bool validRule);
            _label.text = $"Rule {data.text}        {matches} matches";
            _element.AddToClassList("rule");
        }
        else
        {
            var factTest = factMatcher.ruleDB.GetFactTestFromFactIDAndRuleID(data.factIndex,data.ruleIndex);
            var compare = factTest.CreateCompare(factMatcher.ruleDB);
            var isTrue = FactMatching.Functions.Predicate(compare, factMatcher[factTest.factID]);
            var orGroupText = $"    {(factTest.orGroupRuleID!=orGroup ? "IF-OR":"     OR")}";
            var textLabel = $"{(factTest.orGroupRuleID!=-1 ? orGroupText:"")}    {(isTrue ? "OK" : "False")} {data.text}  VS ";
            _label.text = $"{textLabel} {data.factValueText}";
            var factFalseUSSClass = factTest.orGroupRuleID == -1 ? "fact-false" : "fact-or";
            _element.AddToClassList(isTrue ? "fact-true" : factFalseUSSClass);
            
            orGroup = factTest.orGroupRuleID;
        }
        
    }
    

}