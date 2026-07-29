using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class sh_ARSceneController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject completionMessageObject;
    [SerializeField] private TMP_Text completionMessageText;
    [SerializeField] private GameObject returnToLoginButtonObject;
    [SerializeField] private Button returnToLoginButton;
    [SerializeField] private sh_SessionCleanupService sessionCleanupService;

    [Header("Scene Settings")]
    [SerializeField] private string loginSceneName = "LoginScene";

    [Header("Messages")]
    [SerializeField] private string successMessage = "과거의 추억을 되찾았습니다";

    private bool hasHandledTrackingSuccess;
    private bool isReturningToLoginScene;

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

    public void HandleTrackingSuccess(string trackedImageName)
    {
        if (hasHandledTrackingSuccess)
        {
            return;
        }

        hasHandledTrackingSuccess = true;
        ResolveReferences();

        if (completionMessageText != null)
        {
            completionMessageText.text = successMessage;
        }

        SetCompletionMessageVisible(true);
        SetReturnToLoginButtonVisible(true);

        if (!string.IsNullOrWhiteSpace(trackedImageName))
        {
            Debug.Log($"{nameof(sh_ARSceneController)}: 이미지 트래킹 성공 - {trackedImageName}", this);
            return;
        }

        Debug.Log($"{nameof(sh_ARSceneController)}: 이미지 트래킹 성공.", this);
    }

    public void OnReturnToLoginButtonClicked()
    {
        ReturnToLoginScene();
    }

    public void ReturnToLoginScene()
    {
        if (isReturningToLoginScene)
        {
            return;
        }

        isReturningToLoginScene = true;
        ResolveReferences();

        if (sessionCleanupService != null)
        {
            sessionCleanupService.CleanupSessionFiles();
        }
        else
        {
            Debug.LogWarning($"{nameof(sh_ARSceneController)}: {nameof(sessionCleanupService)} 참조가 없어 세션 정리를 건너뜁니다.", this);
        }

        SceneManager.LoadScene(loginSceneName);
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

        if (returnToLoginButtonObject == null && returnToLoginButton != null)
        {
            returnToLoginButtonObject = returnToLoginButton.gameObject;
        }

        if (returnToLoginButton == null && returnToLoginButtonObject != null)
        {
            returnToLoginButton = returnToLoginButtonObject.GetComponent<Button>();
        }

        if (sessionCleanupService == null)
        {
            sessionCleanupService = GetComponent<sh_SessionCleanupService>();
        }
    }

    private void ApplyInitialState()
    {
        hasHandledTrackingSuccess = false;
        isReturningToLoginScene = false;
        SetCompletionMessageVisible(false);
        SetReturnToLoginButtonVisible(false);
    }

    private void SetCompletionMessageVisible(bool isVisible)
    {
        if (completionMessageObject != null)
        {
            completionMessageObject.SetActive(isVisible);
        }
    }

    private void SetReturnToLoginButtonVisible(bool isVisible)
    {
        if (returnToLoginButtonObject != null)
        {
            returnToLoginButtonObject.SetActive(isVisible);
        }

        if (returnToLoginButton != null)
        {
            returnToLoginButton.interactable = isVisible;
        }
    }
}
