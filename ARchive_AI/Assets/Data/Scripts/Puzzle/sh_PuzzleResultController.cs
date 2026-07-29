using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class sh_PuzzleResultController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject completionMessageObject;
    [SerializeField] private TMP_Text completionMessageText;
    [SerializeField] private GameObject moveToArButtonObject;
    [SerializeField] private Button moveToArButton;

    [Header("Messages")]
    [SerializeField] private string completionMessage = "기억의 조각을 수집하였습니다";

    private bool hasHandledPuzzleComplete;

    private void Awake()
    {
        ResolveReferences();
        ApplyInitialState();
    }

    private void Reset()
    {
        ResolveReferences();
        ApplyInitialState();
    }

    public void HandlePuzzleCompleted()
    {
        if (hasHandledPuzzleComplete)
        {
            return;
        }

        hasHandledPuzzleComplete = true;
        ResolveReferences();

        if (completionMessageText != null)
        {
            completionMessageText.text = completionMessage;
        }

        SetCompletionMessageVisible(true);
        SetMoveToArButtonVisible(true);
    }

    

    private void ResolveReferences()
    {
        if (completionMessageObject == null && completionMessageText != null)
        {
            completionMessageObject = completionMessageText.gameObject;
        }

        if (completionMessageText == null && completionMessageObject != null)
        {
            completionMessageText = completionMessageObject.GetComponentInChildren<TMP_Text>(true);
        }

        if (moveToArButtonObject == null && moveToArButton != null)
        {
            moveToArButtonObject = moveToArButton.gameObject;
        }

        if (moveToArButton == null && moveToArButtonObject != null)
        {
            moveToArButton = moveToArButtonObject.GetComponent<Button>();
        }
    }

    private void ApplyInitialState()
    {
        hasHandledPuzzleComplete = false;
        SetCompletionMessageVisible(false);
        SetMoveToArButtonVisible(false);
    }

    private void SetCompletionMessageVisible(bool isVisible)
    {
        if (completionMessageObject != null)
        {
            completionMessageObject.SetActive(isVisible);
        }
    }

    private void SetMoveToArButtonVisible(bool isVisible)
    {
        if (moveToArButtonObject != null)
        {
            moveToArButtonObject.SetActive(isVisible);
        }

        if (moveToArButton != null)
        {
            moveToArButton.interactable = isVisible;
        }
    }
}
