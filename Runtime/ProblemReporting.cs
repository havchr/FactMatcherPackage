using System.Collections.Generic;
using UnityEngine;
using System;
using FactMatching;

[Serializable]
public class ProblemEntry
{
    public enum ProblemTypes { Error, Warning }

    public string ProblemMessage;
    public int LineNumber;
    public ProblemTypes ProblemType;
    public TextAsset File;
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
    public List<ProblemEntry> GetList() { return problems; }
    private static readonly List<ProblemEntry> problems = new();

    public ProblemReporting()
    { }

    public ProblemReporting(ProblemReporting problemReporting)
    {
        if (problemReporting != null)
        {
            problems.AddRange(problemReporting.ToList());
        }
    }

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

    /// <summary>
    /// Will return a list of problems and clear current problem list
    /// </summary>
    /// <returns>Problem list</returns>
    public List<ProblemEntry> ToList()
    {
        List<ProblemEntry> listOfProblems = new(problems);
        problems.Clear();
        return listOfProblems;
    }

    public bool DebugWarningsAndErrors(string message = "", bool removeProblems = false)
    {
        bool containedWarningsOrErrors = false;

        if (ContainsErrors(removeProblems, out int errors, out string errorsString))
        {
            Debug.LogError($"{(message.IsNullOrWhitespace() ? "" : $"{message} ")}Encounter {errors} error{(errors > 1 ? "s" : "")}.{errorsString}");
            containedWarningsOrErrors = true;
        }
        
        if (ContainsWarnings(removeProblems, out int warnings, out string warningsString))
        {
            Debug.LogWarning($"{(message.IsNullOrWhitespace() ? "":$"{message} ")}Encounter {warnings} warning{(warnings > 1 ? "s" : "")} . {warningsString}");
            containedWarningsOrErrors = true;
        }

        return containedWarningsOrErrors;
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
    public bool ContainsErrors(bool removeErrors) { return CheckForErrors(removeErrors, out _, out _); }
    public bool ContainsErrors(bool removeErrors, out string errorsString) { return CheckForErrors(removeErrors, out _, out errorsString); }
    public bool ContainsErrors(bool removeErrors, out int errors) { return CheckForErrors(removeErrors, out errors, out _); }
    public bool ContainsErrors(bool removeErrors, out int errors, out string errorsString) { return CheckForErrors(removeErrors, out errors, out errorsString); }

    private static bool CheckForErrors(bool removeErrors, out int errors, out string errorsString)
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
                if (removeErrors)
                {
                    problems.RemoveAt(i); 
                }
            }
        }
        return errors > 0;
    }

    public bool ContainsWarnings(bool removeWarning) { return CheckForWarnings(removeWarning, out _, out _); }
    public bool ContainsWarnings(bool removeWarning, out int warnings) { return CheckForWarnings(removeWarning, out warnings, out _); }
    public bool ContainsWarnings(bool removeWarning, out string warningsString) { return CheckForWarnings(removeWarning, out _, out warningsString); }
    public bool ContainsWarnings(bool removeWarning, out int warnings, out string warningsString) { return CheckForWarnings(removeWarning, out warnings, out warningsString); }

    private static bool CheckForWarnings(bool removeWarning, out int warnings, out string warningsString)
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
                if (removeWarning)
                {
                    problems.RemoveAt(i); 
                }
            }
        }
        return warnings > 0;
    }

    public bool ContainsWarningsAndErrors(bool removeErrors, bool removeWarning)
    { return CheckForWarnings(removeWarning, out _, out _) & CheckForErrors(removeErrors, out _, out _); }
    public bool ContainsWarningsAndErrors(bool removeErrors, out int errors, bool removeWarning, out int warnings)
    { return CheckForWarnings(removeWarning, out warnings, out _) & CheckForErrors(removeErrors, out errors, out _); }
    public bool ContainsWarningsAndErrors(bool removeErrors, out string errorsString, bool removeWarning, out string warningsString)
    { return CheckForWarnings(removeWarning, out _, out warningsString) & CheckForErrors(removeErrors, out _, out errorsString); }
    public bool ContainsWarningsAndErrors(bool removeErrors, out int errors, out string errorsString, bool removeWarning, out int warnings, out string warningsString)
    { return CheckForWarnings(removeWarning, out warnings, out warningsString) & CheckForErrors(removeErrors, out errors, out errorsString); }
}
