using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using System;
using UnityEditor.PackageManager;

[Serializable]
public class ProblemEntry
{
    public enum ProblemTypes { Error, Warning }

    public ProblemTypes ProblemType;
    public TextAsset File;
    public int LineNumber;
    public string ProblemMessage;
    public Exception Exception;

    public bool IsError()
    { return ProblemType == ProblemTypes.Error; }

    public bool IsWarning()
    { return ProblemType == ProblemTypes.Warning; }
    
    public override string ToString()
    {
        return
            $"{ProblemType} occurred {(File ? $"in the file:  {File.name},{(LineNumber > 0 ? $" at line: {LineNumber}," : "")}" : string.Empty)} " +
            $"with the message:\n{ProblemMessage}" +
            $"{(Exception == null ? string.Empty : $"\nException message: {Exception}")}";
    }
}

public partial class ProblemReporting
{
    private static readonly List<ProblemEntry> problems = new();

    public void ClearList()
    { problems?.Clear(); }

    public void AddNewProblem(ProblemEntry problemEntry)
    { problems.Add(problemEntry); }

    public void AddNewProblemList(List<ProblemEntry> problemEntrys)
    { problems.AddRange(problemEntrys); }

    public void ReportNewProblem(string problemMessage, TextAsset file, int lineNumber, ProblemEntry.ProblemTypes problemType, Exception Exception = null)
    { problems.Add(new() { File = file, LineNumber = lineNumber, ProblemMessage = problemMessage, Exception = Exception, ProblemType = problemType }); }

    public void ReportNewError(string problemMessage, TextAsset file, int lineNumber,
        Exception Exception = null, ProblemEntry.ProblemTypes problemType = ProblemEntry.ProblemTypes.Error)
    { problems.Add(new() { ProblemMessage = problemMessage, File = file, LineNumber = lineNumber, Exception = Exception, ProblemType = problemType }); }

    public void ReportNewWarning(string problemMessage, TextAsset file, int lineNumber,
        Exception Exception = null, ProblemEntry.ProblemTypes problemType = ProblemEntry.ProblemTypes.Warning)
    { problems.Add(new() { File = file, LineNumber = lineNumber, ProblemMessage = problemMessage, Exception = Exception, ProblemType = problemType }); }

    public bool ContainsError(List<ProblemEntry> problemEntries = null)
    {
        problemEntries ??= problems;
        foreach (var problem in problemEntries)
        {
            if (problem.IsError())
            {
                return true;
            }
        }
        return false;
    }

    public bool ContainsWarning(List<ProblemEntry> problemEntries = null)
    {
        problemEntries ??= problems;
        foreach (var problem in problemEntries)
        {
            if (problem.IsWarning())
            {
                return true;
            }
        }
        return false;
    }

    public bool ContainsErrorOrWarning(List<ProblemEntry> problemEntries = null)
    {
        problemEntries ??= problems;
        foreach (var problem in problemEntries)
        {
            if (problem.IsError() || problem.IsWarning())
            {
                return true;
            }
        }
        return false;
    }

    public List<ProblemEntry> ToList()
    {
        List<ProblemEntry> listOfProblems = new(problems);
        problems.Clear();
        return listOfProblems;
    }

    public override string ToString()
    {
        List<ProblemEntry> listOfProblems = new(problems);
        problems?.Clear();
        string listOfProblemsString = "";
        foreach (var problem in listOfProblems)
        {
            listOfProblemsString += problem;
        }
        return listOfProblemsString;
    }
}

public partial class ProblemReporting
{
    public bool ContainsErrors() { return CheckForErrors(out _, out _); }
    public bool ContainsErrors(out string errorsString) { return CheckForErrors(out _, out errorsString); }
    public bool ContainsErrors(out int errors) { return CheckForErrors(out errors, out _); }
    public bool ContainsErrors(out int errors, out string errorsString) { return CheckForErrors(out errors, out errorsString); }

    private static bool CheckForErrors(out int errors, out string errorsString)
    {
        errors = 0;
        errorsString = string.Empty;
        for (int i = 0; i < problems.Count; i++)
        {
            ProblemEntry problem = problems[i];
            if (problem.IsError())
            {
                errorsString += "\n\n" + problem.ToString();
                errors++;
                problems.RemoveAt(i);
            }
        }
        return errors > 0;
    }

    public bool ContainsWarnings() { return CheckForWarnings(out _, out _); }
    public bool ContainsWarnings(out int warnings) { return CheckForWarnings(out warnings, out _); }
    public bool ContainsWarnings(out string warningsString) { return CheckForWarnings(out _, out warningsString); }
    public bool ContainsWarnings(out int warnings, out string warningsString) { return CheckForWarnings(out warnings, out warningsString); }

    private static bool CheckForWarnings(out int warnings, out string warningsString)
    {
        warnings = 0;
        warningsString = string.Empty;
        for (int i = 0; i < problems.Count; i++)
        {
            ProblemEntry problem = problems[i];
            if (problem.IsWarning())
            {
                warningsString += "\n\n" + problem.ToString();
                warnings++;
                problems.RemoveAt(i);
            }
        }
        return warnings > 0;
    }

    public bool ContainsWarningsAndErrors()
    { return CheckForWarnings(out _, out _) & CheckForErrors(out _, out _); }
    public bool ContainsWarningsAndErrors(out int errors, out int warnings)
    { return CheckForWarnings(out warnings, out _) & CheckForErrors(out errors, out _); }
    public bool ContainsWarningsAndErrors(out string errorsString, out string warningsString)
    { return CheckForWarnings(out _, out warningsString) & CheckForErrors(out _, out errorsString); }
    public bool ContainsWarningsAndErrors(out int errors, out string errorsString, out int warnings, out string warningsString)
    { return CheckForWarnings(out warnings, out warningsString) & CheckForErrors(out errors, out errorsString); }
}
