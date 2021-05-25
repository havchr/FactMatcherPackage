To use the factMatcher
Create a RuleDB asset.
This RuleDB Asset will then be filled with RuleScripts,
which are text files containing dialog with some fact requirements.
These text-files gets parsed and populated the RuleDB asset, which in turns generates
a source file containing id's for each fact.

Not you must manually "compile" by turning on the "generate switch" on the RuleDB asset.
Since this generates source code, you can get some annoying errors if something goes wrong.

Create a gameobject that contains a FactMatcherJobSystem component and hook up a RuleDB to it.
then add your own script with your own logic - and when you need to pick a rule , call the FactMatcherJobSystem.

You can compile this with Burst if you want to by adding a define FACTMATCHER_BURST

It should be quite performant with a large number of rules and facts,
but consider scoping your ruleDB for the current situation in your game.
