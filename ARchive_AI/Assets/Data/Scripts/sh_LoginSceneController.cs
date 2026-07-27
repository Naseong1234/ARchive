using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class sh_LoginSceneController : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button attachFileButton;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private sh_ImagePickerService imagePickerService;
    [SerializeField] private sh_ImageStorageService imageStorageService;
    [SerializeField] private sh_ImageSliceService imageSliceService;

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

    private void Reset()
    {
        if (attachFileButton == null)
        {
            attachFileButton = GetComponentInChildren<Button>(true);
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
    }

    private void OnEnable()
    {
        if (attachFileButton != null)
        {
            attachFileButton.onClick.AddListener(OnAttachFileButtonClicked);
        }

        SetStatus(defaultMessage);
    }

    private void OnDisable()
    {
        if (attachFileButton != null)
        {
            attachFileButton.onClick.RemoveListener(OnAttachFileButtonClicked);
        }
    }

    public void OnAttachFileButtonClicked()
    {
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

        SetInteractable(false);
        SetStatus(pickingMessage);

        imagePickerService.PickImage(HandleImagePickSuccess, HandleImagePickCancelled, HandleImagePickFailed);
    }

    private void HandleImagePickSuccess(string selectedPath)
    {
        SelectedImagePath = selectedPath;

        if (imageStorageService == null)
        {
            SetInteractable(true);
            SetStatus("이미지 저장 서비스가 연결되어 있지 않습니다.");
            Debug.LogError($"{nameof(sh_LoginSceneController)}: {nameof(imageStorageService)} reference is missing.", this);
            return;
        }

        SetStatus(savingMessage);
        sh_ImageStorageService.StorageResult storageResult = imageStorageService.SaveImage(selectedPath);
        SetInteractable(true);

        if (!storageResult.IsSuccess)
        {
            SavedImagePath = string.Empty;
            SetStatus(storageResult.ErrorMessage);
            Debug.LogError($"{nameof(sh_LoginSceneController)}: {storageResult.ErrorMessage}", this);
            return;
        }

        SavedImagePath = storageResult.SavedFilePath;
        LastSliceResult = default;

        if (imageSliceService == null)
        {
            SetStatus("이미지 분할 서비스가 연결되어 있지 않습니다.");
            Debug.LogError($"{nameof(sh_LoginSceneController)}: {nameof(imageSliceService)} reference is missing.", this);
            return;
        }

        SetStatus(slicingMessage);
        sh_ImageSliceService.SliceResult sliceResult = imageSliceService.SliceSavedImage(SavedImagePath);

        if (!sliceResult.IsSuccess)
        {
            SelectedMarkerPieceIndex = -1;
            SetStatus(sliceResult.ErrorMessage);
            Debug.LogError($"{nameof(sh_LoginSceneController)}: {sliceResult.ErrorMessage}", this);
            return;
        }

        LastSliceResult = sliceResult;
        SelectedMarkerPieceIndex = sliceResult.SelectedMarkerPiece.PieceIndex;
        SetStatus(
            $"{successMessage}\n" +
            $"원본 저장 경로: {SavedImagePath}\n" +
            $"1차 조각 폴더: {sliceResult.MarkerDirectoryPath}\n" +
            $"2차 조각 폴더: {sliceResult.RotatingDirectoryPath}\n" +
            $"랜덤 선택 조각: {sliceResult.SelectedMarkerPiece.FileName}");
        Debug.Log(
            $"{nameof(sh_LoginSceneController)}: Selected image path = {selectedPath}\n" +
            $"Saved image path = {SavedImagePath}\n" +
            $"Selected marker piece index = {SelectedMarkerPieceIndex}\n" +
            $"Selected marker piece path = {sliceResult.SelectedMarkerPiece.PiecePath}",
            this);
    }

    private void HandleImagePickCancelled()
    {
        SetInteractable(true);
        SetStatus(cancelMessage);
        Debug.Log($"{nameof(sh_LoginSceneController)}: Image selection was cancelled.", this);
    }

    private void HandleImagePickFailed(string errorMessage)
    {
        SetInteractable(true);
        SetStatus(errorMessage);
        Debug.LogError($"{nameof(sh_LoginSceneController)}: {errorMessage}", this);
    }

    private void SetInteractable(bool isInteractable)
    {
        if (attachFileButton != null)
        {
            attachFileButton.interactable = isInteractable;
        }
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }
}
