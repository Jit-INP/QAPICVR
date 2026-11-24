using UnityEngine;
using System;
using System.IO;
using System.Collections.Generic;

// Handles saving test results to JSON files
public class TestDataSaver : MonoBehaviour
{
 [System.Serializable]
    public class QuestionResponse
    {
        public string questionText;
        public string answer;
        public string answeredAt; // ISO 8601 timestamp
    }

    [System.Serializable]
public class SceneResponse
    {
        public int sceneId;
 public int sceneIndex;
 public bool isTrial;
        public string viewStartTime; // ISO 8601
  public string questionnaireStartTime; // ISO 8601
        public float viewDurationSeconds;
public List<QuestionResponse> questions = new List<QuestionResponse>();
    }

    [System.Serializable]
    public class TestData
    {
        public string userId;
public string testStartTime; // ISO 8601
        public string testEndTime; // ISO 8601
      public List<int> sceneOrder = new List<int>();
        public List<SceneResponse> responses = new List<SceneResponse>();
    }

    private TestData currentTestData;
    private SceneResponse currentSceneResponse;
    private DateTime testStartTime;
    private DateTime currentSceneViewStartTime;

    void Awake()
    {
        currentTestData = new TestData();
    }

    // Check if a user ID file already exists
  public static bool UserIdExists(string userId)
    {
string filePath = GetFilePath(userId);
  bool exists = File.Exists(filePath);
        if (exists)
        {
      Debug.Log($"[TestDataSaver] User ID {userId} already exists at: {filePath}");
  }
      return exists;
    }

    // Initialize test data with user ID and scene order
    public void InitializeTest(string userId, List<int> sceneOrder)
    {
        currentTestData = new TestData();
   currentTestData.userId = userId;
currentTestData.sceneOrder = new List<int>(sceneOrder);
        testStartTime = DateTime.UtcNow;
        currentTestData.testStartTime = testStartTime.ToString("o"); // ISO 8601

        Debug.Log($"[TestDataSaver] Initialized test for user {userId} with {sceneOrder.Count} scenes");
    }

    // Start tracking a new scene view
    public void StartSceneView(int sceneId, int sceneIndex, bool isTrial)
    {
        currentSceneResponse = new SceneResponse
    {
            sceneId = sceneId,
            sceneIndex = sceneIndex,
 isTrial = isTrial,
            viewStartTime = DateTime.UtcNow.ToString("o")
        };
        currentSceneViewStartTime = DateTime.UtcNow;

        Debug.Log($"[TestDataSaver] Started scene view: sceneId={sceneId}, index={sceneIndex}, trial={isTrial}");
    }

    // Mark when questionnaire starts (end of viewing phase)
    public void StartQuestionnaire()
  {
      if (currentSceneResponse != null)
        {
     currentSceneResponse.questionnaireStartTime = DateTime.UtcNow.ToString("o");
            currentSceneResponse.viewDurationSeconds = (float)(DateTime.UtcNow - currentSceneViewStartTime).TotalSeconds;
   Debug.Log($"[TestDataSaver] Questionnaire started. View duration: {currentSceneResponse.viewDurationSeconds:F2}s");
        }
    }

    // Record a single question response
    public void RecordQuestionResponse(string questionText, string answer)
    {
        if (currentSceneResponse != null)
        {
          var response = new QuestionResponse
            {
             questionText = questionText,
          answer = answer,
       answeredAt = DateTime.UtcNow.ToString("o")
            };
            currentSceneResponse.questions.Add(response);
 Debug.Log($"[TestDataSaver] Recorded answer: Q='{questionText}' A='{answer}'");
  }
        else
        {
   Debug.LogWarning("[TestDataSaver] Cannot record question response: no active scene");
        }
    }

    // Finish the current scene response and add it to the test data
    public void FinishScene()
    {
        if (currentSceneResponse != null)
        {
       // Only save non-trial responses
   if (!currentSceneResponse.isTrial)
     {
      currentTestData.responses.Add(currentSceneResponse);
     Debug.Log($"[TestDataSaver] Finished scene {currentSceneResponse.sceneId} with {currentSceneResponse.questions.Count} questions");

                    try
                    {
                        string partialFileName = $"{currentTestData.userId}_scene{currentSceneResponse.sceneId}_questionnaire.json";
                        string partialDir = Path.Combine(Application.persistentDataPath, "PartialResults");
                        if (!Directory.Exists(partialDir))
                        {
                            Directory.CreateDirectory(partialDir);
                        }
                        string partialFilePath = Path.Combine(partialDir, partialFileName);
                        string json = JsonUtility.ToJson(currentSceneResponse, true);
                        File.WriteAllText(partialFilePath, json);
                        Debug.Log($"[TestDataSaver] Saved partial questionnaire to: {partialFilePath}");
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"[TestDataSaver] Failed to save partial questionnaire: {e.Message}");
                    }
       }
            else
 {
     Debug.Log($"[TestDataSaver] Trial scene {currentSceneResponse.sceneId} completed (not saved)");
      }
    currentSceneResponse = null;
 }
    }

    // Finalize and save the test data to JSON file
    public void SaveTestData()
 {
        if (currentTestData == null || string.IsNullOrEmpty(currentTestData.userId))
      {
    Debug.LogError("[TestDataSaver] Cannot save: no test data or user ID");
        return;
 }

        currentTestData.testEndTime = DateTime.UtcNow.ToString("o");
        
        string filePath = GetFilePath(currentTestData.userId);
        
        try
  {
            // Serialize to JSON with pretty printing
       string json = JsonUtility.ToJson(currentTestData, true);
       
   // Ensure directory exists
            string directory = Path.GetDirectoryName(filePath);
      if (!Directory.Exists(directory))
       {
Directory.CreateDirectory(directory);
       }

            // Write to file
            File.WriteAllText(filePath, json);
     
            Debug.Log($"[TestDataSaver] Successfully saved test data to: {filePath}");
        Debug.Log($"[TestDataSaver] Total scenes saved: {currentTestData.responses.Count}");
        }
        catch (Exception e)
        {
Debug.LogError($"[TestDataSaver] Failed to save test data: {e.Message}");
        }
    }

    private static string GetFilePath(string userId)
    {
        string fileName = $"responses_{userId}.json";
        // Use persistent data path for Quest/Android compatibility
 string filePath = Path.Combine(Application.persistentDataPath, fileName);
        return filePath;
    }

    // Get the save directory for debugging/inspection
    public static string GetSaveDirectory()
    {
        return Application.persistentDataPath;
    }
}
