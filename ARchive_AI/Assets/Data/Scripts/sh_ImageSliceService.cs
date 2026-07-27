using System;
using System.IO;
using UnityEngine;

public sealed class sh_ImageSliceService : MonoBehaviour
{
    private const string SelectedMarkerPieceIndexPlayerPrefsKey = "sh_selected_marker_piece_index";
    private const string SelectedMarkerPiecePathPlayerPrefsKey = "sh_selected_marker_piece_path";
    private const string SelectedMarkerPieceFileNamePlayerPrefsKey = "sh_selected_marker_piece_file_name";

    [Serializable]
    public struct SliceResult
    {
        public bool IsSuccess;
        public string SourceImagePath;
        public string MarkerDirectoryPath;
        public string RotatingDirectoryPath;
        public SelectedMarkerPieceData SelectedMarkerPiece;
        public string[] MarkerPiecePaths;
        public string[] RotatingPiecePaths;
        public string ErrorMessage;

        public static SliceResult CreateSuccess(
            string sourceImagePath,
            string markerDirectoryPath,
            string rotatingDirectoryPath,
            SelectedMarkerPieceData selectedMarkerPiece,
            string[] markerPiecePaths,
            string[] rotatingPiecePaths)
        {
            return new SliceResult
            {
                IsSuccess = true,
                SourceImagePath = sourceImagePath,
                MarkerDirectoryPath = markerDirectoryPath,
                RotatingDirectoryPath = rotatingDirectoryPath,
                SelectedMarkerPiece = selectedMarkerPiece,
                MarkerPiecePaths = markerPiecePaths,
                RotatingPiecePaths = rotatingPiecePaths,
                ErrorMessage = string.Empty,
            };
        }

        public static SliceResult CreateFailure(string sourceImagePath, string errorMessage)
        {
            return new SliceResult
            {
                IsSuccess = false,
                SourceImagePath = sourceImagePath,
                MarkerDirectoryPath = string.Empty,
                RotatingDirectoryPath = string.Empty,
                SelectedMarkerPiece = SelectedMarkerPieceData.CreateEmpty(),
                MarkerPiecePaths = Array.Empty<string>(),
                RotatingPiecePaths = Array.Empty<string>(),
                ErrorMessage = errorMessage,
            };
        }
    }

    [Serializable]
    public struct SelectedMarkerPieceData
    {
        public int PieceIndex;
        public string PiecePath;
        public string FileName;

        public bool IsValid => PieceIndex >= 0 && !string.IsNullOrWhiteSpace(PiecePath);

        public static SelectedMarkerPieceData Create(int pieceIndex, string piecePath, string fileName)
        {
            return new SelectedMarkerPieceData
            {
                PieceIndex = pieceIndex,
                PiecePath = piecePath,
                FileName = fileName
            };
        }

        public static SelectedMarkerPieceData CreateEmpty()
        {
            return new SelectedMarkerPieceData
            {
                PieceIndex = -1,
                PiecePath = string.Empty,
                FileName = string.Empty
            };
        }
    }

    [Header("Slice Settings")]
    [SerializeField] private string markerFolderName = "MarkerImage";
    [SerializeField] private string rotatingFolderName = "RotatingImage";
    [SerializeField] private string markerFilePrefix = "marker_piece";
    [SerializeField] private string rotatingFilePrefix = "rotating_piece";
    [SerializeField] private int sliceGridSize = 3;

    public string MarkerDirectoryPath => Path.Combine(Application.persistentDataPath, "Data", "Image", markerFolderName);
    public string RotatingDirectoryPath => Path.Combine(MarkerDirectoryPath, rotatingFolderName);
    public SelectedMarkerPieceData LastSelectedMarkerPiece { get; private set; } = SelectedMarkerPieceData.CreateEmpty();

    private void Awake()
    {
        LoadSelectedMarkerPiece();
    }

    public SliceResult SliceSavedImage(string sourceImagePath)
    {
        if (string.IsNullOrWhiteSpace(sourceImagePath))
        {
            return LogFailure(sourceImagePath, "분할할 원본 이미지 경로가 비어 있습니다.");
        }

        if (!File.Exists(sourceImagePath))
        {
            return LogFailure(sourceImagePath, $"분할할 원본 이미지 파일을 찾을 수 없습니다.\n{sourceImagePath}");
        }

        try
        {
            Texture2D sourceTexture = LoadTextureFromFile(sourceImagePath);

            if (sourceTexture == null)
            {
                return LogFailure(sourceImagePath, "원본 이미지를 텍스처로 불러오지 못했습니다.");
            }

            Directory.CreateDirectory(MarkerDirectoryPath);
            Directory.CreateDirectory(RotatingDirectoryPath);
            ClearPngFiles(MarkerDirectoryPath);
            ClearPngFiles(RotatingDirectoryPath);

            string[] markerPiecePaths = SavePieces(sourceTexture, MarkerDirectoryPath, markerFilePrefix);
            int selectedMarkerPieceIndex = UnityEngine.Random.Range(0, markerPiecePaths.Length);
            string selectedMarkerPiecePath = markerPiecePaths[selectedMarkerPieceIndex];
            string selectedMarkerPieceFileName = Path.GetFileName(selectedMarkerPiecePath);
            DestroyTexture(sourceTexture);

            Texture2D selectedMarkerTexture = LoadTextureFromFile(selectedMarkerPiecePath);

            if (selectedMarkerTexture == null)
            {
                return LogFailure(sourceImagePath, "선택된 1차 조각을 다시 불러오지 못했습니다.");
            }

            string[] rotatingPiecePaths = SavePieces(selectedMarkerTexture, RotatingDirectoryPath, rotatingFilePrefix);
            DestroyTexture(selectedMarkerTexture);

            LastSelectedMarkerPiece = SelectedMarkerPieceData.Create(
                selectedMarkerPieceIndex,
                selectedMarkerPiecePath,
                selectedMarkerPieceFileName);
            SaveSelectedMarkerPiece(LastSelectedMarkerPiece);

            Debug.Log(
                $"{nameof(sh_ImageSliceService)}: Image slice completed.\n" +
                $"Marker pieces = {markerPiecePaths.Length}\n" +
                $"Rotating pieces = {rotatingPiecePaths.Length}\n" +
                $"Selected marker piece = {selectedMarkerPieceIndex + 1}\n" +
                $"Marker path = {MarkerDirectoryPath}\n" +
                $"Rotating path = {RotatingDirectoryPath}",
                this);

            return SliceResult.CreateSuccess(
                sourceImagePath,
                MarkerDirectoryPath,
                RotatingDirectoryPath,
                LastSelectedMarkerPiece,
                markerPiecePaths,
                rotatingPiecePaths);
        }
        catch (Exception exception)
        {
            return LogFailure(sourceImagePath, $"이미지 분할 중 오류가 발생했습니다.\n{exception.Message}");
        }
    }

    public bool TryGetSelectedMarkerPieceData(out SelectedMarkerPieceData selectedMarkerPieceData)
    {
        if (LastSelectedMarkerPiece.IsValid && File.Exists(LastSelectedMarkerPiece.PiecePath))
        {
            selectedMarkerPieceData = LastSelectedMarkerPiece;
            return true;
        }

        LoadSelectedMarkerPiece();

        if (!LastSelectedMarkerPiece.IsValid || !File.Exists(LastSelectedMarkerPiece.PiecePath))
        {
            selectedMarkerPieceData = SelectedMarkerPieceData.CreateEmpty();
            return false;
        }

        selectedMarkerPieceData = LastSelectedMarkerPiece;
        return true;
    }

    private SliceResult LogFailure(string sourceImagePath, string errorMessage)
    {
        Debug.LogError($"{nameof(sh_ImageSliceService)}: {errorMessage}", this);
        return SliceResult.CreateFailure(sourceImagePath, errorMessage);
    }

    private string[] SavePieces(Texture2D sourceTexture, string directoryPath, string filePrefix)
    {
        int[] widths = BuildSegmentSizes(sourceTexture.width, sliceGridSize);
        int[] heights = BuildSegmentSizes(sourceTexture.height, sliceGridSize);
        string[] savedPaths = new string[sliceGridSize * sliceGridSize];
        int startY = 0;
        int pieceIndex = 0;

        for (int row = 0; row < sliceGridSize; row++)
        {
            int startX = 0;

            for (int column = 0; column < sliceGridSize; column++)
            {
                int width = widths[column];
                int height = heights[row];
                Texture2D pieceTexture = CreatePieceTexture(sourceTexture, startX, startY, width, height);
                string filePath = Path.Combine(directoryPath, $"{filePrefix}_{pieceIndex + 1:00}.png");
                File.WriteAllBytes(filePath, pieceTexture.EncodeToPNG());
                savedPaths[pieceIndex] = filePath;

                DestroyTexture(pieceTexture);
                startX += width;
                pieceIndex++;
            }

            startY += heights[row];
        }

        return savedPaths;
    }

    private static Texture2D CreatePieceTexture(Texture2D sourceTexture, int startX, int startY, int width, int height)
    {
        Texture2D pieceTexture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        pieceTexture.SetPixels(sourceTexture.GetPixels(startX, startY, width, height));
        pieceTexture.Apply();
        return pieceTexture;
    }

    private static Texture2D LoadTextureFromFile(string filePath)
    {
        byte[] imageBytes = File.ReadAllBytes(filePath);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);

        if (!ImageConversion.LoadImage(texture, imageBytes, false))
        {
            DestroyTexture(texture);
            return null;
        }

        return texture;
    }

    private void LoadSelectedMarkerPiece()
    {
        int pieceIndex = PlayerPrefs.GetInt(SelectedMarkerPieceIndexPlayerPrefsKey, -1);
        string piecePath = PlayerPrefs.GetString(SelectedMarkerPiecePathPlayerPrefsKey, string.Empty);
        string fileName = PlayerPrefs.GetString(SelectedMarkerPieceFileNamePlayerPrefsKey, string.Empty);
        LastSelectedMarkerPiece = SelectedMarkerPieceData.Create(pieceIndex, piecePath, fileName);
    }

    private static void SaveSelectedMarkerPiece(SelectedMarkerPieceData selectedMarkerPieceData)
    {
        PlayerPrefs.SetInt(SelectedMarkerPieceIndexPlayerPrefsKey, selectedMarkerPieceData.PieceIndex);
        PlayerPrefs.SetString(SelectedMarkerPiecePathPlayerPrefsKey, selectedMarkerPieceData.PiecePath);
        PlayerPrefs.SetString(SelectedMarkerPieceFileNamePlayerPrefsKey, selectedMarkerPieceData.FileName);
        PlayerPrefs.Save();
    }

    private static int[] BuildSegmentSizes(int totalSize, int segmentCount)
    {
        int baseSize = totalSize / segmentCount;
        int remainder = totalSize % segmentCount;
        int[] segmentSizes = new int[segmentCount];

        for (int index = 0; index < segmentSizes.Length; index++)
        {
            segmentSizes[index] = baseSize + (index < remainder ? 1 : 0);
        }

        return segmentSizes;
    }

    private static void ClearPngFiles(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            return;
        }

        string[] pngFiles = Directory.GetFiles(directoryPath, "*.png", SearchOption.TopDirectoryOnly);
        for (int index = 0; index < pngFiles.Length; index++)
        {
            File.Delete(pngFiles[index]);
        }
    }

    private static void DestroyTexture(Texture2D texture)
    {
        if (texture == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(texture);
            return;
        }

        DestroyImmediate(texture);
    }
}
