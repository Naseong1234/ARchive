using System;
using System.IO;
using UnityEngine;

[Serializable]
public sealed class sh_PuzzlePieceData
{
    [SerializeField] private int answerSlotNumber = -1;
    [SerializeField] private int currentRotationValue;
    [SerializeField] private string imagePath = string.Empty;

    public int AnswerSlotNumber => answerSlotNumber;
    public int CurrentRotationValue => currentRotationValue;
    public string ImagePath => imagePath;
    public string ImageFileName => string.IsNullOrWhiteSpace(imagePath) ? string.Empty : Path.GetFileName(imagePath);
    public bool IsValid => answerSlotNumber > 0 && !string.IsNullOrWhiteSpace(imagePath);

    public sh_PuzzlePieceData(int answerSlotNumber, int currentRotationValue, string imagePath)
    {
        this.answerSlotNumber = answerSlotNumber;
        this.currentRotationValue = currentRotationValue;
        this.imagePath = imagePath ?? string.Empty;
    }

    public void SetRotation(int rotationValue)
    {
        currentRotationValue = rotationValue;
    }
}
