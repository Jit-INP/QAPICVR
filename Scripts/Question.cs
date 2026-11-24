using UnityEngine;
using System.Collections.Generic;

// This script defines the structure for a single question and its answers.
// The [System.Serializable] attribute allows this class to be displayed and
// edited directly in the Unity Inspector.

[System.Serializable]
public class Question
{
    // The main text of the question
    public string questionText = "What is your confidence in the perceived depth?";

    // The list of possible text answers (e.g., "Very Low", "Low", "Medium", "High", "Very High")
    public List<string> answers = new List<string> { "Answer A", "Answer B", "Answer C" };
}
