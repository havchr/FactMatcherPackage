﻿using System.Collections.Generic;
using System.IO;
using System.Linq;
using System;
using UnityEngine;

namespace FactMatching
{
    public class RuleDocumentationParser
    {
        public enum RuleDocParserKeyword
        {
            EndOfFile, NoKeyword, KeywordDOCS, KeywordSUMMERY_END, KeywordFACT, KeywordFACT_CAN_BE, KeywordEND,
        }

        private static readonly Dictionary<string, RuleDocParserKeyword> _keywordEnums;

        static RuleDocumentationParser()
        {
            _keywordEnums = new Dictionary<string, RuleDocParserKeyword>
            {
                [".DOCS"] = RuleDocParserKeyword.KeywordDOCS,
                [".."] = RuleDocParserKeyword.KeywordSUMMERY_END,
                [".FACT"] = RuleDocParserKeyword.KeywordFACT,
                [".IT CAN BE"] = RuleDocParserKeyword.KeywordFACT_CAN_BE,
                [".END"] = RuleDocParserKeyword.KeywordEND
            };
        }

        RuleDocParserKeyword LookForKeywordInLine(string line)
        {
            line = line.Trim();
            if (line.StartsWith('.'))
            {
                if (_keywordEnums.ContainsKey(line.ToUpper()))
                {
                    return _keywordEnums[line.ToUpper()];
                }

                string[] lineSplit = line.ToUpper().Split('=', ' ');
                string key = lineSplit[0].Trim();
                if (_keywordEnums.ContainsKey(key))
                {
                    RuleDocParserKeyword keywordEnum = _keywordEnums[key];
                    if (parsingDoc && keywordEnum == RuleDocParserKeyword.KeywordDOCS)
                    {
                        localProblems.ReportNewWarning($"Detected .DOCS {string.Join(" ", lineSplit.Skip(1)).Trim()} before .END are you missing a .END?" +
                            $"\nThis can result in facts from the second .DOCS is put in the already started DOCS",
                            currentFile, _lineNumber);
                    }
                    return keywordEnum;
                } 
            }
            return RuleDocParserKeyword.NoKeyword;
        }

        private bool IsKeywordInLine(string line, RuleDocParserKeyword lookFor, out string restOfLine)
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

        private RuleDocParserKeyword WhatIsNextParser()
        {
            string originalString = stringReader.ReadToEnd();
            stringReader = new(originalString);
            StringReader newReader = new(originalString);

            while (newReader.Peek() != -1)
            {
                string line = newReader.ReadLine().ToUpper();

                return LookForKeywordInLine(line);
            }
            return RuleDocParserKeyword.EndOfFile;
        }

        private StringReader stringReader;
        private int _lineNumber;
        private string NextLine()
        {
            string line;
            while ((line = stringReader.ReadLine()) != null && (line.TrimStart().StartsWith("--") || line == ""))
            {
                ++_lineNumber;
            }
            _lineNumber++;
            return line;
        }

        private RuleScriptParsingProblems localProblems;
        private TextAsset currentFile;
        public bool parsingDoc = false;
        public List<DocumentEntry> GenerateFromText(ref RuleScriptParsingProblems problems, TextAsset file)
        {
            List<DocumentEntry> entries = new();
            stringReader = new(file.text);
            currentFile = file;
            localProblems = problems;

            try
            {
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
                            Summary = ParseSummary(),
                            TextFile = currentFile,
                            Facts = GetFactsInDocument(),
                        });
                        parsingDoc = false;
                    }
                }
            }
            catch (Exception e)
            {
                //localProblems.ReportNewError("Failed to generate from given text", file, -1, e);
                localProblems?.ClearList();
                throw e;
            }

            return entries;
        }

        private string ParseSummary()
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
            }
            return summary.TrimStart('\n');
        }

        private List<FactInDocument> GetFactsInDocument()
        {
            List<FactInDocument> facts = new();

            string line;
            RuleDocParserKeyword keywordInLine;
            while ((line = NextLine().Trim()) != null && (keywordInLine = LookForKeywordInLine(line)) != RuleDocParserKeyword.KeywordEND)
            {
                if (keywordInLine == RuleDocParserKeyword.KeywordFACT)
                {
                    var lineSplit = line.Split(' ');
                    facts.Add(new()
                    {
                        FactName = string.Join("_", lineSplit.Skip(1)),
                        FactSummary = ParseSummary(),
                        FactCanBe = GetListOfFactCanBe(),
                    });
                }
            }
            return facts;
        }

        private List<string> GetListOfFactCanBe()
        {
            if (WhatIsNextParser() == RuleDocParserKeyword.KeywordFACT_CAN_BE)
            {
                List<string> canBe = new();
                canBe.AddRange(ParseSummary().Split('\n').ToList());
                return canBe;
            }
            return null;
        }
    }
}