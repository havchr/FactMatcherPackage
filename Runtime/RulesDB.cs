using System;
using System.Collections.Generic;
using FactMatcher;
using UnityEditor;
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
public class RuleDBAtomEntry
{
    public int factID;
    public string factName;
    public string matchString;
    public float matchValue;
    public Comparision compareMethod;
    public FactValueType compareType;

    public enum Comparision
    {
        Equal,NotEqual,LessThan,MoreThan,LessThanEqual,MoreThanEqual,Range
    }

    public FactMatcher.RuleCompare CreateCompare(RulesDB rules)
    {
        //used in the fact system 
        var val = compareType == FactValueType.String ?  rules.StringId(matchString) : matchValue;
        switch (compareMethod)
        {
            case Comparision.Equal:
                return FactMatcher.RuleCompare.Equals(val);
            case Comparision.NotEqual:
                return FactMatcher.RuleCompare.NotEquals(val);
            case Comparision.LessThan:
                return FactMatcher.RuleCompare.LessThan(val);
            case Comparision.LessThanEqual:
                return FactMatcher.RuleCompare.LessThanEquals(val);
            case Comparision.MoreThan:
                return FactMatcher.RuleCompare.MoreThan(val);
            case Comparision.MoreThanEqual:
                return FactMatcher.RuleCompare.MoreThanEquals(val);
        }
        return FactMatcher.RuleCompare.Equals(val);
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
    public List<RuleDBAtomEntry> atoms;
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
    
    public void InitRuleDB()
    {
        RuleStringMap = RuleStringIDs(this);
        RuleMap = CreateEntryFromIDDic(this);
        FactIdsMap = CreateFactIds();
    }
    
    public void CreateRulesFromRulescripts()
    {
        if (generateFrom != null)
        {
            rules.Clear();
            int factID = 0;
            int ruleID = 0;
            Dictionary<string,int> addedFactIDS = new Dictionary<string, int>();
            foreach (var ruleScript in generateFrom)
            {
                var parser = new RuleScriptParser();
                parser.GenerateFromText(ruleScript.text,rules,ref factID,ref addedFactIDS, ref ruleID); 
            }

        }
    }

    private Dictionary<string, int> CreateFactIds()
    {
        var result = new Dictionary<string,int>();
		foreach (var rule in rules)
		{

			foreach (var atom in rule.atoms)
            {
                result[atom.factName] = atom.factID;
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
        }
        return id;
    }
    
    public string  GetFactVariabelNameFromFactID(int factID)
    {
        foreach (var strVal in FactIdsMap)
        {
            if (strVal.Value == factID)
                return strVal.Key;
        }

        return "";
    }
    
    public string ParseFactValueFromFactID(int factID,float value)
    {
        foreach (var rule in rules)
        {
            foreach (var atom in rule.atoms)
            {
                if (atom.factID == factID && atom.compareType == FactValueType.String)
                {
                    return GetStringFromStringID((int)value);
                }
            }
        }
        return value.ToString();
    }
    public string  GetStringFromStringID(int stringID)
    {
        foreach (var strVal in RuleStringMap)
        {
            if (strVal.Value == stringID)
                return strVal.Key;
        }

        return "";
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
        Dictionary<int,RuleDBEntry>  dic = new Dictionary<int,RuleDBEntry>();
        foreach (var rule in current.rules)
        {
            var id = rule.RuleID;
            dic[id] = rule;
        }
        return dic;
    }

    private static Dictionary<string, int> RuleStringIDs(RulesDB current)
    {
        Dictionary<string, int>  dic = new Dictionary<string, int>();
        
        int id = 0;
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
                && factWrite.writeString!=null && factWrite.writeString.Length>=1)
                {
                    
                    if (!dic.ContainsKey(factWrite.writeString))
                    {
                        dic[factWrite.writeString] = id;
                        id++;
                    }
                }
            }

            foreach (var atom in rule.atoms)
            {
                if (atom.compareType == FactValueType.String && atom.matchString!=null && atom.matchString.Length>=1)
                {
                    if (!dic.ContainsKey(atom.matchString))
                    {
                        dic[atom.matchString] = id;
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

			foreach (var atom in rule.atoms)
			{
				if (atom.factID > topFactID)
				{
					topFactID = atom.factID;
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
    
	
}