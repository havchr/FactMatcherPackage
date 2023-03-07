using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using FactMatching;
using Unity.Collections;
using Debug = UnityEngine.Debug;
using Unity.Jobs;
using UnityEngine;

public interface FactMatcherProvider
{
    public FactMatcher GetFactMatcher();
}

public class FactMatcher 
{
    
    public Action OnInited;
    public Action<int> OnRulePicked;
    public const int NotSetValue = -1;
    public RulesDB ruleDB;
    private NativeArray<float> _factValues;
    private NativeArray<Rule> _rules;
    private NativeArray<FactTest> _factTests;
    private NativeArray<int> _bestRule;
    private NativeArray<int> _bestRuleMatches;
    private NativeArray<int> _allRuleIndices;
    private NativeArray<int> _allRulesMatches;
    private NativeArray<int> _allMatchesForAllRules;
    private NativeArray<int> _noOfRulesWithBestMatch;
    private NativeArray<Settings> _settings;
    private NativeArray<int> _slice;
    private FactMatcherMatch _cachedJob;
    private bool hasCachedJob = false;

    
    public bool _dataDisposed = false;
    public bool _hasBeenInited = false;
    public bool _inReload = false;
    
   public bool IsInited => _hasBeenInited;

    public FactMatcher(RulesDB ruleDB)
    {
        this.ruleDB = ruleDB;
    }

    public void SaveToCSV(string filename)
    {
       using (StreamWriter writer = new StreamWriter(filename))  
       {
               
           writer.WriteLine($"Type,  Name,  Value");
           var factTests = ruleDB.CreateFlattenedFactTestListWithNoDuplicateFactIDS();
           for (int i = 0; i < factTests.Count; i++)
           {
               var factTest = factTests[i];
               var rawValue = _factValues[factTest.factID];
               var value = factTest.compareType == FactValueType.String ? ruleDB.GetStringFromStringID((int)rawValue) : $"{rawValue}";
               writer.WriteLine($"{factTest.compareType},  {factTest.factName},  {value}");  
           }
       }   
    }
    
    public void LoadFromCSV(string filename) // Loaded facts from filename
    {
        var lines = File.ReadAllLines(filename).Skip(1);
        foreach (var csvLine in lines)
        {
            var keyValType = csvLine.Split(",");
            for (int i = 0; i < keyValType.Length; i++)
            {
                keyValType[i] = keyValType[i].Trim();
            }
            var factID = FactID(keyValType[1]);
            if (factID != -1)
            {
                FactValueType.TryParse(keyValType[0], out FactValueType compareType);
                if (compareType == FactValueType.String)
                {
                    SetFact(factID, StringID(keyValType[2]));
                }
                else
                {
                    SetFact(factID, float.Parse(keyValType[2]));
                }
            }
        }
    }
    
    public string PrintableFactValueFromFactTest(RuleDBFactTestEntry factTest)
    {
        if (factTest.compareType == FactValueType.String)
        {
            return ruleDB.GetStringFromStringID((int)_factValues[factTest.factID]);
        }

        return _factValues[factTest.factID].ToString();
    }

    public void Init(bool countAllMatches=false)
    {
        ruleDB.InitRuleDB();
        _factValues = new NativeArray<float>(ruleDB.CountNumberOfFacts(),Allocator.Persistent);
        for (int i = 0; i < _factValues.Length; i++)
        {
            var factTest = ruleDB.GetFactTestFromFactID(i);
            if (factTest!=null && factTest.compareType == FactValueType.String)
            {
                _factValues[i] = NotSetValue;
            }
            else
            {
                _factValues[i] = 0;
            }
        }
        FactMatching.Functions.CreateNativeRules(this.ruleDB, out _rules, out _factTests);
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
        
        //counts amount of facts that matches for a given rule-index, encoded such that negative values
        //indicates that at least one rule did not match.
        _allMatchesForAllRules= new NativeArray<int>(_rules.Length,Allocator.Persistent);
        
        _settings = new NativeArray<Settings>(1,Allocator.Persistent);
        _settings[0] = new Settings(ruleDB.FactWriteToAllThatMatches,countAllMatches);
        
        _slice = new NativeArray<int>(2,Allocator.Persistent);
        _slice[0] = 0;
        _slice[1] = _rules.Length;
        
        _dataDisposed = false;
        _inReload = false;
        _hasBeenInited = true;
        OnInited?.Invoke();
    }

    //returns -1 if Not inited
    public int GetMemorySizeInBytesForDatabase()
    {
        if (_hasBeenInited)
        {
            int bytes = 0;
            bytes += _factValues.Length * sizeof(float);
            bytes += _rules.Length * Rule.SizeInBytes();
            bytes += _factTests.Length * FactTest.SizeInBytes();
            bytes += _bestRule.Length * sizeof(int);
            bytes += _bestRuleMatches.Length * sizeof(int);
            bytes += _allRuleIndices.Length * sizeof(int);
            bytes += _allRulesMatches.Length * sizeof(int);
            bytes += _allMatchesForAllRules.Length * sizeof(int);
            bytes += _noOfRulesWithBestMatch.Length * sizeof(int);
            bytes += _noOfRulesWithBestMatch.Length * Settings.SizeInBytes();
            bytes += _slice.Length * sizeof(int);
            return bytes;
        }

        return -1;
    }

    public int GetNumberOfMatchesForRuleID(int ruleID, out bool ruleValid)
    {
        PickRules(false, false);
        ruleValid = _allMatchesForAllRules[ruleID] <= 0;
        return Mathf.Abs(_allMatchesForAllRules[ruleID]);
    }
    public int PickRulesInBucket(BucketSlice bucketSlice)
    {
        bucketSlice.ApplyBucket(this);
        return PickRules(true, true, bucketSlice.startIndex, bucketSlice.endIndex);
    }
    public RuleDBEntry PickRuleInBucket(BucketSlice bucketSlice)
    {
        int result = PickRulesInBucket(bucketSlice);
        if (result > 0)
        {
           return GetRuleFromMatches(0);
        }
        return null;
    }

    public int PickRules(bool factWrites=true,bool fireListener=true,int startIndex=0,int endIndex=-1) // Pick the best matching rule
    {
        _slice[0] = startIndex;
        _slice[1] = endIndex == -1 ? _rules.Length : (endIndex+1);

        if (!hasCachedJob)
        {
            hasCachedJob = true;
            _cachedJob = new FactMatcherMatch() 
            {
                FactValues = _factValues,
                FactTests = _factTests,
                Rules = _rules,
                BestRule = _bestRule,
                BestRuleMatches = _bestRuleMatches,
                AllEmRulesIndices = _allRuleIndices,
                AllEmRulesMatches =  _allRulesMatches,
                AllMatchesForAllRules = _allMatchesForAllRules,
                NoOfRulesWithBestMatch = _noOfRulesWithBestMatch,
                slice = _slice,
                Settings = _settings
                
            };
        }
        _cachedJob.Execute();
        if (factWrites)
        {
            HandleFactWrites(_slice[0],_slice[1]);
        }

        if (fireListener)
        {
            OnRulePicked?.Invoke(_noOfRulesWithBestMatch[0]);
        }
        return _noOfRulesWithBestMatch[0];
    }

    private void HandleFactWrites(int sliceStart,int slicePastEnd)
    {

        if (!ruleDB.FactWriteToAllThatMatches)
        {
            for (int i = 0; i < _noOfRulesWithBestMatch[0]; i++)
            {
                int ruleIndex = _allRulesMatches[i];
                if (ruleIndex >= sliceStart && ruleIndex < slicePastEnd)
                {
                    HandleFactWrites(ruleDB.RuleFromID(_rules[_allRulesMatches[i]].ruleFiredEventId));
                }
            }
        }
        else
        {
            for (int i = 0; i < _allRuleIndices.Length; i++)
            {
                int ruleIndex = _allRuleIndices[i];
                if (ruleIndex != NotSetValue && ruleIndex >= sliceStart && ruleIndex < slicePastEnd)
                {
                    HandleFactWrites(ruleDB.RuleFromID(_rules[_allRuleIndices[i]].ruleFiredEventId));
                }
            }
        }
    }

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
                case RuleDBFactWrite.WriteMode.SetToOtherFactValue:
                    LogWritebacks(
                        $"setting value of factID {(int)factWrite.writeValue} into fact {factWrite.factName} with factID {factWrite.factID}");
                    _factValues[factWrite.factID] = _factValues[(int)factWrite.writeValue];
                    break;
                case RuleDBFactWrite.WriteMode.IncrementByOtherFactValue:
                    LogWritebacks(
                        $"increment by value of factID {(int)factWrite.writeValue} onto fact {factWrite.factName} with factID {factWrite.factID}");
                    _factValues[factWrite.factID] += _factValues[(int)factWrite.writeValue];
                    break;
                case RuleDBFactWrite.WriteMode.SubtractByOtherFactValue:
                    LogWritebacks(
                        $"increment by value of factID {(int)factWrite.writeValue} onto fact {factWrite.factName} with factID {factWrite.factID}");
                    _factValues[factWrite.factID] -= _factValues[(int)factWrite.writeValue];
                    break;
            }
        }
    }
    
    [Conditional("FACTMATCHER_LOG_WRITEBACKS")]
    static void LogWritebacks(object msg)
    {
        Debug.Log(msg);
    }
    
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
        set
        {
            _factValues[i] = value;
        }
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
    
    public RuleDBEntry PickBestRule()
    {
        var amountOfBestRules = PickRules();
        if (amountOfBestRules > 0)
        {
            return GetRuleFromMatches(0);
        }

        return null;
    }
    
    public RuleDBEntry GetRuleFromMatches(int matchIndex)
    {
        if (_inReload)
        {
            Debug.Log("In reload");
            return null;
        }
        if (matchIndex >= 0 && matchIndex < _noOfRulesWithBestMatch[0])
        {
            return ruleDB.RuleFromID(_rules[_allRulesMatches[matchIndex]].ruleFiredEventId);
        }
        return null;
    }

    public int StringID(string str)
    {
        return ruleDB.StringId(str);
    }
    
    public int FactID(string str)
    {
        return ruleDB.FactId(str);
    }
    public BucketSlice BucketSlice(string str)
    {
        var bucketSlice = ruleDB.GetSliceForBucket(str);
        if (bucketSlice != null)
        {
            bucketSlice.Init(this);
            return bucketSlice;
        }
        bucketSlice = ruleDB.GetSliceForBucket("default");
        if (bucketSlice != null)
        {
            Debug.LogError($"Could not find FactMatcher Bucket {str} - choosing default bucket [{bucketSlice.startIndex}:{bucketSlice.endIndex}] instead");
            bucketSlice.Init(this);
            return bucketSlice;
        }
            
        Debug.LogError($"Could not find FactMatcher Bucket {str} - choosing bucket of all rules [{0}:{_rules.Length-1}] instead");
        return new BucketSlice(0, _rules.Length - 1, "");
    }
    
    public RuleDBEntry GetRuleFromRuleID(int ruleID)
    {
        return ruleDB.RuleFromID(ruleID);
    }

    public bool HasDataToDispose()
    {
        return (!_dataDisposed && _hasBeenInited);
    }
    
    public void DisposeData()
    {
        _factValues.Dispose();
        _rules.Dispose();
        _factTests.Dispose();
        _bestRule.Dispose();
        _bestRuleMatches.Dispose();
        _allRuleIndices.Dispose();
        _allRulesMatches.Dispose();
        _allMatchesForAllRules.Dispose();
        _noOfRulesWithBestMatch.Dispose();
        _settings.Dispose();
        _slice.Dispose();
        
        _dataDisposed = true;
        _hasBeenInited = false;
    }
}
