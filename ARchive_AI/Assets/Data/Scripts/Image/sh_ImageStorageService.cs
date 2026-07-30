using System;
using System.IO;
using UnityEngine;

public sealed class sh_ImageStorageService : MonoBehaviour
{
    private const string SavedImagePathPlayerPrefsKey = "sh_saved_original_image_path";

    [Serializable]
    public struct StorageResult
    {
        public bool IsSuccess;
        public string SourceFilePath;
        public string SavedFilePath;
        public string FileName;
        public string ErrorMessage;

        public static StorageResult CreateSuccess(string sourceFilePath, string savedFilePath, string fileName)
        {
            return new StorageResult
            {
                IsSuccess = true,
                SourceFilePath = sourceFilePath,
                SavedFilePath = savedFilePath,
                FileName = fileName,
                ErrorMessage = string.Empty
            };
        }

        public static StorageResult CreateFailure(string sourceFilePath, string errorMessage)
        {
            return new StorageResult
            {
                IsSuccess = false,
                SourceFilePath = sourceFilePath,
                SavedFilePath = string.Empty,
                FileName = string.Empty,
                ErrorMessage = errorMessage
            };
        }
    }

    [Header("Storage Settings")]
    [SerializeField] private string storageFolderName = "SourceImage";
    [SerializeField] private string fileNamePrefix = "user_source";
    [SerializeField] private bool appendSessionStamp = true;

    public string LastSavedFilePath { get; private set; }
    public string StorageDirectoryPath => Path.Combine(Application.persistentDataPath, "Data", "Image", storageFolderName);

    private string sessionStamp;

    private void Awake()
    {
        sessionStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        LastSavedFilePath = PlayerPrefs.GetString(SavedImagePathPlayerPrefsKey, string.Empty);
    }

    public StorageResult SaveImage(string sourceFilePath)
    {
        if (string.IsNullOrWhiteSpace(sourceFilePath))
        {
            StorageResult emptyPathResult = StorageResult.CreateFailure(sourceFilePath, "저장할 원본 이미지 경로가 비어 있습니다.");
            Debug.LogError($"{nameof(sh_ImageStorageService)}: {emptyPathResult.ErrorMessage}", this);
            return emptyPathResult;
        }

        if (!File.Exists(sourceFilePath))
        {
            StorageResult missingFileResult = StorageResult.CreateFailure(sourceFilePath, $"원본 이미지 파일을 찾을 수 없습니다.\n{sourceFilePath}");
            Debug.LogError($"{nameof(sh_ImageStorageService)}: {missingFileResult.ErrorMessage}", this);
            return missingFileResult;
        }

        try
        {
            Directory.CreateDirectory(StorageDirectoryPath);

            string extension = GetValidatedExtension(sourceFilePath);
            if (string.IsNullOrEmpty(extension))
            {
                StorageResult invalidExtensionResult = StorageResult.CreateFailure(sourceFilePath, "지원하지 않는 이미지 형식입니다. PNG 또는 JPG 파일을 선택해주세요.");
                Debug.LogError($"{nameof(sh_ImageStorageService)}: {invalidExtensionResult.ErrorMessage}", this);
                return invalidExtensionResult;
            }

            string fileName = BuildFileName(extension);
            string destinationPath = Path.Combine(StorageDirectoryPath, fileName);

            File.Copy(sourceFilePath, destinationPath, true);

            LastSavedFilePath = destinationPath;
            PlayerPrefs.SetString(SavedImagePathPlayerPrefsKey, LastSavedFilePath);
            PlayerPrefs.Save();
            Debug.Log($"{nameof(sh_ImageStorageService)}: Source image saved.\n{destinationPath}", this);

            return StorageResult.CreateSuccess(sourceFilePath, destinationPath, fileName);
        }
        catch (Exception exception)
        {
            StorageResult failureResult = StorageResult.CreateFailure(sourceFilePath, $"원본 이미지 저장 중 오류가 발생했습니다.\n{exception.Message}");
            Debug.LogError($"{nameof(sh_ImageStorageService)}: {failureResult.ErrorMessage}", this);
            return failureResult;
        }
    }

    public bool TryGetSavedImagePath(out string savedImagePath)
    {
        savedImagePath = LastSavedFilePath;

        if (string.IsNullOrWhiteSpace(savedImagePath))
        {
            savedImagePath = PlayerPrefs.GetString(SavedImagePathPlayerPrefsKey, string.Empty);
        }

        if (string.IsNullOrWhiteSpace(savedImagePath) || !File.Exists(savedImagePath))
        {
            savedImagePath = string.Empty;
            return false;
        }

        LastSavedFilePath = savedImagePath;
        return true;
    }

    public static bool TryLoadSavedImagePath(out string savedImagePath)
    {
        savedImagePath = PlayerPrefs.GetString(SavedImagePathPlayerPrefsKey, string.Empty);

        if (string.IsNullOrWhiteSpace(savedImagePath) || !File.Exists(savedImagePath))
        {
            savedImagePath = string.Empty;
            return false;
        }

        return true;
    }

    public void ClearSavedImageRecord()
    {
        LastSavedFilePath = string.Empty;
        PlayerPrefs.DeleteKey(SavedImagePathPlayerPrefsKey);
        PlayerPrefs.Save();
    }

    private string BuildFileName(string extension)
    {
        if (!appendSessionStamp)
        {
            return $"{fileNamePrefix}{extension}";
        }

        return $"{fileNamePrefix}_{sessionStamp}{extension}";
    }

    private static string GetValidatedExtension(string sourceFilePath)
    {
        string extension = Path.GetExtension(sourceFilePath)?.ToLowerInvariant();

        switch (extension)
        {
            case ".png":
            case ".jpg":
            case ".jpeg":
                return extension;
            default:
                return string.Empty;
        }
    }
}
