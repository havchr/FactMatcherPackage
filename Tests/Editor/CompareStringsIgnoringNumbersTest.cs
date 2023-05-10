using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using NUnit.Framework.Constraints;
using UnityEngine;
using UnityEngine.TestTools;

public class CompareStringsIgnoringNumbersTest
{
    [Test]
    public void NumberAfterIgnoreNumberExpression_TestSimpleThrowException()
    {
        Assert.Throws<Exception>(() => DocumentEntry.CompareStringsIgnoringNumbers("_#3_#_", "_368275698275691652_6782364_"));
        Assert.Throws<Exception>(() => DocumentEntry.CompareStringsIgnoringNumbers("_#336_#_", "_368275698275691652_6782364_"));
    }

    [Test]
    public void CompareStringsIgnoringNumbers_MultipleTestsSimple()
    {
        Assert.IsTrue(DocumentEntry.CompareStringsIgnoringNumbers("_#", "_1"));
        Assert.IsTrue(DocumentEntry.CompareStringsIgnoringNumbers("#", "1"));

        Assert.IsTrue(DocumentEntry.CompareStringsIgnoringNumbers("_#_", "_1_"));
        Assert.IsTrue(DocumentEntry.CompareStringsIgnoringNumbers("_#_", "_12_"));
        Assert.IsTrue(DocumentEntry.CompareStringsIgnoringNumbers("_#_", "_68275698275691652_"));
        Assert.IsTrue(DocumentEntry.CompareStringsIgnoringNumbers("_##_", "_368275698275691652_"));

        Assert.IsFalse(DocumentEntry.CompareStringsIgnoringNumbers("_#_", "__"), "\"_#_\", \"__\"");
        Assert.IsFalse(DocumentEntry.CompareStringsIgnoringNumbers("_#_", "_A_"));
        Assert.IsFalse(DocumentEntry.CompareStringsIgnoringNumbers("_#_", "_#_"));
    }

    [Test]
    public void MultipleCompareStringsIgnoringNumbers_MultipleTestsSimple()
    {
        Assert.IsTrue(DocumentEntry.CompareStringsIgnoringNumbers("#_#_", "1_3_"));
        Assert.IsTrue(DocumentEntry.CompareStringsIgnoringNumbers("#_#", "1_3"));

        Assert.IsTrue(DocumentEntry.CompareStringsIgnoringNumbers("_#_#_", "_1_3_"));
        Assert.IsTrue(DocumentEntry.CompareStringsIgnoringNumbers("_#_#_", "_1_32_"));
        Assert.IsTrue(DocumentEntry.CompareStringsIgnoringNumbers("_#_#_", "_12_1_"));
        Assert.IsTrue(DocumentEntry.CompareStringsIgnoringNumbers("_#_#_", "_12_12_"));
        Assert.IsTrue(DocumentEntry.CompareStringsIgnoringNumbers("_#_#_", "_68275698275691652_6782364_"));
        Assert.IsTrue(DocumentEntry.CompareStringsIgnoringNumbers("_#_3_", "_368275698275691652_3_"));

        Assert.IsTrue(DocumentEntry.CompareStringsIgnoringNumbers("_3#_#_", "_368275698275691652_6782364_"), "Testing number before # and compare string start with same number");
        Assert.IsFalse(DocumentEntry.CompareStringsIgnoringNumbers("_3#_#_", "_168275698275691652_6782364_"), "Testing number before # and compare string start with different number");

        Assert.IsFalse(DocumentEntry.CompareStringsIgnoringNumbers("_#_#_", "_A_b_"));
        Assert.IsFalse(DocumentEntry.CompareStringsIgnoringNumbers("_#_#_", "_#_#_"));

        Assert.IsFalse(DocumentEntry.CompareStringsIgnoringNumbers("_##_3", "_368275698275691652_#"));
        Assert.IsFalse(DocumentEntry.CompareStringsIgnoringNumbers("_#_#_", "_368275698275691652_"));
    }

    [Test]
    public void MultipleStrings_TestIsTrue()
    {
        string[] stringsToTest =
        {
            "_1_",
            "_6928_",
            "_672958406809854908274895270864987932473709267_",
            "_6729584068098549082748952708649879324737092676729584068098549082748952708649879324737092671234567890_",
        };
        string targetString = "_#_";

        foreach (string s in stringsToTest)
        {
            Assert.IsTrue(DocumentEntry.CompareStringsIgnoringNumbers(targetString, s), $"String tested was: {s}");
        }
    }

    [Test]
    public void MultipleStringsSpecialCharacters_TestIsFalse()
    {
        string[] stringsToTest =
        {
            "_!_", "_\"_", "_@_", "_#_", "_г_", "_д_", "_$_", "_%_", "_&_", "_/_",
            "_{_", "_(_",  "_[_", "_]_", "_)_", "_=_", "_}_", "_|_", "_з_", "_`_",
            "_┤_", "_\\_", "__3", "__#", "_!\"`@#гд$%&/{([])=}?+\\|з_",
            "_6729584068098549082748952708649879324737092676729584`068098549082748952708649879324737092671234567890_",
            "_6729584068098549082748952708649879324737092676729584068098549082748952708649879324737092671234567890_2",
        };
        string targetString = "_#_";

        foreach (string s in stringsToTest)
        {
            Assert.IsFalse(DocumentEntry.CompareStringsIgnoringNumbers(targetString, s), $"String tested was: {s}");
        }
    }
}
