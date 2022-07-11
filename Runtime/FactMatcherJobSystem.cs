using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using FactMatcher;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Debug = UnityEngine.Debug;

[Serializable]
public class FactMatcherDebugRewriteEntry{
    public string key;
    public string value;
    public bool handleAsString;
}

public class FactMatcherJobSystem : MonoBehaviour
{

    //Use this to re-cache indices, stringID's when hotloading
    public Action OnInited;
    public bool DebugWhileEditorRewriteEnable = false;
    public List<FactMatcherDebugRewriteEntry> DebugRewriteEntries;

    public RulesDB ruleDB;
    private NativeArray<float> _factValues;
    private NativeArray<FMRule> _rules;
    private NativeArray<RuleAtom> _ruleAtoms;
    private NativeArray<FactMatcher.FMRule> _bestRule;
    private NativeArray<int> _bestRuleMatches;
    private bool _inReload = false;
    private bool _dataDisposed = false;
    private bool _hasBeenInited = false;
    
    #if ODIN_INSPECTOR
    [Sirenix.OdinInspector.Button("Init")]
    #endif
    public void Init()
    {
        ruleDB.InitRuleDB();
        _factValues = new NativeArray<float>(ruleDB.CountNumberOfFacts(),Allocator.Persistent);
        FactMatcher.Functions.CreateNativeRules(this.ruleDB, out _rules, out _ruleAtoms);
        _bestRule = new NativeArray<FMRule>(1,Allocator.Persistent);
        _bestRuleMatches = new NativeArray<int>(1,Allocator.Persistent);
        _dataDisposed = false;
        _inReload = false;
        _hasBeenInited = true;
        OnInited.Invoke();
    }

    
    #if ODIN_INSPECTOR
    [Sirenix.OdinInspector.Button("Reload")]
    #endif
    public void Reload()
    {
        _inReload = true;
        if (_hasBeenInited)
        {
            DisposeData();
        }
        ruleDB.CreateRulesFromRulescripts();
        Init();
        
    }

    public float this[int i]
    {
        get { return _factValues[i]; }
        set { _factValues[i] = value; }
    }
    
    public bool SetFact(int factIndex,float value)
    {
        if (factIndex >= 0 && factIndex < _factValues.Length)
        {
            _factValues[factIndex] = value;
            return true;
        }
        return false;
    }

    public int StringID(string str)
    {
        return ruleDB.StringId(str);
    }
    
    public int FactID(string str)
    {
        return ruleDB.FactId(str);
    }


    #if ODIN_INSPECTOR
    [Sirenix.OdinInspector.Button("Pick Rule")]
    #endif
    public RuleDBEntry PickBestRule()
    {

    
#if UNITY_EDITOR
        if (Application.isEditor && DebugWhileEditorRewriteEnable)
        {
            if (!_hasBeenInited)
            {
                Init();
            }
            HandleDebugRewriteFacts();
        }
#endif
        
        if (_inReload)
        {
            Debug.Log("In reload");
            return null;
        }
        
        var job = new FactMatcherMatch() 
        {
            FactValues = _factValues,
            RuleAtoms = _ruleAtoms,
            Rules = _rules,
            BestRule = _bestRule,
            BestRuleMatches = _bestRuleMatches
        };
        var sw = new Stopwatch();
        sw.Start();
        job.Schedule().Complete();
        sw.Stop();


        if (_bestRule[0].ruleFiredEventId == -1)
        {
            return null;
        }
        RuleDBEntry rule = ruleDB.RuleFromID(_bestRule[0].ruleFiredEventId);
        Debug.Log($"The result of the best match is: {rule.payload} with {_bestRuleMatches[0]} matches and it took {sw.ElapsedMilliseconds} ms and payloadID {rule.payloadStringID}");
        HandleFactWrites(rule);
        return rule;
    }

    #if UNITY_EDITOR
    private void HandleDebugRewriteFacts()
    {
        for (int i = 0; i < DebugRewriteEntries.Count; i++)
        {
            var index = FactID(DebugRewriteEntries[i].key);
            if (index != -1)
            {
                if (DebugRewriteEntries[i].handleAsString)
                {
                    _factValues[index] = DebugRewriteEntries[i].handleAsString ? StringID(DebugRewriteEntries[i].value) : float.Parse(DebugRewriteEntries[i].value);
                }
                else if (float.TryParse(DebugRewriteEntries[i].value, out float value))
                {
                    _factValues[index] = value;
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


    private void HandleFactWrites(RuleDBEntry rule)
    {
        //Handle fact writes.
        foreach (var factWrite in rule.factWrites)
        {
            switch (factWrite.writeMode)
            {
                case RuleDBFactWrite.WriteMode.IncrementValue:
                    LogWritebacks(
                        $"increment value {factWrite.writeValue} to fact {factWrite.factName} with factID {factWrite.factID} , was {_factValues[factWrite.factID]}");
                    _factValues[factWrite.factID] += factWrite.writeValue;
                    LogWritebacks(
                        $"increment value {factWrite.writeValue} to fact {factWrite.factName} with factID {factWrite.factID} , was {_factValues[factWrite.factID]}");
                    break;
                case RuleDBFactWrite.WriteMode.SubtractValue:
                    LogWritebacks(
                        $"subtracting value {factWrite.writeValue} from fact {factWrite.factName} with factID {factWrite.factID} , was {_factValues[factWrite.factID]}");
                    _factValues[factWrite.factID] -= factWrite.writeValue;
                    LogWritebacks(
                        $"subtracting value {factWrite.writeValue} from fact {factWrite.factName} with factID {factWrite.factID} , became {_factValues[factWrite.factID]}");
                    break;
                case RuleDBFactWrite.WriteMode.SetValue:
                    LogWritebacks(
                        $"Writing value {factWrite.writeValue} into fact {factWrite.factName} with factID {factWrite.factID}");
                    _factValues[factWrite.factID] = factWrite.writeValue;
                    break;
                case RuleDBFactWrite.WriteMode.SetString:
                    LogWritebacks(
                        $"Writing String {factWrite.writeString} into fact {factWrite.factName} with factID {factWrite.factID}");
                    _factValues[factWrite.factID] = ruleDB.StringId(factWrite.writeString);
                    break;
            }
        }
    }

    private void OnDestroy()
    {
        if (!_dataDisposed && _hasBeenInited)
        {
           DisposeData(); 
        }
    }

    private void DisposeData()
    {
        _factValues.Dispose();
        _rules.Dispose();
        _ruleAtoms.Dispose();
        _bestRule.Dispose();
        _bestRuleMatches.Dispose();
        _dataDisposed = true;
        _hasBeenInited = false;
    }


#if FACTMATCHER_BURST
    [BurstCompile(CompileSynchronously = true)]
#endif
    private struct FactMatcherMatch : IJob
    {

        [Unity.Collections.ReadOnly] public NativeArray<float> FactValues;

        [Unity.Collections.ReadOnly] public NativeArray<FactMatcher.FMRule> Rules;

        [Unity.Collections.ReadOnly] public NativeArray<FactMatcher.RuleAtom> RuleAtoms;

        [WriteOnly] public NativeArray<FactMatcher.FMRule> BestRule;
        [WriteOnly] public NativeArray<int> BestRuleMatches;


        public void Execute()
        {

            int ruleI = 0;
            int currentBestMatch = 0;
            int bestRuleIndex = -1;

            //Debug.Log($"Natively! We have {Rules.Length} rules to loop");
            for (ruleI = 0; ruleI < Rules.Length; ruleI++)
            {
                var rule = Rules[ruleI];
                int howManyAtomsMatch = 0;
                if (rule.numOfAtoms > currentBestMatch)
                {


                    LogMatchJob($"for rule {ruleI} with ruleFireID {rule.ruleFiredEventId} we are checking atoms from {rule.atomIndex} to {rule.atomIndex + rule.numOfAtoms} ");
                    for (int j = rule.atomIndex; j < (rule.atomIndex + rule.numOfAtoms); j++)
                    {
                        var atom = RuleAtoms[j];
                        LogMatchJob($"for rule {ruleI} with ruleFireID {rule.ruleFiredEventId} , comparing factID {atom.factID} with value {FactValues[atom.factID]} with atom.compare.lowerBound {atom.compare.lowerBound} and upperBound {atom.compare.upperBound} ");
                        if (FactMatcher.Functions.predicate(in atom.compare, FactValues[atom.factID]))
                        {
                            howManyAtomsMatch++;
                        }
                        else if (atom.strict)
                        {
                            howManyAtomsMatch = 0;
                            break;
                        }

                    }

                    if (howManyAtomsMatch > currentBestMatch)
                    {
                        currentBestMatch = howManyAtomsMatch;
                        bestRuleIndex = ruleI;
                    }
                }
            }

            if (bestRuleIndex != -1)
            {
                BestRuleMatches[0] = currentBestMatch;
                BestRule[0] = Rules[bestRuleIndex];
            }
            else
            {
                BestRuleMatches[0] = -1;
                BestRule[0] = emptyRule;
            }
        }

    }

    static FactMatcher.FMRule emptyRule = new FactMatcher.FMRule(-1,0,0);
    
    
    [Conditional("FACTMATCHER_LOG_MATCH_JOB")]
    static void LogMatchJob(object msg)
    {
        Debug.Log(msg);
    }
    
    [Conditional("FACTMATCHER_LOG_WRITEBACKS")]
    static void LogWritebacks(object msg)
    {
        Debug.Log(msg);
    }
}