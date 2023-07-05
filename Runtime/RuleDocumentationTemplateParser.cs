using static FactMatching.RuleDocumentationParser;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Text;
using System.IO;
using System;

namespace FactMatching
{
    public class RuleDocumentationTemplateParser
    {
        private static TextAsset currentFile = new();
        private static readonly ProblemReporting localProblems = new();
        private static readonly Dictionary<string, RuleDocParserKeyword> _keywordEnums;

        static RuleDocumentationTemplateParser()
        {
            _keywordEnums = new Dictionary<string, RuleDocParserKeyword>
            {
                [".TEMPLATE"] = RuleDocParserKeyword.KeywordTEMPLATE,
                [".DOCS"] = RuleDocParserKeyword.KeywordDOCS,
                [".FACT"] = RuleDocParserKeyword.KeywordFACT,
                [".IT CAN BE"] = RuleDocParserKeyword.KeywordFACT_CAN_BE,
                [".."] = RuleDocParserKeyword.KeywordSUMMERY_END,
                [".END"] = RuleDocParserKeyword.KeywordEND
            };
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
                string[] lineSplit = line.Split('=');
                restOfLine = string.Join("", lineSplit[^1]).Trim();
                return true;
            }
            restOfLine = "";
            return false;
        }

           
        public static TextAsset StartTemplateParser(TextAsset thisFile, out ProblemReporting problemsDetected)
        {
            currentFile = thisFile;
            currentFile.name = thisFile.name;
            //todo - https://app.clickup.com/t/85yxerntt (TextAssets loaded from resources will get wrong relative pathing probably)
            string assetFolderPath = "";
            #if UNITY_EDITOR
            assetFolderPath = string.Join("/", AssetDatabase.GetAssetPath(thisFile).Split('/')[..^1]);
            #endif

            _lineNumber = 0;
            List<StringBuilder> stringBuilders = new();
            stringReader = new(thisFile.text);
            try
            {
                List<DocumentTemplate> templates = new();

                string line;
                while ((line = NextLine()) != null)
                {
                    if (IsKeywordInLine(line, RuleDocParserKeyword.KeywordTEMPLATE, out string templateFileName))
                    {
                        DocumentTemplate template = GetTemplate(templateFileName, assetFolderPath);
                        if (template != null)
                        {
                            templates.Add(template);
                        }
                        else
                        {
                           localProblems.ReportNewError($"Could not find template file {templateFileName} in path {assetFolderPath}",thisFile,_lineNumber);
                        }
                    }
                }

                foreach (var template in templates)
                {
                    stringBuilders.Add(template.CreateTemplateStringBuilder());
                }

            }
            catch (Exception)
            {
                localProblems?.ClearList();
                throw;
            }

            string templatedDocumentsText = string.Empty;
            foreach (var stringBuilder in stringBuilders)
            {
                templatedDocumentsText += "\n" + stringBuilder;
            }
            templatedDocumentsText = templatedDocumentsText.Trim();

            problemsDetected = localProblems;
            return new(templatedDocumentsText)
            {
                name = thisFile.name,
            };
        }
            

        private static DocumentTemplate GetTemplate(string templateFileName, string folderPath)
        {
            //todo https://app.clickup.com/t/85yxet4vh DocumentTemplate Files should not need TextAsset and loading from AssetDataBase - just needs to load a file with regular IO | #85yxet4vh
            TextAsset templateFile = null;
            #if UNITY_EDITOR
            templateFile = AssetDatabase.LoadAssetAtPath<TextAsset>($"{folderPath}/{templateFileName}.txt");
            #endif
            if (templateFile == null)
            {
                return null;
            }
            templateFile.name = templateFileName;
            DocumentTemplate template = new()
            {
                templateFile = templateFile,
                templateArguments = GetKeyAndValueFromTemplate(),
            };
            return template;

            static Dictionary<string, string> GetKeyAndValueFromTemplate()
            {
                Dictionary<string, string> templateArguments = new();
                string line;
                RuleDocParserKeyword keywordInLine;
                while ((line = NextLine()) != null && (keywordInLine = LookForKeywordInLine(line)) != RuleDocParserKeyword.KeywordSUMMERY_END)
                {
                    if (keywordInLine == RuleDocParserKeyword.NoKeyword)
                    {
                        string[] splitLine = line.Split('=');
                        templateArguments.Add(splitLine[0].Trim(), splitLine[1].Trim());
                    }
                    else
                    {
                        localProblems.ReportNewWarning($"Found keyword {keywordInLine} when expecting NoKeyword", currentFile, _lineNumber);
                    }
                }
                return templateArguments;
            }
        }

        private class DocumentTemplate
        {
            public TextAsset templateFile;
            public Dictionary<string, string> templateArguments = new();

            public bool IsTemplateKeywordsInLine(string line)
            {
                foreach (string key in templateArguments.Keys)
                {
                    if (!line.Trim().Contains(key.Trim()))
                    {
                        return false;
                    }
                }
                return true;
            }

            public string ReplaceWithTemplateKeyword(string input)
            {
                if (IsTemplateKeywordsInLine(input))
                {
                    foreach (var templateKeyword in templateArguments)
                    {
                        input = input.Replace(templateKeyword.Key.Trim(), templateKeyword.Value.Trim());
                    }
                }

                return input;
            }

            public StringBuilder CreateTemplateStringBuilder()
            {
                StringReader stringReader = new(templateFile.text);
                StringBuilder stringBuilder = new();

                string line;
                bool parsingFact = false;
                while ((line = NextLine(ref stringReader)) != null)
                {
                    RuleDocParserKeyword keyword;
                    if ((keyword = LookForKeywordInLine(line)) == RuleDocParserKeyword.KeywordFACT)
                    {
                        parsingFact = IsTemplateKeywordsInLine(line);
                    }
                    
                    if (parsingFact || keyword == RuleDocParserKeyword.KeywordEND)
                    {
                        stringBuilder.AppendLine(ReplaceWithTemplateKeyword(line));
                    }
                    else if (keyword == RuleDocParserKeyword.KeywordDOCS)
                    {
                        stringBuilder.AppendLine(ReplaceWithTemplateKeyword(line));
                        string docsLine;
                        while ((docsLine = NextLine(ref stringReader)) != null)
                        {
                            stringBuilder.AppendLine(ReplaceWithTemplateKeyword(docsLine));

                            if (LookForKeywordInLine(docsLine) == RuleDocParserKeyword.KeywordSUMMERY_END)
                            {
                                break;
                            }
                        }
                    }
                }
                return stringBuilder;
            }

            private static string NextLine(ref StringReader stringReader)
            {
                string line;
                while ((line = stringReader.ReadLine()) != null && (line.TrimStart().StartsWith("--") || line == ""))
                {
                    ++_lineNumber;
                }
                _lineNumber++;
                return line;
            }

            public override string ToString()
            {
                string result = "DocumentTemplate\n" +
                    $"template file name = {templateFile.name}";

                foreach (var pair in templateArguments)
                {
                    result += $"\n{pair.Key} = {pair.Value}";
                }

                return result.Trim();
            }
        }
    }
}
