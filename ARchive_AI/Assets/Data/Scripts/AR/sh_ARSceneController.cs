using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class sh_ARSceneController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject successEffectUiObject;
    [SerializeField] private TMP_Text debugStatusText;
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

        SetSuccessEffectUiVisible(true);
        SetCompletionMessageVisible(true);
        SetReturnToLoginButtonVisible(true);
        SetDebugStatus($"트래킹 성공: {trackedImageName}");

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
            SetDebugStatus("세션 정리 후 로그인 씬으로 이동합니다.");
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
        SetSuccessEffectUiVisible(false);
        SetCompletionMessageVisible(false);
        SetReturnToLoginButtonVisible(false);
        SetDebugStatus("AR 디버그 대기 중");
    }

    public void SetDebugStatus(string message)
    {
        if (debugStatusText != null)
        {
            debugStatusText.text = message;
        }

        if (!string.IsNullOrWhiteSpace(message))
        {
            Debug.Log($"{nameof(sh_ARSceneController)}: {message}", this);
        }
    }

    private void SetSuccessEffectUiVisible(bool isVisible)
    {
        if (successEffectUiObject != null)
        {
            successEffectUiObject.SetActive(isVisible);
        }
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
