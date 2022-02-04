using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using FactMatcher;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class FactMatcherJobSystem : MonoBehaviour
{

    public RulesDB ruleDB;
    private NativeArray<float> _factValues;
    private NativeArray<FMRule> _rules;
    private NativeArray<RuleAtom> _ruleAtoms;
    private NativeArray<FactMatcher.FMRule> _bestRule;
    private NativeArray<int> _bestRuleMatches;
    
    public void Init()
    {
        ruleDB.InitRuleDB();
        _factValues = new NativeArray<float>(ruleDB.CountNumberOfFacts(),Allocator.Persistent);
        FactMatcher.Functions.CreateNativeRules(this.ruleDB, out _rules, out _ruleAtoms);
        _bestRule = new NativeArray<FMRule>(1,Allocator.Persistent);
        _bestRuleMatches = new NativeArray<int>(1,Allocator.Persistent);
    }

    public float this[int i]
    {
        get { return _factValues[i]; }
        set { _factValues[i] = value; }
    }

    public int StringID(string str)
    {
        return ruleDB.StringId(str);
    }

    public RuleDBEntry PickBestRule()
    {
        
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
        Debug.Log($"The result of the best match is: {rule.payload} with {_bestRuleMatches[0]} matches and it took {sw.ElapsedMilliseconds} ms");
        HandleFactWrites(rule);
        return rule;
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
            }
        }
    }

    private void OnDestroy()
    {
        _factValues.Dispose();
        _rules.Dispose();
        _ruleAtoms.Dispose();
        _bestRule.Dispose();
        _bestRuleMatches.Dispose();
    }

#if FACTMATCHER_BURST
    [BurstCompile(CompileSynchronously = true)]
#endif
    private struct FactMatcherMatch : IJob
    {

        [ReadOnly] public NativeArray<float> FactValues;

        [ReadOnly] public NativeArray<FactMatcher.FMRule> Rules;

        [ReadOnly] public NativeArray<FactMatcher.RuleAtom> RuleAtoms;

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