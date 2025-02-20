﻿using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace FactMatching
{
    public struct Consts
    {
        public const int False = 0;
        public const int True = 1;
        /* 
         we reserve one slot in our Facts-array
         as a place where we stash unused values.
         If we look for a named FactID("some_fact") and find none, we return
         FactIDDevNull, this makes code that transfers values to factMatcher like this 
         _factmatcher[FindID("some_fact")] = someValue; not cause an error if 
         our rules does not contain any tests that uses some_fact
         */
        public const int FactIDDevNull = 0;
        public const int RuleIDNonExisting = -1;
        public const int MaxFrameCounter = Int16.MaxValue;
        public const float MaxSecondsCounter = float.MaxValue*0.5f;
    }
    
    public struct Settings 
    {

        public Settings(bool checkAllRules,bool countAllMatches)
        {
            this.CheckAllRules = checkAllRules;
            CountAllFactMatches = countAllMatches;
        }
        
        /*
         * This is used to calculate memory footprint.
         * Annoying but I think I have to, if I want to avoid
         * enabling unsafe code
         */
        public static int SizeInBytes()
        {
            return sizeof(bool) * 2;;
        }

        public bool CheckAllRules;
        public bool CountAllFactMatches;
    }
    
    public struct FactTest
    {

        public FactTest(int factID,int ruleID,FactCompare cmp,int orGroupRuleID,bool isStrict=true)
        {
            this.factID = factID;
            this.compare = cmp;
            this.ruleID = ruleID;
            this.strict = isStrict;
            this.orGroupRuleID = orGroupRuleID;
        }
            
        /*
         * This is used to calculate memory footprint.
         * Annoying but I think I have to, if I want to avoid
         * enabling unsafe code
         */
        public static int SizeInBytes()
        {
            return sizeof(int) * 3 + sizeof(bool) + FactCompare.SizeInBytes();
        }
        
        public readonly int orGroupRuleID;
        public readonly int factID;
        public int ruleID;
        public readonly bool strict;
        public FactCompare compare;
    }
        
    public readonly struct Rule
    {
        public Rule(int eventId,int factTestIndex,int numOfFactTests)
        {
            ruleFiredEventId = eventId;
            this.factTestIndex = factTestIndex;
            this.numOfFactTests = numOfFactTests;
        }

        /*
         * This is used to calculate memory footprint.
         * Annoying but I think I have to, if I want to avoid
         * enabling unsafe code
         */
        public static int SizeInBytes()
        {
            return sizeof(int) * 3;
        }
        public readonly int ruleFiredEventId;
        public readonly int factTestIndex;
        public readonly int numOfFactTests;
    }
        
    public readonly struct FactCompare
    {
            
        private const float epsiFact = 0.0001f;
        public FactCompare(float lowerBound, float upperBound, float epsilon = 0f,bool negation=false)
        {
            this.lowerBound = lowerBound - epsilon;
            this.upperBound = upperBound + epsilon;
            this.negation = negation;
        }


        public static FactCompare Equals(float a)
        {
            return new FactCompare(a,a,epsiFact);
        }
        public static FactCompare EqualsEpsi(float a,float epsi)
        {
            return new FactCompare(a,a,epsi);
        }
        public static FactCompare NotEquals(float a)
        {
            return new FactCompare(a,a,epsiFact,true);
        }
        public static FactCompare LessThan(float a)
        {
            return new FactCompare(float.MinValue,a);
        }
        public static FactCompare LessThanEquals(float a)
        {
            return new FactCompare(float.MinValue,a,epsiFact);
        }
        public static FactCompare MoreThan(float a)
        {
            return new FactCompare(a,float.MaxValue);
        }
        public static FactCompare MoreThanEquals(float a)
        {
            return new FactCompare(a,float.MaxValue,epsiFact);
        }
        public static FactCompare RangeEquals(float a,float b)
        {
            return new FactCompare(a,b,epsiFact);
        }
        public static FactCompare Range(float a,float b)
        {
            return new FactCompare(a,b);
        }
        
        /*
         * This is used to calculate memory footprint.
         * Annoying but I think I have to, if I want to avoid
         * enabling unsafe code
         */
        public static int SizeInBytes()
        {
            return sizeof(float) * 2 + sizeof(bool);
        }
        
        public readonly double lowerBound;
        public readonly double upperBound;
        public readonly bool negation;
    }
    
    /// <summary>
    /// Class to house Functions for the FactMacher
    /// </summary>
    public static class Functions
    {
        public static void CreateNativeRules(RulesDB db, out NativeArray<Rule> rules, out NativeArray<FactTest> factTests)
        {
            rules = new NativeArray<Rule>(db.rules.Count, Allocator.Persistent);

            int numOfFactTests = 0;
            foreach (var ruleDBEntry in db.rules)
            {
                foreach (var factTest in ruleDBEntry.factTests)
                {
                    numOfFactTests++;
                }
            }

            factTests = new NativeArray<FactTest>(numOfFactTests, Allocator.Persistent);
            int factTestIndex = 0;
        
            
            for (int i = 0; i < db.rules.Count; i++)
            {
                var ruleFiredEventID = db.rules[i].RuleID;
                rules[i] = new Rule(ruleFiredEventID, factTestIndex, db.rules[i].factTests.Count);
                foreach (var factTest  in db.rules[i].factTests)
                {
                    factTests[factTestIndex] = new FactTest(factTest.factID, i, factTest .CreateCompare(db),factTest.orGroupRuleID,factTest.isStrict);
                    factTestIndex++;
                }
            }
        }
        
        public static void CreateMockRules(int worldFacts, int numORules,
            out List<Rule> rules,out List<FactTest> factTests,
            out List<float> factValues)
        {
            rules = new List<Rule>();
            factTests = new List<FactTest>();
            factValues = new List<float>();
            for (int i = 0; i < worldFacts; i++)
            {
                factValues.Add(i);
            }

            int totalFactTests = 0;
            for (int i = 0; i < numORules; i++)
            {
                int numFactTests = UnityEngine.Random.Range(1, 20);
                rules.Add(new Rule(i,totalFactTests,numFactTests));
                totalFactTests += numFactTests;
                for (int j = 0; j < numFactTests; j++)
                {
                    int compary = UnityEngine.Random.Range(0, 6);
                    var compare = FactCompare.Equals(1.0f); 
                    switch (compary)
                    {
                        case 0:
                            compare = FactCompare.Equals(UnityEngine.Random.Range(0, worldFacts));
                            break;
                        case 1:
                            compare = FactCompare.LessThan(UnityEngine.Random.Range(0, worldFacts));
                            break;
                        case 2:
                            compare = FactCompare.LessThanEquals(UnityEngine.Random.Range(0, worldFacts));
                            break;
                        case 3:
                            compare = FactCompare.MoreThan(UnityEngine.Random.Range(0, worldFacts));
                            break;
                        case 4:
                            compare = FactCompare.MoreThanEquals(UnityEngine.Random.Range(0, worldFacts));
                            break;
                        case 5:
                            compare = FactCompare.NotEquals(UnityEngine.Random.Range(0, worldFacts));
                            break;
                    }
                    var vFactIndex = UnityEngine.Random.Range(0, worldFacts);
                    factTests.Add(new FactTest(vFactIndex,i,compare,-1));
                }

            }
        }
        
        public static int[] CreateFactIDSFromEnum(FactMatcher fm,Type enumType, string prefix, string postfix, int defaulyValue = 0)
        {
            string[] enumNames = Enum.GetNames(enumType);
            int[] ids = new int[enumNames.Length];
            for (int i = 0; i < ids.Length; i++)
            {
                ids[i] = fm.FactID($"{prefix}{enumNames[i]}{postfix}");
                fm[ids[i]] = defaulyValue;
            }
            return ids;
        }
        
        public static int[] CreateStringIDSFromEnum(FactMatcher factMatcher,Type enumType,bool logNotFoundWarning=false)
        {
            var names = Enum.GetNames(enumType);
            int[] stringIDS = new int[names.Length];
            for (int i = 0; i < names.Length; i++)
            {
                int sid = factMatcher.StringID(names[i]);
                if (sid == -1 && logNotFoundWarning)
                {
                    Debug.LogWarning($"Could not find {names[i]} in the FactMatcher database");
                }
                stringIDS[i] = sid;
            }

            return stringIDS;
        }
        
        public static bool Predicate(in FactCompare comp,float x)
        {
            return (x > comp.lowerBound && x < comp.upperBound) ^ comp.negation;
        }
    }
    
}