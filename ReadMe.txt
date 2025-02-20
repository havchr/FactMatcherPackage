FactMatcher is a system that chooses the highest "scoring" rule, from
a database of many rules.

FactMatcher is heavily inspired by the dialogue system in Left4Dead,
see this talk By Elan Ruskin, https://www.youtube.com/watch?v=tAbBID3N64A&ab_channel=GDC
to see where the system is coming from.

A rule contains a list of factTests. 
A factTest is a logic test
which either checks a value against another value, or it can check
if a string is equal or not equal to another string.
Every rule is evaluated and given a score, and the highest scoring rule(or rules) is returned from the system.
If a rule has a factTest that fails, by default, it will score 0.

The FactValues are stored inside a RuleDB that has been initialized.
To initialize a RuleDB and start picking rules and manipulating facts,
the FactMatcher class, is what you use.


==Update to API==
Latest update, Documentations are now stored in a Scriptable object instead
of each RuleDB referencing multiple documentation files. This makes it easier to share 
a common set of documentation files.

There has been some updates to the API to make it more expressive from the application side of things.
There is now a distinction of Picking rules, and peeking rules. If you Pick - then the rule-write-back system,
will run for each rule you picked. If you Pick one single best rule, only that rule will write back with FactWrites.
Here is the Gist of it : 
PickBestRule - only write back to one rule
PickBestRules - write back to all rules that share the same bestMatch
PeekBestRule - return bestRule but do not write back to the system
PeekBestRules - return best rules - ie all rules sharing the same best match
PickAllValidRules - return number of valid rules, and run write-backs on all rules that have no failed tests
PeekAllValidRule - return number of valid rules, do not run write-back on them

GetRuleFromMatches(index) returns the rule , i.e if you have 
numRules = peekBestRules() // 2
then GetRuleFromMatches(0) and GetRuleFromMatches(1) would work.

to get the rules after running a "PeekAllValidRules" , use 
GetRuleFromValidMatches(index) where index is lower than result from PeekAllValidRules




To create rules - FactMatcher parses text files called RuleScripts, which follows this syntax:

-- This is comment.
-- Naming conventions , snake_case. 
-- Keywords are written in upper caps.
-- Indentation is optional, but makes it easier to read for humans.
.rule_name_with_template_ IF
    fact_test_string_example = hello world
    fact_test_value_example >= 1337
   ?weak_fact_test_example = 99
    IF fact_test_or_group_example_1 = TRUE 
    OR fact_test_or_group_example_2 = FALSE 
    OR fact_test_or_group_example_3 = pizza
.THEN WRITE
	fact_test_value += 3
	fact_test_value_2 (+=) fact_test_value 
.THEN PAYLOAD
	scriptableobject_path_relative_to_a_resource_folder
.THEN RESPONSE
A string response returned if the rule is picked
.END

This is quite dense, but shows a complete example of the syntax.
If a fact-test starts with ? , it is a weak test, meaning if the test fails.
The scoring of the rule does not get set to 0, it just does not add to its score.

We also see an example of OR-groups, which is a block that starts 
with a factTest prefixed and each test in the OR-group, starts with OR.
An entire OR-GROUP , will only score 1 point if any of its tests passes.

Next up is examples of writing back to the factValue database. This only gets written
if the rule scores the most and is picked. You can set a setting so that all the rules that are valid, that is, scores more than 1, also writes back to its rules. What you need depends on how you use the system.
The .THEN WRITE, is optional, so your rule does not have to write back if it does not want to.
The syntax, operands supported for writing back is as follows : 
fact_value += 1337 , adding a number to a fact_value 
fact_value -= 1337 , subtracting a number to a fact_value 
fact_value = 909 , setting the fact_value to a number
fact_value_str = some_string , setting the fact_value to a string 

And variants where you use another fact_value
fact_value (+=) fact_value_2, adding the value of fact_value_2 to fact_value 
fact_value (-=) fact_value_2, subtracting the value of fact_value_2 to fact_value 
fact_value (=) fact_value_2, setting the fact_value to the value of fact_value_2 
fact_value_str (=) fact_value_str_2, setting the fact_value_str to the value of fact_value_str_2 

The .THEN PAYLOAD , is optional, and expects a line below where the path to a ScriptableObject is located. This is relative to the resource folder.

Finally the 
.THEN RESPONSE
text
.END

is not optional,and defines the end of the rule.



To use the factMatcher
Create a RuleDB asset.
This RuleDB Asset will then be filled with RuleScripts,
which are text files containing dialog with some fact requirements.
These text-files gets parsed and populated the RuleDB asset, which in turns generates
a source file containing id's for each fact.

To get started , you can Create a RuleScript, which creates a default rulescript with
some examples and then use that with the RuleDB asset. Next up, to inspect
and play with your RuleDB , you can open the RuleDBWindow

Working with a RuleDB in your game/GameObject.
Create a FactMatcher and init with your RuleDB asset.

FactMatcher factMatcher = new FactMatcher(rulesDB);
factMatcher.Init();
factMatcher.SetFact(factMatcher.FactID("fact_test_value_example"), 1337.0f);

//remember to Disponse and de-init stuff.
if (factMatcher != null && factMatcher.HasDataToDispose())
{
	factMatcher.DisposeData();
	factMatcher = null;
}

== Template support ==
One can create rules that are templates for other rules.
Here is template_example.rule

.template_rule_%0 IF
	test = %1
	bunch_of_other_tests_1 = 8
	bunch_of_other_tests_2 = 9
.THEN RESPONSE
Hello %2
.END

and then one could write a rulescript that references 
the template file like this

.TEMPLATE = template_example.rule
	%0 = example
	%1 = test_string
	%2 = World
.TEMPLATE_END

and this would now insert a rule that is called
template_rule_example
with a test for 
test = test_string
and the response would be Hello World.


Getting the FactID is quite cumbersome.
Usually you would create a system that caches all the FactIDS you want 
your game to send. You can optionally also compile things to generated c#
which allows you to access the value database like this:

factMatcher[FactMatcherGen.fact_test_value_example] = 1337.0

Strings are stored as stringIDs, so you would do something like


factMatcher[FactMatcherGen.fact_test_string_example] = factMatcher.StringID("test") ;



You can compile this with Burst if you want to by adding a define FACTMATCHER_BURST

It should be quite performant with a large number of rules and facts,
but consider scoping your ruleDB for the current situation in your game.

For added performance , you can also divide your rules into buckets. The following rule
shows the syntax to create a bucket

.bucket_test_on_shot IF
    @concept = on_shot
    @who = Johnny Lemon
    player.health < 10
.THEN RESPONSE
rule matches if health is lower than 10 and name is Johnny Lemon
.END

This creates a bucket "concept:on_shot,who:Johnny Lemon"
The main idea is that all rules that deals with the Johnny Lemon character being shot,
are put into this bucket, and the application can then query against only this specific bucket
when rule matching.
From the application side, the way you use this - is as follows: 

In the example below, we look at the bucket that is 
concept:on_shot,who:everybody

public class FactMatcherBucketPerfTest : MonoBehaviour, FactMatcherProvider
{
    public RulesDB rules;
    private FactMatcher _matcher;
    public RuleDBEntry lastRulePicked;
    private BucketSlice OnShotEveryBodyBucket;
    public string who = "everybody";
    public string concept = "onShot";
    
    // Start is called before the first frame update
    void Start()
    {
        _matcher = new FactMatcher(rules);
        _matcher.Init();
        OnShotEveryBodyBucket = _matcher.BucketSlice($"concept:{concept},who:{who}");
    }

    private void OnDestroy()
    {
        if (_matcher.IsInited && _matcher.HasDataToDispose())
        {
            _matcher.DisposeData();
            _matcher = null;
        }
    }


    // Update is called once per frame
    void Update()
    {
        lastRulePicked = _matcher.PickRuleInBucket(OnShotEveryBodyBucket);
    }

    public FactMatcher GetFactMatcher()
    {
        return _matcher;
    }
}



Credits:
Agens
Håvard Christensen
Additional programming Ole Anders Astad
Feedback, input and support Knut Clausen
Feedback, input and support Trond Abusdal 

Licence 
The MIT License (MIT)
Copyright © 2023 <copyright holders>

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the “Software”), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
