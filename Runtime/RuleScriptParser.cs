#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Plastic.Antlr3.Runtime.Misc;
using UnityEngine;

namespace FactMatcher
{
    public class RuleScriptParser  
    {
        
        public enum RuleScriptParserEnum
        {
            ParsingRule,ParsingResponse,ParsingFactWrite,LookingForRule,ParsingPayloadObject
        }
        public void GenerateFromText(string text,List<RuleDBEntry> rules,ref int factID,ref Dictionary<string,int> addedFactIDNames,ref int ruleID)
        {

            RuleScriptParserEnum state = RuleScriptParserEnum.LookingForRule;
            Dictionary<string,List<RuleDBAtomEntry>> parsedAtoms = new Dictionary<string, List<RuleDBAtomEntry>>();
            RuleDBEntry currentRule = null;
            StringBuilder payload = new StringBuilder();
            ScriptableObject payloadObject = null;
            using (System.IO.StringReader reader = new System.IO.StringReader(text))
            {
                string line;
                while ( (line = reader.ReadLine()) !=null)
                {
                    //-- in ruleScript is a comment , except if we are parsing a response..
                    if (state != RuleScriptParserEnum.ParsingResponse)
                    {
                        if (line.StartsWith("--"))
                        {
                            continue;
                        }
                    }
	            
                    if (state == RuleScriptParserEnum.LookingForRule)
                    {
                        if (line.Length > 0 && line[0] == '.')
                        {
                            //Todo - Rules should start with .
                            //Should give error if not the case... 
                            state = RuleScriptParserEnum.ParsingRule;
                            var ruleNames = line.Split( '.');
                            var finalName = new StringBuilder();
                            var derived = new StringBuilder("");
                            bool foundDerived = false;
                            int lastIndex = ruleNames.Length;
                            //This attempts to figure out if there is any thing to derive from by searching downwards.
                            //if we have NickHit.Allies and then we have a rule that is NickHit.Allies.NearDeath.BySandwhich
                            //We should first see if we can derive from NickHit.Allies.NearDeath , if no match is found.
                            //We should see if we can derive from NickHit.Allies ... and so on.
                            while (!foundDerived && lastIndex > 1)
                            {
                            
                                finalName.Clear(); 
                                derived.Clear();
                                for(int i=1; i < lastIndex; i++)
                                {
                                    finalName.Append($"{ruleNames[i]}");
                                    if (i < lastIndex - 1)
                                    {
                                        finalName.Append(".");
                                        derived.Append($"{ruleNames[i]}");
                                        if (i < lastIndex - 2)
                                        {
                                            derived.Append(".");
                                        }
                                    }

                                }

                                if (derived.Length > 0)
                                {
                                    if (parsedAtoms.ContainsKey(derived.ToString()))
                                    {
                                        foundDerived = true;
                                    }
                                }
                                lastIndex--;
                                //Debug.Log($"derived is {derived} and foundDerived is {foundDerived} and lastIndex {lastIndex} and finalName is {finalName}");
                            }

                            currentRule = new RuleDBEntry();
                            currentRule.atoms = new List<RuleDBAtomEntry>(); 
                            currentRule.factWrites = new List<RuleDBFactWrite>();
                            currentRule.ruleName = finalName.ToString();
                            //Debug.Log($"Adding atoms from derived {derived}");
                            //Grab atoms from derived
                            if (derived.Length > 0)
                            {
                                if (parsedAtoms.ContainsKey(derived.ToString()))
                                {
                                
                        
                                    foreach (var atom in parsedAtoms[derived.ToString()])
                                    {
                                
                                        //Debug.Log($"for rule {currentRule.ruleName} - Adding atom {atom.factName} from derived {derived}");
                                        currentRule.atoms.Add(atom); 
                                    }
                                }
                            }
                            Debug.Log($"line is {line}");
                            Debug.Log($"FinalName is {finalName} and derived is {derived}");
					
                        }
                    }

                    if (state == RuleScriptParserEnum.ParsingRule)
                    {
                        ParseRuleAtoms(line, currentRule);
                        if (line.Trim().Contains(":FactWrite:"))
                        {
                            state = RuleScriptParserEnum.ParsingFactWrite;
                        }
                        if (line.Trim().Contains(":Response:"))
                        {
                            state = RuleScriptParserEnum.ParsingResponse;
                            payload.Clear();
                        }
                        if (line.Trim().Contains(":PayloadObject:"))
                        {
                            
                            Debug.Log($"Getting ready to look for Payload object");
                            state = RuleScriptParserEnum.ParsingPayloadObject;
                            payload.Clear();
                        }
                    
                    }
                    else if (state == RuleScriptParserEnum.ParsingFactWrite)
                    {

                        ParseFactWrites(line, currentRule);
                        if (line.Trim().Contains(":PayloadObject:"))
                        {
                            
                            Debug.Log($"Getting ready to look for Payload object");
                            state = RuleScriptParserEnum.ParsingPayloadObject;
                            payload.Clear();
                        }
                        if (line.Trim().Contains(":Response:"))
                        {
                            state = RuleScriptParserEnum.ParsingResponse;
                            payload.Clear();
                        }
                    }
                    else if (state == RuleScriptParserEnum.ParsingPayloadObject)
                    {
                        if (line.Trim().Contains(":End:"))
                        {
                            Debug.Log($"payload is {payload} trying to parse that as payload object");
                            //Try to load resource from payload into payloadObject
                            payloadObject = Resources.Load<ScriptableObject>(payload.ToString());
                        }
                        else
                        {
                            payload.Append(line);
                        }
                        if (line.Trim().Contains(":Response:"))
                        {
                            state = RuleScriptParserEnum.ParsingResponse;
                            payload.Clear();
                        }
                    }
                    else if (state == RuleScriptParserEnum.ParsingResponse)
                    {
                        if (line.Trim().Contains(":End:"))
                        {
                            //Store rule...
                            state = RuleScriptParserEnum.LookingForRule;
                            currentRule.payload = payload.ToString();
                            currentRule.PayloadObject = payloadObject;
                            payloadObject = null;
                            if (rules.Any(entry => entry.ruleName.Equals(currentRule.ruleName)))
                            {
                               Debug.LogError($"Allready Contains a rule named {currentRule.ruleName} - will not add"); 
                            }
                            else
                            {
                                //Assigns an unique (to the RuleDB) ID to each fact
                                SetFactID(currentRule, ref addedFactIDNames,ref factID);
                                currentRule.RuleID = ruleID;
                                ruleID++;
                                rules.Add(currentRule);
                            }
                        
                            //adding all our atoms into the parsedAtoms array for deriving to work..
                            foreach (var atomEntry in currentRule.atoms)
                            {
                                //Debug.Log($"Adding atom {atomEntry.factName} to ParsedAtoms with key {currentRule.ruleName}");
                                if (!parsedAtoms.ContainsKey(currentRule.ruleName))
                                {
                                    parsedAtoms[currentRule.ruleName] = new List<RuleDBAtomEntry>();
                                }
                                parsedAtoms[currentRule.ruleName].Add(atomEntry);
                            }
                        
                        }
                        else
                        {
                            payload.Append(line);
                        }
                    }

                }
            }
        }

        private static void SetFactID(RuleDBEntry currentRule, ref Dictionary<string,int> addedFactIDNames, ref int factID)
        {
            
            if(currentRule.atoms!=null)
            foreach (var atomEntry in currentRule.atoms)
            {
                if (!addedFactIDNames.ContainsKey(atomEntry.factName))
                {
                    atomEntry.factID = factID;
                    addedFactIDNames[atomEntry.factName] = factID;
                    factID++;
                }
                else
                {
                    atomEntry.factID = addedFactIDNames[atomEntry.factName];
                }
            }

            if(currentRule.factWrites!=null)
            foreach (var factWrite in currentRule.factWrites)
            {
                
                if (!addedFactIDNames.ContainsKey(factWrite.factName))
                {
                    factWrite.factID = factID;
                    addedFactIDNames[factWrite.factName] = factID;
                    factID++;
                }
                else
                {
                    factWrite.factID = addedFactIDNames[factWrite.factName];
                }
            }

        }

        private static void ParseFactWrites(string line, RuleDBEntry currentRule)
        {
            var operands = new List<(string, RuleDBFactWrite.WriteMode)>
            {
                ("+=", RuleDBFactWrite.WriteMode.IncrementValue),
                ("-=", RuleDBFactWrite.WriteMode.SubtractValue),
                ("=", RuleDBFactWrite.WriteMode.SetString),
                
            };
            //Operands with multiple characters must be matched prior to operands of lower character..
            //or else , >= would first be treated as > operand ... 
            operands.Sort((s, s1) => s1.Item1.Length - s.Item1.Length);
            foreach (var operand in operands)
            {
                if (line.Trim().Contains(operand.Item1))
                {
                    var splits = line.Split(new[] {operand.Item1}, StringSplitOptions.RemoveEmptyEntries);
                    if (splits.Length != 2)
                    {
                        Debug.LogError("Error case - Operand split trouble");
                    }
                    else
                    {
                        RuleDBFactWrite factWrite = new RuleDBFactWrite();
                        
                        factWrite.writeMode = operand.Item2;
                        
                        factWrite.factName = splits[0].Trim();
                        var valueMatch = splits[1].Trim();
                        if (float.TryParse(valueMatch, out float floatVal))
                        {
                            factWrite.writeValue = floatVal;
                            if (factWrite.writeMode == RuleDBFactWrite.WriteMode.SetString)
                            {
                                factWrite.writeMode = RuleDBFactWrite.WriteMode.SetValue;
                            }
                        }
                        else
                        {

                            if (factWrite.writeMode != RuleDBFactWrite.WriteMode.SetString)
                            {
                               Debug.LogError("Using wrong operand for string. Only = supported"); 
                            }
                            factWrite.writeString = valueMatch;
                        }
                        currentRule.factWrites.Add(factWrite);
                    }

                    break;
                }
            }
        }
        
        private static void ParseRuleAtoms(string line, RuleDBEntry currentRule)
        {
            if (line.Contains("Range"))
            {
                    
                var splits = line.Split(new[] {"Range"}, StringSplitOptions.RemoveEmptyEntries);
            }
            
            var operands = new List<(string, RuleDBAtomEntry.Comparision)>
            {
                (">", RuleDBAtomEntry.Comparision.MoreThan),
                (">=", RuleDBAtomEntry.Comparision.MoreThanEqual),
                ("<", RuleDBAtomEntry.Comparision.LessThan),
                ("<=", RuleDBAtomEntry.Comparision.LessThanEqual),
                ("=", RuleDBAtomEntry.Comparision.Equal),
                ("Range", RuleDBAtomEntry.Comparision.Range),
                ("!", RuleDBAtomEntry.Comparision.NotEqual)
            };
            //Operands with multiple characters must be matched prior to operands of lower character..
            //or else , >= would first be treated as > operand ... 
            operands.Sort((s, s1) => s1.Item1.Length - s.Item1.Length);
            foreach (var operand in operands)
            {
                if (line.Trim().Contains(operand.Item1))
                {
                    var splits = line.Split(new[] {operand.Item1}, StringSplitOptions.RemoveEmptyEntries);
                    if (splits.Length != 2)
                    {
                        Debug.LogError("Error case - Operand split trouble");
                    }
                    else
                    {
                        //Todo add support for strict
                        RuleDBAtomEntry atomEntry = new RuleDBAtomEntry();
                        atomEntry.compareMethod = operand.Item2;
                        atomEntry.factName = splits[0].Trim();
                        var valueMatch = splits[1].Trim();
                        
                        //If we have added a rule - from derived - but are overwriting that fact-query , then delete the 
                        //derived rule , or else we would get two conflicting rule atoms in our rule...
                        if (currentRule.atoms.Any(entry => entry.factName.Equals(atomEntry.factName)))
                        {
                            currentRule.atoms.Remove(currentRule.atoms.Find(entry =>
                                entry.factName.Equals(atomEntry.factName)));
                        }

                        if (atomEntry.compareMethod == RuleDBAtomEntry.Comparision.Range)
                        {
                            //further parse out the Range..
                            valueMatch = valueMatch.Trim();
                            bool exclusiveLeft = valueMatch.Contains("(");
                            bool exclusiveRight= valueMatch.Contains(")");
                            
                            var splitRange = valueMatch.Trim('(','[',')',']').Split(',');
                            //Debug.Log($"handling {valueMatch} in compareMethodRange");
                            //Debug.Log($"handling left val {splitRange[0]} in compareMethodRange");
                            //Debug.Log($"handling right val {splitRange[1]} in compareMethodRange");
                            if (float.TryParse(splitRange[0], out float left))
                            {
                                atomEntry.matchValue = left;
                                atomEntry.compareType = FactValueType.Value;
                                atomEntry.compareMethod = exclusiveLeft
                                    ? RuleDBAtomEntry.Comparision.MoreThan
                                    : RuleDBAtomEntry.Comparision.MoreThanEqual; 
                                if (float.TryParse(splitRange[1], out float right))
                                {
                                    RuleDBAtomEntry rangeEnd = new RuleDBAtomEntry();
                                    rangeEnd.compareMethod = exclusiveRight
                                        ? RuleDBAtomEntry.Comparision.LessThan
                                        : RuleDBAtomEntry.Comparision.LessThanEqual;
                                    rangeEnd.factName = atomEntry.factName;
                                    rangeEnd.matchValue = right;
                                    rangeEnd.compareType= FactValueType.Value;
                            
                                    currentRule.atoms.Add(atomEntry);
                                    currentRule.atoms.Add(rangeEnd);
                                }
                            }
                            
                        }
                        else
                        {
                            atomEntry.compareType = FactValueType.String;
                            if (float.TryParse(valueMatch, out float floatVal))
                            {
                                atomEntry.matchValue = floatVal;
                                atomEntry.compareType = FactValueType.Value;
                            }
                            else
                            {
                                atomEntry.matchString = valueMatch;
                            }
                            currentRule.atoms.Add(atomEntry);
                        }
                    }

                    break;
                }
            }
        }
    }
}
#endif