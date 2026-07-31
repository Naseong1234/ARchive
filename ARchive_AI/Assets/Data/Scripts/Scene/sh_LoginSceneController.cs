using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class sh_LoginSceneController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button attachFileButton;
    [SerializeField] private GameObject moveToMainButtonObject;
    [SerializeField] private Button moveToMainButton;
    [SerializeField] private Image backgroundImage;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private sh_ImagePickerService imagePickerService;
    [SerializeField] private sh_ImageStorageService imageStorageService;
    [SerializeField] private sh_ImageSliceService imageSliceService;

    [Header("Scene Settings")]
    [SerializeField] private Sprite moveButtonActivatedBackgroundSprite;

    [Header("Status Messages")]
    [SerializeField] private string defaultMessage = "추억이 담긴 사진을 선택해주세요.";
    [SerializeField] private string pickingMessage = "사진 목록을 불러오는 중입니다.";
    [SerializeField] private string savingMessage = "선택한 사진을 작업용 경로에 저장하는 중입니다.";
    [SerializeField] private string slicingMessage = "퍼즐용 이미지 조각을 생성하는 중입니다.";
    [SerializeField] private string successMessage = "사진 저장과 이미지 분할이 완료되었습니다.";
    [SerializeField] private string cancelMessage = "사진 선택이 취소되었습니다.";

    public string SelectedImagePath { get; private set; }
    public bool HasSelectedImage => !string.IsNullOrEmpty(SelectedImagePath);
    public string SavedImagePath { get; private set; }
    public bool HasSavedImage => !string.IsNullOrEmpty(SavedImagePath);
    public sh_ImageSliceService.SliceResult LastSliceResult { get; private set; }
    public int SelectedMarkerPieceIndex { get; private set; } = -1;

    private bool isProcessingImage;
    private Sprite defaultBackgroundSprite;

    private void Awake()
    {
        ResolveReferences();
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
        CacheDefaultBackgroundSprite();
        SetAttachFileButtonVisible(true);
        SetMoveToMainButtonVisible(false);
        SetStatus(defaultMessage);
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
        SetStatus(successMessage);
        Debug.Log(
            $"{nameof(sh_LoginSceneController)}: Selected image path = {selectedPath}\n" +
            $"Saved image path = {SavedImagePath}\n" +
            $"Selected marker piece index = {SelectedMarkerPieceIndex}\n" +
            $"Selected marker piece path = {sliceResult.SelectedMarkerPiece.PiecePath}",
            this);

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


}
