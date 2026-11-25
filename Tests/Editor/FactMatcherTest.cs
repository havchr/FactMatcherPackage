#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
    public void TestFactMatcherPickBestRulesFactWriteToAllValidRules_ReportsEachBestRuleOnce()
    {
        RulesDB rulesDB = Resources.Load<RulesDB>("FactMatcherTestResources/TestData");
        FactMatcher matcher = new FactMatcher(rulesDB);
        matcher.Init();

        var pickedRuleIDs = new List<int>();
        matcher.OnRulePicked += ruleID => pickedRuleIDs.Add(ruleID);

        matcher[matcher.FactID("test1")] = FactMatching.Consts.True;
        matcher[matcher.FactID("test2")] = FactMatching.Consts.True;

        int bestRules = matcher.PickBestRulesFactWriteToAllValidRules();

        Assert.AreEqual(2, bestRules);
        // rule_1 and rule_2 tie for best match, so each should be reported exactly once
        // (not the same rule twice).
        CollectionAssert.AreEquivalent(
            new[] { matcher.RuleID("rule_1"), matcher.RuleID("rule_2") },
            pickedRuleIDs);

        matcher.DisposeData();
    }

    [Test]
    public void TestFactMatcherPickBestRulesInBucketFactWriteToAllValidRules_ReportsEachBestRuleOnce()
    {
        RulesDB rulesDB = Resources.Load<RulesDB>("FactMatcherTestResources/TestData");
        FactMatcher matcher = new FactMatcher(rulesDB);
        matcher.Init();

        BucketSlice fooBucket = matcher.BucketSlice("bucket:foo");

        var pickedRuleIDs = new List<int>();
        matcher.OnRulePicked += ruleID => pickedRuleIDs.Add(ruleID);

        matcher[matcher.FactID("test1")] = FactMatching.Consts.True;
        matcher[matcher.FactID("test2")] = FactMatching.Consts.True;

        int bestRules = matcher.PickBestRulesInBucketFactWriteToAllValidRules(fooBucket);

        Assert.AreEqual(2, bestRules);
        // bucket_foo_rule_1 and bucket_foo_rule_2 tie for best match in bucket:foo.
        CollectionAssert.AreEquivalent(
            new[] { matcher.RuleID("bucket_foo_rule_1"), matcher.RuleID("bucket_foo_rule_2") },
            pickedRuleIDs);

        matcher.DisposeData();
    }

    [Test]
    public void TestFactMatcherBucketSliceDoesNotGrowOnRepeatedLookups()
    {
        RulesDB rulesDB = Resources.Load<RulesDB>("FactMatcherTestResources/TestData");
        FactMatcher matcher = new FactMatcher(rulesDB);
        matcher.Init();

        BucketSlice first = matcher.BucketSlice("bucket:foo");
        int factCount = first.factIds.Count;
        Assert.IsTrue(factCount > 0);

        // BucketSlice returns the same cached slice every time; looking it up again
        // must not add its fact assignments again.
        for (int i = 0; i < 10; i++)
        {
            matcher.BucketSlice("bucket:foo");
        }

        BucketSlice again = matcher.BucketSlice("bucket:foo");
        Assert.AreEqual(factCount, again.factIds.Count);
        Assert.AreEqual(factCount, again.factValues.Count);

        matcher.DisposeData();
    }

    [Test]
    public void TestFactMatcherReInitAfterDisposeStillPicksRules()
    {
        RulesDB rulesDB = Resources.Load<RulesDB>("FactMatcherTestResources/TestData");
        FactMatcher matcher = new FactMatcher(rulesDB);
        matcher.Init();
        matcher.PickBestRules(); // builds and caches the match job

        // The cached job must not keep pointing at the arrays DisposeData freed.
        matcher.DisposeData();
        matcher.Init();

        matcher[matcher.FactID("test1")] = FactMatching.Consts.True;
        matcher[matcher.FactID("test2")] = FactMatching.Consts.True;
        int bestRules = matcher.PickBestRules();
        Assert.AreEqual(2, bestRules);

        matcher.DisposeData();
    }

    [Test]
    public void TestFactMatcherInitTwiceDoesNotLeak()
    {
        RulesDB rulesDB = Resources.Load<RulesDB>("FactMatcherTestResources/TestData");
        FactMatcher matcher = new FactMatcher(rulesDB);

        UnsafeUtility.ForgiveLeaks();
        matcher.Init();
        matcher.Init(); // must free the first set of native arrays, not leak them
        matcher.DisposeData();

        int leaks = UnsafeUtility.CheckForLeaks();
        Assert.AreEqual(0, leaks);
    }

    // Builds a RulesDB with one rule per payload, all testing the facts score and player_name,
    // sets score = 5 and player_name = Bro, and returns each payload interpolated.
    private static Dictionary<string, string> InterpolatePayloads(Dictionary<string, string> payloadsByRuleName)
    {
        var script = new System.Text.StringBuilder();
        foreach (var entry in payloadsByRuleName)
        {
            script.AppendLine($".{entry.Key} IF");
            script.AppendLine("    score > 0");
            script.AppendLine("    player_name = Bro");
            script.AppendLine(".THEN RESPONSE");
            script.AppendLine(entry.Value);
            script.AppendLine(".END");
            script.AppendLine();
        }

        RulesDB rulesDB = RulesDB.CreateRulesDBFromStringList(new List<string> { script.ToString() }, null, out var problems);
        Assert.IsFalse(problems.ContainsError(), problems.ToString());
        FactMatcher matcher = new FactMatcher(rulesDB);
        var result = new Dictionary<string, string>();
        try
        {
            matcher.Init();
            matcher[matcher.FactID("score")] = 5;
            matcher[matcher.FactID("player_name")] = matcher.StringID("Bro");

            System.Text.StringBuilder sb = null;
            foreach (var ruleName in payloadsByRuleName.Keys)
            {
                RuleDBEntry rule = rulesDB.rules.Find(r => r.ruleName == ruleName);
                result[ruleName] = rule.Interpolate(matcher, ref sb);
            }
        }
        finally
        {
            matcher.DisposeData();
            UnityEngine.Object.DestroyImmediate(rulesDB);
        }
        return result;
    }

    [Test]
    public void TestPayloadInterpolationValueAtEndOfPayload()
    {
        var result = InterpolatePayloads(new Dictionary<string, string>
        {
            // Control: a value interpolation followed by a space already works.
            { "value_then_text", "You have #{score}F0 points" },
            // Problem 8: a value interpolation at the very end keeps its last character.
            { "value_at_end", "Score #{score}" },               // today: "Score 5}"
            { "value_with_format_at_end", "Score #{score}F0" }, // today: "Score 50"
        });

        Assert.AreEqual("You have 5 points", result["value_then_text"]);
        Assert.AreEqual("Score 5", result["value_at_end"]);
        Assert.AreEqual("Score 5", result["value_with_format_at_end"]);
    }

    [Test]
    public void TestPayloadInterpolationLoneMarkerDoesNotBreakLaterInterpolations()
    {
        var result = InterpolatePayloads(new Dictionary<string, string>
        {
            // Control: string interpolation with no stray markers.
            { "string_only", "Hello ${player_name} today" },
            // Problem 9: a '$' or '#' that doesn't start an interpolation must not change
            // how the next interpolation is read.
            { "lone_dollar_then_value", "Costs $5 for #{score}F0 items" },  // today: #{score} read as a string interpolation
            { "lone_hash_then_string", "Item #5 is for ${player_name} today" }, // today: ${player_name} read as a value interpolation
        });

        Assert.AreEqual("Hello Bro today", result["string_only"]);
        Assert.AreEqual("Costs $5 for 5 items", result["lone_dollar_then_value"]);
        Assert.AreEqual("Item #5 is for Bro today", result["lone_hash_then_string"]);
    }

    [Test]
    public void TestPayloadInterpolationValueFormatStopsAtNonFormatChar()
    {
        // A value format is a standard .NET numeric format: one letter, then optional digits (N0, F2, P1).
        // Whatever comes after it is payload text, not part of the format.
        var result = InterpolatePayloads(new Dictionary<string, string>
        {
            { "format_then_space", "You have #{score}N0 points" },
            { "format_at_end", "Score #{score}F0" },
            // Punctuation straight after the interpolation.
            { "no_format_then_period", "Your score is #{score}." },
            { "no_format_then_bang", "You got #{score}!" },
            { "format_then_comma", "#{score}F0, well done" },
            { "in_parentheses", "Score (#{score}) today" },
            // Two interpolations with nothing between them.
            { "adjacent_values", "#{score}#{score}" },
        });

        Assert.AreEqual("You have 5 points", result["format_then_space"]);
        Assert.AreEqual("Score 5", result["format_at_end"]);
        Assert.AreEqual("Your score is 5.", result["no_format_then_period"]);
        Assert.AreEqual("You got 5!", result["no_format_then_bang"]);
        Assert.AreEqual("5, well done", result["format_then_comma"]);
        Assert.AreEqual("Score (5) today", result["in_parentheses"]);
        Assert.AreEqual("55", result["adjacent_values"]);
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

    [Test]
    public void TestFactMatcherRange()
    {
        RulesDB rulesDB = Resources.Load<RulesDB>("FactMatcherTestResources/TestData");
        FactMatcher matcher = new FactMatcher(rulesDB);
        matcher.Init();

        matcher[matcher.FactID("rule_test_rangy_result")] = FactMatching.Consts.False;
        matcher[matcher.FactID("test_case")] = matcher.StringID("TestRange");

        //See test_data_range.fmrs.txt , our test rules have age in 2,12 range mixing exclusive and inclusive syntax


        //Checking outside, lower 
        matcher[matcher.FactID("age")] = 1;
        int rulesPicked = matcher.PickValidRules(false);
        Assert.AreEqual(rulesPicked,0);

        //Checking inside
        matcher[matcher.FactID("age")] = 11;
        rulesPicked = matcher.PickValidRules(false);
        Assert.AreEqual(rulesPicked,4);

        //Checking outside, higher 
        matcher[matcher.FactID("age")] = 13;
        rulesPicked = matcher.PickValidRules(false);
        Assert.AreEqual(rulesPicked,0);

        //Checking Boundaries
        matcher[matcher.FactID("age")] = 2;
        rulesPicked = matcher.PickValidRules(false);
        Assert.AreEqual(rulesPicked,2);
        Assert.IsTrue(
            ( matcher.GetRuleFromMatches(0).ruleName.Equals("rule_test_range_inclusive_exclusive") ||
             matcher.GetRuleFromMatches(0).ruleName.Equals("rule_test_range_inclusive_inclusive") )
            &&
            ( matcher.GetRuleFromMatches(1).ruleName.Equals("rule_test_range_inclusive_exclusive") ||
                matcher.GetRuleFromMatches(1).ruleName.Equals("rule_test_range_inclusive_inclusive") )
            );

        matcher[matcher.FactID("age")] = 12;
        rulesPicked = matcher.PickValidRules(false);
        Assert.AreEqual(rulesPicked,2);
        Assert.IsTrue(
            (matcher.GetRuleFromMatches(0).ruleName.Equals("rule_test_range_inclusive_inclusive") ||
            matcher.GetRuleFromMatches(0).ruleName.Equals("rule_test_range_exclusive_inclusive"))
            &&
            (matcher.GetRuleFromMatches(1).ruleName.Equals("rule_test_range_inclusive_inclusive") ||
            matcher.GetRuleFromMatches(1).ruleName.Equals("rule_test_range_exclusive_inclusive"))
            );

        
        matcher.DisposeData();
    }

    // Parses the rule script under different cultures, to check that numbers in rule scripts
    // always use '.' as the decimal separator, no matter the machine's locale.
    // (Loading the pre-parsed TestData asset would only test the culture it was parsed with.)
    [TestCase("nb-NO")]   // decimal comma
    [TestCase("de-DE")]   // decimal comma, '.' as thousands separator
    [TestCase("en-US")]   // decimal point
    public void TestFactMatcherInvariantParse(string cultureName)
    {
        var originalCulture = CultureInfo.CurrentCulture;
        RulesDB rulesDB = null;
        try
        {
            var culture = new CultureInfo(cultureName);
            CultureInfo.CurrentCulture = culture;
            // Make sure the culture switch actually took effect, or the test proves nothing.
            Assert.AreEqual(culture.NumberFormat.NumberDecimalSeparator,
                CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);

            var script = Resources.Load<TextAsset>("FactMatcherTestResources/test_data_invariant_parse.fmrs");
            Assert.IsNotNull(script, "Could not load test_data_invariant_parse.fmrs.txt");
            rulesDB = RulesDB.CreateRulesDBFromStringList(new List<string> { script.text }, null, out var problems);
            Assert.IsFalse(problems.ContainsError(), problems.ToString());

            var floaty = rulesDB.GetFactTestFromFactID(rulesDB.FactId("floaty"));
            Assert.AreEqual(FactValueType.Value, floaty.compareType);
            Assert.AreEqual(1.75f, floaty.matchValue); // not 175 (de-DE thousands) and not a string

            var nonFloaty = rulesDB.GetFactTestFromFactID(rulesDB.FactId("non_floaty"));
            Assert.AreEqual(FactValueType.String, nonFloaty.compareType);
            Assert.AreEqual("1,75", nonFloaty.matchString);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture; // don't leak into other tests
            if (rulesDB != null)
            {
                UnityEngine.Object.DestroyImmediate(rulesDB);
            }
        }
    }
    
}
#endif