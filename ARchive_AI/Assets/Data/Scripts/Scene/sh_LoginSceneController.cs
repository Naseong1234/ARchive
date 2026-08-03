using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class sh_LoginSceneController : MonoBehaviour
{
    private const string ArchivedImageQueuePlayerPrefsKey = "sh_login_archived_original_image_queue";

    [Serializable]
    private struct ArchivedImageEntry
    {
        public string filePath;
        public string fileName;
        public long savedTicks;
    }

    [Serializable]
    private sealed class ArchivedImageQueueData
    {
        public List<ArchivedImageEntry> entries = new List<ArchivedImageEntry>();
    }

    [Header("UI References")]
    [SerializeField] private Button attachFileButton;
    [SerializeField] private GameObject moveToMainButtonObject;
    [SerializeField] private Button moveToMainButton;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private sh_ImagePickerService imagePickerService;
    [SerializeField] private sh_ImageStorageService imageStorageService;
    [SerializeField] private sh_ImageSliceService imageSliceService;
    [SerializeField] private sh_PuzzlePieceUI archivedImagePreviewPrefab;
    [SerializeField] private RectTransform archivedImagePreviewRoot;
    [SerializeField] private RectTransform[] archivedImagePreviewPoints = Array.Empty<RectTransform>();

    [Header("Scene Settings")]
    [SerializeField] private Sprite moveButtonActivatedBackgroundSprite;
    [SerializeField] private float archivedImagePreviewSpritePixelsPerUnit = 100f;
    [SerializeField] private string archivedImageFolderName = "LoginOriginalArchive";
    [SerializeField] [Min(1)] private int maxArchivedImageCount = 6;

    [Header("Status Messages")]
    [SerializeField] private string defaultMessage = "추억이 담긴 사진을 선택해주세요.";
    [SerializeField] private string pickingMessage = "사진 목록을 불러오는 중입니다.";
    [SerializeField] private string savingMessage = "선택한 사진을 작업용 경로에 저장하는 중입니다.";
    [SerializeField] private string slicingMessage = "퍼즐용 이미지 조각을 생성하는 중입니다.";
    [SerializeField] private string successMessage = "사진 저장과 이미지 분할이 완료되었습니다.";
    [SerializeField] private string cancelMessage = "사진 선택이 취소되었습니다.";
    [SerializeField] private string archiveSaveFailedMessage = "원본 이미지 보관에는 실패했지만 퍼즐용 데이터는 생성되었습니다.";

    public string SelectedImagePath { get; private set; }
    public bool HasSelectedImage => !string.IsNullOrEmpty(SelectedImagePath);
    public string SavedImagePath { get; private set; }
    public bool HasSavedImage => !string.IsNullOrEmpty(SavedImagePath);
    public sh_ImageSliceService.SliceResult LastSliceResult { get; private set; }
    public int SelectedMarkerPieceIndex { get; private set; } = -1;

    private bool isProcessingImage;
    private Sprite defaultBackgroundSprite;
    private readonly List<ArchivedImageEntry> archivedImageEntries = new List<ArchivedImageEntry>();
    private readonly List<sh_PuzzlePieceUI> archivedImagePreviewInstances = new List<sh_PuzzlePieceUI>();
    private readonly List<Sprite> archivedImagePreviewSprites = new List<Sprite>();
    private readonly List<Texture2D> archivedImagePreviewTextures = new List<Texture2D>();

    private string ArchivedImageDirectoryPath =>
        Path.Combine(Application.persistentDataPath, "Data", "Image", archivedImageFolderName);

    private void Awake()
    {
        ResolveReferences();
        LoadArchivedImageEntries();
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void ResolveReferences()
    {
        if (attachFileButton == null)
        {
            attachFileButton = GetComponentInChildren<Button>(true);
        }

        if (moveToMainButton == null && moveToMainButtonObject != null)
        {
            moveToMainButton = moveToMainButtonObject.GetComponent<Button>();
        }

        if (moveToMainButtonObject == null && moveToMainButton != null)
        {
            moveToMainButtonObject = moveToMainButton.gameObject;
        }

        if (imagePickerService == null)
        {
            imagePickerService = GetComponent<sh_ImagePickerService>();
        }

        if (imageStorageService == null)
        {
            imageStorageService = GetComponent<sh_ImageStorageService>();
        }

        if (imageSliceService == null)
        {
            imageSliceService = GetComponent<sh_ImageSliceService>();
        }

        if (statusText == null)
        {
            statusText = GetComponentInChildren<TMP_Text>(true);
        }

        if (backgroundImage == null)
        {
            Image[] images = GetComponentsInChildren<Image>(true);

            for (int index = 0; index < images.Length; index++)
            {
                if (images[index].name == "Background")
                {
                    backgroundImage = images[index];
                    break;
                }
            }
        }
    }

    private void OnEnable()
    {
        LoadArchivedImageEntries();
        CacheDefaultBackgroundSprite();
        SetAttachFileButtonVisible(true);
        SetMoveToMainButtonVisible(false);
        SetStatus(defaultMessage);
        RefreshArchivedImagePreviews();
    }

    private void OnDisable()
    {
        ClearArchivedImagePreviews();
    }

    public void OpenImagePickerFromButton()
    {
        if (isProcessingImage)
        {
            return;
        }

        if (imagePickerService == null)
        {
            SetStatus("이미지 선택 서비스가 연결되어 있지 않습니다.");
            Debug.LogError($"{nameof(sh_LoginSceneController)}: {nameof(imagePickerService)} reference is missing.", this);
            return;
        }

        if (imagePickerService.IsPicking)
        {
            return;
        }

        SetStatus(pickingMessage);

        imagePickerService.PickImage(HandleImagePickSuccess, HandleImagePickCancelled, HandleImagePickFailed);
    }

    public void OnExitButtonClicked()
    {
        Debug.Log($"{nameof(sh_LoginSceneController)}: 종료 버튼이 눌려 앱을 종료합니다.", this);

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void HandleImagePickSuccess(string selectedPath)
    {
        SelectedImagePath = selectedPath;
        StartCoroutine(ProcessSelectedImageRoutine(selectedPath));
    }

    private void HandleImagePickCancelled()
    {
        SetInteractable(true);
        SetMoveToMainButtonVisible(false);
        SetStatus(cancelMessage);
        Debug.Log($"{nameof(sh_LoginSceneController)}: Image selection was cancelled.", this);
    }

    private void HandleImagePickFailed(string errorMessage)
    {
        SetInteractable(true);
        SetMoveToMainButtonVisible(false);
        SetStatus(errorMessage);
        Debug.LogError($"{nameof(sh_LoginSceneController)}: {errorMessage}", this);
    }



    private void SetInteractable(bool isInteractable)
    {
        if (attachFileButton != null)
        {
            attachFileButton.interactable = isInteractable;
        }

        if (moveToMainButton != null)
        {
            moveToMainButton.interactable = isInteractable;
        }
    }

    private void SetMoveToMainButtonVisible(bool isVisible)
    {
        if (moveToMainButtonObject != null)
        {
            moveToMainButtonObject.SetActive(isVisible);
        }

        if (moveToMainButton != null)
        {
            moveToMainButton.interactable = isVisible;
        }

        SetAttachFileButtonVisible(!isVisible);
        UpdateBackgroundSprite(isVisible);
    }

    private void SetAttachFileButtonVisible(bool isVisible)
    {
        if (attachFileButton == null)
        {
            return;
        }

        attachFileButton.gameObject.SetActive(isVisible);
    }

    private void CacheDefaultBackgroundSprite()
    {
        if (backgroundImage == null || backgroundImage.sprite == null)
        {
            return;
        }

        if (defaultBackgroundSprite == null)
        {
            defaultBackgroundSprite = backgroundImage.sprite;
        }
    }

    private void UpdateBackgroundSprite(bool isMoveButtonVisible)
    {
        if (backgroundImage == null)
        {
            return;
        }

        if (isMoveButtonVisible)
        {
            if (moveButtonActivatedBackgroundSprite != null)
            {
                backgroundImage.sprite = moveButtonActivatedBackgroundSprite;
            }

            return;
        }

        if (defaultBackgroundSprite != null)
        {
            backgroundImage.sprite = defaultBackgroundSprite;
        }
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }

    private IEnumerator ProcessSelectedImageRoutine(string selectedPath)
    {
        isProcessingImage = true;

        if (imageStorageService == null)
        {
            SetInteractable(true);
            SetStatus("이미지 저장 서비스가 연결되어 있지 않습니다.");
            Debug.LogError($"{nameof(sh_LoginSceneController)}: {nameof(imageStorageService)} reference is missing.", this);
            isProcessingImage = false;
            yield break;
        }

        SetStatus(savingMessage);
        yield return null;

        sh_ImageStorageService.StorageResult storageResult = imageStorageService.SaveImage(selectedPath);

        if (!storageResult.IsSuccess)
        {
            SavedImagePath = string.Empty;
            SetInteractable(true);
            SetMoveToMainButtonVisible(false);
            SetStatus(storageResult.ErrorMessage);
            Debug.LogError($"{nameof(sh_LoginSceneController)}: {storageResult.ErrorMessage}", this);
            isProcessingImage = false;
            yield break;
        }

        SavedImagePath = storageResult.SavedFilePath;
        LastSliceResult = default;

        bool archiveSaveSucceeded = TryArchiveOriginalImage(SavedImagePath, out string archiveErrorMessage);

        if (imageSliceService == null)
        {
            SetInteractable(true);
            SetMoveToMainButtonVisible(false);
            SetStatus("이미지 분할 서비스가 연결되어 있지 않습니다.");
            Debug.LogError($"{nameof(sh_LoginSceneController)}: {nameof(imageSliceService)} reference is missing.", this);
            isProcessingImage = false;
            yield break;
        }

        SetStatus(slicingMessage);
        yield return null;

        sh_ImageSliceService.SliceResult sliceResult = imageSliceService.SliceSavedImage(SavedImagePath);

        SetInteractable(true);

        if (!sliceResult.IsSuccess)
        {
            SelectedMarkerPieceIndex = -1;
            SetMoveToMainButtonVisible(false);
            SetStatus(sliceResult.ErrorMessage);
            Debug.LogError($"{nameof(sh_LoginSceneController)}: {sliceResult.ErrorMessage}", this);
            isProcessingImage = false;
            yield break;
        }

        LastSliceResult = sliceResult;
        SelectedMarkerPieceIndex = sliceResult.SelectedMarkerPiece.PieceIndex;
        SetMoveToMainButtonVisible(true);
        SetStatus(archiveSaveSucceeded ? successMessage : $"{successMessage}\n{archiveSaveFailedMessage}");
        Debug.Log(
            $"{nameof(sh_LoginSceneController)}: Selected image path = {selectedPath}\n" +
            $"Saved image path = {SavedImagePath}\n" +
            $"Archived image count = {archivedImageEntries.Count}\n" +
            $"Selected marker piece index = {SelectedMarkerPieceIndex}\n" +
            $"Selected marker piece path = {sliceResult.SelectedMarkerPiece.PiecePath}",
            this);

        if (!archiveSaveSucceeded)
        {
            Debug.LogWarning($"{nameof(sh_LoginSceneController)}: {archiveErrorMessage}", this);
        }

        isProcessingImage = false;
    }

    public void ShowImage(GameObject imageObject)
    {
        if (imageObject == null)
        {
            return;
        }

        imageObject.SetActive(true);
    }

    public void CloseImage(GameObject imageObject)
    {
        if (imageObject == null)
        {
            return;
        }

        imageObject.SetActive(false);
    }

    public void ResetAllDeviceSavedDataForDeveloper()
    {
        ResolveReferences();
        ClearArchivedImagePreviews();
        DeleteAllArchivedImages();
        DeleteSessionGeneratedFiles();
        ClearRuntimeRecords();
        RefreshArchivedImagePreviews();
        SetMoveToMainButtonVisible(false);
        SetStatus("개발자 초기화가 완료되었습니다.");

        Debug.Log($"{nameof(sh_LoginSceneController)}: 개발자용 기기 저장 데이터 초기화가 완료되었습니다.", this);
    }

    private bool TryArchiveOriginalImage(string sourceFilePath, out string errorMessage)
    {
        errorMessage = string.Empty;

        if (string.IsNullOrWhiteSpace(sourceFilePath) || !File.Exists(sourceFilePath))
        {
            errorMessage = "보관할 원본 이미지 파일을 찾을 수 없습니다.";
            return false;
        }

        try
        {
            Directory.CreateDirectory(ArchivedImageDirectoryPath);

            string extension = Path.GetExtension(sourceFilePath);
            string archivedFileName = $"login_original_{DateTime.Now:yyyyMMdd_HHmmss_fff}{extension}";
            string archivedFilePath = Path.Combine(ArchivedImageDirectoryPath, archivedFileName);
            File.Copy(sourceFilePath, archivedFilePath, true);

            ArchivedImageEntry newEntry = new ArchivedImageEntry
            {
                filePath = archivedFilePath,
                fileName = archivedFileName,
                savedTicks = DateTime.UtcNow.Ticks
            };

            archivedImageEntries.Add(newEntry);
            SortArchivedImageEntries();
            TrimArchivedImageEntriesToLimit();
            SaveArchivedImageEntries();
            RefreshArchivedImagePreviews();
            return true;
        }
        catch (Exception exception)
        {
            errorMessage = $"원본 이미지 보관 중 오류가 발생했습니다.\n{exception.Message}";
            return false;
        }
    }

    private void LoadArchivedImageEntries()
    {
        archivedImageEntries.Clear();

        string json = PlayerPrefs.GetString(ArchivedImageQueuePlayerPrefsKey, string.Empty);
        if (!string.IsNullOrWhiteSpace(json))
        {
            ArchivedImageQueueData queueData = JsonUtility.FromJson<ArchivedImageQueueData>(json);
            if (queueData != null && queueData.entries != null)
            {
                archivedImageEntries.AddRange(queueData.entries);
            }
        }

        RemoveMissingArchivedEntries();
        SortArchivedImageEntries();
        TrimArchivedImageEntriesToLimit();
        SaveArchivedImageEntries();
    }

    private void SaveArchivedImageEntries()
    {
        ArchivedImageQueueData queueData = new ArchivedImageQueueData();
        queueData.entries.AddRange(archivedImageEntries);
        string json = JsonUtility.ToJson(queueData);
        PlayerPrefs.SetString(ArchivedImageQueuePlayerPrefsKey, json);
        PlayerPrefs.Save();
    }

    private void RemoveMissingArchivedEntries()
    {
        for (int index = archivedImageEntries.Count - 1; index >= 0; index--)
        {
            if (string.IsNullOrWhiteSpace(archivedImageEntries[index].filePath) ||
                !File.Exists(archivedImageEntries[index].filePath))
            {
                archivedImageEntries.RemoveAt(index);
            }
        }
    }

    private void SortArchivedImageEntries()
    {
        archivedImageEntries.Sort((left, right) => left.savedTicks.CompareTo(right.savedTicks));
    }

    private void TrimArchivedImageEntriesToLimit()
    {
        while (archivedImageEntries.Count > maxArchivedImageCount)
        {
            DeleteArchivedEntryFile(archivedImageEntries[0]);
            archivedImageEntries.RemoveAt(0);
        }
    }

    private static void DeleteArchivedEntryFile(ArchivedImageEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.filePath) || !File.Exists(entry.filePath))
        {
            return;
        }

        File.Delete(entry.filePath);
    }

    private void RefreshArchivedImagePreviews()
    {
        ClearArchivedImagePreviews();

        if (archivedImagePreviewPrefab == null ||
            archivedImagePreviewRoot == null ||
            archivedImagePreviewPoints == null ||
            archivedImagePreviewPoints.Length == 0)
        {
            return;
        }

        int previewCount = Mathf.Min(archivedImageEntries.Count, archivedImagePreviewPoints.Length);

        for (int index = 0; index < previewCount; index++)
        {
            ArchivedImageEntry entry = archivedImageEntries[archivedImageEntries.Count - 1 - index];
            RectTransform spawnPoint = archivedImagePreviewPoints[index];

            if (spawnPoint == null)
            {
                continue;
            }

            Sprite previewSprite = CreateSpriteFromFile(entry.filePath, out Texture2D previewTexture);
            if (previewSprite == null)
            {
                continue;
            }

            sh_PuzzlePieceUI previewInstance = Instantiate(archivedImagePreviewPrefab, archivedImagePreviewRoot);
            previewInstance.Configure(new sh_PuzzlePieceData(index + 1, 0, entry.filePath), previewSprite);
            ApplySpawnPointLayout(previewInstance.RectTransform, spawnPoint);

            archivedImagePreviewInstances.Add(previewInstance);
            archivedImagePreviewSprites.Add(previewSprite);
            archivedImagePreviewTextures.Add(previewTexture);
        }
    }

    private void ApplySpawnPointLayout(RectTransform targetRectTransform, RectTransform spawnPoint)
    {
        if (targetRectTransform == null || spawnPoint == null)
        {
            return;
        }

        Vector2 originalAnchorMin = targetRectTransform.anchorMin;
        Vector2 originalAnchorMax = targetRectTransform.anchorMax;
        Vector2 originalPivot = targetRectTransform.pivot;
        Vector2 originalSizeDelta = targetRectTransform.sizeDelta;
        RectTransform parentRectTransform = spawnPoint.parent as RectTransform;

        if (parentRectTransform != null)
        {
            targetRectTransform.SetParent(parentRectTransform, false);
        }

        targetRectTransform.anchorMin = originalAnchorMin;
        targetRectTransform.anchorMax = originalAnchorMax;
        targetRectTransform.pivot = originalPivot;
        targetRectTransform.sizeDelta = originalSizeDelta;
        targetRectTransform.position = spawnPoint.position;
        targetRectTransform.localScale = Vector3.one;
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
            DestroyUnityObject(loadedTexture);
            loadedTexture = null;
            return null;
        }

        loadedTexture.name = Path.GetFileNameWithoutExtension(filePath);

        Sprite sprite = Sprite.Create(
            loadedTexture,
            new Rect(0f, 0f, loadedTexture.width, loadedTexture.height),
            new Vector2(0.5f, 0.5f),
            archivedImagePreviewSpritePixelsPerUnit);
        sprite.name = loadedTexture.name;
        return sprite;
    }

    private void ClearArchivedImagePreviews()
    {
        for (int index = 0; index < archivedImagePreviewInstances.Count; index++)
        {
            if (archivedImagePreviewInstances[index] != null)
            {
                DestroyUnityObject(archivedImagePreviewInstances[index].gameObject);
            }
        }

        for (int index = 0; index < archivedImagePreviewSprites.Count; index++)
        {
            DestroyUnityObject(archivedImagePreviewSprites[index]);
        }

        for (int index = 0; index < archivedImagePreviewTextures.Count; index++)
        {
            DestroyUnityObject(archivedImagePreviewTextures[index]);
        }

        archivedImagePreviewInstances.Clear();
        archivedImagePreviewSprites.Clear();
        archivedImagePreviewTextures.Clear();
    }

    private void DeleteAllArchivedImages()
    {
        for (int index = 0; index < archivedImageEntries.Count; index++)
        {
            DeleteArchivedEntryFile(archivedImageEntries[index]);
        }

        archivedImageEntries.Clear();

        if (Directory.Exists(ArchivedImageDirectoryPath))
        {
            Directory.Delete(ArchivedImageDirectoryPath, true);
        }

        PlayerPrefs.DeleteKey(ArchivedImageQueuePlayerPrefsKey);
        PlayerPrefs.Save();
    }

    private void DeleteSessionGeneratedFiles()
    {
        if (imageStorageService != null)
        {
            if (imageStorageService.TryGetSavedImagePath(out string savedImagePath))
            {
                DeleteFileIfExists(savedImagePath);
            }

            DeleteDirectoryIfExists(imageStorageService.StorageDirectoryPath);
            imageStorageService.ClearSavedImageRecord();
        }

        if (imageSliceService != null)
        {
            DeleteDirectoryIfExists(imageSliceService.RotatingDirectoryPath);
            DeleteDirectoryIfExists(imageSliceService.MarkerDirectoryPath);
            imageSliceService.ClearSelectedMarkerPieceRecord();
        }
    }

    private void ClearRuntimeRecords()
    {
        SelectedImagePath = string.Empty;
        SavedImagePath = string.Empty;
        LastSliceResult = default;
        SelectedMarkerPieceIndex = -1;
        isProcessingImage = false;
    }

    private static void DeleteFileIfExists(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return;
        }

        File.Delete(filePath);
    }

    private static void DeleteDirectoryIfExists(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
        {
            return;
        }

        Directory.Delete(directoryPath, true);
    }

    private static void DestroyUnityObject(UnityEngine.Object targetObject)
    {
        if (targetObject == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(targetObject);
            return;
        }

        DestroyImmediate(targetObject);
    }
}
