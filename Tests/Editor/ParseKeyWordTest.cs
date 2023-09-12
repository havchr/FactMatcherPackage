using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using UnityEngine;
using UnityEngine.TestTools;

public class ParseKeyWordTest 
{

    [Test]
    public void KeywordParsingTest()
    {
        string[] stringsToTest =
        {
            "_1_",
            "_6928_",
            "_672958406809854908274895270864987932473709267_",
            "_6729584068098549082748952708649879324737092676729584068098549082748952708649879324737092671234567890_",
        };

        string option1 = "option 1";
        string option2 = "option 2";
        string strippedTest = $"{option1}\n{option2}";
        string testString = $"[op good]{option1}[/op]\n" +
                            $"[op bad]{option2}[/op]";
        
        var KeyWords = Payload.GetKeyWordsFromString(testString, out string stripedText);
        Assert.IsTrue(stripedText.Equals(strippedTest));
        Assert.IsTrue(KeyWords[0].index == 0);
        Assert.IsTrue(KeyWords[1].index == option1.Length);
        Assert.IsTrue(KeyWords[2].index == option1.Length+1);
        Assert.IsTrue(KeyWords[3].index == option1.Length+1 + option2.Length);


        List<string> extractedBetween = new List<string>();
        int startIndex = 0;
        foreach (var keyword in KeyWords)
        {
            if (keyword.KeywordType == Payload.Keyword.KeywordTypeEnum.Start)
            {
                startIndex = keyword.index;
            }
            if (keyword.KeywordType == Payload.Keyword.KeywordTypeEnum.End)
            {
                extractedBetween.Add(stripedText.Substring(startIndex,keyword.index-startIndex));
               //extractedBetween.Add("extraction"); 
            }
        }
        Assert.IsTrue(extractedBetween.Count == 2);
        
    }

}
