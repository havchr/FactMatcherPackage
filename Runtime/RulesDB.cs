using System;
using System.Collections.Generic;
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
        SetString,SetValue,IncrementValue,SubtractValue
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
        isStrict = rhs.isStrict;
        orGroupRuleID = rhs.orGroupRuleID;
        factID = rhs.factID;
        factName = rhs.factName;
        matchString = rhs.matchString;
        matchValue = rhs.matchValue;
        compareMethod = rhs.compareMethod;
        compareType = rhs.compareType;
        ruleOwnerID = rhs.ruleOwnerID;
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
public class RuleDBEntry
{
    public int RuleID;
    public string ruleName;
    public string payload;
    [NonSerialized]
    public int payloadStringID;
    public ScriptableObject PayloadObject;
    public List<RuleDBFactWrite> factWrites;
    public List<RuleDBFactTestEntry> factTests;
}

[CreateAssetMenu(fileName = "RulesDB", menuName = "FactMatcher/RulesDB", order = 1)]
public class RulesDB : ScriptableObject
{

    public bool PickMultipleBestRules = false;
    public bool FactWriteToAllThatMatches = false;
    public bool CompileToCSharp = false;
    private Dictionary<string, int> FactIdsMap;
    private Dictionary<string, int> RuleStringMap;
    private Dictionary<int, RuleDBEntry> RuleMap;
    public List<TextAsset> generateFrom;
    public List<RuleDBEntry> rules;
    public Action OnRulesParsed;

    public void InitRuleDB()
    {
        RuleStringMap = RuleStringIDs(this);
        RuleMap = CreateEntryFromIDDic(this);
        FactIdsMap = CreateFactIds();
    }

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

    public void CreateRulesFromRulescripts()
    {
        if (generateFrom != null)
        {
            rules.Clear();
            int factID = 0;
            int ruleID = 0;
            Dictionary<string, int> addedFactIDS = new Dictionary<string, int>();
            foreach (var ruleScript in generateFrom)
            {
                var parser = new RuleScriptParser();
                var path = "";
                #if UNITY_EDITOR
                path= AssetDatabase.GetAssetPath(ruleScript);
                var lastIndexOf = path.LastIndexOf('/');
                if (lastIndexOf == -1)
                {
                    lastIndexOf = path.LastIndexOf('\\');
                }

                if (lastIndexOf != -1)
                {
                    path = path.Substring(0, lastIndexOf +1);
                }
                #endif
                parser.GenerateFromText(ruleScript.text, rules, ref factID, ref addedFactIDS, ref ruleID,path);
            }

            OnRulesParsed?.Invoke();
        }
    }

    private Dictionary<string, int> CreateFactIds()
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

    public int StringId(string str)
    {
        if (RuleStringMap == null)
        {
            InitRuleDB();
        }

        int id = -1;
        if (!RuleStringMap.TryGetValue(str, out id))
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

    public int FactId(string str)
    {
        if (FactIdsMap == null)
        {
            InitRuleDB();
        }

        int id = -1;
        if (!FactIdsMap.TryGetValue(str, out id))
        {
            id = -1;
            Debug.Log($"did not find factID {id} for fact {str}");
        }
        else
        {
            //Debug.Log($"Found factID {id} for fact {str}");
        }

        return id;
    }

    public string GetFactVariabelNameFromFactID(int factID)
    {
        foreach (var strVal in FactIdsMap)
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
        if (RuleStringMap == null)
        {
            return "Non-inited";
        }

        foreach (var strVal in RuleStringMap)
        {
            if (strVal.Value == stringID)
                return strVal.Key;
        }

        return "NA";
    }

    public RuleDBEntry RuleFromID(int id)
    {
        if (RuleMap == null)
        {
            InitRuleDB();
        }

        RuleDBEntry rule;
        if (!RuleMap.TryGetValue(id, out rule))
        {
            rule = null;
        }

        return rule;
    }

    private static Dictionary<int, RuleDBEntry> CreateEntryFromIDDic(RulesDB current)
    {
        Dictionary<int, RuleDBEntry> dic = new Dictionary<int, RuleDBEntry>();
        foreach (var rule in current.rules)
        {
            var id = rule.RuleID;
            dic[id] = rule;
        }

        return dic;
    }

    private static Dictionary<string, int> RuleStringIDs(RulesDB current)
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
        for (int i = 0; i < current.rules.Count; i++)
        {
            var rule = current.rules[i];
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
}