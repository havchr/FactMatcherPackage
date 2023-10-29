using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using FactMatching;
using Unity.Collections;
using Debug = UnityEngine.Debug;
using UnityEngine;

public interface FactMatcherProvider
{
    public FactMatcher GetFactMatcher();
}

public class FactMatcher 
{
    
    public Action OnInited;
    public Action<int> OnRulePicked;
    public Action<int> OnRulePeeked;
    public Action<int> OnValidRulePicked;
    public Action<int> OnValidRulePeeked;
    public const int NotSetValue = -1;
    public RulesDB ruleDB;
    private NativeArray<float> _factValues;
    private NativeArray<Rule> _rules;
    private NativeArray<FactTest> _factTests;
    private NativeArray<int> _bestRule;
    private NativeArray<int> _bestRuleMatches;
    private NativeArray<int> _allValidRuleIndices;
    private NativeArray<int> _allBestRulesIndices;
    private NativeArray<int> _allMatchesForAllRules;
    private NativeArray<int> _noOfRulesWithBestMatch;
    private NativeArray<int> _noOfValidRules;
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

    public ProblemReporting SaveToCSV(string filename)
    {
        ProblemReporting problems = new();
        using StreamWriter writer = new(filename);
        writer.WriteLine($"Type,  Name,  Value");
        var factTests = ruleDB.CreateFlattenedFactTestListWithNoDuplicateFactIDS();
        for (int i = 0; i < factTests.Count; i++)
        {
            try
            {
                var factTest = factTests[i];
                var rawValue = _factValues[factTest.factID];
                var value = factTest.compareType == FactValueType.String ? ruleDB.GetStringFromStringID((int)rawValue) : $"{rawValue}";
                writer.WriteLine($"{factTest.compareType},  {factTest.factName},  {value}");
            }
            catch (Exception e)
            {
                //problems.ReportNewError($"Error while saving to CSV file,\nfactID is ({factTests[i].factID})", null, -1, e);
                problems?.ClearList();
                throw e;
            }
        }
        return problems;
    }
    
    public string LoadFromCSV(string filename)
    {
        ProblemReporting ruleScriptParsingProblems = new();
        string problemString = "";
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
            else
            {
                problemString += $"factID == -1";
            }
        }
        return problemString;
    }
    
    public string PrintableFactValueFromFactTest(RuleDBFactTestEntry factTest)
    {
        if (factTest.compareType == FactValueType.String)
        {
            return ruleDB.GetStringFromStringID((int)_factValues[factTest.factID]);
        }

        return _factValues[factTest.factID].ToString();
    }

    public void Init()
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
        _allValidRuleIndices = new NativeArray<int>(_rules.Length,Allocator.Persistent);
       
        //if we have four rules that all match with 2 - and that is our best match - four is stored in _noOfRulesWithBestMatch
        _noOfRulesWithBestMatch = new NativeArray<int>(1,Allocator.Persistent);
        _noOfValidRules = new NativeArray<int>(1,Allocator.Persistent);
        
        //folowing that same example - _bestRuleMatches is 2
        _bestRuleMatches = new NativeArray<int>(1,Allocator.Persistent);
        //to read out these four rules from our example - expect to find their indices in _allRulesMatches[0 - 3]
        _allBestRulesIndices = new NativeArray<int>(_rules.Length,Allocator.Persistent);
        
        //counts amount of facts that matches for a given rule-index, encoded such that negative values
        //indicates that at least one rule did not match.
        _allMatchesForAllRules= new NativeArray<int>(_rules.Length,Allocator.Persistent);
        
        _settings = new NativeArray<Settings>(1,Allocator.Persistent);
        _settings[0] = new Settings(false,false);
        
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
            bytes += _allValidRuleIndices.Length * sizeof(int);
            bytes += _allBestRulesIndices.Length * sizeof(int);
            bytes += _allMatchesForAllRules.Length * sizeof(int);
            bytes += _noOfRulesWithBestMatch.Length * sizeof(int);
            bytes += _noOfValidRules.Length * sizeof(int);
            bytes += _noOfRulesWithBestMatch.Length * Settings.SizeInBytes();
            bytes += _slice.Length * sizeof(int);
            return bytes;
        }

        return -1;
    }

    public int GetNumberOfMatchesForRuleID(int ruleID, out bool ruleValid)
    {
        if (_inReload)
        {
            ruleValid = false;
            return 0;
        }
        PeekAllValidRules(true);
        ruleValid = _allMatchesForAllRules[ruleID] >= 0;
        return Mathf.Abs(_allMatchesForAllRules[ruleID]);
    }
    
         
    // - only picks one rule only does factWrite only on that rule
    public RuleDBEntry PickBestRule(bool fireListener=true)
    {
        var amountOfBestRules = PeekBestRules(false);
        if (amountOfBestRules > 0)
        {
            RuleDBEntry entry = GetRuleFromMatches(0);
            HandleFactWrites(entry);
            if (fireListener)
            {
                OnRulePicked?.Invoke(entry.RuleID);
            }
            return entry;
        }

        return null;
    }
    
    /*
     *Picks Best rules,
     * ie , all rules picked have share highest match score
     * It then runs FactWrite on all these rules
     * FactWrite on all rules 
     */
    public int PickBestRules(bool fireListener=true)
    {
        int rules = PeekBestRules(false);
        for (int i = 0; i < rules; i++)
        {
            RuleDBEntry rule = GetRuleFromMatches(i);
            HandleFactWrites(rule);
            if (fireListener)
            {
                OnRulePicked?.Invoke(rule.RuleID);
            }
        }
        return rules;
    }
    
    public int PickBestRulesInBucket(BucketSlice bucketSlice,bool fireListener=true)
    {
        int rules = PeekBestRulesInBucket(bucketSlice);
        for (int i = 0; i < rules; i++)
        {
            RuleDBEntry rule = GetRuleFromMatches(i);
            HandleFactWrites(rule);
            if (fireListener)
            {
                OnRulePicked?.Invoke(rule.RuleID);
            }
        }
        return rules;
    }
    
    public RuleDBEntry PickBestRuleInBucket(BucketSlice bucketSlice,bool fireListener=true)
    {
        RuleDBEntry rule = PeekBestRuleInBucket(bucketSlice);
        if (rule != null)
        {
            HandleFactWrites(rule);
            if (fireListener)
            {
                OnRulePicked?.Invoke(rule.RuleID);
            }
        }
        return rule;
    }
    
    public int PeekBestRulesInBucket(BucketSlice bucketSlice,bool fireListener=true)
    {
        if (_inReload)
        {
            return 0;
        }

        int rules = 0;
        if (!bucketSlice.IsNullBucket())
        {
            bucketSlice.ApplyBucket(this);
            rules = PeekBestRules(false,bucketSlice.startIndex, bucketSlice.endIndex);
            if (fireListener)
            {
                for (int i = 0; i < rules; i++)
                {
                    RuleDBEntry entry = GetRuleFromMatches(i);
                    OnRulePeeked?.Invoke(entry.RuleID);
                }
            }
        }
        return rules;
    }
    public RuleDBEntry PeekBestRuleInBucket(BucketSlice bucketSlice,bool fireListener=true)
    {
        RuleDBEntry peekedRule = null;
        if (_inReload)
        {
            return peekedRule;
        }
        if (!bucketSlice.IsNullBucket())
        {
            bucketSlice.ApplyBucket(this);
            int rules = PeekBestRules(false,bucketSlice.startIndex, bucketSlice.endIndex);
            if (rules> 0)
            {
                peekedRule = GetRuleFromMatches(0);
                if (fireListener && peekedRule!=null)
                {
                   OnRulePeeked?.Invoke(peekedRule.RuleID); 
                }
            }
        }
        return peekedRule;
    }

    //peeks best rule - but does not do any factWrites
    public RuleDBEntry PeekBestRule(bool fireListeners=true)
    {
        var amountOfBestRules = PeekBestRules(false);
        if (amountOfBestRules > 0)
        {
            RuleDBEntry entry = GetRuleFromMatches(0);
            if (entry != null && fireListeners)
            {
                OnRulePeeked?.Invoke(entry.RuleID);
            }
            return entry;
        }
        return null;
    }
        
    // Pick the best matching rule
    public int PeekBestRules(bool fireListeners=true,int startIndex=0,int endIndex=-1)
    {
        int rules = PeekRules(false, false, startIndex, endIndex);
        for(int i=0; i < rules; i++)
        {
            RuleDBEntry entry = GetRuleFromMatches(i);
            if (entry != null && fireListeners)
            {
                OnRulePeeked?.Invoke(entry.RuleID);
            }
        }
        return rules;
    }
    
    public int PeekAllValidRules(bool countAllMatches=false,int startIndex=0,int endIndex=-1)
    {
        //GetRuleFromMatches(0);
        return PeekRules(true, countAllMatches, startIndex, endIndex);
    }
    
    public int PeekRules(bool checkAllRules,bool countAllMatches,int startIndex=0,int endIndex=-1) 
    {
        if (_inReload)
        {
            return 0;
        }

        if (startIndex < 0)
        {
            return 0;
        }
        _slice[0] = startIndex;
        _slice[1] = endIndex == -1 ? _rules.Length : (endIndex+1);
        _settings[0] = new Settings(checkAllRules,countAllMatches);

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
                AllValidRulesIndices = _allValidRuleIndices,
                AllBestRulesIndices =  _allBestRulesIndices,
                AllMatchesForAllRules = _allMatchesForAllRules,
                NoOfRulesWithBestMatch = _noOfRulesWithBestMatch,
                NoOfValidRules = _noOfValidRules,
                slice = _slice,
                Settings = _settings
            };
        }
        _cachedJob.Execute();
        return _noOfRulesWithBestMatch[0];
    }

    public int PeekValidRulesInBucket(BucketSlice bucketSlice,bool fireListeners = true)
    {
        if (!bucketSlice.IsNullBucket())
        {
            bucketSlice.ApplyBucket(this);
            int rules = PeekValidRules(fireListeners,bucketSlice.startIndex, bucketSlice.endIndex);
            return rules;
        }
        return 0;
    }
    public int PickValidRulesInBucket(BucketSlice bucketSlice,bool fireListeners = true )
    {
        int validRules = PeekValidRulesInBucket(bucketSlice, false);
        for (int i = 0; i < validRules; i++)
        {
            RuleDBEntry rule = GetRuleFromValidMatches(i);
            HandleFactWrites(rule);
            if (fireListeners)
            {
                OnValidRulePicked?.Invoke(rule.RuleID);
            }
        }
        return validRules;
    }
    
    // Pick all valid rules i.e rules that has no failed tests.
    public int PeekValidRules(bool fireListeners=true,int startIndex=0,int endIndex=-1)
    {
        PeekRules(true, false, startIndex, endIndex);
        if (fireListeners)
        {
            for (int i = 0; i < _noOfValidRules[0]; i++)
            {
                RuleDBEntry rule = GetRuleFromValidMatches(i);
                OnValidRulePeeked?.Invoke(rule.RuleID);
            }
        }
        return _noOfValidRules[0];
    }
    
    public int PickValidRules(bool fireListeners=true,int startIndex=0,int endIndex=-1)
    {
        PeekRules(true, false, startIndex, endIndex);
        for (int i = 0; i < _noOfValidRules[0]; i++)
        {
            RuleDBEntry rule = GetRuleFromValidMatches(i);
            HandleFactWrites(rule);
            if (fireListeners)
            {
                OnValidRulePicked?.Invoke(rule.RuleID);
            }
        }
        return _noOfValidRules[0];
    }

    public void HandleFactWrites(RuleDBEntry rule)
    {
        //Handle fact writes.
        foreach (var factWrite in rule.factWrites)
        {
                    
            switch (factWrite.writeMode)
            {
                case RuleDBFactWrite.WriteMode.IncrementValue:
                    LogWritebacks(
                        $"increment fact (id_{factWrite.factID}){factWrite.factName} by {factWrite.writeValue} PRIOR = {_factValues[factWrite.factID]}",rule);
                    _factValues[factWrite.factID] += factWrite.writeValue;
                    LogWritebacks(
                        $"POST increment = {_factValues[factWrite.factID]}",rule);
                    break;
                case RuleDBFactWrite.WriteMode.SubtractValue:
                    LogWritebacks(
                        $"subtract fact (id_{factWrite.factID}){factWrite.factName} by {factWrite.writeValue} PRIOR = {_factValues[factWrite.factID]}",rule);
                    _factValues[factWrite.factID] -= factWrite.writeValue;
                    LogWritebacks(
                        $"POST subtract= {_factValues[factWrite.factID]}",rule);
                    break;
                case RuleDBFactWrite.WriteMode.SetValue:
                    LogWritebacks(
                        $"set fact (id_{factWrite.factID}){factWrite.factName} to {factWrite.writeValue} PRIOR = {_factValues[factWrite.factID]}",rule);
                    _factValues[factWrite.factID] = factWrite.writeValue;
                    LogWritebacks(
                        $"POST set = {_factValues[factWrite.factID]}",rule);
                    break;
                case RuleDBFactWrite.WriteMode.SetString:
                    LogWritebacks(
                        $"set fact (id_{factWrite.factID}){factWrite.factName} to string (id_{ruleDB.StringId(factWrite.writeString)}){factWrite.writeString} PRIOR = {_factValues[factWrite.factID]}",rule);
                    _factValues[factWrite.factID] = ruleDB.StringId(factWrite.writeString);
                    LogWritebacks(
                        $"POST set = {_factValues[factWrite.factID]}, {ruleDB.GetStringFromStringID((int)_factValues[factWrite.factID])}",rule);
                    break;
                case RuleDBFactWrite.WriteMode.SetToOtherFactValue:
                    LogWritebacks(
                        $"set fact (id_{factWrite.factID}){factWrite.factName} to value of (id_{(int)factWrite.writeValue}){factWrite.writeString},{_factValues[(int)factWrite.writeValue]} PRIOR = {_factValues[factWrite.factID]}",rule);
                    _factValues[factWrite.factID] = _factValues[(int)factWrite.writeValue];
                    LogWritebacks(
                        $"POST set = {_factValues[factWrite.factID]}",rule);
                    break;
                case RuleDBFactWrite.WriteMode.IncrementByOtherFactValue:
                    LogWritebacks(
                        $"increment fact (id_{factWrite.factID}){factWrite.factName} by value of (id_{(int)factWrite.writeValue}){factWrite.writeString},{_factValues[(int)factWrite.writeValue]} PRIOR = {_factValues[factWrite.factID]}",rule);
                    _factValues[factWrite.factID] += _factValues[(int)factWrite.writeValue];
                    LogWritebacks(
                        $"POST = increment {_factValues[factWrite.factID]}",rule);
                    break;
                case RuleDBFactWrite.WriteMode.SubtractByOtherFactValue:
                    LogWritebacks(
                        $"subtract fact (id_{factWrite.factID}){factWrite.factName} by value of (id_{(int)factWrite.writeValue}){factWrite.writeString},{_factValues[(int)factWrite.writeValue]} PRIOR = {_factValues[factWrite.factID]}",rule);
                    _factValues[factWrite.factID] -= _factValues[(int)factWrite.writeValue];
                    LogWritebacks(
                        $"POST subtract = {_factValues[factWrite.factID]}",rule);
                    break;
            }
        }
    }
    
    [Conditional("FACTMATCHER_LOG_WRITEBACKS")]
    static void LogWritebacks(object msg,RuleDBEntry entry)
    {
        Debug.Log($"for rule (id_{entry.RuleID}){entry.ruleName}, {msg}");
    }
    
    public void Reload()
    {
        _inReload = true;
        hasCachedJob = false;
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
            var factName = ruleDB.GetFactVariableNameFromFactID(i);
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
    
    public int GetNumberOfMatchesInBestMatch()
    {
        return _bestRuleMatches[0];
    }
    
    public int GetNumberOfMatchesFromMatches(int ruleIndex)
    {
        if (_settings[0].CountAllFactMatches && _settings[0].CheckAllRules)
        {
            return _allMatchesForAllRules[ruleIndex];
        }
        return 0;
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
            return ruleDB.RuleFromID(_rules[_allBestRulesIndices[matchIndex]].ruleFiredEventId);
        }
        return null;
    }
    
    public RuleDBEntry GetRuleFromValidMatches(int matchIndex)
    {
        if (_inReload)
        {
            Debug.Log("In reload");
            return null;
        }
        return ruleDB.RuleFromID(_rules[_allValidRuleIndices[matchIndex]].ruleFiredEventId);
    }

    public int StringID(string str)
    {
        return ruleDB.StringId(str);
    }
    
    public int FactID(string str)
    {
        return ruleDB.FactId(str);
    }

    public int RuleID(string str)
    {
        return ruleDB.RuleID(str);
    }

    public BucketSlice BucketSlice(string str,bool logNullBucket=true)
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
            if (logNullBucket)
            {
                Debug.LogError($"Could not find FactMatcher Bucket {str} - choosing default bucket [{bucketSlice.startIndex}:{bucketSlice.endIndex}] instead");
            }
            bucketSlice.Init(this);
            return bucketSlice;
        }

        if (logNullBucket)
        {
            Debug.LogError($"Could not find FactMatcher Bucket {str} - choosing null-bucket instead");
        }
        return global::BucketSlice.CreateNullBucket();
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
        _allValidRuleIndices.Dispose();
        _allBestRulesIndices.Dispose();
        _allMatchesForAllRules.Dispose();
        _noOfRulesWithBestMatch.Dispose();
        _settings.Dispose();
        _slice.Dispose();
        
        _dataDisposed = true;
        _hasBeenInited = false;
    }

}