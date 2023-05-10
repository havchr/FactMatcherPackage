using System.Collections.Generic;
using UnityEngine.UIElements;
using System;

public class RuleDBListViewController
{
    public static Action<int> factChanged;

    private Label _label;
    private Button _pingButton;
    private Button _pingDocButton;

    public int index;
    public bool useDoc;
    public List<Data> rules;
    public static Action<Data> pingedRuleButtonAction;
    public static Action<Data> pingedDocButtonAction;
    public Data currentData;

    public class Data
    {
        public enum Type
        {
            Title,

            Rule,
            FactWrite,
            FactTest,
            Payload,
        }

        public Type type;
        public string text;
        public string defaultName;
        public string styleName;
        public int ruleIndex;
        public int innerIndex;
    }

    public static TemplateContainer MakeItem(VisualTreeAsset visualAsset, bool ignoreDocumentationDemand)
    {
        var item = visualAsset.Instantiate();
        var controller = new RuleDBListViewController();
        controller.SetVisualElement(item, ignoreDocumentationDemand);
        item.userData = controller;
        return item;
    }

    public static void BindItem(VisualElement visElement, int index, List<Data> datas)
    {
        visElement.AddToClassList(datas[index].styleName);
        var controller = (RuleDBListViewController)visElement.userData;
        controller.index = index;
        controller.rules = datas;

        Data data = datas[index];
        controller.Publish(data);
    }

    public float SetVisualElement(TemplateContainer item, bool ignoreDocumentationDemand)
    {
        _pingButton = item.Q<Button>("pingButton");
        _pingButton?.RegisterCallback(PingButtonCallback(this));

        _pingDocButton = item.Q<Button>("pingDocButton");
        if (!ignoreDocumentationDemand)
        {
            _pingDocButton?.RegisterCallback(PingDocButtonCallback(this)); 
        }
        else
        {
            _pingDocButton.visible = !ignoreDocumentationDemand;
        }

        _label = item.Q<Label>("Label");
        return _label.layout.height;
    }

    private static EventCallback<ClickEvent> PingButtonCallback(RuleDBListViewController controller)
    { return evt => pingedRuleButtonAction?.Invoke(controller.currentData); }

    private static EventCallback<ClickEvent> PingDocButtonCallback(RuleDBListViewController controller)
    { return evt => pingedDocButtonAction?.Invoke(controller.currentData); }

    public void Publish(Data data)
    {
        _label.SetEnabled(false);
        _label.text = data.text;
        _label.userData = data;

        currentData = data;
    }
}
