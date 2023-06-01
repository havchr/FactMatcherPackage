using System.Collections.Generic;
using UnityEngine.TestTools;
using System.Collections;
using System.Reflection;
using NUnit.Framework;
using FactMatching;
using UnityEngine;
using System.IO;
using System;

public class DocumentParser_Test
{
    [Test]
    public void ParsingDefaultDocumentation_TestSimpleIsNotNullAndNoProblems()
    {
        ProblemReporting problems = new();
        List<DocumentEntry> documentation = RuleDocumentationParser.GenerateFromText(ref problems, DocTestInfoLoader.GetDefaultDocFileAsTextAsset());
        Assert.IsFalse(problems.ContainsErrorOrWarning());
        Assert.IsNotNull(documentation);
    }

    [Test]
    public void FactInDocumentFindingCanBe_TestSimple()
    {
        List<string> factCanBeList = new()
        {
            "TestFactCanBe",
        };

        FactInDocument factInDocument = new()
        {
            FactName = "Test",
            LineNumber = 1,
            FactSummary = "TestSummary",
        };
        DocumentEntry doc = new()
        {
            Facts = new()
            {
                factInDocument,
            },
        };
        
        FactInDocument foundFactInDocument = new(doc.GetFactInDocumentByName(factInDocument.FactName));

        DocumentEntry foundDoc = new()
        {
            Facts = new()
            {
                foundFactInDocument,
            },
        };
        List<DocumentEntry> foundDocs = new()
        {
            foundDoc,
        };
        Assert.IsFalse(DocTestInfoLoader.CanFactInDocsBe(foundDocs, factInDocument.FactName, factCanBeList[0]));
        foundDocs[0].Facts[0].FactCanBe = factCanBeList;
        Assert.IsTrue(DocTestInfoLoader.CanFactInDocsBe(foundDocs, factInDocument.FactName, factCanBeList[0]));
    }

    [Test]
    public void MassiveDocumentEntryList_TestCreatingDocumentEntry_1000()
    {
        DocumentEntry doc = DocTestInfoLoader.GenerateDocumentEntryOfSize(1000);
        Assert.IsNotNull(doc);
    }

    [Test]
    public void TestingTurningDocToTextFileAndParsing100000_TestAdvanced()
    {
        List<DocumentEntry> docs = new()
        {
            new(DocTestInfoLoader.GenerateDocumentEntryOfSize(100000))
        };
        Assert.IsNotNull(docs);

        string docToRuleScriptString = "";
        foreach (var doc in docs)
        {
            docToRuleScriptString += '\n' + doc.ToDocumentationString();
        }
        docToRuleScriptString.Trim();

        ProblemReporting problems = new();
        List<DocumentEntry> generatedDocs = RuleDocumentationParser.GenerateFromText(ref problems, new(docToRuleScriptString));
        Assert.IsEmpty(problems.ToString(), problems.ToString());
        Assert.IsNotNull(generatedDocs, $"GeneratedDoc is null");
        Assert.IsTrue(generatedDocs.Count > 0);

        string generatedDocText = "";
        foreach (var doc in generatedDocs)
        {
            generatedDocText += '\n' + doc.ToDocumentationString();
        }
        generatedDocText.Trim();

        Assert.IsTrue(docToRuleScriptString == generatedDocText, "Trying to see if we can manage to match a string from manually generated documentEntry list and then generate a documentEntryList from the manually generated one");
    }

    [Test]
    public void GenerateLargeDocumentationFile()
    {
        List<DocumentEntry> docs = new()
        {
            new(DocTestInfoLoader.GenerateDocumentEntryOfSize(100000))
        };
        Assert.IsNotNull(docs);

        string docToRuleScriptString = "";
        foreach (var doc in docs)
        {
            docToRuleScriptString += '\n' + doc.ToDocumentationString();
        }
        docToRuleScriptString.Trim();

        const string testDocUnityPath = "/Packages/no.agens.factmatcher/Tests/Editor/DocumentTests/MassiveDocumentation.txt";
        string fullPath = Environment.CurrentDirectory + testDocUnityPath;

        File.WriteAllText(fullPath, docToRuleScriptString);
    }

    [Test]
    public void ParseMassiveDocumentationTextAsset100000Facts_LongTestTimeMassiveTest()
    {
        TextAsset massiveDocTextAsset = DocTestInfoLoader.massiveDocTextAsset;

        ProblemReporting problems = new();
        List<DocumentEntry> generatedDocs = RuleDocumentationParser.GenerateFromText(ref problems, massiveDocTextAsset);
        Assert.IsNotEmpty(generatedDocs);
    }

    [Test]
    public void TestingTurningDocToTextFileAndParsing10000_TestAdvanced()
    {
        List<DocumentEntry> docs = new()
        {
            new(DocTestInfoLoader.GenerateDocumentEntryOfSize(10000))
        };
        Assert.IsNotNull(docs);

        string docToRuleScriptString = "";
        foreach (var doc in docs)
        {
            docToRuleScriptString += '\n' + doc.ToDocumentationString();
        }
        docToRuleScriptString.Trim();

        ProblemReporting problems = new();
        List<DocumentEntry> generatedDocs = RuleDocumentationParser.GenerateFromText(ref problems, new(docToRuleScriptString));
        Assert.IsEmpty(problems.ToString(), problems.ToString());
        Assert.IsNotNull(generatedDocs, $"GeneratedDoc is null");
        Assert.IsTrue(generatedDocs.Count > 0);

        string generatedDocText = "";
        foreach (var doc in generatedDocs)
        {
            generatedDocText += '\n' + doc.ToDocumentationString();
        }
        generatedDocText.Trim();

        Assert.IsTrue(docToRuleScriptString == generatedDocText, "Trying to see if we can manage to match a string from manually generated documentEntry list and then generate a documentEntryList from the manually generated one");
    }

    [Test]
    public void TestingIfDefaultDocumentationEqualsExpectedDocumentation_TestSimpleEquals()
    {
        List<DocumentEntry> expectedDocs = new()
        {
            new()
            {
                DocumentName = "rule_name",
                DocID = 0,
                Summary = "As much text as you want to, until you reach .. on a single line, which indicates the end\nof free form text.",
                Facts = new()
                {
                    new()
                    {
                        FactName = "player.age",
                        FactID = 0,
                        LineNumber = 10,
                        FactSummary = "as much text as you want here.",
                        IgnoreNumber = false,
                        FactCanBe = null,
                    },
                    new()
                    {
                        FactName = "player.name",
                        LineNumber = 19,
                        FactSummary = "this is player.name that can link names into a sequence, like Johnny, Bob, etc.\nrelated to player.name",
                        FactCanBe = new()
                        {
                            "Johnny Lemon",
                            "Bob Jonson",
                        },
                        IgnoreNumber = false,
                        FactID = 1,
                    },
                    new()
                    {
                        FactName = "player.height",
                        LineNumber = 30,
                        FactSummary = "as much text as you want here.",
                        FactCanBe = null,
                        IgnoreNumber = false,
                        FactID = 2,
                    },
                    new()
                    {
                        FactName = "player.health",
                        LineNumber = 34,
                        FactSummary = "as much text as you want here.",
                        FactCanBe = null,
                        IgnoreNumber = false,
                        FactID = 3,
                    },
                    new()
                    {
                        FactName = "player.street_smart",
                        LineNumber = 38,
                        FactSummary = "as much text as you want here.",
                        FactCanBe = null,
                        IgnoreNumber = false,
                        FactID = 4,
                    },
                    new()
                    {
                        FactName = "player.intelligence_level",
                        LineNumber = 42,
                        FactSummary = "as much text as you want here.",
                        FactCanBe = null,
                        IgnoreNumber = false,
                        FactID = 5,
                    },
                },
                StartLine = 2,
                TextFile = DocTestInfoLoader.GetDefaultDocFileAsTextAsset(),
            },
        };

        ProblemReporting problems = new();
        List<DocumentEntry> documentation = RuleDocumentationParser.GenerateFromText(ref problems, DocTestInfoLoader.GetDefaultDocFileAsTextAsset());
        Assert.IsFalse(problems.ContainsErrorOrWarning());
        Assert.IsNotNull(documentation);
    }
}
