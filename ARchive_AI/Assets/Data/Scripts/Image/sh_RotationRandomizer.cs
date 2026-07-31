using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public sealed class sh_RotationRandomizer : MonoBehaviour
{
    [Serializable]
    public struct RotationResult
    {
        public bool IsSuccess;
        public string SourceDirectoryPath;
        public List<sh_PuzzlePieceData> PieceDataList;
        public string ErrorMessage;

        public static RotationResult CreateSuccess(string sourceDirectoryPath, List<sh_PuzzlePieceData> pieceDataList)
        {
            return new RotationResult
            {
                IsSuccess = true,
                SourceDirectoryPath = sourceDirectoryPath,
                PieceDataList = pieceDataList,
                ErrorMessage = string.Empty
            };
        }

        public static RotationResult CreateFailure(string sourceDirectoryPath, string errorMessage)
        {
            return new RotationResult
            {
                IsSuccess = false,
                SourceDirectoryPath = sourceDirectoryPath,
                PieceDataList = new List<sh_PuzzlePieceData>(),
                ErrorMessage = errorMessage
            };
        }
    }

    [Header("Rotation Settings")]
    [SerializeField] private int expectedPieceCount = 9;
    [SerializeField] private bool allowZeroRotation = true;

    private static readonly int[] AllowedRotationValues = { 0, 90, 180, 270 };

    public RotationResult CreatePieceDataList(IReadOnlyList<string> pieceImagePaths)
    {
        if (pieceImagePaths == null)
        {
            return LogFailure(string.Empty, "퍼즐 조각 경로 목록이 비어 있습니다.");
        }

        if (pieceImagePaths.Count != expectedPieceCount)
        {
            return LogFailure(string.Empty, $"퍼즐 조각 개수가 {expectedPieceCount}개가 아닙니다. 현재 개수: {pieceImagePaths.Count}");
        }

        List<sh_PuzzlePieceData> generatedPieceData = new List<sh_PuzzlePieceData>(pieceImagePaths.Count);
        string sourceDirectoryPath = string.Empty;

        for (int index = 0; index < pieceImagePaths.Count; index++)
        {
            string pieceImagePath = pieceImagePaths[index];

            if (string.IsNullOrWhiteSpace(pieceImagePath))
            {
                return LogFailure(sourceDirectoryPath, $"{index + 1}번 조각 경로가 비어 있습니다.");
            }

            if (!File.Exists(pieceImagePath))
            {
                return LogFailure(sourceDirectoryPath, $"퍼즐 조각 파일을 찾을 수 없습니다.\n{pieceImagePath}");
            }

            if (string.IsNullOrEmpty(sourceDirectoryPath))
            {
                sourceDirectoryPath = Path.GetDirectoryName(pieceImagePath) ?? string.Empty;
            }

            int rotationValue = GetRandomRotationValue();
            generatedPieceData.Add(new sh_PuzzlePieceData(index + 1, rotationValue, pieceImagePath));
        }

        Debug.Log(
            $"{nameof(sh_RotationRandomizer)}: Rotation data generated.\n" +
            $"Piece count = {generatedPieceData.Count}\n" +
            $"Source path = {sourceDirectoryPath}",
            this);

        return RotationResult.CreateSuccess(sourceDirectoryPath, generatedPieceData);
    }

    private RotationResult LogFailure(string sourceDirectoryPath, string errorMessage)
    {
        Debug.LogError($"{nameof(sh_RotationRandomizer)}: {errorMessage}", this);
        return RotationResult.CreateFailure(sourceDirectoryPath, errorMessage);
    }

    private int GetRandomRotationValue()
    {
        int startIndex = allowZeroRotation ? 0 : 1;
        int randomIndex = UnityEngine.Random.Range(startIndex, AllowedRotationValues.Length);
        return AllowedRotationValues[randomIndex];
    }
}
