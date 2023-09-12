using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using System.IO;
using System;

namespace FactMatching
{
    public class RuleDocumentationParser
    {
        public enum RuleDocParserKeyword
        {
            EndOfFile, NoKeyword, KeywordDOCS, KeywordTEMPLATE, KeywordSUMMERY_END, KeywordFACT, KeywordFACT_CAN_BE, KeywordEND,
        }

        public static string RuleDocParserKeywordToString()
        {
            string result = string.Empty;
            foreach (var keywordEnum in _keywordEnums)
            {
                result += $"{keywordEnum.Key}, ";
            }
            return result + "(the keywords are not case sensitive).";
        }

        private static readonly Dictionary<string, RuleDocParserKeyword> _keywordEnums = new()
            {
                [".TEMPLATE"] = RuleDocParserKeyword.KeywordTEMPLATE,
                [".DOCS"] = RuleDocParserKeyword.KeywordDOCS,
                [".DOC"] = RuleDocParserKeyword.KeywordDOCS,
                [".."] = RuleDocParserKeyword.KeywordSUMMERY_END,
                [".FACT"] = RuleDocParserKeyword.KeywordFACT,
                [".IT CAN BE"] = RuleDocParserKeyword.KeywordFACT_CAN_BE,
                [".END"] = RuleDocParserKeyword.KeywordEND
            };

        private static RuleDocParserKeyword LookForKeywordInLine(string line)
        {
            line = line.Trim();
            if (line.StartsWith('.'))
            {
                if (_keywordEnums.ContainsKey(line.ToUpper()))
                {
                    return _keywordEnums[line.ToUpper()];
                }

                string[] lineSplit = line.Split('=', ' ');
                string key = lineSplit[0].Trim().ToUpper();
                if (_keywordEnums.ContainsKey(key))
                {
                    RuleDocParserKeyword keywordEnum = _keywordEnums[key];
                    if (parsingDoc && keywordEnum == RuleDocParserKeyword.KeywordDOCS)
                    {
                        localProblems.ReportNewWarning($"Detected .DOCS {string.Join(" ", lineSplit.Skip(1)).Trim()} before .END are you missing a .END?" +
                            $"\nThis can result in facts from the second .DOCS is put in the already started DOCS",
                            currentFile, _lineNumber);
                    }
                    else if (!templateDoc && keywordEnum == RuleDocParserKeyword.KeywordTEMPLATE)
                    {
                        templateDoc = true;
                        TextAsset templateTextAsset = RuleDocumentationTemplateParser.StartTemplateParser
                            (currentFile, out ProblemReporting problemsDetected);
                        currentFile = templateTextAsset;
                        stringReader = new(templateTextAsset.text);
                        if (problemsDetected.ContainsErrorOrWarning())
                        {
                            localProblems.AddNewProblemList(problemsDetected.ToList());
                        }
                        return RuleDocParserKeyword.NoKeyword;
                    }
                    return keywordEnum;
                }
                else
                {
                    localProblems.ReportNewWarning($"Did not find any keyword from the following line: {line}\n" +
                        $"The available keywords are: {RuleDocParserKeywordToString()}", currentFile, _lineNumber);
                }
            }
            return RuleDocParserKeyword.NoKeyword;
        }

        private static bool IsKeywordInLine(string line, RuleDocParserKeyword lookFor, out string restOfLine)
        {
            line = line.Trim();
            if (LookForKeywordInLine(line) == lookFor)
            {
                string[] lineSplit = line.Split('=', ' ');
                restOfLine = string.Join("_", lineSplit.Skip(1)).Trim();
                return true;
            }
            restOfLine = "";
            return false;
        }

        private static RuleDocParserKeyword WhatIsNextParser()
        {
            string originalString = stringReader.ReadToEnd();
            stringReader = new(originalString);
            StringReader newReader = new(originalString);
            return ReadLineInStringReader(newReader);

            static RuleDocParserKeyword ReadLineInStringReader(StringReader localStringReader)
            {
                string line;
                while ((line = localStringReader.ReadLine()) != null && (line.TrimStart().StartsWith("--") || line == ""))
                { }
                return LookForKeywordInLine(line);
            }
        }

        private static StringReader stringReader;
        private static int _lineNumber = 0;
        private static string NextLine()
        {
            string line;
            while ((line = stringReader.ReadLine()) != null && (line.TrimStart().StartsWith("--") || line == ""))
            {
                ++_lineNumber;
            }
            _lineNumber++;
            return line;
        }

        private static ProblemReporting localProblems;
        private static TextAsset currentFile;
        private static bool parsingDoc = false;
        private static bool templateDoc = false;

        public static List<DocumentEntry> GenerateFromText(ref ProblemReporting problems, TextAsset docFile)
        {
            _lineNumber = 0;
            templateDoc = false;
            parsingDoc = false;
            List<DocumentEntry> entries = new();
            stringReader = new(docFile.text);
            currentFile = docFile;
            localProblems = problems;

            int docID = 0;
            string line;
            while ((line = NextLine()) != null)
            {
                if (line == null)
                {
                    break;
                }

                if (!parsingDoc && IsKeywordInLine(line, RuleDocParserKeyword.KeywordDOCS, out string nameOfDoc))
                {
                    parsingDoc = true;
                    entries.Add(new()
                    {
                        StartLine = _lineNumber,
                        DocumentName = nameOfDoc,
                        DocID = docID++,
                        Summary = ParseSummary(),
                        TextFile = currentFile,
                        Facts = GetFactsInDocument(),
                    });
                    parsingDoc = false;
                }
            }
            problems = localProblems;
            return entries;
        }

        private static string ParseSummary()
        {
            string summary = "";

            string line;
            RuleDocParserKeyword keywordInLine;
            while ((line = NextLine()) != null && (keywordInLine = LookForKeywordInLine(line)) != RuleDocParserKeyword.KeywordSUMMERY_END)
            {
                if (keywordInLine == RuleDocParserKeyword.NoKeyword)
                {
                    summary += '\n' + line.Trim();
                }
                else
                {
                    localProblems.ReportNewWarning($"Unexpected keyword inside of summary: {line.Trim()}", currentFile, _lineNumber);
                }
            }
            return summary.TrimStart('\n');
        }

        private static readonly char ignoreNumSymbol = '#'; 
        private static List<FactInDocument> GetFactsInDocument()
        {
            List<FactInDocument> facts = new();

            int factID = 0;
            string line;
            RuleDocParserKeyword keywordInLine;
            while ((line = NextLine().Trim()) != null && (keywordInLine = LookForKeywordInLine(line)) != RuleDocParserKeyword.KeywordEND)
            {
                if (keywordInLine == RuleDocParserKeyword.KeywordFACT)
                {
                    string[] lineSplit = line.Split(' ');
                    string factName = string.Join("_", lineSplit.Skip(1));

                    FactInDocument fact = new()
                    {
                        factName = factName,
                        LineNumber = _lineNumber,
                        FactID = factID++,
                        FactSummary = ParseSummary(),
                        IgnoreNumber = factName.Contains(ignoreNumSymbol),
                        FactCanBe = GetListOfFactCanBe(out List<string> enumNames),
                    };
                    fact.EnumNames = enumNames;
                    facts.Add(fact);
                }
            }
            return facts;
        }

        private static List<string> GetListOfFactCanBe(out List<string> enumNames)
        {
            enumNames = new List<string>();
            if (WhatIsNextParser() == RuleDocParserKeyword.KeywordFACT_CAN_BE)
            {
                List<string> canBe = new();
                string line;
                RuleDocParserKeyword keywordInLine;
                while ((line = NextLine()) != null && (keywordInLine = LookForKeywordInLine(line)) != RuleDocParserKeyword.KeywordSUMMERY_END)
                {
                    if (line.Contains("C#_ENUM"))
                    {
                        string enumName = line.Trim().Split(' ')[1].Trim();
                        Array enumValues = GetEnumValuesFromEnumName(enumName);
                        if (enumValues != null)
                        {
                            enumNames.Add(enumName);
                            foreach (var enumValue in enumValues)
                            {
                                canBe.Add(enumValue.ToString());
                            }
                        }
                        else
                        {
                            localProblems.ReportNewError
                                ($"Did not find enum of type: {enumName}, please verify that this enum exists in current context.",
                                currentFile, _lineNumber);
                        }
                    }
                    else if (keywordInLine == RuleDocParserKeyword.NoKeyword)
                    {
                        canBe.Add(line.Trim());
                    }
                    else if (keywordInLine != RuleDocParserKeyword.KeywordFACT_CAN_BE)
                    {
                        localProblems.ReportNewWarning($"Unexpected keyword inside of .IT CAN BE: {line.Trim()}", currentFile, _lineNumber);
                    }
                }
                return canBe;
            }
            return null;
        }

        public static Array GetEnumValuesFromEnumName(string enumName)
        {
            System.Reflection.Assembly[] scriptAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            foreach (var assembly in scriptAssemblies)
            {
                if (assembly != null)
                {
                    Type enumType = assembly.GetTypes().FirstOrDefault(t => t.IsEnum && t.Name == enumName);
                    if (enumType != null)
                    {
                        return Enum.GetValues(enumType);
                    }
                }
                else
                {
                    localProblems.ReportNewError($"Could not find Assemblies, (looking for enum of type {enumName}", currentFile, _lineNumber);
                }
            }
            return null;
        }
    }
}
