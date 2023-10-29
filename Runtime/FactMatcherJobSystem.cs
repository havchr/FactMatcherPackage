using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;


[Serializable]
public class FactMatcherDebugRewriteEntry{
    public string key;
    public string value;
    public bool handleAsString;
}

public class FactMatcherJobSystem : MonoBehaviour, FactMatcherProvider
{
    public bool AutoInitOnStart = false;
    //Use this to re-cache indices, stringID's when hotloading
    public Action OnInited;
    public Action<int> OnRulePicked;
    public bool DebugWhileEditorRewriteEnable = false;
    public List<FactMatcherDebugRewriteEntry> DebugRewriteEntries;
    public RulesDB ruleDB;
    private FactMatcher _factMatcher;

    public FactMatcher GetFactMatcherData()
    {
        return _factMatcher;
    }

   public bool IsInited => _factMatcher !=null && _factMatcher._hasBeenInited;

    private void Start()
    {
        if (AutoInitOnStart)
        {
            Init();
        }
    }

#if ODIN_INSPECTOR
    [Sirenix.OdinInspector.Button("Init")]
    #endif
    public void Init()
    {
        _factMatcher = new FactMatcher(ruleDB);
        _factMatcher.Init();
        OnInited?.Invoke();
    }

    
    #if ODIN_INSPECTOR
    [Sirenix.OdinInspector.Button("Reload")]
    #endif
    public void Reload()
    {
        _factMatcher.Reload();
    }
    
    public int StringID(string str)
    {
        return ruleDB.StringId(str);
    }
    
    public int FactID(string str)
    {
        return ruleDB.FactId(str);
    }

    public float this[int i]
    {
        get => _factMatcher[i];
        set => _factMatcher[i] = value;
    }
    
    public bool SetFact(int factIndex,float value)
    {
        return _factMatcher.SetFact(factIndex, value);
    }

    #if ODIN_INSPECTOR
    [Sirenix.OdinInspector.Button("Pick Rule")]
    #endif
    public RuleDBEntry PickBestRule()
    {
        return _factMatcher.PickBestRule();
    }
    
    public int PickRules()
    {
    
#if UNITY_EDITOR
        if (Application.isEditor && DebugWhileEditorRewriteEnable)
        {
            if (!_factMatcher._hasBeenInited)
            {
                _factMatcher.Init();
            }
            HandleDebugRewriteFacts();
        }
#endif
        return _factMatcher.PickBestRules();
    }

    #if UNITY_EDITOR
    private void HandleDebugRewriteFacts()
    {
        for (int i = 0; i < DebugRewriteEntries.Count; i++)
        {
            var index = FactID(DebugRewriteEntries[i].key);
            if (index != FactMatcher.NotSetValue)
            {
                if (DebugRewriteEntries[i].handleAsString)
                {
                    _factMatcher[index] = DebugRewriteEntries[i].handleAsString ? StringID(DebugRewriteEntries[i].value) : float.Parse(DebugRewriteEntries[i].value);
                }
                else if (float.TryParse(DebugRewriteEntries[i].value, out float value))
                {
                    _factMatcher[index] = value;
                }
                else
                {
                    Debug.LogWarning($"could not parse value {DebugRewriteEntries[i].value} to float for key {DebugRewriteEntries[i].key}.");
                }
            }
            else
            {
                Debug.LogWarning($"Trying to rewrite key {DebugRewriteEntries[i].key} but could not find it in the FactMatcher system.");
            }
        }
    }
    #endif


    private void OnDestroy()
    {
        if (_factMatcher!=null && _factMatcher.HasDataToDispose())
        {
            _factMatcher.DisposeData();
        }
    }



    static FactMatching.Rule emptyRule = new FactMatching.Rule(FactMatcher.NotSetValue,0,0);
    
    
    
    [Conditional("FACTMATCHER_LOG_WRITEBACKS")]
    static void LogWritebacks(object msg)
    {
        Debug.Log(msg);
    }

    public RuleDBEntry GetRule(int i)
    {
        return _factMatcher.GetRuleFromMatches(i);
    }

    public FactMatcher GetFactMatcher()
    {
        return _factMatcher;
    }
}
