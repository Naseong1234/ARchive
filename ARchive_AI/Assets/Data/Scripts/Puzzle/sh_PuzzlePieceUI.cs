using UnityEngine;
using UnityEngine.UI;

public sealed class sh_PuzzlePieceUI : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Image pieceImage;
    [SerializeField] private RectTransform rectTransform;

    [Header("Debug Data")]
    [SerializeField] private int answerSlotNumber;
    [SerializeField] private int currentRotationValue;

    public Image PieceImage => pieceImage;
    public RectTransform RectTransform => rectTransform;
    public int AnswerSlotNumber => answerSlotNumber;
    public int CurrentRotationValue => currentRotationValue;

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
        currentRotationValue = pieceData.CurrentRotationValue;
        gameObject.name = $"PuzzlePiece_{answerSlotNumber:00}";

        if (pieceImage != null)
        {
            pieceImage.sprite = pieceSprite;
            pieceImage.preserveAspect = true;
            pieceImage.enabled = pieceSprite != null;
        }

        if (rectTransform != null)
        {
            rectTransform.localRotation = Quaternion.Euler(0f, 0f, currentRotationValue);
        }
    }

    private void ResolveReferences()
    {
        if (pieceImage == null)
        {
            pieceImage = GetComponent<Image>();
        }

        if (rectTransform == null)
        {
            rectTransform = transform as RectTransform;
        }
    }
}
