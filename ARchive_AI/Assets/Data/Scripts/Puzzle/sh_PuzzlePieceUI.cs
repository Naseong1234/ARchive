using UnityEngine;
using UnityEngine.UI;

public sealed class sh_PuzzlePieceUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image inputImage;
    [SerializeField] private Image pieceImage;
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private AspectRatioFitter pieceImageAspectFitter;

    [Header("Debug Data")]
    [SerializeField] private int answerSlotNumber;
    [SerializeField] private int currentRotationValue;
    [SerializeField] private bool isPlacedCorrectly;

    public Image InputImage => inputImage;
    public Image PieceImage => pieceImage;
    public RectTransform RectTransform => rectTransform;
    public int AnswerSlotNumber => answerSlotNumber;
    public int CurrentRotationValue => currentRotationValue;
    public bool IsPlacedCorrectly => isPlacedCorrectly;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Reset()
    {
        ResolveReferences();
    }

    public void Configure(sh_PuzzlePieceData pieceData, Sprite pieceSprite)
    {
        if (pieceData == null)
        {
            Debug.LogError($"{nameof(sh_PuzzlePieceUI)}: 퍼즐 조각 데이터가 비어 있습니다.", this);
            return;
        }

        ResolveReferences();

        answerSlotNumber = pieceData.AnswerSlotNumber;
        currentRotationValue = NormalizeRotation(pieceData.CurrentRotationValue);
        isPlacedCorrectly = false;
        gameObject.name = $"PuzzlePiece_{answerSlotNumber:00}";

        if (pieceImage != null)
        {
            pieceImage.sprite = pieceSprite;
            pieceImage.preserveAspect = false;
            pieceImage.enabled = pieceSprite != null;
            ApplyPieceImageVisibility(pieceImage, pieceSprite != null);
        }

        if (pieceImageAspectFitter != null)
        {
            pieceImageAspectFitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            pieceImageAspectFitter.aspectRatio = GetAspectRatio(pieceSprite);
            pieceImageAspectFitter.enabled = pieceSprite != null;
        }

        if (rectTransform != null)
        {
            rectTransform.localRotation = Quaternion.Euler(0f, 0f, currentRotationValue);
        }
    }

    public void SetCorrectState(bool isCorrect)
    {
        isPlacedCorrectly = isCorrect;
    }

    public int RotateClockwise()
    {
        currentRotationValue = NormalizeRotation(currentRotationValue + 90);

        if (rectTransform != null)
        {
            rectTransform.localRotation = Quaternion.Euler(0f, 0f, currentRotationValue);
        }

        return currentRotationValue;
    }

    public int GetNormalizedRotationValue()
    {
        return NormalizeRotation(currentRotationValue);
    }

    public void SetRotationValue(int rotationValue)
    {
        currentRotationValue = NormalizeRotation(rotationValue);

        if (rectTransform != null)
        {
            rectTransform.localRotation = Quaternion.Euler(0f, 0f, currentRotationValue);
        }
    }

    private void ResolveReferences()
    {
        if (inputImage == null)
        {
            inputImage = GetComponent<Image>();
        }

        if (pieceImage == null)
        {
            pieceImage = GetComponentInChildren<Image>(true);

            if (pieceImage == inputImage)
            {
                pieceImage = inputImage;
            }
        }

        if (rectTransform == null)
        {
            rectTransform = transform as RectTransform;
        }

        if (pieceImageAspectFitter == null && pieceImage != null)
        {
            pieceImageAspectFitter = pieceImage.GetComponent<AspectRatioFitter>();
        }
    }

    private static void ApplyPieceImageVisibility(Image targetImage, bool isVisible)
    {
        if (targetImage == null)
        {
            return;
        }

        Color imageColor = targetImage.color;
        imageColor.a = isVisible ? 1f : 0f;
        targetImage.color = imageColor;
    }

    private static int NormalizeRotation(int rotationValue)
    {
        int normalizedValue = rotationValue % 360;

        if (normalizedValue < 0)
        {
            normalizedValue += 360;
        }

        return normalizedValue;
    }

    private static float GetAspectRatio(Sprite pieceSprite)
    {
        if (pieceSprite == null || pieceSprite.rect.height <= 0f)
        {
            return 1f;
        }

        return pieceSprite.rect.width / pieceSprite.rect.height;
    }
}
