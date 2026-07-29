using System;
using System.IO;
using UnityEngine;

public sealed class sh_SessionCleanupService : MonoBehaviour
{
    [Serializable]
    public struct CleanupResult
    {
        public int DeletedFileCount;
        public int DeletedDirectoryCount;
        public string LastDeletedOriginalImagePath;
    }

    [Header("Service References")]
    [SerializeField] private sh_ImageStorageService imageStorageService;
    [SerializeField] private sh_ImageSliceService imageSliceService;

    [Header("Cleanup Settings")]
    [SerializeField] private bool deleteEmptyDirectories = true;

    [Header("Debug Data")]
    [SerializeField] private CleanupResult lastCleanupResult;

    public CleanupResult LastCleanupResult => lastCleanupResult;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Reset()
    {
        ResolveReferences();
    }

    public CleanupResult CleanupSessionFiles()
    {
        ResolveReferences();

        CleanupResult cleanupResult = default;

        if (imageStorageService != null && imageStorageService.TryGetSavedImagePath(out string savedImagePath))
        {
            if (DeleteFileIfExists(savedImagePath))
            {
                cleanupResult.DeletedFileCount++;
                cleanupResult.LastDeletedOriginalImagePath = savedImagePath;
            }
        }

        if (imageSliceService != null)
        {
            cleanupResult.DeletedFileCount += DeletePngFilesInDirectory(imageSliceService.RotatingDirectoryPath);
            cleanupResult.DeletedFileCount += DeletePngFilesInDirectory(imageSliceService.MarkerDirectoryPath);

            if (deleteEmptyDirectories)
            {
                cleanupResult.DeletedDirectoryCount += DeleteDirectoryIfEmpty(imageSliceService.RotatingDirectoryPath);
                cleanupResult.DeletedDirectoryCount += DeleteDirectoryIfEmpty(imageSliceService.MarkerDirectoryPath);
            }

            imageSliceService.ClearSelectedMarkerPieceRecord();
        }

        if (imageStorageService != null)
        {
            if (deleteEmptyDirectories)
            {
                cleanupResult.DeletedDirectoryCount += DeleteDirectoryIfEmpty(imageStorageService.StorageDirectoryPath);
            }

            imageStorageService.ClearSavedImageRecord();
        }

        lastCleanupResult = cleanupResult;

        Debug.Log(
            $"{nameof(sh_SessionCleanupService)}: 세션 정리 완료.\n" +
            $"삭제한 파일 수: {cleanupResult.DeletedFileCount}\n" +
            $"삭제한 폴더 수: {cleanupResult.DeletedDirectoryCount}\n" +
            $"원본 이미지 경로: {cleanupResult.LastDeletedOriginalImagePath}",
            this);

        return cleanupResult;
    }

    private void ResolveReferences()
    {
        if (imageStorageService == null)
        {
            imageStorageService = FindAnyObjectByType<sh_ImageStorageService>();
        }

        if (imageSliceService == null)
        {
            imageSliceService = FindAnyObjectByType<sh_ImageSliceService>();
        }
    }

    private static bool DeleteFileIfExists(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return false;
        }

        File.Delete(filePath);
        return true;
    }

    private static int DeletePngFilesInDirectory(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
        {
            return 0;
        }

        string[] pngFiles = Directory.GetFiles(directoryPath, "*.png", SearchOption.TopDirectoryOnly);
        int deletedFileCount = 0;

        for (int index = 0; index < pngFiles.Length; index++)
        {
            if (!DeleteFileIfExists(pngFiles[index]))
            {
                continue;
            }

            deletedFileCount++;
        }

        return deletedFileCount;
    }

    private static int DeleteDirectoryIfEmpty(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
        {
            return 0;
        }

        if (Directory.GetFiles(directoryPath).Length > 0 || Directory.GetDirectories(directoryPath).Length > 0)
        {
            return 0;
        }

        Directory.Delete(directoryPath, false);
        return 1;
    }
}
