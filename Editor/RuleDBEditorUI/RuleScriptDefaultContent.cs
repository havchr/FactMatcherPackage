using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RuleScriptDefaultContent : MonoBehaviour
{
    
    public static readonly string DocumentationContent = @"
-- .DOCS %label starts a documentation block
.DOCS rule_name
Documentation about all things related to rule_name.
As much text as you want to, until you reach .. on a single line, which indicates the end
of free form text.
..

--.FACT %fact_name starts documentation of a fact , contains a block of text until .. with
--as much text as you want.
.FACT player.age
as much text as you want here.
..

--newlines are ignored

--if we have a .IT CAN BE block after a .FACT block
--we list up the valid names for the previous fact
--see example below
.FACT player.name
this is player.name that can link names into a sequence, like Johnny, Bob, etc.
related to player.name
..

.IT CAN BE
	Johnny Lemon
    Bob Jonson
..
-- Use .. to end IT CAN BE

.FACT player.height
as much text as you want here.
..

.FACT player.health
as much text as you want here.
..

.FACT player.street_smart
as much text as you want here.
..

.FACT player.intelligence_level
as much text as you want here.
..

.END
    -- .END indicates the end of the document.
";
    
    public static string GetDefaultRuleScriptContent()
    {
        var rule1 = ".rule_name IF\n" +
                    "    player.age > 10\n" +
                    "    player.name = Johnny Lemon\n" +
                    ".THEN WRITE\n" +
                    "    --exmple of writing back to the fact system\n" +
                    "    player.age = 9\n" +
                    ".THEN RESPONSE\n" +
                    "rule matches if age is bigger than 10 and name is Johnny Lemon\n" +
                    ".END\n\n\n";

        var comment = "-- A comment starts with -- , but, remark,it is not handled within the response block..\n" +
                      "-- Variables start with a namespace. If none is given, it will be automatically be given global as the namespace\n" +
                      "-- The variables defined here will end up in generated c# code in FactMatcherCodeGenerator.cs\n" +
                      "-- Everything is case-sensitive..\n" +
                      "-- Deriving another rule allows you to copy all the checks of that rule. See rule below for an example\n\n\n"; 
        
        var comment_convections = "-- the prefered naming convection is snake_case , divide by under_score\n" +
                                  "-- Keywords are preferably written in upper caps\n" +
                                  "-- .THEN WRITE\n" +
                                  "-- .THEN RESPONSE\n" +
                                  "-- .THEN PAYLOAD\n" +
                                  "-- .IF\n" +
                                  "-- .OR\n" +
                                  "-- Deriving another rule allows you to copy all the checks of that rule. See rule below for an example\n\n\n"; 
        
        var rule2 = ".rule_name.derived IF\n" +
                    "   player.height > 180\n" +
                    "--You can also use Range which expands into to checks when the rulescript is parsed ( for exclusive [ for inclusive ..\n" +
                    "   player.health Range(5,25]\n" +
                    ".THEN RESPONSE\n" +
                    "rule matches if base rule .RuleName matches and height is bigger than 180\n" +
                    ".END\n\n\n";
        
        var commentOrGroups = "-- You can use OR by starting a test with IF and including OR statements further down\n" +
                              "-- a group of IF OR, is evaluated as matching if one of the elements in the OR group is true.\n" +
                              "-- for judging which rule has the most matches, a match in an OR group counts as 1, even though multiple ORS match\n" +
                              "-- \n\n\n"; 
        
        var rule3 = ".rule_or_group_example IF\n" +
                    "   IF player.height > 180\n" +
                    "      OR player.street_smart > 15\n" +
                    ".THEN RESPONSE\n" +
                    "you matched the rule , player.height is above 150 and/or street_smart is above 15\n" +
                    ".END\n\n\n";
        
        //var commentPayloads= "-- You can use the .THEN PAYLOAD keyword to list locations of Scriptable object resources\n" +
        //                     "-- Unity will then parse and make accessible that payload as a scriptable object when a rule is picked\n" +
        //                     "-- \n\n\n"; 
        
        var rule4 = ".rule_payload_example IF\n" +
                    "   player.height > 180\n" +
                    "   player.intelligence_level >= 10\n" +
                    ".THEN PAYLOAD\n" +
                    "    payload_example.asset\n" +
                    ".THEN RESPONSE\n" +
                    "Rule with payload example\n" +
                    ".END\n\n\n";

        return rule1 + comment + rule2 + commentOrGroups + rule3 + comment_convections + rule4;
    }
}
