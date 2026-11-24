using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Random = UnityEngine.Random;
using UnityEngine.SceneManagement;

public class Orchestrator : MonoBehaviour
{
    [Header("UI & Logic")]
    public QuestionPanel questionPanel;
    public PointCloudGenerator pointCloudDisplay;
    public GameObject completionPopup;
    public UserIdInputPanel userIdInputPanel;
    public TestDataSaver dataSaver; // NEW: Reference to data saver

    [Header("Scene Configuration")]
    public string resourceBasePath = "RGBD/";
    public List<int> availableSceneIDs = new List<int>();

    private enum TestState
    {
        Idle,
        AwaitingUserId,
        TrialPC,
        TrialQuestionnaire,
        DisplayingPC,
        WaitingForInput,
        QuestionnaireActive,
        Finished
    }

    private TestState currentState = TestState.Idle;
    private List<int> shuffledSceneIDs = new List<int>();
    private int currentSceneIndex = 0;
    private bool trialCompleted = false;
    private string userId = null;

    void Start()
    {
        if (questionPanel == null || pointCloudDisplay == null)
        {
            Debug.LogError("Orchestrator missing QuestionPanel or PointCloudDisplay reference.");
            currentState = TestState.Finished;
            return;
        }

        // Ensure dataSaver exists
        if (dataSaver == null)
        {
            dataSaver = gameObject.AddComponent<TestDataSaver>();
            Debug.Log("Orchestrator auto-created TestDataSaver component");
        }

        // Hide completion popup initially
        if (completionPopup != null && completionPopup.activeSelf) completionPopup.SetActive(false);

        // Subscribe questionnaire completion
        questionPanel.OnQuestionnaireComplete -= OnQuestionnaireFinished;
        questionPanel.OnQuestionnaireComplete += OnQuestionnaireFinished;

        // Subscribe to individual question answers
        questionPanel.OnQuestionAnswered -= OnQuestionAnswered;
        questionPanel.OnQuestionAnswered += OnQuestionAnswered;

        if (availableSceneIDs.Count == 0)
        {
            Debug.LogError("No scene IDs provided. Cannot start test.");
            currentState = TestState.Finished;
            return;
        }

        ShuffleSceneIDs();

        // Show user ID input first
        if (userIdInputPanel != null)
        {
            currentState = TestState.AwaitingUserId;
            userIdInputPanel.OnUserIdSubmitted += OnUserIdSubmitted;
            userIdInputPanel.Show();
            Debug.Log("Awaiting user ID before starting trial.");
        }
        else
        {
            Debug.LogWarning("UserIdInputPanel not assigned; proceeding without user ID.");
            StartTrialPhase();
        }
    }

    private void OnUserIdSubmitted(string id)
    {
        userId = id;
        Debug.Log("User ID received: " + userId);
        userIdInputPanel.OnUserIdSubmitted -= OnUserIdSubmitted;

        // Initialize the data saver with user ID and scene order
        dataSaver.InitializeTest(userId, shuffledSceneIDs);
        Debug.Log($"Save location: {TestDataSaver.GetSaveDirectory()}");

        StartTrialPhase();
    }

    private void OnQuestionAnswered(string questionText, string answer)
    {
        // Forward to data saver
        dataSaver.RecordQuestionResponse(questionText, answer);
    }

    void Update()
    {
        if (currentState == TestState.WaitingForInput)
        {
            if (OVRInput.GetDown(OVRInput.Button.One))
            {
                Debug.Log("A Button Pressed. Proceeding to questionnaire.");

                // Mark questionnaire start time (end of viewing)
                dataSaver.StartQuestionnaire();

                StartCurrentQuestionnaire();
            }
        }
    }

    private void ShuffleSceneIDs()
    {
        UnityEngine.Random.InitState((int)System.DateTime.Now.Ticks);
        shuffledSceneIDs = new List<int>(availableSceneIDs);
        int n = shuffledSceneIDs.Count;
        while (n > 1)
        {
            n--;
            int k = Random.Range(0, n + 1);
            int value = shuffledSceneIDs[k];
            shuffledSceneIDs[k] = shuffledSceneIDs[n];
            shuffledSceneIDs[n] = value;
        }
        Debug.Log("Scenes shuffled. Order: " + string.Join(", ", shuffledSceneIDs));
    }

    private void StartTrialPhase()
    {
        currentState = TestState.TrialPC;
        Debug.Log("--- STARTING TRIAL POINT CLOUD (TRIAL / TRIAL_depth) ---" + (userId != null ? " [User=" + userId + "]" : ""));

        // Ensure the point cloud is visible
        pointCloudDisplay.showPointCloud = true;

        // Start tracking trial scene (sceneId=-1 for trial, not in shuffled list)
        dataSaver.StartSceneView(-1, -1, true);

        LoadTrialPointCloud();
        currentState = TestState.WaitingForInput;
        Debug.Log("Trial PC Loaded. Press 'A' on controller to proceed to trial questionnaire.");
    }

    private void StartCurrentQuestionnaire()
    {
        pointCloudDisplay.showPointCloud = false;
        currentState = TestState.QuestionnaireActive;
        questionPanel.StartQuestionnaire();
    }

    public void OnQuestionnaireFinished()
    {
        if (currentState != TestState.QuestionnaireActive) return;

        // Finish recording the current scene
        dataSaver.FinishScene();

        if (!trialCompleted)
        {
            trialCompleted = true;
            Debug.Log("--- TRIAL PHASE COMPLETE. STARTING REAL SCENES ---" + (userId != null ? " [User=" + userId + "]" : ""));
            pointCloudDisplay.showPointCloud = true;
            currentState = TestState.DisplayingPC;
            currentSceneIndex = 0;

            // Start tracking first real scene
            int sceneId = shuffledSceneIDs[currentSceneIndex];
            dataSaver.StartSceneView(sceneId, currentSceneIndex, false);

            LoadCurrentPointCloud();
            currentState = TestState.WaitingForInput;
            Debug.Log("Press 'A' on controller when viewing is complete.");
            return;
        }

        ProceedToNextScene();
    }

    private void ProceedToNextScene()
    {
        currentSceneIndex++;
        if (currentSceneIndex >= shuffledSceneIDs.Count)
        {
            FinishTest();
            return;
        }
        int nextSceneID = shuffledSceneIDs[currentSceneIndex];
        Debug.Log($"--- STARTING SCENE {currentSceneIndex + 1}/{shuffledSceneIDs.Count}: ID {nextSceneID} " + (userId != null ? "[User=" + userId + "]" : ""));

        // Ensure point cloud is turned back on after the questionnaire
        pointCloudDisplay.showPointCloud = true;
        currentState = TestState.DisplayingPC;

        // Start tracking this scene
        dataSaver.StartSceneView(nextSceneID, currentSceneIndex, false);

        LoadCurrentPointCloud();
        currentState = TestState.WaitingForInput;
        Debug.Log("Press 'A' on controller when viewing is complete.");
    }

    private void LoadTrialPointCloud()
    {
        string rgbResourcePath = resourceBasePath + "TRIAL";
        string depthResourcePath = resourceBasePath + "TRIAL_depth";
        Debug.Log($"Attempting to load TRIAL from Resources: RGB={rgbResourcePath}, Depth={depthResourcePath}");
        Texture2D rgbTexture = Resources.Load<Texture2D>(rgbResourcePath);
        Texture2D depthTexture = Resources.Load<Texture2D>(depthResourcePath);
        if (rgbTexture != null && depthTexture != null)
        {
            pointCloudDisplay.colorTexture = rgbTexture;
            pointCloudDisplay.depthTexture = depthTexture;
            Debug.Log("Successfully loaded TRIAL textures.");
        }
        else
        {
            Debug.LogError("Failed to load TRIAL textures. Ensure 'TRIAL.png' and 'TRIAL_depth.png' are in Resources/" + resourceBasePath);
            if (rgbTexture == null) Debug.LogError("TRIAL RGB Texture NOT FOUND at path: " + rgbResourcePath);
            if (depthTexture == null) Debug.LogError("TRIAL Depth Texture NOT FOUND at path: " + depthResourcePath);
        }
    }

    private void LoadCurrentPointCloud()
    {
        if (currentSceneIndex < 0 || currentSceneIndex >= shuffledSceneIDs.Count)
        {
            Debug.LogError("LoadCurrentPointCloud called with invalid index: " + currentSceneIndex);
            return;
        }
        int sceneID = shuffledSceneIDs[currentSceneIndex];
        string rgbResourcePath = resourceBasePath + "scene" + sceneID;
        string depthResourcePath = resourceBasePath + "scene" + sceneID + "_depth";
        Debug.Log($"Attempting to load from Resources: RGB={rgbResourcePath}, Depth={depthResourcePath}");
        Texture2D rgbTexture = Resources.Load<Texture2D>(rgbResourcePath);
        Texture2D depthTexture = Resources.Load<Texture2D>(depthResourcePath);
        if (rgbTexture != null && depthTexture != null)
        {
            pointCloudDisplay.colorTexture = rgbTexture;
            pointCloudDisplay.depthTexture = depthTexture;
            Debug.Log($"Successfully loaded scene ID {sceneID}.");
        }
        else
        {
            Debug.LogError("Failed to load both textures for scene ID " + sceneID + ". Check Resources path.");
            if (rgbTexture == null) Debug.LogError("RGB Texture NOT FOUND at path: " + rgbResourcePath);
            if (depthTexture == null) Debug.LogError("Depth Texture NOT FOUND at path: " + depthResourcePath);
        }
    }

    private void FinishTest()
    {
        currentState = TestState.Finished;
        pointCloudDisplay.showPointCloud = false;
        Debug.Log("--- TEST COMPLETE ---" + (userId != null ? " [User=" + userId + "]" : ""));

        // Save all collected data to JSON file
        dataSaver.SaveTestData();

        if (completionPopup != null)
        {
            completionPopup.SetActive(true);
        }
        else
        {
            Debug.LogWarning("Completion popup not assigned. Assign a UI GameObject to 'completionPopup' in the Inspector.");
        }
    }
}