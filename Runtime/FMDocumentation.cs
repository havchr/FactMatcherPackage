using System.Collections.Generic;
using FactMatching;
using UnityEngine;

[CreateAssetMenu(fileName = "FMDoc", menuName = "FactMatcher/Documentations", order = 1)]
public class FMDocumentation : ScriptableObject
{
    
    public List<TextAsset> generateDocumentationFrom;
    public List<DocumentEntry> documentations;
    
    public ProblemReporting CreateDocumentations()
    {
        ProblemReporting problems = new();
        //problemList?.ClearList();
        if (generateDocumentationFrom.Count != 0)
        {
            documentations?.Clear();
            foreach (var document in generateDocumentationFrom)
            {
                documentations ??= new();
                documentations?.AddRange(RuleDocumentationParser.GenerateFromText(ref problems, document));
            }

            if (problems.ContainsError())
            {
                documentations?.Clear();
            }
        }
        else
        {
            documentations?.Clear();
            problems.ReportNewError("There is nothing to generate from", null, -1);
        }
        return problems;
    }
}