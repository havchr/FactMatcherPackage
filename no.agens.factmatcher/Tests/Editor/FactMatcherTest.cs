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
using FactMatcher;

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
        var nativeRules = new NativeArray<FactMatcher.FMRule>(rules.Count, Allocator.Persistent);
        var nativeAtoms= new NativeArray<FactMatcher.RuleAtom>(atoms.Count, Allocator.Persistent);
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
        FactMatcher.FMRule bestFmRule = new FactMatcher.FMRule(-1,0,0);


        int howManyRulesToPick = 50;
        sw = new Stopwatch();
        sw.Start();
        int matches = -1;
        for (int i = 0; i < howManyRulesToPick; i++)
        {
            matches = PickBestRule(nativeRules,nativeAtoms,nativeFacts, ref bestFmRule);
        }

        sw.Stop();
        nativeAtoms.Dispose();
        nativeFacts.Dispose();
        nativeRules.Dispose();
        Debug.Log(
            $"Picking {howManyRulesToPick} bestMatch rules in {worldFacts} facts with {numORules} rules , took {sw.ElapsedMilliseconds} milliseconds");
        Debug.Log($"Our best rule is {bestFmRule.ruleFiredEventId} with {matches} matches");
    }


    int PickBestRule(NativeArray<FactMatcher.FMRule> rules,
        NativeArray<FactMatcher.RuleAtom> atoms,
        NativeArray<float> facts,
        ref FactMatcher.FMRule bestMatch)
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
                for (int j = rule.atomIndex; j < (rule.atomIndex + rule.numOfAtoms); j++)
                {
                    var atom = atoms[j];
                    if (FactMatcher.Functions.predicate(in atom.compare,facts[atom.factID] ))
                    {
                        howManyAtomsMatch++;
                    }
                    int localIndex = j - rule.atomIndex;
                    //Debug.Log($"Checking Native rule {ruleI} with atom local index {localIndex} and atomIndex is {j} , factID {atom.factID} with factValue {facts[atom.factID]} and found {howManyAtomsMatch}");
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
    public void TestEqualsString()
    {
        //Equals
        var text = "nick";
        var x = text.GetHashCode();
        Assert.IsTrue(Functions.predicate(FactMatcher.RuleCompare.Equals(text.GetHashCode()),x));
        x = "nack".GetHashCode();
        Assert.IsFalse(Functions.predicate(FactMatcher.RuleCompare.Equals(text.GetHashCode()),x));

    }
    
    [Test]
    public void TestNotEquals()
    {
        var compare = FactMatcher.RuleCompare.NotEquals(1.0f);
        Assert.IsFalse(Functions.predicate(in compare,1.0f));
        Assert.IsTrue(Functions.predicate(in compare,2.0f));
    }
    [Test]
    public void TestEquals()
    {
        //Equals
        float x = 1.0f;
        Assert.IsTrue(Functions.predicate(FactMatcher.RuleCompare.Equals(1.0f),x));
        x = 10.0f;
        Assert.IsFalse(Functions.predicate(FactMatcher.RuleCompare.Equals(1.0f),x));
        Assert.IsFalse(Functions.predicate(FactMatcher.RuleCompare.Equals(10.001f),x));

    }
    [Test]
    public void TestMore()
    {
        //MORE THAN
        var x = 1.0f;
        Assert.IsFalse(Functions.predicate(FactMatcher.RuleCompare.MoreThan(1.0f),x));
        x = 2.0f;
        Assert.IsTrue(Functions.predicate(FactMatcher.RuleCompare.MoreThan(1.0f),x));
        x = 0.0f;
        Assert.IsFalse(Functions.predicate(FactMatcher.RuleCompare.MoreThan(1.0f),x));

    }
    
    [Test]
    public void TestMoreEquals()
    {
        //MORE THAN
        var x = 1.0f;
        Assert.IsTrue(Functions.predicate(FactMatcher.RuleCompare.MoreThanEquals(1.0f),x));
        x = 2.0f;
        Assert.IsTrue(Functions.predicate(FactMatcher.RuleCompare.MoreThanEquals(1.0f),x));
        x = 0.0f;
        Assert.IsFalse(Functions.predicate(FactMatcher.RuleCompare.MoreThanEquals(1.0f),x));

    }
    [Test]
    public void TestLess()
    {
        //Less THAN
        var x = 1.0f;
        Assert.IsFalse(Functions.predicate(FactMatcher.RuleCompare.LessThan(1.0f),x));
        x = 1.0f;
        Assert.IsTrue(Functions.predicate(FactMatcher.RuleCompare.LessThan(2.0f),x));

    }
    
    [Test]
    public void TestLessEquals()
    {
        var x = 1.0f;
        Assert.IsTrue(Functions.predicate(FactMatcher.RuleCompare.LessThanEquals(1.0f),x));
        x = 1.0f;
        Assert.IsTrue(Functions.predicate(FactMatcher.RuleCompare.LessThanEquals(2.0f),x));
        x = 3.0f;
        Assert.IsFalse(Functions.predicate(FactMatcher.RuleCompare.LessThanEquals(2.0f),x));

    }

    //a is the lower bound, b is the higher bound, x is the value compared
    public bool predicate(float a, float b, float x,float eps=0,bool negation=false)
    {
        var xEps = x + eps;
        var aPart = a< (x + eps); 
        var bPart = x < (b + eps); 
        Debug.Log($"xEps is {xEps}");
        Debug.Log($"a is {a}");
        Debug.Log($"A part is {aPart}");
        Debug.Log($"B part is {bPart}");
        return  negation ? !(aPart && bPart) :(aPart && bPart);
    }
    
    
}
#endif