using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;

public class DocTestInfoLoader
{
    public static TextAsset defaultDocAsset = GetDefaultDocFileAsTextAsset();
    public static TextAsset massiveDocTextAsset = GetDefaultDocFileAsTextAsset("/Packages/no.agens.factmatcher/Tests/Editor/DocumentTests/MassiveDocumentation.txt");

    public static TextAsset GetDefaultDocFileAsTextAsset(string testDocUnityPath = "/Packages/no.agens.factmatcher/Tests/Editor/DocumentTests/TestDocument.txt")
    {
        string fullPath = Environment.CurrentDirectory + testDocUnityPath;
        string path = fullPath;
        StreamReader reader = new(path);
        string result = reader.ReadToEnd();
        reader.Close();
        if (result == null || result == "")
        {
            throw new Exception($"Did not manage to read from the text file at path: ({fullPath})");
        }

        return new(result);
    }

    public static bool CanFactInDocsBe(List<DocumentEntry> docs, string factName, string canBe)
    {
        if (factName.StartsWith('_'))
        {
            return true;
        }

        foreach (var doc in docs)
        {
            if (doc.CanFactBe(factName, canBe))
            {
                return true;
            }
        }

        return false;
    }

    public static DocumentEntry GenerateDocumentEntryOfSize(int size)
    {
        System.Random rand = new();
        string[] docNames =
        {
            $"MassiveDocTest_{size}",
        };
        
        DocumentEntry result = new()
        {
            DocumentName = docNames[rand.Next(0, docNames.Length)],
            StartLine = 1,
            DocID = 1,
            Facts = CreateFacts(size),
        };

        return result;
    }

    private static List<FactInDocument> CreateFacts(int size)
    {
        System.Random rand = new();
        List<FactInDocument> facts = new();
        for (int i = 0; i < size; i++)
        {
            bool useFactCanBe = rand.Next(0, 2) < 1;
            facts.Add(CreateFact(i, useFactCanBe));
        }

        return facts;
    }

    private static FactInDocument CreateFact(int factId, bool useFactCanBe)
    {
        System.Random rand = new();
        string[] factUsingCanBeNames =
        {
            "Sting_fact",
            "Name_Fact",
            "Party",
        };

        string[] factNotUsingCanBeNames =
        {
            "Number_fact",
            "Count_Fact",
            "FavoritedNumber",
            "BestNumber",
        };

        if (useFactCanBe)
        {
            string name = factUsingCanBeNames[rand.Next(0, factUsingCanBeNames.Length)] + $"_{factId}";
            return new()
            {
                factName = name,
                FactID = factId,
                IgnoreNumber = false,
                LineNumber = factId + 2,
                FactCanBe = GenerateMassiveFactCanBeList(rand.Next(15, 30)),
            };
        }
        else
        {
            string name = factNotUsingCanBeNames[rand.Next(0, factNotUsingCanBeNames.Length)] + $"_{factId}";
            return new()
            {
                factName = name,
                FactID = factId,
                IgnoreNumber = false,
                LineNumber = factId + 2,
            };
        }
    }

    public static List<string> GenerateMassiveFactCanBeList(int size)
    {
        List<string> result = new();
        System.Random rand = new();
        string[] factCanBe =
        {
            "Apple",
            "App",
            "Amazing",
            "Anthropology",
            "Bok",
            "Bob",
            "Barber",
            "Box",
            "Backseats",
            "Basket",
            "BasketBall",
            "Bowling",
            "Car",
            "Career",
            "Cartel",
            "Cargo",
            "Carnage",
            "Caring"
        };

        for (int i = 0; i < size; i++)
        {
            string factCanBeString = factCanBe[rand.Next(0, factCanBe.Length)] + $"_{i}";
            result.Add(factCanBeString);
        }

        return result;
    }
}
