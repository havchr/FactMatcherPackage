using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FactMatching;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;

public enum FactValueType
{
    String,Value	
}

[Serializable]
public class RuleDBFactWrite
{
    
    public int factID;
    public string factName;
    public string writeString;
    public float writeValue;
    public WriteMode writeMode = WriteMode.SetString;

    public enum WriteMode
    {
        SetString,SetValue,IncrementValue,SubtractValue,SetToOtherFactValue,IncrementByOtherFactValue,SubtractByOtherFactValue
    }
    
}

/// <summary>
/// Tasked to record the problems encountered when creating the rules for factMacher 
/// </summary>
public class RuleScriptParsingProblems
{
    private static readonly List<ProblemEntry> problems = new();

    public void ClearList()
    { problems?.Clear(); }

    public void AddNewProblem(ProblemEntry problemEntry)
    { problems.Add(problemEntry); }

    public void AddNewProblemList(List<ProblemEntry> problemEntrys)
    { problems.AddRange(problemEntrys); }

    /// <summary>
    /// Reports new problem (user defined problem type)
    /// </summary>
    /// <param name="problemMessage"></param>
    /// <param name="file"></param>
    /// <param name="lineNumber"></param>
    /// <param name="problemType"></param>
    public void ReportNewProblem(string problemMessage, TextAsset file, int lineNumber, ProblemEntry.ProblemTypes problemType, Exception Exception = null)
    { problems.Add(new ProblemEntry() { File = file, LineNumber = lineNumber, ProblemMessage = problemMessage, Exception = Exception, ProblemType = problemType }); }

    /// <summary>
    /// Reports new problem (auto error user can change)
    /// </summary>
    /// <param name="problemMessage"></param>
    /// <param name="file"></param>
    /// <param name="lineNumber"></param>
    /// <param name="problemType"></param>
    public void ReportNewError(string problemMessage, TextAsset file, int lineNumber, Exception Exception = null, ProblemEntry.ProblemTypes problemType = ProblemEntry.ProblemTypes.Error)
    { problems.Add(new ProblemEntry() { ProblemMessage = problemMessage, File = file, LineNumber = lineNumber, Exception = Exception, ProblemType = problemType }); }

    /// <summary>
    /// Reports new problem (auto warning user can change)
    /// </summary>
    /// <param name="problemMessage"></param>
    /// <param name="file"></param>
    /// <param name="lineNumber"></param>
    /// <param name="problemType"></param>
    public void ReportNewWarning(string problemMessage, TextAsset file, int lineNumber, Exception Exception = null, ProblemEntry.ProblemTypes problemType = ProblemEntry.ProblemTypes.Warning)
    { problems.Add(new ProblemEntry() { File = file, LineNumber = lineNumber, ProblemMessage = problemMessage, Exception = Exception, ProblemType = problemType }); }
    

    /// <summary>
    /// Checks if there is any error in the List of problems
    /// </summary>
    /// <param name="problemEntries">If this is left empty it will check the active list in RuleScriptParsingProblems but will check any ProblemEntry list given</param>
    /// <returns>True if the list contains at least one error</returns>
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

    /// <summary>
    /// Checks if there is any warning in the List of problems
    /// </summary>
    /// <param name="problemEntries">If this is left empty it will check the active list in RuleScriptParsingProblems but will check any ProblemEntry list given</param>
    /// <returns>True if the list contains at least one warning</returns>
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

    /// <summary>
    /// Checks if there is any warning or error in the List of problems
    /// </summary>
    /// <param name="problemEntries">If this is left empty it will check the active list in RuleScriptParsingProblems but will check any ProblemEntry list given</param>
    /// <returns>True if the list contains at least one warning or error</returns>
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
    /// Returns the list of problems and clears the previews problems
    /// </summary>
    /// <returns>List of ProblemEntry's</returns>
    public List<ProblemEntry> ToList()
    {
        List<ProblemEntry> listOfProblems = new(problems);
        problems.Clear();
        return listOfProblems;
    }

    public bool ContainsErrorsOutErrorsAmountErrorsString(out int errors, out string errorsString)
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

    public bool ContainsWarningsOutWarningsAmountWarningsString(out int warnings, out string warningsString)
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

    public bool ContainsWarningsAndErrorsOutWarningsAndErrorsAmountWarningsAndErrorsString(out int errors, out string errorsString, out int warnings, out string warningsString)
    {
        errors = 0;
        errorsString = string.Empty;
        warnings = 0;
        warningsString = string.Empty;
        for (int i = 0; i < problems.Count; i++)
        {
            ProblemEntry problem = problems[i];
            if (problem.IsError())
            {
                errorsString += "\n\n" + problem.ToString();
                errors++;
                problems.RemoveAt(i);
            }
            else if (problem.IsWarning())
            {
                warningsString += "\n\n" + problem.ToString();
                warnings++;
                problems.RemoveAt(i);
            }
        }
        return errors > 0 || warnings > 0;
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

[Serializable]
public class ProblemEntry
{
    public enum ProblemTypes { Error, Warning }

    public ProblemTypes ProblemType;
    public TextAsset File;
    public int LineNumber;
    public string ProblemMessage;
    public Exception Exception;
    public override string ToString()
    {
        return
            $"{ProblemType} occurred {(File ? $"in the file:  {File.name},{(LineNumber > 0 ? $" at line: {LineNumber}," : "")}" : string.Empty)} " +
            $"with the message:\n{ProblemMessage}" +
            $"{(Exception == null ? string.Empty : $"\nException message: {Exception}")}";
    }

    public bool IsError()
    { return ProblemType == ProblemTypes.Error; }

    public bool IsWarning()
    { return ProblemType == ProblemTypes.Warning; }
}

[Serializable]
public class RuleDBFactTestEntry
{

    public RuleDBFactTestEntry()
    {
        
    }
    public RuleDBFactTestEntry(RuleDBFactTestEntry rhs)
    {
        isStrict = rhs.isStrict;
        orGroupRuleID = rhs.orGroupRuleID;
        factID = rhs.factID;
        factName = rhs.factName;
        matchString = rhs.matchString;
        matchValue = rhs.matchValue;
        compareMethod = rhs.compareMethod;
        compareType = rhs.compareType;
        ruleOwnerID = rhs.ruleOwnerID;
        lineNumber = rhs.lineNumber;
    }
    public bool isStrict;
    public int orGroupRuleID;
    public int factID;
    public string factName;
    public string matchString;
    public float matchValue;
    public Comparision compareMethod;
    public FactValueType compareType;
    public int ruleOwnerID;
    public int lineNumber;

    public enum Comparision
    {
        Equal,NotEqual,LessThan,MoreThan,LessThanEqual,MoreThanEqual,Range
    }

    public string CompareMethodPrintable()
    {
        switch (compareMethod)
        {
            case Comparision.Equal:
                return "=";
            case Comparision.NotEqual:
                return "!=";
            case Comparision.LessThan:
                return "<";
            case Comparision.LessThanEqual:
                return "<=";
            case Comparision.MoreThan:
                return ">";
            case Comparision.MoreThanEqual:
                return ">=";
        }
        return "";
    }

    public string MatchValuePrintable()
    {
        return (compareType == FactValueType.Value) ? $"{matchValue}" : matchString;
    }

    public FactMatching.FactCompare CreateCompare(RulesDB rules)
    {
        //used in the fact system 
        var val = compareType == FactValueType.String ?  rules.StringId(matchString) : matchValue;
        switch (compareMethod)
        {
            case Comparision.Equal:
                return FactMatching.FactCompare.Equals(val);
            case Comparision.NotEqual:
                return FactMatching.FactCompare.NotEquals(val);
            case Comparision.LessThan:
                return FactMatching.FactCompare.LessThan(val);
            case Comparision.LessThanEqual:
                return FactMatching.FactCompare.LessThanEquals(val);
            case Comparision.MoreThan:
                return FactMatching.FactCompare.MoreThan(val);
            case Comparision.MoreThanEqual:
                return FactMatching.FactCompare.MoreThanEquals(val);
        }
        return FactMatching.FactCompare.Equals(val);
    }
}

[Serializable]
public class RulePayloadInterpolation
{
    public int payLoadStringStartIndex;
    public int payLoadStringEndIndex;
    public int factValueIndex;
    public FactValueType type;
    public string numberFormat;
}

[Serializable]
public class RuleDBEntry
{
    public int RuleID;
    public string bucket;
    public string ruleName;
    public string payload;
    [NonSerialized]
    public int payloadStringID;
    public ScriptableObject PayloadObject;
    public List<RuleDBFactWrite> factWrites;
    public List<RuleDBFactTestEntry> factTests;
    public List<RulePayloadInterpolation> interpolations;
    public int bucketSliceStartIndex;
    public int bucketSliceEndIndex;
    public int startLine;
    public TextAsset textFile;

    public string Interpolate(FactMatcher matcher,ref StringBuilder stringBuilder)
    {
        if (matcher.IsInited)
        {
            if (stringBuilder == null)
            {
                stringBuilder = new StringBuilder();
            }
            stringBuilder.Clear();
            int currentInterpolationIndex = 0;
            if( currentInterpolationIndex < interpolations.Count )
            {
                RulePayloadInterpolation interpolation = interpolations[currentInterpolationIndex];
                for (int i = 0; i < payload.Length; i++)
                {
                    if (interpolation!= null && i >= interpolation.payLoadStringStartIndex && i <= interpolation.payLoadStringEndIndex)
                    {
                    }
                    else
                    {
                        stringBuilder.Append(payload[i]);
                    }
                    if (interpolation!=null && i == interpolation.payLoadStringEndIndex)
                    {
                        if (interpolation.type == FactValueType.String)
                        {
                            stringBuilder.Append($"{matcher.ruleDB.GetStringFromStringID((int)matcher[interpolation.factValueIndex])}");
                        }
                        else if (interpolation.type == FactValueType.Value)
                        {
                            if (interpolation.numberFormat.Length > 0)
                            {
                                stringBuilder.Append(matcher[interpolation.factValueIndex].ToString(interpolation.numberFormat));
                            }
                            else
                            {
                                stringBuilder.Append($"{matcher[interpolation.factValueIndex]}");
                            }
                        }
                        currentInterpolationIndex++;
                        if (currentInterpolationIndex >= interpolations.Count)
                        {
                            interpolation = null;
                        }
                        else
                        {
                            interpolation = interpolations[currentInterpolationIndex];
                        }
                    }
                }
                return stringBuilder.ToString();
            }
        }
        return payload;
    }
}

[Serializable]
public class FactInDocument
{
    public string FactName;
    [TextArea]
    public string FactSummary;
    public List<string> FactCanBe;

    public bool FactCanBeContains(string canBe)
    {
        if (canBe == null)
        {
            return true;
        }
        return FactCanBe.Contains(canBe.Trim());
    }
}

[Serializable]
public class DocumentEntry
{
    public string DocumentName;
    [TextArea(1, 5)]
    public string Summary;
    public List<FactInDocument> Facts;
    public int StartLine;
    public TextAsset TextFile;

    public bool IsFactInDoc(string factName)
    {
        foreach (var fact in Facts)
        {
            if (fact.FactName == factName.Trim())
            {
                return true;
            }
        }

        return false;
    }

    public bool CanFactBe(string factName, string canBe)
    {
        if (canBe != null)
        {
            foreach (var fact in Facts)
            {
                if (fact.FactName == factName.Trim() && fact.FactCanBeContains(canBe))
                {
                    return true;
                }
            } 
        }

        return false;
    }
}


[CreateAssetMenu(fileName = "RulesDB", menuName = "FactMatcher/RulesDB", order = 1)]
public class RulesDB : ScriptableObject
{
    [NonSerialized]
    public List<ProblemEntry> problemList;
    public bool PickMultipleBestRules = false;
    public bool FactWriteToAllThatMatches = false;
    public bool ignoreDocumentationDemand = false;
    private Dictionary<string, int> _factIDsMap;
    private Dictionary<string, int> _ruleIDsMap;
    private Dictionary<string, int> _stringIDsMap;
   
    private Dictionary<string, BucketSlice> _bucketSlices;
    
    private Dictionary<int, RuleDBEntry> _ruleMap;
    [Space(10)]
    public List<TextAsset> generateDocumentationFrom;
    public List<DocumentEntry> documentations;
    public Action OnDocumentationParsed;
    [Space(10)]
    public List<TextAsset> generateFrom;
    public List<RuleDBEntry> rules;
    public Action OnRulesParsed;
    
    private StringBuilder _interpolationBuilder; 

    public void InitRuleDB()
    {
        _stringIDsMap = CreateStringIDs(rules);
        _ruleIDsMap = CreateRuleIDs(rules);
        _ruleMap = CreateEntryFromIDDic(rules);
        _factIDsMap = CreateFactIDs(rules);
        _bucketSlices = CreateBucketSlices();
    }

    private Dictionary<string, BucketSlice> CreateBucketSlices()
    {
        _bucketSlices = new Dictionary<string, BucketSlice>();
        foreach (var entry in rules)
        {
            if (entry.bucket!=null && !_bucketSlices.ContainsKey(entry.bucket))
            {
                _bucketSlices[entry.bucket] = new BucketSlice(entry.bucketSliceStartIndex,entry.bucketSliceEndIndex,entry.bucket);
            }
        }
        return _bucketSlices;
    }

    /// <summary>
    /// Creates a list of the current info tests and makes sure none have duplicated IDs
    /// </summary>
    /// <param name="filter"></param>
    /// <returns>
    /// List of factTests
    /// </returns>
    public List<RuleDBFactTestEntry> CreateFlattenedFactTestListWithNoDuplicateFactIDS(Func<RuleDBFactTestEntry, bool> filter = null)
    {
        List<RuleDBFactTestEntry> factTests = new List<RuleDBFactTestEntry>();
        List<int> usedIDs = new List<int>();
        foreach (var rule in rules)
        {
            foreach (var factTest in rule.factTests)
            {
                if (!usedIDs.Contains(factTest.factID) && ((filter != null && filter(factTest)) || filter == null))
                {
                    usedIDs.Add(factTest.factID);
                    factTests.Add(factTest);
                }
            }
        }

        return factTests;
    }
    
    public List<RuleDBFactTestEntry> CreateFlattenedRuleAtomListWithPotentiallyDuplicateFactIDS(Func<RuleDBFactTestEntry, bool> filter = null)
    {
        List<RuleDBFactTestEntry> ruleAtoms = new List<RuleDBFactTestEntry>();
        foreach (var rule in rules)
        {
            foreach (var factTest in rule.factTests)
            {
                if (((filter != null && filter(factTest)) || filter == null))
                {
                    ruleAtoms.Add(factTest);
                }
            }
        }

        return ruleAtoms;
    }

    public RuleScriptParsingProblems CreateRulesFromRulescripts()
    {
        RuleScriptParsingProblems problems = new();
        problemList?.Clear();

        if (!ignoreDocumentationDemand)
        {
            problems = CreateRulesFromDocumentation();
        }

        if (generateFrom.Count != 0)
        {
            rules.Clear();
            int factID = Consts.FactIDDevNull + 1;
            int ruleID = 0;
            int bucketID = 0;
            Dictionary<string, int> addedFactIDS = new Dictionary<string, int>();
            Dictionary<string, BucketSlice> slicesForBuckets = new Dictionary<string, BucketSlice>();
            Dictionary<string, string> bucketPartNames = new Dictionary<string, string>();
            foreach (var ruleScript in generateFrom)
            {
                var parser = new RuleScriptParser();
                var path = "";
                #if UNITY_EDITOR
                path = AssetDatabase.GetAssetPath(ruleScript);
                var lastIndexOf = path.LastIndexOf('/');
                if (lastIndexOf == -1)
                {
                    lastIndexOf = path.LastIndexOf('\\');
                }

                if (lastIndexOf != -1)
                {
                    path = path.Substring(0, lastIndexOf + 1);
                }
                #endif
                parser.GenerateFromText(ruleScript.text, 
                                        rules,
                                        ref factID,
                                        ref addedFactIDS,
                                        ref slicesForBuckets,
                                        ref bucketPartNames,
                                        ref bucketID,
                                        ref ruleID,
                                        path,
                                        ruleScript, 
                                        ref problems,
                                        ignoreDocumentationDemand ? null : documentations);
            }

            InitFactWriteIndexers(ref addedFactIDS);
            if (!problems.ContainsError())
            {
                /*
                 * We need to sort on our buckets, so that we can use bucketSlices (slices of the array)
                 * to test only specific parts of the RuleDB, see the FactMatcher documentation about buckets
                 */
                rules = SortListByBucketIndexThenDescendingFactCounts(rules);
                rules = BucketSlicer.SliceIntoBuckets(rules); 
                PayloadInterpolationParser payloadInterpolationParser = new PayloadInterpolationParser();
                foreach (var rule in rules)
                {
                    payloadInterpolationParser.Parsey(rule, ref addedFactIDS);
                }
                OnRulesParsed?.Invoke();
            }
            else
            {
                rules?.Clear();
            }
        }
        else
        {
            rules?.Clear();
            problems.ReportNewError("There is noting to generate from", null, -1);
        }

        return problems;
    }
    
    public RuleScriptParsingProblems CreateRulesFromDocumentation()
    {
        RuleScriptParsingProblems problems = new();
        problemList?.Clear();
        if (generateDocumentationFrom.Count != 0)
        {
            documentations.Clear();
            foreach (var document in generateDocumentationFrom)
            {
                #if UNITY_EDITOR
                var path = "";
                path = AssetDatabase.GetAssetPath(document);
                var lastIndexOf = path.LastIndexOf('/');
                if (lastIndexOf == -1)
                {
                    lastIndexOf = path.LastIndexOf('\\');
                }

                if (lastIndexOf != -1)
                {
                    path = path[..(lastIndexOf + 1)];
                }
                #endif
                RuleDocumentationParser parser = new();
                documentations.AddRange(parser.GenerateFromText(ref problems, document));
            }

            if (problems.ContainsError())
            {
                documentations?.Clear();
            }
        }
        else
        {
            documentations?.Clear();
            problems.ReportNewError("There is noting to generate from", null, -1);
        }
        return problems;
    }
    
    //FactWrites that are referencing another factID - must now be converted to their factIDS.
    void InitFactWriteIndexers(ref Dictionary<string, int> addedFactIDS)
    {
        foreach (var rule in rules)
        {
            foreach( var factWrite in rule.factWrites)
            {
                switch (factWrite.writeMode)
                {
                    case RuleDBFactWrite.WriteMode.SetToOtherFactValue:
                    case RuleDBFactWrite.WriteMode.IncrementByOtherFactValue:
                    case RuleDBFactWrite.WriteMode.SubtractByOtherFactValue:
                        if (addedFactIDS.ContainsKey(factWrite.writeString))
                        {
                            //encoding the factID index into the writeValue
                            factWrite.writeValue = addedFactIDS[factWrite.writeString];
                        }
                        else
                        {
                            Debug.LogError($"Trying to FactWrite by a non existing other FactValue {factWrite.writeString}");
                        }
                        //now convert the name into the factID.
                        break;
                }
            }
        }
    }

    private static Dictionary<string, int> CreateFactIDs(List<RuleDBEntry> rules)
    {
        var result = new Dictionary<string, int>();
        foreach (var rule in rules)
        {

            foreach (var factTest in rule.factTests)
            {
                result[factTest.factName] = factTest.factID;
            }

            foreach (var factWrite in rule.factWrites)
            {
                result[factWrite.factName] = factWrite.factID;
            }
        }

        return result;
    }
    
    private static Dictionary<string, int> CreateRuleIDs(List<RuleDBEntry> rules)
    {
        var result = new Dictionary<string, int>();
        foreach (var rule in rules)
        {
            result[rule.ruleName] = rule.RuleID;
        }

        return result;
    }

    public int StringId(string str)
    {
        if (_stringIDsMap == null)
        {
            InitRuleDB();
        }

        int id = -1;
        if (!_stringIDsMap.TryGetValue(str, out id))
        {
            id = -1;
            Debug.Log($"did not find stringID {id} for string {str}");
        }
        else
        {
            //Debug.Log($"Found stringID {id} for string {str}");
        }

        return id;
    }
    
    public BucketSlice GetSliceForBucket(string bucket)
    {
        if (_bucketSlices.TryGetValue(bucket, out BucketSlice bucketSlice))
        {
            return bucketSlice;
        }
        return null;
    }

    public int FactId(string str)
    {
        if (_factIDsMap == null)
        {
            InitRuleDB();
        }

        int id = Consts.FactIDDevNull;
        if (!_factIDsMap.TryGetValue(str, out id))
        {
            id = Consts.FactIDDevNull;
            Debug.Log($"did not find factID {id} for fact {str}");
        }
        else
        {
            //Debug.Log($"Found factID {id} for fact {str}");
        }

        return id;
    }
    
    public int RuleID(string str)
    {
        if (_ruleIDsMap == null)
        {
            InitRuleDB();
        }

        int id = Consts.RuleIDNonExisting;
        if (!_ruleIDsMap.TryGetValue(str, out id))
        {
            id = Consts.RuleIDNonExisting;
            Debug.Log($"did not find ruleID {id} for rule {str}");
        }
        else
        {
            //Debug.Log($"Found ruleID {id} for rule {str}");
        }

        return id;
    }

    public string GetFactVariabelNameFromFactID(int factID)
    {
        foreach (var strVal in _factIDsMap)
        {
            if (strVal.Value == factID)
                return strVal.Key;
        }

        return "";
    }

    public string ParseFactValueFromFactID(int factID, float value)
    {
        foreach (var rule in rules)
        {
            foreach (var factTest in rule.factTests)
            {
                if (factTest.factID == factID && factTest.compareType == FactValueType.String)
                {
                    return GetStringFromStringID((int) value);
                }
            }
        }

        return value.ToString();
    }

    public string GetStringFromStringID(int stringID)
    {
        if (_stringIDsMap == null)
        {
            return "Non-inited";
        }

        foreach (var strVal in _stringIDsMap)
        {
            if (strVal.Value == stringID)
                return strVal.Key;
        }

        return "NA";
    }

    public RuleDBEntry RuleFromID(int id)
    {
        if (_ruleMap == null)
        {
            InitRuleDB();
        }

        RuleDBEntry rule;
        if (!_ruleMap.TryGetValue(id, out rule))
        {
            rule = null;
        }

        return rule;
    }

    private static Dictionary<int, RuleDBEntry> CreateEntryFromIDDic(List<RuleDBEntry> rules)
    {
        Dictionary<int, RuleDBEntry> dic = new Dictionary<int, RuleDBEntry>();
        foreach (var rule in rules)
        {
            var id = rule.RuleID;
            dic[id] = rule;
        }

        return dic;
    }

    private static Dictionary<string, int> CreateStringIDs(List<RuleDBEntry> rules)
    {
        Dictionary<string, int> dic = new Dictionary<string, int>();

        int id = 0;
        dic.Add("FALSE", FactMatching.Consts.False);
        dic.Add("False", FactMatching.Consts.False);
        dic.Add("false", FactMatching.Consts.False);
        dic.Add("TRUE", FactMatching.Consts.True);
        dic.Add("True", FactMatching.Consts.True);
        dic.Add("true", FactMatching.Consts.True);
        id = FactMatching.Consts.True + 1;
        for (int i = 0; i < rules.Count; i++)
        {
            var rule = rules[i];
            if (!dic.ContainsKey(rule.payload))
            {
                dic[rule.payload] = id;
                rule.payloadStringID = id;
                id++;
            }
            else
            {
                rule.payloadStringID = dic[rule.payload];
            }

            foreach (var factWrite in rule.factWrites)
            {
                if (factWrite.writeMode == RuleDBFactWrite.WriteMode.SetString
                    && factWrite.writeString != null && factWrite.writeString.Length >= 1)
                {

                    if (!dic.ContainsKey(factWrite.writeString))
                    {
                        dic[factWrite.writeString] = id;
                        id++;
                    }
                }
            }

            foreach (var factTest in rule.factTests)
            {
                if (factTest.compareType == FactValueType.String && factTest.matchString != null && factTest.matchString.Length >= 1)
                {
                    if (!dic.ContainsKey(factTest.matchString))
                    {
                        dic[factTest.matchString] = id;
                        id++;
                    }
                }
            }
        }

        return dic;
    }

    public int CountNumberOfFacts()
    {
        int topFactID = 0;
        foreach (var rule in rules)
        {

            foreach (var factTest in rule.factTests)
            {
                if (factTest.factID > topFactID)
                {
                    topFactID = factTest.factID;
                }
            }

            foreach (var factWrite in rule.factWrites)
            {
                if (factWrite.factID > topFactID)
                {
                    topFactID = factWrite.factID;
                }
            }
        }

        return topFactID + 1;
    }

    public RuleDBFactTestEntry GetFactTestFromFactID(int i)
    {
        foreach (var rule in rules)
        {
            foreach (var factTest in rule.factTests)
            {
                if (factTest.factID == i)
                {
                    return factTest;
                }
            }
        }

        return null;
    }
    public RuleDBFactTestEntry GetFactTestFromFactIDAndRuleID(int factID,int ruleID)
    {
        foreach (var rule in rules)
        {
            foreach (var factTest in rule.factTests)
            {
                if (factTest.factID == factID && factTest.ruleOwnerID == ruleID)
                {
                    return factTest;
                }
            }
        }

        return null;
    }

    public List<RuleDBEntry> SortListByBucketIndexThenDescendingFactCounts(List<RuleDBEntry> ruleToSort)
    {
        return ruleToSort.OrderBy(entry => entry.bucketSliceStartIndex).ThenByDescending(entry => entry.factTests.Count).ToList();
    }
}