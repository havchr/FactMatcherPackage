using System.Collections.Generic;
using FactMatching;
using UnityEngine;
using System;

[Serializable]
public class Payload
{
    public Payload(string payloadText) => RawText = payloadText;

    [SerializeField]
    private string rawText = string.Empty;

    /// <summary>
    /// This will set the rawText and also automatically setup the rest of the Payload based on the rawText.
    /// </summary>
    public string RawText
    {
        get => rawText;
        set
        {
            KeyWords = GetKeyWordsFromString(value, out string stripedText);
            StrippedText = stripedText;
            rawText = value;
        }
    }

    [SerializeField]
    private string strippedText = string.Empty;
    public string StrippedText
    {
        get => strippedText;
        private set => strippedText = value;
    }

    [SerializeField]
    private List<Keyword> keyWords;
    public List<Keyword> KeyWords
    {
        get => keyWords;
        private set => keyWords = value;
    }

    public static List<Keyword> GetKeyWordsFromString(string input, out string strippedText)
    {
        strippedText = string.Empty;
        List<Keyword> result = new();

        string keyword = string.Empty;
        bool parsingKeyword = false;
        int keywordID = 0;
        int index = 0;
        foreach (char character in input)
        {
            if (character == '[')
            {
                parsingKeyword = true;
            }
            else if (character == ']')
            {
                parsingKeyword = false;
            }
            else if (!parsingKeyword)
            {
                strippedText += character;
                index++;
            }

            if (parsingKeyword && character != '\n')
            {
                keyword += character;
            }
            else if (!keyword.IsNullOrWhitespace())
            {
                result.Add(new(keyword, keywordID++, index));
                keyword = string.Empty;
            }
        }
        return result;
    }

    public List<Keyword> GetKeywordsAtIndex(int index)
    {
        List<Keyword> result = new();
        foreach (var keyword in KeyWords)
        {
            if (keyword.index == index)
            {
                result.Add(keyword);
            }
        }
        return result;
    }

    public void UpdateKeywordParameters(string changeFrom, string changeTo, char variableSymbol = '$')
    {
        rawText = rawText.Replace(variableSymbol + changeFrom, changeTo);
        if (!KeyWords.IsNullOrEmpty())
        {
            foreach (Keyword keywords in KeyWords)
            {
                keywords.UpdateParameters(changeFrom, changeTo, variableSymbol);
            }
        }
    }

    public Payload UpdateKeywordParameters(RuleDBEntry ruleDB, char variableSymbol = '$')
    {
        if (!KeyWords.IsNullOrEmpty())
        {
            foreach (Keyword keywords in KeyWords)
            {
                keywords.UpdateParameters(ruleDB, variableSymbol);
            }
        }
        return this;
    }

    public string DebugKeyWordStrippedText()
    {
        string strippedTextIndicatedKeywords = strippedText;
        for (int i = strippedText.Length; i > -1; i--)
        {
            if (!GetKeywordsAtIndex(i).IsNullOrEmpty())
            {
                strippedTextIndicatedKeywords = strippedTextIndicatedKeywords.Insert(i, "|");
            }
        }
        Debug.Log($"Debugged StrippedText: {strippedTextIndicatedKeywords}");
        return strippedTextIndicatedKeywords;
    }

    public override string ToString()
    {
        string result =
            $"Raw text = ({RawText})\n" +
            $"Stripped text = ({StrippedText})";


        if (!KeyWords.IsNullOrEmpty())
        {
            foreach (Keyword keyWord in KeyWords)
            {
                result += "\n\n" + keyWord.ToString();
            }
        }

        return result.Trim();
    }

    [Serializable]
    public class Keyword
    {
        public string keyword;
        public List<Parameter> parameters = new();

        [SerializeField]
        private KeywordTypeEnum keywordType;
        public KeywordTypeEnum KeywordType { get => keywordType; private set => keywordType = value; }
        public int index = -1;

        [SerializeField]
        private int keywordID = -1;
        public int KeywordID { get => keywordID; set => keywordID = value; }

        public Keyword(string keyword, int KeywordID, int index)
        {
            this.KeywordID = KeywordID;
            this.index = index;

            char divider = ' ';
            keyword = keyword.Trim().TrimStart('[').TrimEnd(']');
            if (keyword.Contains(divider))
            {
                string[] splitted = keyword.Split(divider);

                keyword = splitted[0];

                if (splitted.Length > 1)
                {
                    for (int i = 1; i < splitted.Length; i++)
                    {
                        parameters.Add(new(splitted[i], i));
                    }
                }
            }

            if (keyword.Trim().StartsWith('/'))
            {
                KeywordType = KeywordTypeEnum.End;
                keyword = keyword.TrimStart('/');
            }
            else
            {
                KeywordType = KeywordTypeEnum.Start;
            }

            this.keyword = keyword;
        }

        public void UpdateParameters(string changeFrom, string changeTo, char variableSymbol = '$')
        {
            if (!parameters.IsNullOrEmpty())
            {
                foreach (var parameter in parameters)
                {
                    parameter.UpdateParameter(changeFrom, changeTo, variableSymbol);
                }
            }
        }

        public void UpdateParameters(RuleDBEntry ruleDB, char variableSymbol = '$')
        {
            if (!parameters.IsNullOrEmpty())
            {
                foreach (var parameter in parameters)
                {
                    if (parameter.parameter.StartsWith(variableSymbol))
                    {
                        parameter.UpdateParameter(ruleDB, variableSymbol); 
                    }
                }
            }
        }

        [Serializable]
        public enum KeywordTypeEnum
        {
            Start,
            End,

            NoKeyword
        }

        public override string ToString()
        {
            string result =
                $"Keyword = {keyword}\n" +
                $"KeywordID = {KeywordID}\n" +
                $"Keyword type = {KeywordType}\n" +
                $"Start index = {index}";

            if (!parameters.IsNullOrEmpty())
            {
                result += $"\n\nParameters:";
                foreach (var param in parameters)
                {
                    result += "\n\n" + param.ToString();
                }
            }

            return result;
        }

        [Serializable]
        public class Parameter
        {
            public string parameter;
            public float parameterValue;
            public int parameterID;

            [SerializeField]
            private FactValueType factValueType;

            public FactValueType FactValueType
            { get => factValueType; private set => factValueType = value; }

            public Parameter(string parameterInput, int parameterID)
            {
                FactValueType = float.TryParse(parameterInput, out float result) ? FactValueType.Value : FactValueType.String;
                switch (FactValueType)
                {
                    case FactValueType.String:
                        parameter = parameterInput.Trim();
                        break;
                    case FactValueType.Value:
                        parameter = result.ToString();
                        parameterValue = result;
                        break;
                }

                this.parameterID = parameterID;
            }

            public Parameter UpdateParameter(string changeFrom, string changeTo, char variableSymbol = '$')
            {
                if (parameter.StartsWith(variableSymbol))
                {
                    if (parameter.TrimStart(variableSymbol) == changeFrom)
                    {
                        parameter = changeTo;
                        FactValueType = int.TryParse(changeTo, out _) ? FactValueType.Value : FactValueType.String;
                    }
                }

                return this;
                throw new NotImplementedException();
            }

            public Parameter UpdateParameter(RuleDBEntry ruleDB, char variableSymbol = '$')
            {
                if (parameter.StartsWith(variableSymbol))
                {
                    foreach (var factTest in ruleDB.factTests)
                    {
                        if (factTest.compareType == FactValueType.String && parameter.Trim().TrimStart(variableSymbol) == factTest.factName)
                        {
                            parameter = factTest.matchString;
                            FactValueType = int.TryParse(factTest.matchString, out _) ? FactValueType.Value : FactValueType.String;
                        } 
                    }
                }

                return this;
                throw new NotImplementedException();
            }

            public override string ToString()
            {
                string result = parameter == null ? string.Empty :
                    $"Parameter = {parameter}\n" +
                    $"ParameterID = {parameterID}\n" +
                    $"FactValueType = {factValueType}";

                return result;
            }
        }
    }
}
