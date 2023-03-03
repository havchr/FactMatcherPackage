#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Claims;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using Assert = UnityEngine.Assertions.Assert;
using Debug = UnityEngine.Debug;
using Random = System.Random;
using FactMatching;

public class FactMatcherTest  
{
    public enum FactSymbols
    {
        HealthLevel,
        IsBeingShotAt,
        PinModeCounter,
        RecordingModeCounter,
        HintNotShownOnSlamCounter,
        Concept
    }
    
    public enum ConceptSymbol 
    {
        OnHit,
        OnSlam
    }

    [Test]
    public void TestPerformance10kWorldFacts100RulesWithNativeArrays()
    {
        int worldFacts = 10000;
        int numORules = 100;
        PerfTestNativeFactChecker(worldFacts, numORules);
    }
    
    [Test]
    public void TestPerformance10kWorldFacts1000RulesWithNativeArrays()
    {
        int worldFacts = 10000;
        int numORules = 1000;
        PerfTestNativeFactChecker(worldFacts, numORules);
    }
    
    [Test]
    public void TestPerformance10kWorldFacts10000RulesWithNativeArrays()
    {
        int worldFacts = 10000;
        int numORules = 10000;
        PerfTestNativeFactChecker(worldFacts, numORules);
    }

    
    private void PerfTestNativeFactChecker(int worldFacts, int numORules)
    {
        var sw = new Stopwatch();
        sw.Start();

        sw.Stop();
        Debug.Log($"took {sw.ElapsedMilliseconds} ms to add {worldFacts} Facts");


        sw = new Stopwatch();
        sw.Start();
        
        Functions.CreateMockRules(worldFacts,numORules,out var rules,out var atoms,out var facts);
        var nativeRules = new NativeArray<FactMatching.Rule>(rules.Count, Allocator.Persistent);
        var nativeAtoms= new NativeArray<FactMatching.FactTest>(atoms.Count, Allocator.Persistent);
        var nativeFacts = new NativeArray<float>(facts.Count, Allocator.Persistent);
        for (int i=0; i < nativeRules.Length; i++)
        {
            nativeRules[i] = rules[i];
        }
        for (int i=0; i < nativeAtoms.Length; i++)
        {
            nativeAtoms[i] = atoms[i];
        }
        for (int i=0; i < nativeFacts.Length; i++)
        {
            nativeFacts[i] = facts[i];
        }
        
        sw.Stop();
        Debug.Log($"took {sw.ElapsedMilliseconds} ms to add {numORules} rules");
        FactMatching.Rule bestRule = new FactMatching.Rule(-1,0,0);


        int howManyRulesToPick = 50;
        sw = new Stopwatch();
        sw.Start();
        int matches = -1;
        for (int i = 0; i < howManyRulesToPick; i++)
        {
            matches = PickBestRule(nativeRules,nativeAtoms,nativeFacts, ref bestRule);
        }

        sw.Stop();
        nativeAtoms.Dispose();
        nativeFacts.Dispose();
        nativeRules.Dispose();
        Debug.Log(
            $"Picking {howManyRulesToPick} bestMatch rules in {worldFacts} facts with {numORules} rules , took {sw.ElapsedMilliseconds} milliseconds");
        Debug.Log($"Our best rule is {bestRule.ruleFiredEventId} with {matches} matches");
    }


    int PickBestRule(NativeArray<FactMatching.Rule> rules,
        NativeArray<FactMatching.FactTest> atoms,
        NativeArray<float> facts,
        ref FactMatching.Rule bestMatch)
    {
       
        int ruleI = 0;
        int currentBestMatch = 0;
        int bestRuleIndex = -1;
        for (ruleI = 0; ruleI < rules.Length; ruleI++)
        {
            var rule = rules[ruleI];
            int howManyAtomsMatch = 0;
            //if (rule.atoms.Length > currentBestMatch)
            if (true)
            {
                    int orGroupHits = 0;
                    int orGroupMisses= 0;
                    int lastOrGroup = -1;
                    int lastIndex = rule.factTestIndex + rule.numOfFactTests;
                    
                    for (int j = rule.factTestIndex; j < (lastIndex); j++)
                    {
                        var atom = atoms[j];
                        if(lastOrGroup != atom.orGroupRuleID)
                        {
                            if (lastOrGroup != -1 && orGroupHits==0)
                            {
                                howManyAtomsMatch = 0;
                                break;
                            }
                            orGroupHits = 0;
                        }

                        if (FactMatching.Functions.Predicate(in atom.compare, facts[atom.factID]))
                        {
                            if (atom.orGroupRuleID != -1)
                            {
                                orGroupHits++;
                                if (orGroupHits == 1)
                                {
                                    howManyAtomsMatch++;
                                }
                            }
                            else
                            {
                                orGroupHits = 0;
                                howManyAtomsMatch++;
                            }
                        }
                        else if (atom.strict)
                        {
                            //missing - in a group.
                            if (atom.orGroupRuleID != -1)
                            {
                                orGroupMisses++;
                                if (j == lastIndex - 1 && orGroupHits==0)
                                {
                                    howManyAtomsMatch = 0;
                                    orGroupHits = 0;
                                    break;
                                }
                            }
                            //missing - not in a group.
                            else
                            {
                                //missing not in a group but last was group
                                if (lastOrGroup != -1 && orGroupHits==0)
                                {
                                    howManyAtomsMatch = 0;
                                }
                                howManyAtomsMatch = 0;
                                orGroupHits = 0;
                                break;
                            }
                        }

                        lastOrGroup = atom.orGroupRuleID;

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
            bestMatch = rules[bestRuleIndex];
        }

        return currentBestMatch;
    }

   
    [Test]
    public void TestEqualsHighNumber()
    {
        float x = 123456f;
        float y = 123456f;
        float epsiFact = 0.0001f;
        Assert.IsTrue(Functions.Predicate(FactMatching.FactCompare.EqualsEpsi(x,epsiFact),y));
        y = 1234567.9f;
        Assert.IsFalse(Functions.Predicate(FactMatching.FactCompare.Equals(x),y));

    }
    
    [Test]
    public void TestNotEquals()
    {
        var compare = FactMatching.FactCompare.NotEquals(1.0f);
        Assert.IsFalse(Functions.Predicate(in compare,1.0f));
        Assert.IsTrue(Functions.Predicate(in compare,2.0f));
    }
    [Test]
    public void TestEquals()
    {
        //Equals
        float x = 1.0f;
        Assert.IsTrue(Functions.Predicate(FactMatching.FactCompare.Equals(1.0f),x));
        x = 10.0f;
        Assert.IsFalse(Functions.Predicate(FactMatching.FactCompare.Equals(1.0f),x));
        Assert.IsFalse(Functions.Predicate(FactMatching.FactCompare.Equals(10.01f),x));

    }
    [Test]
    public void TestMore()
    {
        //MORE THAN
        var x = 1.0f;
        Assert.IsFalse(Functions.Predicate(FactMatching.FactCompare.MoreThan(1.0f),x));
        x = 2.0f;
        Assert.IsTrue(Functions.Predicate(FactMatching.FactCompare.MoreThan(1.0f),x));
        x = 0.0f;
        Assert.IsFalse(Functions.Predicate(FactMatching.FactCompare.MoreThan(1.0f),x));

    }
    
    [Test]
    public void TestMoreEquals()
    {
        //MORE THAN
        var x = 1.0f;
        Assert.IsTrue(Functions.Predicate(FactMatching.FactCompare.MoreThanEquals(1.0f),x));
        x = 2.0f;
        Assert.IsTrue(Functions.Predicate(FactMatching.FactCompare.MoreThanEquals(1.0f),x));
        x = 0.0f;
        Assert.IsFalse(Functions.Predicate(FactMatching.FactCompare.MoreThanEquals(1.0f),x));

    }
    [Test]
    public void TestLess()
    {
        //Less THAN
        var x = 1.0f;
        Assert.IsFalse(Functions.Predicate(FactMatching.FactCompare.LessThan(1.0f),x));
        x = 1.0f;
        Assert.IsTrue(Functions.Predicate(FactMatching.FactCompare.LessThan(2.0f),x));

    }
    
    [Test]
    public void TestLessEquals()
    {
        var x = 1.0f;
        Assert.IsTrue(Functions.Predicate(FactMatching.FactCompare.LessThanEquals(1.0f),x));
        x = 1.0f;
        Assert.IsTrue(Functions.Predicate(FactMatching.FactCompare.LessThanEquals(2.0f),x));
        x = 3.0f;
        Assert.IsFalse(Functions.Predicate(FactMatching.FactCompare.LessThanEquals(2.0f),x));

    }

    //a is the lower bound, b is the higher bound, x is the value compared
    public bool predicate(float a, float b, float x,float eps=0,bool negation=false)
    {
        var xEps = x + eps;
        var aPart = a < (x + eps); 
        var bPart = x < (b + eps); 
        Debug.Log($"xEps is {xEps}");
        Debug.Log($"a is {a}");
        Debug.Log($"A part is {aPart}");
        Debug.Log($"B part is {bPart}");
        return  negation ? !(aPart && bPart) :(aPart && bPart);
    }
    
    
}
#endif