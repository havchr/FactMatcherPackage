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
    private NativeArray<int> _bestRule;
    private NativeArray<int> _bestRuleMatches;
    private NativeArray<int> _allRuleIndices;
    private NativeArray<int> _allRulesMatches;
    private NativeArray<int> _noOfRulesWithBestMatch;
    private NativeArray<Settings> _settings;
    private bool _inReload = false;
    private bool _dataDisposed = false;
    private bool _hasBeenInited = false;
    public const int NotSetValue = -1;
    
    #if ODIN_INSPECTOR
    [Sirenix.OdinInspector.Button("Init")]
    #endif
    public void Init()
    {
        ruleDB.InitRuleDB();
        _factValues = new NativeArray<float>(ruleDB.CountNumberOfFacts(),Allocator.Persistent);
        for (int i = 0; i < _factValues.Length; i++)
        {
            _factValues[i] = NotSetValue;
        }
        FactMatcher.Functions.CreateNativeRules(this.ruleDB, out _rules, out _ruleAtoms);
        _bestRule = new NativeArray<int>(1,Allocator.Persistent);
        
        //if our setting is not in need of these - we could ditch/Skip allocating them, but optimize that later if you feel like it.
        //We need them if we want to FactWrite to all Rules that has matches (irrespective of amount of matches) - see ruleDB.FactWriteToAllThatMatches
        _allRuleIndices = new NativeArray<int>(_rules.Length,Allocator.Persistent);
       
        //if we have four rules that all match with 2 - and that is our best match - four is stored in _noOfRulesWithBestMatch
        _noOfRulesWithBestMatch = new NativeArray<int>(1,Allocator.Persistent);
        //folowing that same example - _bestRuleMatches is 2
        _bestRuleMatches = new NativeArray<int>(1,Allocator.Persistent);
        //to read out these four rules from our example - expect to find their indices in _allRulesMatches[0 - 3]
        _allRulesMatches = new NativeArray<int>(_rules.Length,Allocator.Persistent);
        
        _settings = new NativeArray<Settings>(1,Allocator.Persistent);
        _settings[0] = new Settings(ruleDB.FactWriteToAllThatMatches);
        _dataDisposed = false;
        _inReload = false;
        _hasBeenInited = true;
        OnInited?.Invoke();
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

    public void DebugLogDump()
    {
        for (int i = 0; i < _factValues.Length; i++)
        {
            var factName = ruleDB.GetFactVariabelNameFromFactID(i);
            if (_factValues[i] < 0f)
            {
                Debug.Log($"fact {factName} is {_factValues[i]}");
            }
            else
            {
                var parsedValue = ruleDB.ParseFactValueFromFactID(i, _factValues[i]);
                Debug.Log($"fact {factName} is {parsedValue} ");
            }

        }
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
        var amountOfBestRules = PickRules();
        if (amountOfBestRules > 0)
        {
            return GetRule(0);
        }

        return null;
    }

    public RuleDBEntry GetRule(int ruleIndex)
    {
        if (_inReload)
        {
            Debug.Log("In reload");
            return null;
        }
        if (ruleIndex >= 0 && ruleIndex < _noOfRulesWithBestMatch[0])
        {
            return ruleDB.RuleFromID(_rules[_allRulesMatches[ruleIndex]].ruleFiredEventId);
        }
        return null;
    }
    
    public int PickRules()
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
            return NotSetValue;
        }
        
        var job = new FactMatcherMatch() 
        {
            FactValues = _factValues,
            RuleAtoms = _ruleAtoms,
            Rules = _rules,
            BestRule = _bestRule,
            BestRuleMatches = _bestRuleMatches,
            AllEmRulesIndices = _allRuleIndices,
            AllEmRulesMatches =  _allRulesMatches,
            NoOfRulesWithBestMatch =  _noOfRulesWithBestMatch,
            Settings = _settings
            
        };
        var sw = new Stopwatch();
        sw.Start();
        job.Schedule().Complete();
        sw.Stop();
        
        
        if (ruleDB.FactWriteToAllThatMatches)
        {
            for (int i = 0; i < _allRuleIndices.Length; i++)
            {
                if (_allRuleIndices[i] != NotSetValue)
                {
                    HandleFactWrites(ruleDB.RuleFromID(_rules[_allRuleIndices[i]].ruleFiredEventId));
                }
            }
        }
        else
        {
            for (int i = 0; i < _noOfRulesWithBestMatch[0]; i++)
            {
                var rule = ruleDB.RuleFromID(_rules[_allRulesMatches[i]].ruleFiredEventId);
                Debug.Log($"The result of the best match {i+1} of {_noOfRulesWithBestMatch[0]} is: {rule.payload} with {_bestRuleMatches[0]} matches and it took {sw.ElapsedMilliseconds} ms and payloadID {rule.payloadStringID}");
                HandleFactWrites(ruleDB.RuleFromID(_rules[_allRulesMatches[i]].ruleFiredEventId));
            }
        }
        return _noOfRulesWithBestMatch[0];
    }

    #if UNITY_EDITOR
    private void HandleDebugRewriteFacts()
    {
        for (int i = 0; i < DebugRewriteEntries.Count; i++)
        {
            var index = FactID(DebugRewriteEntries[i].key);
            if (index != NotSetValue)
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
        _allRuleIndices.Dispose();
        _allRulesMatches.Dispose();
        _noOfRulesWithBestMatch.Dispose();
        _settings.Dispose();
        
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
        [Unity.Collections.ReadOnly] public NativeArray<FactMatcher.Settings> Settings;

        [WriteOnly] public NativeArray<int> BestRule;
        [WriteOnly] public NativeArray<int> BestRuleMatches;
        [WriteOnly] public NativeArray<int> AllEmRulesIndices;
        [WriteOnly] public NativeArray<int> AllEmRulesMatches;
        [WriteOnly] public NativeArray<int> NoOfRulesWithBestMatch;

        public void Execute()
        {

            int ruleI = 0;
            int currentBestMatch = 0;
            int bestRuleIndex = NotSetValue;
            int allBestMatchesIndex = 0;

            //Debug.Log($"Natively! We have {Rules.Length} rules to loop");
            for (ruleI = 0; ruleI < Rules.Length; ruleI++)
            {
                var rule = Rules[ruleI];
                int howManyAtomsMatch = 0;
                if (rule.numOfAtoms >= currentBestMatch || Settings[0].FactWriteToAllMatches)
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
                    AllEmRulesIndices[ruleI] = howManyAtomsMatch > 0 ? ruleI : NotSetValue;
                    if (howManyAtomsMatch == currentBestMatch)
                    {
                        allBestMatchesIndex++;
                        AllEmRulesMatches[allBestMatchesIndex] = ruleI;
                    }
                    else if (howManyAtomsMatch > currentBestMatch)
                    {
                        currentBestMatch = howManyAtomsMatch;
                        bestRuleIndex = ruleI;
                        allBestMatchesIndex = 0;
                        AllEmRulesMatches[allBestMatchesIndex] = ruleI;
                    }
                }
            }

            if (bestRuleIndex != NotSetValue)
            {
                BestRuleMatches[0] = currentBestMatch;
                BestRule[0] = bestRuleIndex;
                NoOfRulesWithBestMatch[0] = allBestMatchesIndex+1;
            }
            else
            {
                BestRuleMatches[0] = NotSetValue;
                BestRule[0] = NotSetValue;
                NoOfRulesWithBestMatch[0] = NotSetValue;
            }
        }

    }

    static FactMatcher.FMRule emptyRule = new FactMatcher.FMRule(NotSetValue,0,0);
    
    
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
