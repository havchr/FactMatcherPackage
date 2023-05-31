using System.Collections.Generic;

public class BucketSlice
{
    public BucketSlice(int startIndex, int endIndex,string bucketLine)
    {
        this.startIndex = startIndex;
        this.endIndex = endIndex;
        this.bucketLine = bucketLine;
        factMap = new Dictionary<string, float>();
        factIds = new List<int>();
        factValues = new List<float>();
    }

    public static BucketSlice CreateNullBucket()
    {
        return new BucketSlice(-1, -1, null);
    }
    
    public void Init(FactMatcher fm)
    {
        if (bucketLine == null)
        {
            return;
        }
        var splits = bucketLine.Split(",");
        for (int i = 0; i < splits.Length; i++)
        {
            var keyValue = splits[i].Split(":");
            if (keyValue.Length != 2)
            {
                break;
            }
            if (float.TryParse(keyValue[1], out float result))
            {
                factMap[keyValue[0]] = result;
            }
            else
            {
                factMap[keyValue[0]] = fm.StringID(keyValue[1]);
            }

            factIds.Add(fm.FactID(keyValue[0]));    
            factValues.Add(factMap[keyValue[0]]);    
        }
    }

    public void ApplyBucket(FactMatcher fm)
    {
        if (bucketLine!=null)
        {
            for (int i = 0; i < factIds.Count; i++)
            {
                fm[factIds[i]] = factValues[i];
            }
        }
    }
    public int startIndex;
    public int endIndex;
    public string bucketLine;
    public List<int> factIds;
    public List<float> factValues;
    private Dictionary<string, float> factMap;

    public bool IsNullBucket()
    {
        return startIndex == -1;
    }
}

public class BucketSlicer  
{

    /*
     * This function calculates the start and end indices for our buckets.
     * A bucket is specific sets of the ruleDB that we limit in order to make queries faster.
     * an example is if we create a bucket for
     * concept = onShot
     * and all our rules that has concept = onShot will be in a specific slice of the array
     * so that when we PickRulesForConcept(onShot) , only need to check a specific part of
     * of all of our rules.
     *
     * For a reference of this technique , see the Valve video on Optimization #3 ,
     * hierarchical partition
     * https://www.youtube.com/watch?v=tAbBID3N64A&t=2751s&ab_channel=GDC
     *
     * Gotchas.
     * This function depends on the ruleParser having stored IDS in the bucketSliceStartIndex
     * the ID is an int for every unique bucket we have, with increasing value, and the bucket name is stored in
     * rule.bucket
     *
     * First step - loop through and figure out the length of our buckets.
     * Then convert bucketID and length of bucket, into actual BucketSlice Indices
     *
     * Example :
     * five rules
     * rule 1 default bucket, bucketIndex 0
     * rule 2 default bucket, bucketIndex 0
     * rule 3 onShot bucket, bucketIndex 1
     * rule 4 onDied bucket, bucketIndex 2
     * rule 5 onDied bucket, bucketIndex 2
     *
     * We run the first step to get the length of each bucket
     * rule 1 default bucket, bucketIndex 0 , length 2
     * rule 2 default bucket, bucketIndex 0, length 2
     * rule 3 onShot bucket, bucketIndex 1, length 1
     * rule 4 onDied bucket, bucketIndex 2, length 2
     * rule 5 onDied bucket, bucketIndex 2, length 2
     *
     * Convert into actual indices
     * 0 rule 1 default bucket, bucketIndex 0 , bucketEndIndex 1
     * 1 rule 2 default bucket, bucketIndex 0, bucketEndIndex 1
     * 2 rule 3 onShot bucket, bucketIndex 2, bucketEndIndex 2
     * 3 rule 4 onDied bucket, bucketIndex 3, bucketEndIndex 4
     * 4 rule 5 onDied bucket, bucketIndex 3, bucketEndIndex 4
     */
        public static List<RuleDBEntry> SliceIntoBuckets(List<RuleDBEntry> rules)
        {
            rules = StoreBucketLengthInEndIndices(rules);
            int index = 0;
            rules[0].bucketSliceStartIndex = index;
            rules[0].bucketSliceEndIndex -= 1;
            for (int i = 1; i < rules.Count; i++)
            {
                var rule = rules[i];
                var prevRule= rules[i-1];
                var bucketIndexNow = rule.bucketSliceStartIndex; 
                if (bucketIndexNow != index)
                {
                    index = bucketIndexNow;
                    rule.bucketSliceStartIndex = prevRule.bucketSliceEndIndex+1; 
                }
                else
                {
                    rule.bucketSliceStartIndex = prevRule.bucketSliceStartIndex; 
                }
                rule.bucketSliceEndIndex += rule.bucketSliceStartIndex-1; 
            }
            return rules;
        }
        
        public static List<RuleDBEntry>StoreBucketLengthInEndIndices(List<RuleDBEntry> rules)
        {
            Dictionary<int, int> buckedEnd = new Dictionary<int, int>();
            for (int i = 0; i < rules.Count; i++)
            {
                if (buckedEnd.ContainsKey(rules[i].bucketSliceStartIndex))
                {
                    buckedEnd[rules[i].bucketSliceStartIndex]++;
                }
                else
                {
                    buckedEnd[rules[i].bucketSliceStartIndex] = 1;
                }
            }

            for (int i = 0; i < rules.Count; i++)
            {
                rules[i].bucketSliceEndIndex = buckedEnd[rules[i].bucketSliceStartIndex];
            }
            return rules;
        }
}
