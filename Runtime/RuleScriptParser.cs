using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace FactMatching
{
    public class RuleScriptParser  
    {
        /*
         * This file parses RuleScripts for the FactMatcher system.
         * Given a file that complies to the RuleScript syntax, it works,
         * but the code is very winding and not that robust in terms of
         * readability, parsing speed and communicating parsing errors.
         *
         * For the future refactoring this code would be nice.
         */
        
        public enum RuleScriptParserEnum
        {
            ParsingRule,ParsingResponse,ParsingFactWrite,LookingForRule,ParsingPayloadObject,ParsingTemplate
        }
        
        public enum RuleScriptParserKeywordEnum
        {
            KeywordEND,KeywordRESPONSE,KeywordWRITE,KeywordPAYLOAD,NoKeyword,KeywordTEMPLATE,KeywordTEMPLATE_END,
        }

        static private Dictionary<string, RuleScriptParserKeywordEnum> _keywordEnums;
        const string template_keyword = ".template";
        private static int _lineNumber = 0;
        private static int _templateLineNumber = 0;

        static RuleScriptParser()
        {
            _keywordEnums = new Dictionary<string, RuleScriptParserKeywordEnum>();
            _keywordEnums[".then write"] = RuleScriptParserKeywordEnum.KeywordWRITE;
            _keywordEnums[".end"] = RuleScriptParserKeywordEnum.KeywordEND;
            _keywordEnums[".then response"] = RuleScriptParserKeywordEnum.KeywordRESPONSE;
            _keywordEnums[".then payload"] = RuleScriptParserKeywordEnum.KeywordPAYLOAD;
            _keywordEnums[template_keyword] = RuleScriptParserKeywordEnum.KeywordTEMPLATE;
            _keywordEnums[".template_end"] = RuleScriptParserKeywordEnum.KeywordTEMPLATE_END;
        }
        RuleScriptParserKeywordEnum LookForKeywordInLine(string line)
        {
            var lineSplit = line.Trim().ToLower().Split('=');
            var key = lineSplit[0].Trim();
            if (_keywordEnums.ContainsKey(key))
            {
                return _keywordEnums[key];
            }
            return RuleScriptParserKeywordEnum.NoKeyword;
        }
        
        
        public void GenerateFromText(string text,List<RuleDBEntry> rules,ref int factID,ref Dictionary<string,int> addedFactIDNames,ref int ruleID,string folderPath)
        {

            RuleScriptParserEnum state = RuleScriptParserEnum.LookingForRule;
            Dictionary<string,List<RuleDBFactTestEntry>> parsedFactTests = new Dictionary<string, List<RuleDBFactTestEntry>>();
            RuleDBEntry currentRule = null;
            StringBuilder payload = new StringBuilder();
            ScriptableObject payloadObject = null;
            string templatePath = "";
            Dictionary<string,string> templateArguments = new Dictionary<string, string>();
            List<RuleDBFactTestEntry> derivedFactTests = new List<RuleDBFactTestEntry>();
            
            //If a RuleScript references a template file, we replace the currentReader with reading from the
            //template before returning the currentReader to the original reader
            System.IO.StringReader originalReader = new System.IO.StringReader(text);
            System.IO.StringReader currentReader = originalReader;
            _lineNumber = 0;
            _templateLineNumber = 0;
            try
                //using (System.IO.StringReader reader = new System.IO.StringReader(text))
            {
                string line;
                int orGroupID = 0;
                while ((line = currentReader.ReadLine()) != null || currentReader != originalReader)
                {
                    if (line == null && currentReader != originalReader)
                    {
                        if (currentReader != null)
                        {
                            currentReader.Dispose(); 
                        }
                        currentReader = originalReader;
                        continue;
                    }

                    if (currentReader == originalReader)
                    {
                        _lineNumber++;
                    }
                    else
                    {
                        _templateLineNumber++;
                    }
                    //-- in ruleScript is a comment , except if we are parsing a response..
                    if (state != RuleScriptParserEnum.ParsingResponse)
                    {
                        if (line.StartsWith("--"))
                        {
                            continue;
                        }
                    }

                    if (state == RuleScriptParserEnum.ParsingTemplate)
                    {
                        var keyword = LookForKeywordInLine(line);
                        if (keyword != RuleScriptParserKeywordEnum.KeywordTEMPLATE_END)
                        {
                            if (line.StartsWith("%"))
                            {

                                var templateVariable = line.Split("=")[0].Trim();
                                var argument = line.Split("=")[1].Trim();
                                templateArguments[templateVariable] = argument;
                            }
                        }
                        else
                        {
                            currentReader = HandleTheTemplateFile(templatePath, templateArguments);
                            state = RuleScriptParserEnum.LookingForRule;
                            continue;
                        }
                    }

                    if (state == RuleScriptParserEnum.LookingForRule)
                    {
                        var keyword = LookForKeywordInLine(line);
                        var noKeyword = keyword == RuleScriptParserKeywordEnum.NoKeyword;
                        if (keyword == RuleScriptParserKeywordEnum.KeywordTEMPLATE)
                        {
                            templatePath = folderPath + line.Split('=')[1].Trim();
                            state = RuleScriptParserEnum.ParsingTemplate;
                            templateArguments = new Dictionary<string, string>();
                            _templateLineNumber = 0;
                        }

                        if (noKeyword && line.Length > 0 && line[0] == '.')
                        {
                            derivedFactTests.Clear();
                            //Todo - Rules should start with .
                            //Should give error if not the case... 
                            state = RuleScriptParserEnum.ParsingRule;
                            line = line.Trim().Split(' ')[0];
                            var ruleNames = line.Split('.');
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
                                for (int i = 1; i < lastIndex; i++)
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
                                    if (parsedFactTests.ContainsKey(derived.ToString()))
                                    {
                                        foundDerived = true;
                                    }
                                }

                                lastIndex--;
                                //Debug.Log($"derived is {derived} and foundDerived is {foundDerived} and lastIndex {lastIndex} and finalName is {finalName}");
                            }

                            currentRule = new RuleDBEntry {factTests = new List<RuleDBFactTestEntry>(), factWrites = new List<RuleDBFactWrite>(), ruleName = finalName.ToString()};

                            //Debug.Log($"Adding factTests from derived {derived}");
                            //Grab factTests from derived
                            if (derived.Length > 0)
                            {
                                if (parsedFactTests.ContainsKey(derived.ToString()))
                                {
                                    foreach (var factTest in parsedFactTests[derived.ToString()])
                                    {

                                        //Debug.Log($"for rule {currentRule.ruleName} - Adding factTest {factTest.factName} from derived {derived}");
                                        //To ensure proper serialization and avoid bugs, we make a new copy of factTest
                                        RuleDBFactTestEntry copyFactTest = new RuleDBFactTestEntry(factTest);
                                        derivedFactTests.Add(copyFactTest);
                                        currentRule.factTests.Add(copyFactTest);
                                    }
                                }
                            }
                        }
                    }

                    if (state == RuleScriptParserEnum.ParsingRule)
                    {
                        ParseFactTests(line, currentRule, ref orGroupID,ref derivedFactTests);
                        switch (LookForKeywordInLine(line))
                        {
                            case RuleScriptParserKeywordEnum.KeywordWRITE:
                                state = RuleScriptParserEnum.ParsingFactWrite;
                                break;
                            case RuleScriptParserKeywordEnum.KeywordRESPONSE:
                                state = RuleScriptParserEnum.ParsingResponse;
                                payload.Clear();
                                break;
                            case RuleScriptParserKeywordEnum.KeywordPAYLOAD:
                                state = RuleScriptParserEnum.ParsingPayloadObject;
                                payload.Clear();
                                break;
                        }

                    }
                    else if (state == RuleScriptParserEnum.ParsingFactWrite)
                    {

                        ParseFactWrites(line, currentRule);
                        switch (LookForKeywordInLine(line))
                        {
                            case RuleScriptParserKeywordEnum.KeywordRESPONSE:
                                state = RuleScriptParserEnum.ParsingResponse;
                                payload.Clear();
                                break;
                            case RuleScriptParserKeywordEnum.KeywordPAYLOAD:
                                state = RuleScriptParserEnum.ParsingPayloadObject;
                                payload.Clear();
                                break;
                        }
                    }
                    else if (state == RuleScriptParserEnum.ParsingPayloadObject)
                    {
                        var keyword = LookForKeywordInLine(line);
                        if (keyword != RuleScriptParserKeywordEnum.NoKeyword)
                        {
                            Debug.Log($"payload is {payload} trying to parse that as payload object");
                            //Try to load resource from payload into payloadObject
                            payloadObject = Resources.Load<ScriptableObject>(payload.ToString());
                            payload.Clear();

                            switch (keyword)
                            {
                                case RuleScriptParserKeywordEnum.KeywordWRITE:
                                    state = RuleScriptParserEnum.ParsingFactWrite;
                                    break;
                                case RuleScriptParserKeywordEnum.KeywordRESPONSE:
                                    state = RuleScriptParserEnum.ParsingResponse;
                                    break;
                            }
                        }
                        else
                        {
                            payload.Append(line.Trim());
                        }
                    }
                    else if (state == RuleScriptParserEnum.ParsingResponse)
                    {


                        var keyword = LookForKeywordInLine(line);
                        if (keyword == RuleScriptParserKeywordEnum.KeywordEND)
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
                                SetFactID(currentRule, ref addedFactIDNames, ref factID, ruleID);
                                currentRule.RuleID = ruleID;
                                ruleID++;
                                rules.Add(currentRule);
                            }

                            //adding all our factTests into the factTest array for deriving to work..
                            foreach (var factTest in currentRule.factTests)
                            {
                                //Debug.Log($"Adding factTest {factTest.factName} to ParsedFactTests with key {currentRule.ruleName}");
                                if (!parsedFactTests.ContainsKey(currentRule.ruleName))
                                {
                                    parsedFactTests[currentRule.ruleName] = new List<RuleDBFactTestEntry>();
                                }

                                parsedFactTests[currentRule.ruleName].Add(factTest);
                            }
                        }
                        else
                        {
                            payload.Append(line);
                        }
                    }

                }
            }
            finally
            {
                if (originalReader!= null)
                {
                   originalReader.Dispose();
                }
                if (originalReader!= null)
                {
                   originalReader.Dispose();
                }
            }

            //Sort rules on orGroupRuleID so we can check orGroups sequentially.
            foreach (var rule in rules)
            {
                rule.factTests.Sort((factTest1, factTest2) => factTest1.orGroupRuleID - factTest2.orGroupRuleID);
            }
        }

        private StringReader HandleTheTemplateFile(string templatePath, Dictionary<string, string> templateArguments)
        {
            StringBuilder builder = new StringBuilder();
            using (FileStream fs = File.Open(templatePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (BufferedStream bs = new BufferedStream(fs))
            using (StreamReader sr = new StreamReader(bs))
            {
                string line;
                while ((line = sr.ReadLine()) != null)
                {
                    if (line.StartsWith("--"))
                    {
                        continue;
                    }
                    foreach (var keyVal in templateArguments)
                    {
                        line = line.Replace(keyVal.Key, keyVal.Value);
                        
                    }
                    builder.AppendLine(line);
                }
            }
            return new StringReader(builder.ToString());
        }

        private static void SetFactID(RuleDBEntry currentRule, ref Dictionary<string,int> addedFactIDNames, ref int factID, int ruleID)
        {
            
            if(currentRule.factTests!=null)
            foreach (var factTest in currentRule.factTests)
            {
                if (!addedFactIDNames.ContainsKey(factTest.factName))
                {
                    factTest.factID = factID;
                    addedFactIDNames[factTest.factName] = factID;
                    factID++;
                }
                else
                {
                    factTest.factID = addedFactIDNames[factTest.factName];
                }
                //Debug.Log($"for fact {factTest.factName} for rule {currentRule.ruleName} we set ruleOwnerID {ruleID}");
                factTest.ruleOwnerID = ruleID;
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
                ("(+=)", RuleDBFactWrite.WriteMode.IncrementByOtherFactValue),
                ("(-=)", RuleDBFactWrite.WriteMode.SubtractByOtherFactValue),
                ("(=)", RuleDBFactWrite.WriteMode.SetToOtherFactValue),
                
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

                            if (
                                factWrite.writeMode == RuleDBFactWrite.WriteMode.SetValue
                                || factWrite.writeMode == RuleDBFactWrite.WriteMode.IncrementValue
                                || factWrite.writeMode == RuleDBFactWrite.WriteMode.SubtractValue
                            )
                            {
                               Debug.LogError("Using wrong operand for string. Only = supported, or (=) (+=) or (-=)"); 
                            }
                            factWrite.writeString = valueMatch;
                        }
                        currentRule.factWrites.Add(factWrite);
                    }

                    break;
                }
            }
            
        }

        private static void ParseFactTests(string line, RuleDBEntry currentRule, ref int orGroupID, ref List<RuleDBFactTestEntry> derived)
        {
            
            var operands = new List<(string, RuleDBFactTestEntry.Comparision)>
            {
                (">", RuleDBFactTestEntry.Comparision.MoreThan),
                (">=", RuleDBFactTestEntry.Comparision.MoreThanEqual),
                ("<", RuleDBFactTestEntry.Comparision.LessThan),
                ("<=", RuleDBFactTestEntry.Comparision.LessThanEqual),
                ("=", RuleDBFactTestEntry.Comparision.Equal),
                ("range", RuleDBFactTestEntry.Comparision.Range),
                ("!", RuleDBFactTestEntry.Comparision.NotEqual)
            };
            //Operands with multiple characters must be matched prior to operands of lower character..
            //or else , >= would first be treated as > operand ... 
            operands.Sort((s, s1) => s1.Item1.Length - s.Item1.Length);
            foreach (var operand in operands)
            {
                if (line.Trim().Contains(operand.Item1,StringComparison.InvariantCultureIgnoreCase))
                {
                    var splits = Regex.Split(line, operand.Item1, RegexOptions.IgnoreCase).Where(s => s.Length > 0).ToArray();
                    //var splits = line.Split(new[] {operand.Item1}, StringSplitOptions.RemoveEmptyEntries);
                    if (splits.Length != 2)
                    {
                        Debug.LogError($"Error case - Operand split trouble , lineNumber = {_lineNumber} and templateLineNumber {_templateLineNumber}");
                    }
                    else
                    {
                        RuleDBFactTestEntry factTestEntry = new RuleDBFactTestEntry();
                        factTestEntry.compareMethod = operand.Item2;
                        var factNameOrLogicCandidate = splits[0].Trim();
                        
                        var startsWithQuestion = factNameOrLogicCandidate.StartsWith("?");
                        var startsWithIF = factNameOrLogicCandidate.StartsWith("IF");
                        var startsWithOR = factNameOrLogicCandidate.StartsWith("OR");
                        if (startsWithOR || startsWithIF)
                        {
                            if (startsWithIF)
                            {
                                orGroupID++;
                            }
                            factTestEntry.factName = factNameOrLogicCandidate.Remove(0,2).Trim();
                            factTestEntry.orGroupRuleID = orGroupID;
                        }
                        else
                        {
                            factTestEntry.factName = splits[0].Trim();
                            factTestEntry.orGroupRuleID = -1;
                        }
                        factTestEntry.isStrict = !startsWithQuestion;
                        if (startsWithQuestion)
                        {
                            factTestEntry.factName = factTestEntry.factName.Trim('?');
                        }
                        var valueMatch = splits[1].Trim();
                        
                        //If we have added a rule - from derived - but are overwriting that fact-query , then delete the 
                        //derived rule , or else we would get two conflicting rule atoms in our rule...
                        if (derived.Any(entry => entry.factName.Equals(factTestEntry.factName)))
                        {
                            currentRule.factTests.Remove(currentRule.factTests.Find(entry => entry.factName.Equals(factTestEntry.factName)));
                            derived.Remove(derived.Find(entry => entry.factName.Equals(factTestEntry.factName)));
                        }

                        if (factTestEntry.compareMethod == RuleDBFactTestEntry.Comparision.Range)
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
                                factTestEntry.matchValue = left;
                                factTestEntry.compareType = FactValueType.Value;
                                factTestEntry.compareMethod = exclusiveLeft
                                    ? RuleDBFactTestEntry.Comparision.MoreThan
                                    : RuleDBFactTestEntry.Comparision.MoreThanEqual; 
                                if (float.TryParse(splitRange[1], out float right))
                                {
                                    RuleDBFactTestEntry rangeEnd = new RuleDBFactTestEntry();
                                    rangeEnd.compareMethod = exclusiveRight
                                        ? RuleDBFactTestEntry.Comparision.LessThan
                                        : RuleDBFactTestEntry.Comparision.LessThanEqual;
                                    rangeEnd.factName = factTestEntry.factName;
                                    rangeEnd.matchValue = right;
                                    rangeEnd.compareType= FactValueType.Value;
                            
                                    currentRule.factTests.Add(factTestEntry);
                                    currentRule.factTests.Add(rangeEnd);
                                }
                            }
                            
                        }
                        else
                        {
                            factTestEntry.compareType = FactValueType.String;
                            if (float.TryParse(valueMatch, out float floatVal))
                            {
                                factTestEntry.matchValue = floatVal;
                                factTestEntry.compareType = FactValueType.Value;
                            }
                            else
                            {
                                factTestEntry.matchString = valueMatch;
                            }
                            currentRule.factTests.Add(factTestEntry);
                        }
                    }

                    break;
                }
            }
        }
    }
}
