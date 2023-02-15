using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

public class PayloadInterpolationParser  
{
    private enum State
    {
        PotentialStringInterpolation,
        PotentialValueInterpolation,
        InValueInterpolation,
        InValueFormatInterpolation,
        InStringInterpolation,
        ReadingString,
        AddingStringIterpolation,
        AddingValueInterpolation
    }
    private struct StateTransformer
    {
        public StateTransformer(char keyword, State requirement)
        {
            this.keyword = keyword;
            this.stateRequirement = requirement;
        }
        
        public char keyword;
        public State stateRequirement;
    }
    public void Parsey(RuleDBEntry ruleDBEntry,ref Dictionary<string, int> addedFactIDS)
    {
        State state = State.ReadingString;
        Dictionary<StateTransformer, State> parser = new Dictionary<StateTransformer, State>();
        parser.Add(new StateTransformer('#',State.ReadingString),State.PotentialValueInterpolation);
        parser.Add(new StateTransformer('{',State.PotentialValueInterpolation),State.InValueInterpolation);
        parser.Add(new StateTransformer('}',State.InValueInterpolation),State.InValueFormatInterpolation);
        parser.Add(new StateTransformer(' ',State.InValueFormatInterpolation),State.AddingValueInterpolation);
        
        parser.Add(new StateTransformer('$',State.ReadingString),State.PotentialStringInterpolation);
        parser.Add(new StateTransformer('{',State.PotentialStringInterpolation),State.InStringInterpolation);
        parser.Add(new StateTransformer('}',State.InStringInterpolation),State.AddingStringIterpolation);

        ruleDBEntry.interpolations = new List<RulePayloadInterpolation>();
        RulePayloadInterpolation payloadInterpolation = null;
        //The interpolationVariable , i.e what is inside the interpolation example,
        //${this_is_inside} , expect this_is_inside after parsing
        StringBuilder interpolationVariable = new StringBuilder();
        StringBuilder valueFormatInterpolation = new StringBuilder();
        
        for (int i = 0; i < ruleDBEntry.payload.Length; i++)
        {
            char theChar = ruleDBEntry.payload[i];
            StateTransformer stateTransformer = new StateTransformer(theChar, state);
            if (parser.ContainsKey(stateTransformer))
            {
                state = parser[stateTransformer];
                state = HandleNewState(ruleDBEntry, addedFactIDS,  state, interpolationVariable, i, valueFormatInterpolation, ref payloadInterpolation);
            }
            else
            {
                switch (state)
                {
                    case State.InStringInterpolation:
                    case State.InValueInterpolation:
                        interpolationVariable.Append(theChar);
                        break;
                    case State.InValueFormatInterpolation:
                        valueFormatInterpolation.Append(theChar);
                        break;
                }
            }
        }
        //Handle case of ValueInterpolation at end of string.
        if (state == State.InValueFormatInterpolation)
        {
            state = State.AddingValueInterpolation; 
            HandleNewState(ruleDBEntry, addedFactIDS,  state, interpolationVariable, ruleDBEntry.payload.Length-1, valueFormatInterpolation, ref payloadInterpolation);
        }

        int currentInterpolationIndex = 0;
        StringBuilder strb = new StringBuilder();
        if( currentInterpolationIndex < ruleDBEntry.interpolations.Count )
        {
            RulePayloadInterpolation interpolation = ruleDBEntry.interpolations[currentInterpolationIndex];
            bool inInterpolation = false;
            for (int i = 0; i < ruleDBEntry.payload.Length; i++)
            {
                if (i >= interpolation.payLoadStringStartIndex && i <=interpolation.payLoadStringEndIndex)
                {
                }
                else
                {
                    strb.Append(ruleDBEntry.payload[i]);
                }
                if (i == interpolation.payLoadStringEndIndex)
                {
                    strb.Append($"FactIndex_{interpolation.factValueIndex}");
                    currentInterpolationIndex++;
                    if (currentInterpolationIndex >= ruleDBEntry.interpolations.Count)
                    {
                        break;
                    }
                    interpolation = ruleDBEntry.interpolations[currentInterpolationIndex];
                }
            }
        }
    }

    private static State HandleNewState(RuleDBEntry ruleDBEntry, Dictionary<string, int> addedFactIDS, 
        State state, StringBuilder interpolationVariable, int i,
        StringBuilder valueFormatInterpolation, ref RulePayloadInterpolation payloadInterpolation)
    {
        switch (state)
        {
            case State.InStringInterpolation:
            case State.InValueInterpolation:
                interpolationVariable.Clear();
                payloadInterpolation = new RulePayloadInterpolation();
                payloadInterpolation.payLoadStringStartIndex = i-1;
                payloadInterpolation.type =
                    state == State.InStringInterpolation ? FactValueType.String : FactValueType.Value;
                break;
            case State.InValueFormatInterpolation:
                valueFormatInterpolation.Clear();
                break;
            case State.AddingStringIterpolation:
                if (!AddPayLoadInterpolation(ruleDBEntry, addedFactIDS, payloadInterpolation, i,
                        interpolationVariable))
                {
                    //todo add Error handling
                }

                state = State.ReadingString;
                break;
            case State.AddingValueInterpolation:
                payloadInterpolation.numberFormat = valueFormatInterpolation.ToString();
                if (!AddPayLoadInterpolation(ruleDBEntry, addedFactIDS, payloadInterpolation, i-1,
                        interpolationVariable))
                {
                    //todo add Error handling
                }

                state = State.ReadingString;
                break;
        }

        return state;
    }

    private static bool AddPayLoadInterpolation(RuleDBEntry ruleDBEntry, Dictionary<string, int> addedFactIDS,
        RulePayloadInterpolation payloadInterpolation, int i, StringBuilder interpolationVariable)
    {
        payloadInterpolation.payLoadStringEndIndex = i;
        if (!addedFactIDS.TryGetValue(interpolationVariable.ToString(),
                out payloadInterpolation.factValueIndex))
        {
            payloadInterpolation.factValueIndex = -1;
            //todo add error handling here. 
            return false;
        }
        ruleDBEntry.interpolations.Add(payloadInterpolation);
        return true;
    }
}
