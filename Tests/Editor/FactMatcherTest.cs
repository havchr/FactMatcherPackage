﻿#if UNITY_EDITOR
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
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs.LowLevel.Unsafe;
using UnityEngine.Profiling;
using UnityEngine.Scripting;

public class FactMatcherTest  
{

   /*
    * Testing that
         * PickBestRule - only picks one rule only does one factWrite on that rule
         * PeekBestRule - same as PickBestRule - but without FactWrite
    */
   
   
    [Test]
    public void TestMemoryAllocationDeallocation()
    {
        RulesDB rulesDB = Resources.Load<RulesDB>("FactMatcherTestResources/Test_1000_rules_dataset");
        FactMatcher matcher = new FactMatcher(rulesDB);
        
        UnsafeUtility.ForgiveLeaks();
        matcher.Init();

        long memoryAfterAllocation = matcher.GetMemorySizeInBytesForDatabase(); 
        matcher.DisposeData();
        int leaks = UnsafeUtility.CheckForLeaks();
        long memoryAfterDispose = matcher.GetMemorySizeInBytesForDatabase(); 

        
        Debug.Log($"leaks found = {leaks}");
        
        Debug.Log($"memory after allocation {memoryAfterAllocation}");
        Debug.Log($"memory after Dispose {memoryAfterDispose}");
        
        Debug.Log("If this test fails , check NativeArray memory count, allocation and disposing");
        
        Assert.IsTrue(leaks == 0);
        Assert.IsTrue(memoryAfterAllocation > 0);
        Assert.IsTrue(memoryAfterDispose == 0);
        
    }
   
    [Test]
    public void TestPickBestRuleIn10_000_rules_dataset()
    {
        
        var sw = new Stopwatch();
        sw.Start();

        RulesDB rulesDB = Resources.Load<RulesDB>("FactMatcherTestResources/Test_10_000_rules_dataset");
        FactMatcher matcher = new FactMatcher(rulesDB);
        matcher.Init();
        
        Debug.Log($"took {sw.ElapsedMilliseconds} ms to load {matcher.ruleDB.rules.Count} rules");
        sw.Stop();


        sw.Start();
        int mutations = 1000;
        FactMatching.RuleScriptGenerator.MutateRuleScriptTestData(matcher, mutations);
        Debug.Log($"took {sw.ElapsedMilliseconds} ms to mutate {mutations} - {matcher.ruleDB.rules.Count} rules");
        sw.Stop();
        
        sw.Start();
        RuleDBEntry rule = matcher.PickBestRule();
        if (rule != null)
        {
            int matches = 0;
            matches = matcher.GetNumberOfMatchesInBestMatch();
            Debug.Log($"took {sw.ElapsedMilliseconds} ms to pick best rule {rule.ruleName} with {matches} matches among {matcher.ruleDB.rules.Count} rules");
        }
        else
        {
            Debug.Log($"took {sw.ElapsedMilliseconds} ms to pick best rules among {matcher.ruleDB.rules.Count} rules");
        }
        sw.Stop();
        matcher.DisposeData();
    }
    
    [Test]
    public void TestPickBestRuleIn_1000_rules_dataset()
    {
        
        var sw = new Stopwatch();
        sw.Start();

        RulesDB rulesDB = Resources.Load<RulesDB>("FactMatcherTestResources/Test_1000_rules_dataset");
        FactMatcher matcher = new FactMatcher(rulesDB);
        matcher.Init();
        
        Debug.Log($"took {sw.ElapsedMilliseconds} ms to load {matcher.ruleDB.rules.Count} rules");
        sw.Stop();


        sw.Start();
        int mutations = 1000;
        FactMatching.RuleScriptGenerator.MutateRuleScriptTestData(matcher, mutations);
        Debug.Log($"took {sw.ElapsedMilliseconds} ms to mutate {mutations} - {matcher.ruleDB.rules.Count} rules");
        sw.Stop();
        
        sw.Start();
        RuleDBEntry rule = matcher.PickBestRule();
        if (rule != null)
        {
            int matches = 0;
            matches = matcher.GetNumberOfMatchesInBestMatch();
            Debug.Log($"took {sw.ElapsedMilliseconds} ms to pick best rule {rule.ruleName} with {matches} matches among {matcher.ruleDB.rules.Count} rules");
        }
        else
        {
            Debug.Log($"took {sw.ElapsedMilliseconds} ms to pick best rules among {matcher.ruleDB.rules.Count} rules");
        }
        sw.Stop();
        matcher.DisposeData();
    }
    
    [Test]
    public void TestFactMatcherPeekAndPickBestRule()
    {
        RulesDB rulesDB = Resources.Load<RulesDB>("FactMatcherTestResources/TestData");
        FactMatcher matcher = new FactMatcher(rulesDB);
        matcher.Init();

        matcher[matcher.FactID("test1")] = FactMatching.Consts.True;
        matcher[matcher.FactID("test2")] = FactMatching.Consts.True;

        RuleDBEntry rule = matcher.PeekBestRule();
        Assert.IsNotNull(rule);
        Assert.IsTrue(matcher[matcher.FactID("testWrite1")] == FactMatching.Consts.False);
        Assert.IsTrue(matcher[matcher.FactID("testWrite2")] == FactMatching.Consts.False);
        
        rule = matcher.PickBestRule();
        Assert.IsNotNull(rule);
        Assert.IsTrue(matcher[matcher.FactID("testWrite1")] == FactMatching.Consts.True);
        Assert.IsTrue(matcher[matcher.FactID("testWrite2")] == FactMatching.Consts.False);

        matcher.DisposeData();
    }
    
    /*
     * Testing that PeekBestRules
     * returns all rules that have the best match score, and no FactWrite is run on these rules
     * Testing that PickBestRules
     * picks all rules that share the number of best match score, and runs FactWrite on the rules
     * i.e , if in all our rules, the best match score is 5,
     * and we have two rules that have 5 best matches - both those rules
     * are returned
     *
     * 
     */
    [Test]
    public void TestFactMatcherPeekAndPickBestRules()
    {
        RulesDB rulesDB = Resources.Load<RulesDB>("FactMatcherTestResources/TestData");
        FactMatcher matcher = new FactMatcher(rulesDB);
        matcher.Init();

        matcher[matcher.FactID("test1")] = FactMatching.Consts.True;
        matcher[matcher.FactID("test2")] = FactMatching.Consts.True;

        int rules = matcher.PeekBestRules();
        Assert.IsTrue(rules == 2);
        Assert.IsTrue(matcher[matcher.FactID("testWrite1")] == FactMatching.Consts.False);
        Assert.IsTrue(matcher[matcher.FactID("testWrite2")] == FactMatching.Consts.False );
        
        rules = matcher.PickBestRules();
        Assert.IsTrue(rules == 2);
        Assert.IsTrue(matcher[matcher.FactID("testWrite1")] == FactMatching.Consts.True);
        Assert.IsTrue(matcher[matcher.FactID("testWrite2")] == FactMatching.Consts.True);

        matcher.DisposeData();
    }
    
    [Test]
    public void TestFactMatcherPickBestRuleFactWriteToAllThatMatches()
    {
        RulesDB rulesDB = Resources.Load<RulesDB>("FactMatcherTestResources/TestData");
        FactMatcher matcher = new FactMatcher(rulesDB);
        matcher.Init();

        matcher[matcher.FactID("test1")] = FactMatching.Consts.True;
        matcher[matcher.FactID("test2")] = FactMatching.Consts.True;

        Assert.IsTrue(matcher[matcher.FactID("testWrite1")] != FactMatching.Consts.True);
        Assert.IsTrue(matcher[matcher.FactID("testWrite2")] != FactMatching.Consts.True);
        Assert.IsTrue(matcher[matcher.FactID("testWrite3")] != FactMatching.Consts.True);
        RuleDBEntry rule = matcher.PickBestRuleFactWriteToAllValidRules();
        Assert.IsTrue(matcher[matcher.FactID("testWrite1")] == FactMatching.Consts.True);
        Assert.IsTrue(matcher[matcher.FactID("testWrite2")] == FactMatching.Consts.True);
        Assert.IsTrue(matcher[matcher.FactID("testWrite3")] == FactMatching.Consts.True);
        Assert.IsFalse(rule.ruleName.Equals("rule_3")); //best rule cannot be rule_3 because rule 3 has only one match
        
        matcher.DisposeData();
    }
    
    [Test]
    public void TestFactMatcherPickBestRulesFactWriteToAllThatMatches()
    {
        RulesDB rulesDB = Resources.Load<RulesDB>("FactMatcherTestResources/TestData");
        FactMatcher matcher = new FactMatcher(rulesDB);
        matcher.Init();

        matcher[matcher.FactID("test1")] = FactMatching.Consts.True;
        matcher[matcher.FactID("test2")] = FactMatching.Consts.True;

        Assert.IsTrue(matcher[matcher.FactID("testWrite1")] != FactMatching.Consts.True);
        Assert.IsTrue(matcher[matcher.FactID("testWrite2")] != FactMatching.Consts.True);
        Assert.IsTrue(matcher[matcher.FactID("testWrite3")] != FactMatching.Consts.True);
        int bestRules = matcher.PickBestRulesFactWriteToAllValidRules();
        Assert.IsTrue(matcher[matcher.FactID("testWrite1")] == FactMatching.Consts.True);
        Assert.IsTrue(matcher[matcher.FactID("testWrite2")] == FactMatching.Consts.True);
        Assert.IsTrue(matcher[matcher.FactID("testWrite3")] == FactMatching.Consts.True);
        Assert.IsTrue(bestRules == 2);
        string ruleName1 = matcher.GetRuleFromMatches(0).ruleName;
        string ruleName2 = matcher.GetRuleFromMatches(1).ruleName;
        Assert.IsTrue(ruleName1.Equals("rule_1") || ruleName1.Equals("rule_2")); 
        Assert.IsTrue(ruleName2.Equals("rule_1") || ruleName2.Equals("rule_2")); 
        
        matcher.DisposeData();
    }
    
    [Test]
    public void TestFactMatcherPickBestRuleInBucketFactWriteToAllThatMatches()
    {
        RulesDB rulesDB = Resources.Load<RulesDB>("FactMatcherTestResources/TestData");
        FactMatcher matcher = new FactMatcher(rulesDB);
        matcher.Init();

        
        BucketSlice fooBucket = matcher.BucketSlice("bucket:foo");
        BucketSlice booBucket = matcher.BucketSlice("bucket:boo");
        
        matcher[matcher.FactID("test1")] = FactMatching.Consts.True;
        matcher[matcher.FactID("test2")] = FactMatching.Consts.True;

        Assert.IsTrue(matcher[matcher.FactID("foo_testWrite1")] != FactMatching.Consts.True);
        Assert.IsTrue(matcher[matcher.FactID("foo_testWrite2")] != FactMatching.Consts.True);
        Assert.IsTrue(matcher[matcher.FactID("foo_testWrite3")] != FactMatching.Consts.True);
        
        Assert.IsTrue(matcher[matcher.FactID("boo_testWrite1")] != FactMatching.Consts.True);
        Assert.IsTrue(matcher[matcher.FactID("boo_testWrite2")] != FactMatching.Consts.True);
        Assert.IsTrue(matcher[matcher.FactID("boo_testWrite3")] != FactMatching.Consts.True);
        
        RuleDBEntry ruleFoo = matcher.PickBestRuleInBucketFactWriteToAllValidRules(fooBucket);
        Assert.IsTrue(matcher[matcher.FactID("foo_testWrite1")] == FactMatching.Consts.True);
        Assert.IsTrue(matcher[matcher.FactID("foo_testWrite2")] == FactMatching.Consts.True);
        Assert.IsTrue(matcher[matcher.FactID("foo_testWrite3")] == FactMatching.Consts.True);
        
        Assert.IsTrue(matcher[matcher.FactID("boo_testWrite1")] != FactMatching.Consts.True);
        Assert.IsTrue(matcher[matcher.FactID("boo_testWrite2")] != FactMatching.Consts.True);
        Assert.IsTrue(matcher[matcher.FactID("boo_testWrite3")] != FactMatching.Consts.True);
        Assert.IsTrue(ruleFoo.ruleName.Equals("bucket_foo_rule_1") || ruleFoo.ruleName.Equals("bucket_foo_rule_2")); 
        
        RuleDBEntry ruleBoo = matcher.PickBestRuleInBucketFactWriteToAllValidRules(booBucket);
        
        Assert.IsTrue(matcher[matcher.FactID("boo_testWrite1")] == FactMatching.Consts.True);
        Assert.IsTrue(matcher[matcher.FactID("boo_testWrite2")] == FactMatching.Consts.True);
        Assert.IsTrue(matcher[matcher.FactID("boo_testWrite3")] == FactMatching.Consts.True);
        Assert.IsTrue(ruleBoo.ruleName.Equals("bucket_boo_rule_1") || ruleBoo.ruleName.Equals("bucket_boo_rule_2")); 
        
        
        matcher.DisposeData();
    }
    
    [Test]
    public void TestFactMatcherPickBestRulesInBucketFactWriteToAllThatMatches()
    {
        
        
        RulesDB rulesDB = Resources.Load<RulesDB>("FactMatcherTestResources/TestData");
        FactMatcher matcher = new FactMatcher(rulesDB);
        matcher.Init();

        
        BucketSlice fooBucket = matcher.BucketSlice("bucket:foo");
        BucketSlice booBucket = matcher.BucketSlice("bucket:boo");
        
        matcher[matcher.FactID("test1")] = FactMatching.Consts.True;
        matcher[matcher.FactID("test2")] = FactMatching.Consts.True;

        Assert.IsTrue(matcher[matcher.FactID("foo_testWrite1")] != FactMatching.Consts.True);
        Assert.IsTrue(matcher[matcher.FactID("foo_testWrite2")] != FactMatching.Consts.True);
        Assert.IsTrue(matcher[matcher.FactID("foo_testWrite3")] != FactMatching.Consts.True);
        
        Assert.IsTrue(matcher[matcher.FactID("boo_testWrite1")] != FactMatching.Consts.True);
        Assert.IsTrue(matcher[matcher.FactID("boo_testWrite2")] != FactMatching.Consts.True);
        Assert.IsTrue(matcher[matcher.FactID("boo_testWrite3")] != FactMatching.Consts.True);
        
        int ruleFoos = matcher.PickBestRulesInBucketFactWriteToAllValidRules(fooBucket);
        Assert.IsTrue(matcher[matcher.FactID("foo_testWrite1")] == FactMatching.Consts.True);
        Assert.IsTrue(matcher[matcher.FactID("foo_testWrite2")] == FactMatching.Consts.True);
        Assert.IsTrue(matcher[matcher.FactID("foo_testWrite3")] == FactMatching.Consts.True);
        
        Assert.IsTrue(matcher[matcher.FactID("boo_testWrite1")] != FactMatching.Consts.True);
        Assert.IsTrue(matcher[matcher.FactID("boo_testWrite2")] != FactMatching.Consts.True);
        Assert.IsTrue(matcher[matcher.FactID("boo_testWrite3")] != FactMatching.Consts.True);
        
        
        Assert.IsTrue(ruleFoos == 2); 
        
        int rulesBoo = matcher.PickBestRulesInBucketFactWriteToAllValidRules(booBucket);
        Assert.IsTrue(rulesBoo == 2); 
        
        matcher.DisposeData();
    }
    
    [Test]
    public void TestFactMatcherCountAllMatches()
    {
        RulesDB rulesDB = Resources.Load<RulesDB>("FactMatcherTestResources/TestData");
        FactMatcher matcher = new FactMatcher(rulesDB);
        matcher.Init();

        matcher[matcher.FactID("test1")] = FactMatching.Consts.True;
        matcher[matcher.FactID("test2")] = FactMatching.Consts.True;

        int matches = matcher.GetNumberOfMatchesForRuleID(matcher.RuleID("rule_1"), out bool validRule);
        Assert.IsTrue(matches == 2 && validRule);
        matches = matcher.GetNumberOfMatchesForRuleID(matcher.RuleID("rule_2"), out validRule);
        Assert.IsTrue(matches == 2 && validRule);
        matches = matcher.GetNumberOfMatchesForRuleID(matcher.RuleID("rule_3"), out validRule);
        Assert.IsTrue(matches == 1 && validRule);
        
        matcher[matcher.FactID("test1")] = FactMatching.Consts.True;
        matcher[matcher.FactID("test2")] = FactMatching.Consts.False;
        
        matches = matcher.GetNumberOfMatchesForRuleID(matcher.RuleID("rule_1"), out validRule);
        Assert.IsTrue(matches == 1 && !validRule);
        matches = matcher.GetNumberOfMatchesForRuleID(matcher.RuleID("rule_2"), out validRule);
        Assert.IsTrue(matches == 1 && !validRule);
        matches = matcher.GetNumberOfMatchesForRuleID(matcher.RuleID("rule_3"), out validRule);
        Assert.IsTrue(matches == 1 && validRule);
        
        matcher.DisposeData();
    }
    
    [Test]
    public void TestFactMatcherPeekAndPickAllValidRules()
    {
        RulesDB rulesDB = Resources.Load<RulesDB>("FactMatcherTestResources/TestData");
        FactMatcher matcher = new FactMatcher(rulesDB);
        matcher.Init();

        matcher[matcher.FactID("test1")] = FactMatching.Consts.True;
        matcher[matcher.FactID("test2")] = FactMatching.Consts.True;

        int rules = matcher.PeekValidRules();
        Assert.IsTrue(rules == 3);
        RuleDBEntry rule1 = matcher.GetRuleFromValidMatches(0);
        Assert.IsNotNull(rule1);
        RuleDBEntry rule2 = matcher.GetRuleFromValidMatches(1);
        Assert.IsNotNull(rule2);
        RuleDBEntry rule3 = matcher.GetRuleFromValidMatches(2);
        Assert.IsNotNull(rule3);
        Assert.IsTrue(rule1.RuleID != rule2.RuleID && rule2.RuleID!= rule3.RuleID && rule1.RuleID!= rule3.RuleID);
        
        Assert.IsTrue((int)matcher[matcher.FactID("testWrite1")] != FactMatching.Consts.True);
        Assert.IsTrue((int)matcher[matcher.FactID("testWrite2")] != FactMatching.Consts.True);
        Assert.IsTrue((int)matcher[matcher.FactID("testWrite3")] != FactMatching.Consts.True);

        int rulesPicked = matcher.PickValidRules();
        Assert.IsTrue(rules == rulesPicked);
        Assert.IsTrue((int)matcher[matcher.FactID("testWrite1")] == FactMatching.Consts.True);
        Assert.IsTrue((int)matcher[matcher.FactID("testWrite2")] == FactMatching.Consts.True);
        Assert.IsTrue((int)matcher[matcher.FactID("testWrite3")] == FactMatching.Consts.True);
        
        matcher.DisposeData();
    }
    
    [Test]
    public void TestFactMatcherPeekAllValidRulesInBucket()
    {
        RulesDB rulesDB = Resources.Load<RulesDB>("FactMatcherTestResources/TestData");
        FactMatcher matcher = new FactMatcher(rulesDB);
        matcher.Init();

        matcher[matcher.FactID("test1")] = FactMatching.Consts.True;
        matcher[matcher.FactID("test2")] = FactMatching.Consts.True;

        BucketSlice fooBucket = matcher.BucketSlice("bucket:foo");
        BucketSlice booBucket = matcher.BucketSlice("bucket:boo");

        int rules = matcher.PeekValidRulesInBucket(fooBucket);
        for (int i = 0; i < rules; i++)
        {
            RuleDBEntry entry = matcher.GetRuleFromValidMatches(i);
            Assert.IsTrue(entry.PayloadRaw.Contains("foo"));
        }
        Assert.IsTrue(rules == 3);
        
        rules = matcher.PeekValidRulesInBucket(booBucket);
        for (int i = 0; i < rules; i++)
        {
            RuleDBEntry entry = matcher.GetRuleFromValidMatches(i);
            Assert.IsTrue(entry.PayloadRaw.Contains("boo"));
        }
        Assert.IsTrue(rules == 4);
        
        matcher.DisposeData();
    }
    
    [Test]
    public void TestFactMatcherPickAllValidRulesInBucket()
    {
        RulesDB rulesDB = Resources.Load<RulesDB>("FactMatcherTestResources/TestData");
        FactMatcher matcher = new FactMatcher(rulesDB);
        matcher.Init();

        matcher[matcher.FactID("test1")] = FactMatching.Consts.True;
        matcher[matcher.FactID("test2")] = FactMatching.Consts.True;

        BucketSlice fooBucket = matcher.BucketSlice("bucket:foo");
        BucketSlice booBucket = matcher.BucketSlice("bucket:boo");
        
        Assert.IsTrue((int)matcher[matcher.FactID("foo_testWrite1")] == FactMatching.Consts.False);
        Assert.IsTrue((int)matcher[matcher.FactID("foo_testWrite2")] == FactMatching.Consts.False);
        Assert.IsTrue((int)matcher[matcher.FactID("foo_testWrite3")] == FactMatching.Consts.False);
        
        Assert.IsTrue((int)matcher[matcher.FactID("boo_testWrite1")] == FactMatching.Consts.False);
        Assert.IsTrue((int)matcher[matcher.FactID("boo_testWrite2")] == FactMatching.Consts.False);
        Assert.IsTrue((int)matcher[matcher.FactID("boo_testWrite3")] == FactMatching.Consts.False);

        int rules = matcher.PickValidRulesInBucket(fooBucket);
        for (int i = 0; i < rules; i++)
        {
            RuleDBEntry entry = matcher.GetRuleFromValidMatches(i);
            Assert.IsTrue(entry.PayloadRaw.Contains("foo"));
            Assert.IsTrue((int)matcher[matcher.FactID("foo_testWrite1")] == FactMatching.Consts.True);
            Assert.IsTrue((int)matcher[matcher.FactID("foo_testWrite2")] == FactMatching.Consts.True);
            Assert.IsTrue((int)matcher[matcher.FactID("foo_testWrite3")] == FactMatching.Consts.True);
        }
        Assert.IsTrue(rules == 3);
        
        rules = matcher.PickValidRulesInBucket(booBucket);
        for (int i = 0; i < rules; i++)
        {
            RuleDBEntry entry = matcher.GetRuleFromValidMatches(i);
            Assert.IsTrue((int)matcher[matcher.FactID("boo_testWrite1")] == FactMatching.Consts.True);
            Assert.IsTrue((int)matcher[matcher.FactID("boo_testWrite2")] == FactMatching.Consts.True);
            Assert.IsTrue((int)matcher[matcher.FactID("boo_testWrite3")] == FactMatching.Consts.True);
            Assert.IsTrue(entry.PayloadRaw.Contains("boo"));
        }
        Assert.IsTrue(rules == 4);
        
        matcher.DisposeData();
    }
    
}
#endif