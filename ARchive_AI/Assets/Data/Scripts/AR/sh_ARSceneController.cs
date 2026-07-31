using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class sh_ARSceneController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private GameObject successEffectUiObject;
    [SerializeField] private GameObject completionMessageObject;
    [SerializeField] private TMP_Text completionMessageText;
    [SerializeField] private GameObject returnToLoginButtonObject;
    [SerializeField] private Button returnToLoginButton;
    [SerializeField] private sh_SessionCleanupService sessionCleanupService;

    [Header("Scene Settings")]
    [SerializeField] private string loginSceneName = "LoginScene";

    [Header("Original Image Preview")]
    [SerializeField] private sh_PuzzlePieceUI originalImagePreviewPrefab;
    [SerializeField] private RectTransform originalImagePreviewSpawnRoot;
    [SerializeField] private RectTransform originalImagePreviewSpawnPoint;
    [SerializeField] private float originalImagePreviewSpritePixelsPerUnit = 100f;

    [Header("Messages")]
    [SerializeField] private string successMessage = "과거의 추억을 되찾았습니다";

    private bool hasHandledTrackingSuccess;
    private bool isReturningToLoginScene;
    private sh_PuzzlePieceUI originalImagePreviewInstance;
    private Sprite originalImagePreviewSprite;
    private Texture2D originalImagePreviewTexture;

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

        CreateOriginalImagePreview();
        SetSuccessEffectUiVisible(true);
        SetCompletionMessageVisible(true);
        SetReturnToLoginButtonVisible(true);

        if (!string.IsNullOrWhiteSpace(trackedImageName))
        {
            Debug.Log($"{nameof(sh_ARSceneController)}: 이미지 트래킹 성공 - {trackedImageName}", this);
            return;
        }

        Debug.Log($"{nameof(sh_ARSceneController)}: 이미지 트래킹 성공.", this);
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
        SetSuccessEffectUiVisible(false);
        SetCompletionMessageVisible(false);
        SetReturnToLoginButtonVisible(false);
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

    private void CreateOriginalImagePreview()
    {
        if (originalImagePreviewPrefab == null ||
            originalImagePreviewSpawnRoot == null ||
            originalImagePreviewSpawnPoint == null)
        {
            return;
        }

        if (!sh_ImageStorageService.TryLoadSavedImagePath(out string savedImagePath))
        {
            Debug.LogWarning($"{nameof(sh_ARSceneController)}: 저장된 원본 이미지 경로가 없어 원본 이미지 프리뷰를 생성하지 못했습니다.", this);
            return;
        }

        ClearOriginalImagePreview();

        originalImagePreviewSprite = CreateSpriteFromFile(savedImagePath, out originalImagePreviewTexture);
        if (originalImagePreviewSprite == null)
        {
            Debug.LogWarning($"{nameof(sh_ARSceneController)}: 원본 이미지 프리뷰 스프라이트를 만들지 못했습니다.\n{savedImagePath}", this);
            return;
        }

        originalImagePreviewInstance = Instantiate(originalImagePreviewPrefab, originalImagePreviewSpawnRoot);
        originalImagePreviewInstance.Configure(
            new sh_PuzzlePieceData(1, 0, savedImagePath),
            originalImagePreviewSprite);

        ApplySpawnPointLayout(originalImagePreviewInstance.RectTransform, originalImagePreviewSpawnPoint);
    }

    private void ApplySpawnPointLayout(RectTransform pieceRectTransform, RectTransform spawnPoint)
    {
        if (pieceRectTransform == null || spawnPoint == null)
        {
            return;
        }

        Vector2 originalAnchorMin = pieceRectTransform.anchorMin;
        Vector2 originalAnchorMax = pieceRectTransform.anchorMax;
        Vector2 originalPivot = pieceRectTransform.pivot;
        Vector2 originalSizeDelta = pieceRectTransform.sizeDelta;
        RectTransform parentRectTransform = spawnPoint.parent as RectTransform;

        if (parentRectTransform != null)
        {
            pieceRectTransform.SetParent(parentRectTransform, false);
        }

        pieceRectTransform.anchorMin = originalAnchorMin;
        pieceRectTransform.anchorMax = originalAnchorMax;
        pieceRectTransform.pivot = originalPivot;
        pieceRectTransform.sizeDelta = originalSizeDelta;
        pieceRectTransform.position = spawnPoint.position;
        pieceRectTransform.localScale = Vector3.one;
    }

    private Sprite CreateSpriteFromFile(string filePath, out Texture2D loadedTexture)
    {
        loadedTexture = null;

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return null;
        }

        byte[] imageBytes = File.ReadAllBytes(filePath);
        loadedTexture = new Texture2D(2, 2, TextureFormat.RGBA32, false);

        if (!ImageConversion.LoadImage(loadedTexture, imageBytes, false))
        {
            Destroy(loadedTexture);
            loadedTexture = null;
            return null;
        }

        loadedTexture.name = Path.GetFileNameWithoutExtension(filePath);

        Sprite sprite = Sprite.Create(
            loadedTexture,
            new Rect(0f, 0f, loadedTexture.width, loadedTexture.height),
            new Vector2(0.5f, 0.5f),
            originalImagePreviewSpritePixelsPerUnit);
        sprite.name = loadedTexture.name;
        return sprite;
    }

    private void ClearOriginalImagePreview()
    {
        if (originalImagePreviewInstance != null)
        {
            Destroy(originalImagePreviewInstance.gameObject);
            originalImagePreviewInstance = null;
        }

        if (originalImagePreviewSprite != null)
        {
            Destroy(originalImagePreviewSprite);
            originalImagePreviewSprite = null;
        }

        if (originalImagePreviewTexture != null)
        {
            Destroy(originalImagePreviewTexture);
            originalImagePreviewTexture = null;
        }
    }

    private void OnDestroy()
    {
        ClearOriginalImagePreview();
    }
}
