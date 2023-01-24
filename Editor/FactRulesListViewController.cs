using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using UnityEditor.Graphs;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Button = UnityEngine.UIElements.Button;

public class FactRulesListViewController  
{
    public static Action<int> factChanged;
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

        controller._index = index;
        controller.factMatcher = factMatcher;
        controller.datas = datas;
        controller.Publish(datas[index],factMatcher);
    }
    
    private Label _label;
    private VisualElement _element;
    private Button _pingButton;
    private Button _setButton;
    private int _index;
    public List<Data> datas;
    public FactMatcher factMatcher;

    public float SetVisualElement(TemplateContainer item)
    {
        _element = item;
        _label = item.Q<Label>("Label");
        _pingButton = item.Q<Button>("pingButton");
        _setButton = item.Q<Button>("setButton");

        _setButton.RegisterCallback<ClickEvent>(EventCallback(this));
        return _label.layout.height;
    }

    private static EventCallback<ClickEvent> EventCallback(FactRulesListViewController controller)
    {
        return evt =>
        {
            var data = controller.datas[controller._index];
            if (!data.isRule)
            {
                var factTest = controller.factMatcher.ruleDB.GetFactTestFromFactIDAndRuleID(data.factIndex,data.ruleIndex);
                Debug.Log($"We have our data that is {data.text} and factTest is {factTest.factName} and value is {factTest.matchValue}");
                if (factTest.compareType == FactValueType.Value)
                {
                    switch (factTest.compareMethod)
                    {
                       case RuleDBFactTestEntry.Comparision.Equal:
                           controller.factMatcher[factTest.factID] = factTest.matchValue;
                           break;
                       case RuleDBFactTestEntry.Comparision.LessThan:
                       case RuleDBFactTestEntry.Comparision.LessThanEqual:
                       case RuleDBFactTestEntry.Comparision.NotEqual:
                           controller.factMatcher[factTest.factID] = factTest.matchValue - 10.0f;
                           break;
                       case RuleDBFactTestEntry.Comparision.MoreThan:
                       case RuleDBFactTestEntry.Comparision.MoreThanEqual:
                           controller.factMatcher[factTest.factID] = factTest.matchValue + 10.0f;
                           break;
                    }
                }
                if (factTest.compareType == FactValueType.String)
                {
                    switch (factTest.compareMethod)
                    {
                       case RuleDBFactTestEntry.Comparision.Equal:
                           controller.factMatcher[factTest.factID] = controller.factMatcher.StringID(factTest.matchString);
                           break;
                       case RuleDBFactTestEntry.Comparision.NotEqual:
                           controller.factMatcher[factTest.factID] =controller.factMatcher.StringID(factTest.matchString) - 1;
                           break;
                    }
                }
                
                factChanged?.Invoke(factTest.factID);
                //controller.Publish(data,controller.factMatcher);
            }
            //var factTest 
            //controller.factMatcher
        };
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
            
            data.factValueText = $"{factMatcher.PrintableFactValueFromFactTest(factTest)}";
            _label.text = $"{textLabel} {data.factValueText}";
            var factFalseUSSClass = factTest.orGroupRuleID == -1 ? "fact-false" : "fact-or";
            _element.AddToClassList(isTrue ? "fact-true" : factFalseUSSClass);
            
            orGroup = factTest.orGroupRuleID;
        }
        
    }
    

}