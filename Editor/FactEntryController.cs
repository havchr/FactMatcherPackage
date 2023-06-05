using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class FactEntryController
{
    public static Action<int> factChanged;
    
    private TextField _label;
    public int index;
    public List<RuleDBFactTestEntry> factTests;
    public FactMatcher factMatcher;
    
    public static TemplateContainer MakeItem(VisualTreeAsset visualAsset)
    {
        var item = visualAsset.Instantiate();
        var controller = new FactEntryController();
        controller.SetVisualElement(item);
        item.userData = controller;
        return item;
    }

    
    public static void BindItem(VisualElement visElement, int index,List<RuleDBFactTestEntry> factTests,FactMatcher factMatcher)
    {
        var controller = (FactEntryController)visElement.userData;
        controller.index = index;
        controller.factTests = factTests;
        controller.factMatcher = factMatcher;
        
        var factValue = "";
        var factTest = factTests[index];
        if (factMatcher!=null)
        {
            factValue = factTest.compareType == FactValueType.String ? factMatcher.ruleDB.GetStringFromStringID((int)factMatcher[factTest.factID]) : $"{factMatcher[factTest.factID]}";
        }
        controller.Publish(factMatcher,factTest,factValue);
    }

    public float SetVisualElement(TemplateContainer item)
    {
        _label = item.Q<TextField>("TextField");
        _label.RegisterCallback(CreateCallback(this));
        return _label.layout.height;
    }


    public void Publish(FactMatcher Facty,RuleDBFactTestEntry factTest,string value)
    {
        _label.labelElement.SetEnabled(false);
        var label = _label.labelElement as INotifyValueChanged<string>;
        label.SetValueWithoutNotify(factTest.factName);
        _label.SetValueWithoutNotify(value);
        label.SetValueWithoutNotify($"{factTest.factName}");
        _label.userData = factTest;
    }

    static EventCallback<ChangeEvent<string>> CreateCallback(FactEntryController controller)
    {
        return evt =>
        {
            var factTest = controller.factTests[controller.index];
            if (controller.factMatcher.IsInited)
            {
                if (float.TryParse(evt.newValue, out float result))
                {
                    controller.factMatcher[factTest.factID] = result;
                }
                else
                {
                    controller.factMatcher[factTest.factID] = controller.factMatcher.StringID(evt.newValue);
                }
                factChanged?.Invoke(factTest.factID);
            }
        };
    }

}