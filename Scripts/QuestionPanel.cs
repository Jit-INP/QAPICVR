using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;
using System;

public class QuestionPanel : MonoBehaviour
{
    [Header("UI Elements")]
    [Tooltip("The TextMeshPro element to display the question.")]
    public TextMeshProUGUI questionTextUI;

    [Tooltip("The parent container where answer buttons will be instantiated (e.g., a Vertical Layout Group).")]
    public Transform answersContainer;

    [Tooltip("The template button to clone for each possible answer.")]
    public Button answerButtonTemplate;

    [Tooltip("The main button to submit the selected answer.")]
    public Button submitButton;

    [Header("Testing Data")]
    [Tooltip("A list of all questions for the test session.")]
    public List<Question> testQuestions = new List<Question>();

    [Header("Visual Configuration")]
    [Tooltip("The default color for unselected answer buttons.")]
    public Color defaultButtonColor = Color.white;
    [Tooltip("The highlight color for the currently selected answer button.")]
    public Color selectedButtonColor = new Color(0.9f, 0.8f, 0.4f);

    // --- Private State ---
    private int currentQuestionIndex = 0;
    private string selectedAnswer = null;
    private bool isPanelVisible = false;
    private Button highlightedButton = null;

    // Callback for orchestrator and data saver
    public Action OnQuestionnaireComplete;
    public Action<string, string> OnQuestionAnswered; // (questionText, answer)

    void Awake()
    {
        gameObject.SetActive(false);
        answerButtonTemplate.gameObject.SetActive(false);
        submitButton.onClick.RemoveAllListeners();
        submitButton.onClick.AddListener(SubmitAnswer);
        submitButton.interactable = false;
        ColorBlock colors = answerButtonTemplate.colors;
        colors.normalColor = defaultButtonColor;
        answerButtonTemplate.colors = colors;
    }

    public void StartQuestionnaire()
    {
        if (testQuestions.Count == 0)
        {
            Debug.LogError("Question list is empty. Cannot start questionnaire.");
            return;
        }
        currentQuestionIndex = 0;
        gameObject.SetActive(true);
        isPanelVisible = true;
        DisplayCurrentQuestion();
    }

    public void HidePanel()
    {
        gameObject.SetActive(false);
        isPanelVisible = false;
    }

    private void DisplayCurrentQuestion()
    {
        if (currentQuestionIndex >= testQuestions.Count)
        {
            Debug.Log("Questionnaire complete!");
            HidePanel();
            OnQuestionnaireComplete?.Invoke();
            return;
        }

        Question currentQuestion = testQuestions[currentQuestionIndex];
        questionTextUI.text = currentQuestion.questionText;
        ClearAnswerButtons();
        selectedAnswer = null;
        highlightedButton = null;
        submitButton.interactable = false;

        foreach (string answerText in currentQuestion.answers)
        {
            Button newButton = Instantiate(answerButtonTemplate, answersContainer);
            newButton.gameObject.SetActive(true);
            TextMeshProUGUI buttonText = newButton.GetComponentInChildren<TextMeshProUGUI>();
            if (buttonText != null)
            {
                buttonText.text = answerText;
            }
            newButton.onClick.RemoveAllListeners();
            newButton.onClick.AddListener(() => OnAnswerSelected(answerText, newButton));
        }
    }

    private void ClearAnswerButtons()
    {
        foreach (Transform child in answersContainer)
        {
            if (child.gameObject != answerButtonTemplate.gameObject)
            {
                Destroy(child.gameObject);
            }
        }
    }

    private void OnAnswerSelected(string answer, Button clickedButton)
    {
        if (highlightedButton != null)
        {
            ColorBlock resetColors = highlightedButton.colors;
            resetColors.normalColor = defaultButtonColor;
            resetColors.highlightedColor = defaultButtonColor;
            resetColors.pressedColor = defaultButtonColor;
            resetColors.selectedColor = defaultButtonColor;
            highlightedButton.colors = resetColors;
        }

        selectedAnswer = answer;
        highlightedButton = clickedButton;

        ColorBlock clickedColors = clickedButton.colors;
        clickedColors.normalColor = selectedButtonColor;
        clickedColors.highlightedColor = selectedButtonColor;
        clickedColors.pressedColor = selectedButtonColor;
        clickedColors.selectedColor = selectedButtonColor;
        clickedButton.colors = clickedColors;

        submitButton.interactable = true;
    }

    private void SubmitAnswer()
    {
        if (selectedAnswer == null)
        {
            Debug.LogWarning("Please select an answer before submitting.");
            return;
        }

        string questionText = testQuestions[currentQuestionIndex].questionText;
        Debug.Log($"Recorded Answer: Q='{questionText}' A='{selectedAnswer}'");

        // Notify data saver
        OnQuestionAnswered?.Invoke(questionText, selectedAnswer);

        currentQuestionIndex++;
        DisplayCurrentQuestion();
    }
}