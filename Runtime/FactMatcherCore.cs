using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace FactMatching
{
    public struct Consts
    {
        public const int False = 0;
        public const int True = 1;
        
    }
    
    public struct Settings 
    {

        public Settings(bool factWriteToAllMatches,bool countAllMatches)
        {
            this.FactWriteToAllMatches = factWriteToAllMatches;
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

        public bool FactWriteToAllMatches;
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
            this.lowerBound = lowerBound;
            this.upperBound = upperBound;
            this.epsilon = epsilon;
            this.negation = negation;
        }


        public static FactCompare Equals(float a)
        {
            return new FactCompare(a,a,epsiFact);
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
            return sizeof(float) * 3 + sizeof(bool);
        }
        public readonly float lowerBound;
        public readonly float upperBound;
        public readonly float epsilon;
        public readonly bool negation;
    }
    
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
        
        public static bool Predicate(in FactCompare comp,float x)
        {
            
            //Debug.Log($"Epsilon is {comp.epsilon}");
            var aPart = comp.lowerBound < (x + comp.epsilon); 
            var bPart = x < (comp.upperBound + comp.epsilon); 
            //Debug.Log($"Testing Predicate aPart {aPart} vs bPart {bPart}");
            //Debug.Log($"lowerCheck {comp.lowerBound} <? ({x + comp.epsilon}");
            //Debug.Log($"upperCheck  x {x} <? {comp.lowerBound + comp.epsilon}");
            return  comp.negation ? !(aPart && bPart) :(aPart && bPart);
        }
    }
    
}