using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace FactMatcher
{
        public struct Consts
        {
            public const int False = 0;
            public const int True = 1;
        }
    
        public struct Settings 
        {

            public Settings(bool factWriteToAllMatches)
            {
                this.FactWriteToAllMatches = factWriteToAllMatches;
            }

            public bool FactWriteToAllMatches;
        }
    
        public struct RuleAtom
        {

            public RuleAtom(int factID,int ruleID,RuleCompare cmp,int orGroupRuleID,bool isStrict=true)
            {
                this.factID = factID;
                this.compare = cmp;
                this.ruleID = ruleID;
                this.strict = isStrict;
                this.orGroupRuleID = orGroupRuleID;
            }
            
            public int orGroupRuleID;
            public int factID;
            public int ruleID;
            public bool strict;
            public RuleCompare compare;
        }
        
        public struct FMRule
        {

            public FMRule(int eventId,int atomIndex,int numOfAtoms)
            {
                ruleFiredEventId = eventId;
                this.atomIndex = atomIndex;
                this.numOfAtoms = numOfAtoms;
            }

            public int ruleFiredEventId;
            public int atomIndex;
            public int numOfAtoms;
        }
        
        public readonly struct RuleCompare
        {
            
            private const float epsiFact = 0.0001f;
            public RuleCompare(float lowerBound, float upperBound, float epsilon = 0f,bool negation=false)
            {
                this.lowerBound = lowerBound;
                this.upperBound = upperBound;
                this.epsilon = epsilon;
                this.negation = negation;
            }

            public static RuleCompare Equals(float a)
            {
                return new RuleCompare(a,a,epsiFact);
            }
            public static RuleCompare NotEquals(float a)
            {
                return new RuleCompare(a,a,epsiFact,true);
            }
            public static RuleCompare LessThan(float a)
            {
                return new RuleCompare(float.MinValue,a);
            }
            public static RuleCompare LessThanEquals(float a)
            {
                return new RuleCompare(float.MinValue,a,epsiFact);
            }
            public static RuleCompare MoreThan(float a)
            {
                return new RuleCompare(a,float.MaxValue);
            }
            public static RuleCompare MoreThanEquals(float a)
            {
                return new RuleCompare(a,float.MaxValue,epsiFact);
            }
            public static RuleCompare RangeEquals(float a,float b)
            {
                return new RuleCompare(a,b,epsiFact);
            }
            public static RuleCompare Range(float a,float b)
            {
                return new RuleCompare(a,b);
            }
            public readonly float lowerBound;
            public readonly float upperBound;
            public readonly float epsilon;
            public readonly bool negation;
        }
    
        public class  Functions
    {
        
        
        
    public static void CreateNativeRules(RulesDB db, out NativeArray<FactMatcher.FMRule> rules, out NativeArray<FactMatcher.RuleAtom> ruleAtoms)
    {
        rules = new NativeArray<FactMatcher.FMRule>(db.rules.Count, Allocator.Persistent);

        int numOfAtoms = 0;
        foreach (var ruleDBEntry in db.rules)
        {
            foreach (var atom in ruleDBEntry.atoms)
            {
                numOfAtoms++;
            }
        }

        ruleAtoms = new NativeArray<FactMatcher.RuleAtom>(numOfAtoms, Allocator.Persistent);
        int atomIndex = 0;
        
            
        //Debug.Log($"Creating native version of this many rules {db.rules.Count}");
        for (int i = 0; i < db.rules.Count; i++)
        {

            //Debug.Log($"Creating native version of rule {db.rules[i].ruleName} with {db.rules[i].atoms} atoms");
            var ruleFiredEventID = db.rules[i].RuleID;
            rules[i] = new FactMatcher.FMRule(ruleFiredEventID, atomIndex, db.rules[i].atoms.Count);
            foreach (var atom in db.rules[i].atoms)
            {
                ruleAtoms[atomIndex] = new FactMatcher.RuleAtom(atom.factID, i, atom.CreateCompare(db),atom.orGroupRuleID);
                atomIndex++;
            }
        }
    }
        
   public static void CreateMockRules(int worldFacts, int numORules,
       out List<FMRule> rules,out List<RuleAtom> atoms,
       out List<float> factValues)
   {
       rules = new List<FMRule>();
       atoms = new List<RuleAtom>();
       factValues = new List<float>();
       for (int i = 0; i < worldFacts; i++)
       {
           factValues.Add(i);
       }

       int totalAtoms = 0;
       for (int i = 0; i < numORules; i++)
       {
           int numAtoms = UnityEngine.Random.Range(1, 20);
           rules.Add(new FMRule(i,totalAtoms,numAtoms));
           totalAtoms += numAtoms;
           for (int j = 0; j < numAtoms; j++)
           {
               int compary = UnityEngine.Random.Range(0, 6);
               var compare = RuleCompare.Equals(1.0f); 
               switch (compary)
               {
                   case 0:
                        compare = RuleCompare.Equals(UnityEngine.Random.Range(0, worldFacts));
                       break;
                   case 1:
                       compare = RuleCompare.LessThan(UnityEngine.Random.Range(0, worldFacts));
                       break;
                   case 2:
                       compare = RuleCompare.LessThanEquals(UnityEngine.Random.Range(0, worldFacts));
                       break;
                   case 3:
                       compare = RuleCompare.MoreThan(UnityEngine.Random.Range(0, worldFacts));
                       break;
                   case 4:
                       compare = RuleCompare.MoreThanEquals(UnityEngine.Random.Range(0, worldFacts));
                       break;
                   case 5:
                       compare = RuleCompare.NotEquals(UnityEngine.Random.Range(0, worldFacts));
                       break;
               }
               var vFactIndex = UnityEngine.Random.Range(0, worldFacts);
               atoms.Add(new RuleAtom(vFactIndex,i,compare,-1));
           }

       }

   }
        
        public static bool predicate(in RuleCompare comp,float x)
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
