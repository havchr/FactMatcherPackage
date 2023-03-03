using System.Diagnostics;
using FactMatching;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Profiling;
using Debug = UnityEngine.Debug;

#if FACTMATCHER_BURST
using Unity.Burst;
#endif

public struct FactMatcherMatch : IJob
{

    [Unity.Collections.ReadOnly] public NativeArray<float> FactValues;
    [Unity.Collections.ReadOnly] public NativeArray<FactMatching.Rule> Rules;
    [Unity.Collections.ReadOnly] public NativeArray<FactMatching.FactTest> FactTests;
    [Unity.Collections.ReadOnly] public NativeArray<FactMatching.Settings> Settings;
    [Unity.Collections.ReadOnly] public NativeArray<int> slice;

    [WriteOnly] public NativeArray<int> BestRule;
    [WriteOnly] public NativeArray<int> BestRuleMatches;
    [WriteOnly] public NativeArray<int> AllEmRulesIndices;
    [WriteOnly] public NativeArray<int> AllEmRulesMatches;
    [WriteOnly] public NativeArray<int> AllMatchesForAllRules;
    [WriteOnly] public NativeArray<int> NoOfRulesWithBestMatch;

#if FACTMATCHER_BURST
    [BurstCompile(CompileSynchronously = true)]
#endif
    public void Execute()
    {

        int ruleI = 0;
        int currentBestMatch = 0;
        int bestRuleIndex = FactMatcher.NotSetValue;
        int allBestMatchesIndex = 0;
        int sliceStart = slice[0];
        int sliceEnd = slice[1];
        bool validRule = true;

        //Debug.Log($"Natively! We have {Rules.Length} rules to loop");
        for (ruleI = sliceStart; ruleI < sliceEnd; ruleI++)
        {
            var rule = Rules[ruleI];
            int howManyFactTestsMatch = 0;
            if (rule.numOfFactTests >= currentBestMatch || Settings[0].FactWriteToAllMatches)
            {
                LogMatchJob($"for rule {ruleI} with ruleFireID {rule.ruleFiredEventId} we are checking atoms from {rule.factTestIndex} to {rule.factTestIndex + rule.numOfFactTests} ");
                    
                //assuming sorted on factTest.factID, means that any orGroup can be checked sequentially.
                int orGroupHits = 0;
                int lastOrGroup = -1;
                int lastIndex = rule.factTestIndex + rule.numOfFactTests;
                howManyFactTestsMatch = (int)HowManyFactTestsMatch(rule, lastIndex, lastOrGroup, orGroupHits, howManyFactTestsMatch,Settings[0].CountAllFactMatches,ref validRule);
                AllEmRulesIndices[ruleI] = validRule ? ruleI : FactMatcher.NotSetValue;
                AllMatchesForAllRules[ruleI] = howManyFactTestsMatch;
                if (howManyFactTestsMatch == currentBestMatch && howManyFactTestsMatch >= 1 && validRule)
                {
                    allBestMatchesIndex++;
                    AllEmRulesMatches[allBestMatchesIndex] = ruleI;
                }
                else if (howManyFactTestsMatch > currentBestMatch && validRule)
                {
                    currentBestMatch = howManyFactTestsMatch;
                    bestRuleIndex = ruleI;
                    allBestMatchesIndex = 0;
                    AllEmRulesMatches[allBestMatchesIndex] = ruleI;
                }
            }
        }

        if (bestRuleIndex != FactMatcher.NotSetValue)
        {
            BestRuleMatches[0] = currentBestMatch;
            BestRule[0] = bestRuleIndex;
            NoOfRulesWithBestMatch[0] = allBestMatchesIndex+1;
        }
        else
        {
            BestRuleMatches[0] = FactMatcher.NotSetValue;
            BestRule[0] = FactMatcher.NotSetValue;
            NoOfRulesWithBestMatch[0] = FactMatcher.NotSetValue;
        }
    }

    /*
     * This function checks how many FactTests that matches in a rule.
     * if there is one, or more than one strict FactTest that fails,
     * it returns the amount of FactTests that matches, and you can
     * read out if the ruleFailed in the validRule boolean
     *
     *
     * Probably one might consider simplyfying the algorithm by making it not support
     * counting all factTests-matches , and rather duplicating the algorithm  with that twist instead.
     *
     * Example : Rule 1 , 4 tests, all passes , function returns 4 , validRule not modified
     *           Rule 2 , 5 tests, 3 passes, two fails , function returns 3 , validRule set to false
     *           Rule 3, 6 strict tests 1 unstrict. 6 strict tests passes, unstrict test fails, returns 6 
     *           Rule 4, 6 strict tests 1 unstrict. 6 strict tests passes, unstrict test passes, returns 7
     *           Rule 5, 7 strict tests, all fails, returns 0f validRule set to false
     */
    private float HowManyFactTestsMatch(Rule rule, int lastIndex, int lastOrGroup, int orGroupHits, float howManyFactTestsMatch,bool countAllMatches,ref bool validRule)
    {
            
        for (int j = rule.factTestIndex; j < (lastIndex); j++)
        {
            var factTest = FactTests[j];
            LogMatchJob(
                $"for rule {rule} with ruleFireID {rule.ruleFiredEventId} , comparing factID {factTest.factID} with value {FactValues[factTest.factID]} with atom.compare.lowerBound {factTest.compare.lowerBound} and upperBound {factTest.compare.upperBound} ");
            if (lastOrGroup != factTest.orGroupRuleID)
            {
                if (lastOrGroup != -1 && orGroupHits == 0)
                {
                    validRule = false;
                    break;
                }

                orGroupHits = 0;
            }

            //if (FactMatching.Functions.Predicate(in factTest.compare, FactValues[factTest.factID]))
            //normally a function call would be nice, but it seems to take a bit of performance
            float x = FactValues[factTest.factID];
            if ((x > factTest.compare.lowerBound && x < factTest.compare.upperBound) ^ factTest.compare.negation)
            {
                if (factTest.orGroupRuleID != -1)
                {
                    orGroupHits++;
                    if (orGroupHits == 1)
                    {
                        howManyFactTestsMatch += 1.0f;
                    }
                }
                else
                {
                    orGroupHits = 0;
                    howManyFactTestsMatch += 1.0f;
                }
            }
            else if (factTest.strict)
            {
                //missing - in a group.
                if (factTest.orGroupRuleID != -1)
                {
                    if (j == lastIndex - 1 && orGroupHits == 0)
                    {
                        validRule = false;
                        if (countAllMatches)
                        {
                            continue;
                        }
                        break; 
                        //ok, this means we will not count any other facts.
                        //if we drop this - then we are going to go much much slower!
                    }
                }
                //missing - not in a group.
                else
                {
                    validRule = false;
                    if (countAllMatches)
                    {
                        continue;
                    }
                    break;
                }
            }
            lastOrGroup = factTest.orGroupRuleID;
        }
        return howManyFactTestsMatch;
    }

    [Conditional("FACTMATCHER_LOG_MATCH_JOB")]
    static void LogMatchJob(object msg)
    {
        Debug.Log(msg);
    }
}