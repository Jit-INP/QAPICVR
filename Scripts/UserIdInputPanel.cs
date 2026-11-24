using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

// Numeric user ID input panel.
public class UserIdInputPanel : MonoBehaviour
{
    [Header("UI Elements")]
    public TextMeshProUGUI userIdDisplay;
    public TextMeshProUGUI feedbackText;
    public Button submitButton;
    public Button backspaceButton;
    [Tooltip("Optional parent container holding all keypad buttons (digits, backspace, submit). If null, will scan children.")]
  public Transform keypadContainer;

    [Header("Constraints")]
    public int maxDigits = 6;

    public Action<string> OnUserIdSubmitted;

    private string currentId = string.Empty;

    void Awake()
    {
        Debug.Log("[UserIdInputPanel] Awake starting - ensuring entire panel visible");
    
  // CRITICAL: Force the root panel AND all parents active
        EnsureHierarchyActive();
        
        if (submitButton != null)
   {
        submitButton.onClick.RemoveAllListeners();
     submitButton.onClick.AddListener(Submit);
        }
   if (backspaceButton != null)
        {
  backspaceButton.onClick.RemoveAllListeners();
  backspaceButton.onClick.AddListener(Backspace);
        }

     // Ensure all keypad buttons are active
     ActivateAllKeypadButtons();

        UpdateDisplay();
        
        // Comprehensive diagnostics
        LogCompleteHierarchy();
    }

    private void EnsureHierarchyActive()
    {
    // Activate this GameObject
        if (!gameObject.activeSelf)
      {
            Debug.Log($"[UserIdInputPanel] Activating root panel: {name}");
          gameObject.SetActive(true);
        }
        
        // Activate all parent GameObjects up the chain
      Transform current = transform.parent;
        while (current != null)
        {
            if (!current.gameObject.activeSelf)
    {
        Debug.Log($"[UserIdInputPanel] Activating parent: {current.name}");
          current.gameObject.SetActive(true);
            }
 current = current.parent;
        }
    }

    private void ActivateAllKeypadButtons()
    {
        Transform root = keypadContainer != null ? keypadContainer : transform;
        Debug.Log($"[UserIdInputPanel] Activating all buttons under: {root.name}");
        
        int activatedCount = 0;
foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
  {
      // Activate the GameObject itself
    if (!t.gameObject.activeSelf)
  {
     Debug.Log($"[UserIdInputPanel] Activating inactive GameObject: {GetFullPath(t)}");
                t.gameObject.SetActive(true);
                activatedCount++;
            }
            
      // If it has a Button, ensure it's interactable
            Button b = t.GetComponent<Button>();
            if (b != null && !b.interactable)
      {
     b.interactable = true;
        Debug.Log($"[UserIdInputPanel] Made button interactable: {GetFullPath(t)}");
            }
            
            // If it has a Canvas, ensure it's enabled
       Canvas c = t.GetComponent<Canvas>();
         if (c != null && !c.enabled)
     {
     c.enabled = true;
        Debug.Log($"[UserIdInputPanel] Enabled Canvas on: {GetFullPath(t)}");
            }
            
        // If it has a CanvasGroup, ensure it's not blocking
 CanvasGroup cg = t.GetComponent<CanvasGroup>();
     if (cg != null)
     {
  if (cg.alpha < 1f)
          {
   Debug.Log($"[UserIdInputPanel] CanvasGroup alpha was {cg.alpha}, setting to 1 on: {GetFullPath(t)}");
        cg.alpha = 1f;
      }
           if (!cg.interactable)
 {
    cg.interactable = true;
   Debug.Log($"[UserIdInputPanel] Made CanvasGroup interactable on: {GetFullPath(t)}");
    }
     if (cg.blocksRaycasts == false)
         {
            cg.blocksRaycasts = true;
      Debug.Log($"[UserIdInputPanel] Enabled CanvasGroup raycasts on: {GetFullPath(t)}");
                }
        }
        }
     Debug.Log($"[UserIdInputPanel] Activated {activatedCount} previously inactive GameObjects");
    }

    private void LogCompleteHierarchy()
    {
        Transform root = keypadContainer != null ? keypadContainer : transform;
        Debug.Log($"[UserIdInputPanel] === COMPLETE HIERARCHY DUMP ===");
        Debug.Log($"[UserIdInputPanel] Root: {name}, Active: {gameObject.activeSelf}, ActiveInHierarchy: {gameObject.activeInHierarchy}");
  
        int totalObjects = 0;
        int activeObjects = 0;
        int totalButtons = 0;
        int activeButtons = 0;
        
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
        {
            totalObjects++;
  bool isActive = t.gameObject.activeInHierarchy;
            if (isActive) activeObjects++;
      
     Button b = t.GetComponent<Button>();
            string buttonInfo = "";
            if (b != null)
    {
             totalButtons++;
 if (isActive) activeButtons++;
   TextMeshProUGUI btnText = b.GetComponentInChildren<TextMeshProUGUI>();
   string label = btnText != null ? btnText.text : "NO TEXT";
          buttonInfo = $" [BUTTON: {label}, Interactable: {b.interactable}]";
            }
     
            Canvas c = t.GetComponent<Canvas>();
         string canvasInfo = c != null ? $" [Canvas: enabled={c.enabled}]" : "";
            
     CanvasGroup cg = t.GetComponent<CanvasGroup>();
     string cgInfo = cg != null ? $" [CanvasGroup: alpha={cg.alpha}, interactable={cg.interactable}]" : "";
       
            Debug.Log($"[UserIdInputPanel]   {GetFullPath(t)} | Active: {isActive}{buttonInfo}{canvasInfo}{cgInfo}");
   }

        Debug.Log($"[UserIdInputPanel] === SUMMARY: {activeObjects}/{totalObjects} objects active, {activeButtons}/{totalButtons} buttons active ===");
    }

  private string GetFullPath(Transform t)
    {
        string path = t.name;
    Transform current = t.parent;
        while (current != null && current != transform.parent)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }
     return path;
    }

    public void Show()
    {
        Debug.Log("[UserIdInputPanel] Show() called");
        EnsureHierarchyActive();
        currentId = string.Empty;
        UpdateDisplay();
   if (feedbackText != null) feedbackText.text = string.Empty;
        ActivateAllKeypadButtons();
     LogCompleteHierarchy();
    }

    public void Hide()
    {
    Debug.Log("[UserIdInputPanel] Hide() called");
        gameObject.SetActive(false);
    }

  public void AddDigit(string digit)
  {
        if (string.IsNullOrEmpty(digit)) return;
        if (digit.Length != 1 || digit[0] < '0' || digit[0] > '9') return;
    if (maxDigits > 0 && currentId.Length >= maxDigits) return;
        currentId += digit;
    UpdateDisplay();
        Debug.Log($"[UserIdInputPanel] Digit added: {digit}, current ID: {currentId}");
    }

    public void Backspace()
    {
      if (currentId.Length == 0) return;
        currentId = currentId.Substring(0, currentId.Length - 1);
        UpdateDisplay();
        Debug.Log($"[UserIdInputPanel] Backspace, current ID: {currentId}");
    }

    public void Submit()
    {
        if (string.IsNullOrEmpty(currentId))
 {
            if (feedbackText != null) feedbackText.text = "ID cannot be empty.";
            Debug.LogWarning("[UserIdInputPanel] Submit attempted with empty ID");
     return;
        }

        // Check if user ID already exists
     if (TestDataSaver.UserIdExists(currentId))
        {
       if (feedbackText != null) feedbackText.text = $"User ID {currentId} already exists. Please choose a different ID.";
Debug.LogWarning($"[UserIdInputPanel] User ID {currentId} already exists");
            return;
        }

        Debug.Log($"[UserIdInputPanel] Submit: User ID = {currentId}");
      OnUserIdSubmitted?.Invoke(currentId);
   Hide();
    }
    private void UpdateDisplay()
    {
  if (userIdDisplay != null)
        {
   userIdDisplay.text = currentId.Length == 0 ? "_" : currentId;
 }
    }
}
