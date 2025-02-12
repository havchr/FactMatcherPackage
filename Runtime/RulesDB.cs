using TextAsset = UnityEngine.TextAsset;
using System.Collections.Generic;
using FactMatching;
using System.Linq;
using System.Text;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using System;
using System.Threading;
using System.IO;

public enum FactValueType
{
    String,Value	
}

[Serializable]
public class RuleDBFactWrite
{
    
    public string factName;
    public int factID;
    public string writeString;
    public float writeValue;
    public WriteMode writeMode = WriteMode.SetString;
    public int lineNumber;

    public enum WriteMode
    {
        SetString,SetValue,IncrementValue,SubtractValue,SetToOtherFactValue,IncrementByOtherFactValue,SubtractByOtherFactValue
    }

    public bool TryGetOtherFactIDOther(ref int otherFactID)
    {
        switch (writeMode)
        {
            case WriteMode.IncrementByOtherFactValue: 
            case WriteMode.SubtractByOtherFactValue: 
            case WriteMode.SetToOtherFactValue:
                otherFactID = (int)writeValue;
                return true;
        }
        otherFactID = -1;
        return false;
    }

    public override string ToString()
    {
        string result = $"{factName}";

        result += writeMode switch
        {
            WriteMode.SetString => $" = {writeString}",
            WriteMode.SetValue => $" = {writeValue}",
            WriteMode.IncrementValue => $" += {writeValue}",
            WriteMode.SubtractValue => $" -= {writeValue}",
            WriteMode.SetToOtherFactValue => $" (=) {writeString}",
            WriteMode.IncrementByOtherFactValue => $" (+=) {writeString}",
            WriteMode.SubtractByOtherFactValue => $" (-=) {writeString}",
            _ => "",
        };

        return result;
    }
}

[Serializable]
public class RuleDBFactTestEntry
{

    public RuleDBFactTestEntry()
    {
        
    }
    public RuleDBFactTestEntry(RuleDBFactTestEntry rhs)
    {
        factName = rhs.factName;
        isStrict = rhs.isStrict;
        orGroupRuleID = rhs.orGroupRuleID;
        factID = rhs.factID;
        matchString = rhs.matchString;
        matchValue = rhs.matchValue;
        compareMethod = rhs.compareMethod;
        compareType = rhs.compareType;
        ruleOwnerID = rhs.ruleOwnerID;
        lineNumber = rhs.lineNumber;
    }
    public string factName;
    public bool isStrict;
    public int orGroupRuleID;
    public int factID;
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
            default:
                break;
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
            default: break;
        }
        return FactMatching.FactCompare.Equals(val);
    }

    public override string ToString()
    {
        return $"{(isStrict ? "" : " ? ")}{factName} {CompareMethodPrintable()} {MatchValuePrintable()}";
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
    public string ruleName;
    public int RuleID;
    public string bucket;

    public string PayloadRaw => payload.RawText;
    public Payload payload;
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
            stringBuilder ??= new StringBuilder();
            stringBuilder.Clear();
            int currentInterpolationIndex = 0;
            if( currentInterpolationIndex < interpolations.Count )
            {
                RulePayloadInterpolation interpolation = interpolations[currentInterpolationIndex];
                for (int i = 0; i < payload.StrippedText.Length; i++)
                {
                    if (interpolation!= null && i >= interpolation.payLoadStringStartIndex && i <= interpolation.payLoadStringEndIndex)
                    {
                    }
                    else
                    {
                        stringBuilder.Append(payload.StrippedText[i]);
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
        return payload.StrippedText;
    }
}

[Serializable]
public class FactInDocument
{
    [SerializeField]
    public string factName;
    public bool containsTemplateSymbol = false;
    public int FactID;
    public int LineNumber;
    [TextArea]
    public string FactSummary;
    public bool IgnoreNumber;
    public List<string> FactCanBe;
    public List<string> EnumNames;

    public FactInDocument(FactInDocument fact = null)
    {
        if (fact != null)
        {
            factName = fact.factName;
            FactID = fact.FactID;
            LineNumber = fact.LineNumber;
            FactSummary = fact.FactSummary;
            IgnoreNumber = fact.IgnoreNumber;
            FactCanBe = fact.FactCanBe; 
        }
    }

    public bool FactCanBeContains(string canBe)
    {
        if (canBe.IsNullOrWhitespace() && FactCanBe.IsNullOrEmpty())
        {
            return true;
        }
        return FactCanBe != null && FactCanBe.Contains(canBe.Trim());
    }

    public override string ToString()
    {
        string result = $"FactName: {factName}";
        result += $"{(FactSummary.IsNullOrWhitespace() ? "" : $"\nSummary: {FactSummary}")}" +
                  $"\nAt line {LineNumber}";

        if (!FactCanBe.IsNullOrEmpty())
        {
            result += "\n\nFact can be:";
            foreach (var factCanBe in FactCanBe)
            {
                result += "\n\t" + factCanBe;
            }
            result += "\n";
        }

        return result.Trim();
    }

    public string ToFactDocumentationString()
    {
        string result = $".FACT {factName}";
        result += $"{(FactSummary.IsNullOrWhitespace() ? "" : $"\n{FactSummary}")}\n..";

        if (!FactCanBe.IsNullOrEmpty())
        {
            result += "\n\n.IT CAN BE";
            foreach (var factCanBe in FactCanBe)
            {
                result += "\n\t" + factCanBe;
            }
            result += "\n..";
            result += "\n";
        }

        return result.Trim();
    }
}

[Serializable]
public class DocumentEntry
{
    public string DocumentName;
    public int DocID;
    [TextArea(1, 5)]
    public string Summary;
    public List<FactInDocument> Facts;
    public int StartLine;
    public TextAsset TextFile;

    public DocumentEntry(DocumentEntry documentEntry = null)
    {
        if (documentEntry != null)
        {
            DocumentName = documentEntry.DocumentName;
            DocID = documentEntry.DocID;
            Summary = documentEntry.Summary;
            Facts = documentEntry.Facts;
            StartLine = documentEntry.StartLine;
            TextFile = documentEntry.TextFile; 
        }
    }

    public FactInDocument GetFactInDocumentByName(string factName)
    {
        foreach (var currentFact in Facts)
        {
            if (CompareStringsIgnoringNumbers(currentFact.factName, factName))
            {
                return currentFact;
            }
        }

        return null;
    }

    public bool IsFactInDoc(string factName)
    {
        foreach (var currentFact in Facts)
        {
            if (currentFact.factName == factName.Trim())
            {
                return true;
            }
            else if (currentFact.IgnoreNumber)
            {
                if (CompareStringsIgnoringNumbers(currentFact.factName, factName))
                {
                    return true;
                }
            }
        }

        return false;
    }

    public bool CanFactBe(string factName, string canBe)
    {
        if (!canBe.IsNullOrWhitespace())
        {
            foreach (var currentFact in Facts)
            {
                if (currentFact.factName == factName.Trim() && currentFact.FactCanBeContains(canBe))
                {
                    return true;
                }
                else if (currentFact.IgnoreNumber)
                {
                    if (CompareStringsIgnoringNumbers(currentFact.factName, factName))
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    public static bool CompareStringsIgnoringNumbers(string targetString, string stringToTest)
    {
        int y = 0;
        int j = 0;
        bool result = false;
        while (y < targetString.Length && j < stringToTest.Length)
        {
            if (targetString[y] == '#')
            {
                while (y < targetString.Length && targetString[y] == '#')
                {
                    targetString = targetString?.Remove(y, 1); 
                }

                if (!char.IsDigit(stringToTest[j]))
                {
                    result = false;
                    break;
                }

                if (y < targetString.Length && char.IsDigit(targetString[y]))
                {
                    throw new Exception("Detected numb after #");
                }
                while (j < stringToTest.Length && char.IsDigit(stringToTest[j]))
                {
                    stringToTest = stringToTest.Remove(j, 1);
                }
            }
            else if (targetString[y] == stringToTest[j])
            {
                y++;
                j++;
            }
            else
            {
                break;
            }

            if (targetString == stringToTest)
            {
                result = true;
            }
        }

        return result;
    }

    public override string ToString()
    {
        string result = DocumentName;
        result += $"{(Summary == "" ? "" : $"\nSummary: {Summary}")}" +
                  $"\nDocID: {DocID}" +
                  $"\nStart line {StartLine}" +
                  $"{(TextFile != null ? $"\nText file: {TextFile.name}" : "")}";
        if (Facts != null)
        {
            result += $"\n\nFacts:";
            foreach (var fact in Facts)
            {
                result += "\n\n" + fact.ToString();
            } 
        }

        return result;
    }

    public string ToDocumentationString()
    {
        string result = $".DOCS {DocumentName}";
        result += $"{(Summary.IsNullOrWhitespace() ? "" : $"\n{Summary}")}\n..";
        if (Facts != null)
        {
            foreach (var fact in Facts)
            {
                result += "\n\n" + fact.ToFactDocumentationString();
            } 
        }
        result += "\n\n.END";

        return result;
    }
}

#if UNITY_EDITOR
public class SetupForFileSystemWatcherAutoParse
{
    [InitializeOnLoadMethod]
    private static void Initialize()
    {
        EditorApplication.update += EditorUpdateCallback;
    }

    private static bool isInitializeSetup = false;
    private static RulesDB[] allRulesDB;

    private static void EditorUpdateCallback()
    {
        if (!isInitializeSetup)
        {
            isInitializeSetup = true;
            string[] guids = AssetDatabase.FindAssets("t:" + typeof(RulesDB).Name);
            allRulesDB = new RulesDB[guids.Length];
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                allRulesDB[i] = AssetDatabase.LoadAssetAtPath<RulesDB>(path);

                if (allRulesDB[i].HasDocumentation() || !allRulesDB[i].generateRuleFrom.IsNullOrEmpty())
                {
                    allRulesDB[i].SetUpFileWatcher();
                }
            }
        }
    }
}
#endif

[CreateAssetMenu(fileName = "RulesDB", menuName = "FactMatcher/RulesDB", order = 1)]
public class RulesDB : ScriptableObject
{
    [NonSerialized]
    public ProblemReporting problemList;
    public bool autoParseRuleScript = false;
    public bool ignoreDocumentationDemand = false;
    public bool debugLogMissingIDS = false;
    public bool debugLogAutoparsing = false;
    private Dictionary<string, int> _factIDsMap;
    private Dictionary<string, int> _ruleIDsMap;
    private Dictionary<string, int> _stringIDsMap;
   
    private Dictionary<string, BucketSlice> _bucketSlices;
    private Dictionary<int, RuleDBEntry> _ruleMap;

    public List<DocumentEntry> DocumentationList
    {
        get
        {
            if (documentation != null && documentation.documentations != null)
            {
                return documentation.documentations;
            }
            return _emptyDocList;
        }
    }


    public FMDocumentation documentation;
    private List<DocumentEntry> _emptyDocList = new List<DocumentEntry>();

    public Action OnDocumentationParsed;

    [Space(10)]
    public List<TextAsset> generateRuleFrom;
    public List<RuleDBEntry> rules;
    public Action OnRulesParsed;

    public List<FileSystemWatcher> fileWatchers = new List<FileSystemWatcher>();
    public Action<ProblemReporting> AutoParserTrigger;
    public Action<ProblemReporting> AutoParserTriggerForEditorUI;

    public void InitRuleDB()
    {
        _stringIDsMap = CreateStringIDs(rules,DocumentationList);
        _ruleIDsMap = CreateRuleIDs(rules);
        _ruleMap = CreateEntryFromIDDic(rules);
        _factIDsMap = CreateFactIDs(rules);
        _bucketSlices = CreateBucketSlices();
    }

#if UNITY_EDITOR
    
    public void SetUpFileWatcher(bool setupFileWatcher)
    {
        autoParseRuleScript = setupFileWatcher;
        ClearAllFileWatchers();
        if (autoParseRuleScript)
        {
            if (documentation != null && documentation.generateDocumentationFrom != null)
            {
                SetupFileWatcherUsingTextAssetList(documentation.generateDocumentationFrom);
            }
            SetupFileWatcherUsingTextAssetList(generateRuleFrom); 
        
        }
    }
    public void SetUpFileWatcher()
    {
        ClearAllFileWatchers();
        if (autoParseRuleScript)
        {
            if (documentation != null && documentation.generateDocumentationFrom != null)
            {
                SetupFileWatcherUsingTextAssetList(documentation.generateDocumentationFrom);
            }
            SetupFileWatcherUsingTextAssetList(generateRuleFrom); 
        
        }
    }

    public void SetupFileWatcherUsingTextAssetList(List<TextAsset> textAssetList, bool setupFileWatcher = true)
    {
        if (setupFileWatcher)
        {
            foreach (var textAsset in textAssetList)
            {
                GetFullPathFromAsset(textAsset, out string directoryPath, out string fileName);
                if (!fileName.IsNullOrWhitespace())
                {
                    var fileWatcha = new FileSystemWatcher()
                    {
                        Path = directoryPath,
                        Filter = fileName,
                        EnableRaisingEvents = true,
                    };
                    if (debugLogAutoparsing)
                    {
                        Debug.Log($"FactMatcher autoparse: Adding FileWatcher for {fileName} from {name}");
                    }
                    fileWatcha.Changed += OnFileChanged;
                    fileWatchers.Add(fileWatcha);
                }
            } 
        }
    }

    public void ClearAllFileWatchers()
    {
        foreach (var fileWatcher in fileWatchers)
        {
            if (fileWatcher != null)
            {
                if (debugLogAutoparsing)
                {
                    Debug.Log($"FactMatcher autoparse: clearing FileWatcher for {fileWatcher.Path}");
                }
                fileWatcher.Changed -= OnFileChanged;
                fileWatcher.Dispose();
            }
        }
        fileWatchers.Clear();
    }
    

    private void OnFileChanged(object sender, FileSystemEventArgs e)
    {
        Debug.Log($"Detected change in file: {e.Name}\nAt path: {e.FullPath}");
        // We have to create a subscription to this event so we can run AssetDatabase.GetAssetPath from main thread
        
        // Give Unity some time to also react to the file change, because it seems we would parse the old file, 
        //if we did not sleep
        Thread.Sleep(1000); 
        EditorApplication.update += RunRuleGenerating;
    }

    private void RunRuleGenerating()
    {
        EditorApplication.update -= RunRuleGenerating;
        problemList?.ClearList();
        problemList = CreateRulesFromRulescripts();
        problemList.DebugWarningsAndErrors($"Auto parsing from {name}:");
        EditorUtility.SetDirty(this);
        AssetDatabase.SaveAssets();
        Thread.Sleep(1000); 
        AutoParserTriggerForEditorUI?.Invoke(problemList);
        AutoParserTrigger?.Invoke(problemList);
    }

    private void OnDestroy()
    {
        ClearAllFileWatchers();
    }

    public static string GetFullPathFromAsset(UnityEngine.Object @object, out string folderPath, out string fileName)
    {
        string workingDirectory = Environment.CurrentDirectory;
        string path = AssetDatabase.GetAssetPath(@object);
        string fullPath = workingDirectory + "/" + path;

#if UNITY_EDITOR_WIN
        fullPath = fullPath.Replace("/", "\\");
#endif

        folderPath = Path.GetDirectoryName(fullPath);
        fileName = Path.GetFileName(fullPath);
        return fullPath;
    }
#endif

    public DocumentEntry GetDocumentEntryByName(string nameOfDoc)
    {
        foreach (var doc in DocumentationList)
        {
            if (doc.DocumentName == nameOfDoc)
            {
                return doc;
            }
        }
        return null;
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

    /// <summary>Creates a list of the current info tests and makes sure none have duplicated IDs</summary>
    /// <param name="filter"></param>
    /// <returns>List of factTests</returns>
    public List<RuleDBFactTestEntry> CreateFlattenedFactTestListWithNoDuplicateFactIDS(Func<RuleDBFactTestEntry, bool> filter = null)
    {
        List<RuleDBFactTestEntry> factTests = new();
        List<int> usedIDs = new();
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
        List<RuleDBFactTestEntry> ruleAtoms = new();
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

    public ProblemReporting CreateRulesFromRulescripts()
    {
        ProblemReporting problems = new();
        problemList?.ClearList();

        if (!ignoreDocumentationDemand)
        {
            problems = CreateDocumentations();
            if (DocumentationList == null)
            {
                problems.ReportNewWarning("Documentations is null", null, -1);
            }
        }
        else
        {
            problems.ReportNewWarning("Ignore documentation demand is on", null, -1);
        }

        if (generateRuleFrom.Count != 0)
        {
            rules.Clear();
            int factID = Consts.FactIDDevNull + 1;
            int ruleID = 0;
            int bucketID = 0;
            Dictionary<string, int> addedFactIDS = new();
            Dictionary<string, BucketSlice> slicesForBuckets = new();
            Dictionary<string, string> bucketPartNames = new();
            foreach (var ruleScript in generateRuleFrom)
            {
                var parser = new RuleScriptParser();
                
            //todo - https://app.clickup.com/t/85yxerntt (TextAssets loaded from resources will get wrong relative pathing probably)
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
                    path = path[..(lastIndexOf + 1)];
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
                    ignoreDocumentationDemand ? null : DocumentationList);
            }

            InitFactWriteIndexers(ref addedFactIDS,ref factID);
            if (!problems.ContainsError())
            {
                /*
                 * We need to sort on our buckets, so that we can use bucketSlices (slices of the array)
                 * to test only specific parts of the RuleDB, see the FactMatcher documentation about buckets
                 */
                rules = SortListByBucketIndexThenDescendingFactCounts(rules);
                rules = BucketSlicer.SliceIntoBuckets(rules); 
                PayloadInterpolationParser payloadInterpolationParser = new();
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
    
    public ProblemReporting CreateDocumentations()
    {
        problemList?.ClearList();
        return documentation.CreateDocumentations();
    }
    
    //FactWrites that are referencing another factID - must now be converted to their factIDS.
    void InitFactWriteIndexers(ref Dictionary<string, int> addedFactIDS,ref int nextFactID)
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
                        if (!addedFactIDS.ContainsKey(factWrite.writeString))
                        {
                            addedFactIDS[factWrite.writeString] = nextFactID;
                            nextFactID++;
                        }
                        factWrite.writeValue = addedFactIDS[factWrite.writeString];
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
                int factIdOther = -1;
                if (factWrite.TryGetOtherFactIDOther(ref factIdOther))
                {
                    result[factWrite.writeString] = factIdOther;
                }
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

        if (!_stringIDsMap.TryGetValue(str, out int id))
        {
            id = -1;
            if (debugLogMissingIDS)
            {
                Debug.Log($"did not find stringID {id} for string {str}");
            }
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

        if (!_factIDsMap.TryGetValue(str, out int id))
        {
            id = Consts.FactIDDevNull;
            if (debugLogMissingIDS)
            {
                Debug.Log($"did not find factID {id} for fact {str}");
            }
        }

        return id;
    }
    
    public int RuleID(string str)
    {
        if (_ruleIDsMap == null)
        {
            InitRuleDB();
        }

        if (!_ruleIDsMap.TryGetValue(str, out int id))
        {
            id = Consts.RuleIDNonExisting;
            
            if (debugLogMissingIDS)
            {
                Debug.Log($"did not find ruleID {id} for rule {str}");
            }
        }

        return id;
    }

    public string GetFactVariableNameFromFactID(int factID)
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

    public RuleDBEntry RuleFromID(int id, bool usePayloadVariable = false)
    {
        if (_ruleMap == null)
        {
            InitRuleDB();
        }

        if (!_ruleMap.TryGetValue(id, out RuleDBEntry rule))
        {
            rule = null;
        }

        if (usePayloadVariable)
        {
            rule.payload = rule.payload.UpdateKeywordParameters(rule);
        }

        return rule;
    }

    private static Dictionary<int, RuleDBEntry> CreateEntryFromIDDic(List<RuleDBEntry> rules)
    {
        Dictionary<int, RuleDBEntry> dic = new();
        foreach (var rule in rules)
        {
            var id = rule.RuleID;
            dic[id] = rule;
        }

        return dic;
    }

    private static Dictionary<string, int> CreateStringIDs(List<RuleDBEntry> rules, List<DocumentEntry> documentations)
    {
        Dictionary<string, int> dic = new()
        {
            { "FALSE", FactMatching.Consts.False },
            { "False", FactMatching.Consts.False },
            { "false", FactMatching.Consts.False },
            { "TRUE", FactMatching.Consts.True },
            { "True", FactMatching.Consts.True },
            { "true", FactMatching.Consts.True }
        };
        int id = FactMatching.Consts.True + 1;
        for (int i = 0; i < rules.Count; i++)
        {
            var rule = rules[i];
            if (rule.payload !=null && rule.payload.StrippedText != null)
            {
                if (!dic.ContainsKey(rule.payload.StrippedText))
                {
                    dic[rule.payload.StrippedText] = id;
                    rule.payloadStringID = id;
                    id++;
                }
                else
                {
                    rule.payloadStringID = dic[rule.payload.StrippedText];
                }
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
        
        foreach (var documentEntry in documentations)
        {
            foreach (var fact in documentEntry.Facts)
            {
                foreach (var factsCanBe in fact.FactCanBe)
                {
                    if (!dic.ContainsKey(factsCanBe))
                    {
                        dic[factsCanBe] = id;
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

                int topFactIDCandidate = -1;
                if (factWrite.TryGetOtherFactIDOther(ref topFactIDCandidate))
                {
                    if (topFactIDCandidate > topFactID)
                    {
                        topFactID = topFactIDCandidate;
                    }
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

    public bool HasDocumentation()
    {
        return documentation != null && !documentation.generateDocumentationFrom.IsNullOrEmpty();
    }
}