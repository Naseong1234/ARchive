using UnityEngine;

public sealed class sh_PuzzleSlot : MonoBehaviour
{
    [SerializeField] private int slotNumber = 1;
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private sh_PuzzlePieceUI currentPiece;

    public int SlotNumber => slotNumber;
    public RectTransform RectTransform => rectTransform;
    public sh_PuzzlePieceUI CurrentPiece => currentPiece;
    public bool IsValid => slotNumber > 0;
    public bool IsOccupied => currentPiece != null;

    private void Awake()
    {
        ResolveReferences();
    }

    public void SetSlotNumber(int newSlotNumber)
    {
        slotNumber = Mathf.Max(1, newSlotNumber);
    }

    public bool CanAssign(sh_PuzzlePieceUI piece)
    {
        return piece != null && (currentPiece == null || currentPiece == piece);
    }

    public void AssignPiece(sh_PuzzlePieceUI piece)
    {
        if (piece == null)
        {
            return;
        }

        currentPiece = piece;
    }

    public void ClearPiece(sh_PuzzlePieceUI piece)
    {
        if (piece != null && currentPiece == piece)
        {
            currentPiece = null;
        }
    }

    public void ClearPiece()
    {
        currentPiece = null;
    }

    private void OnValidate()
    {
        slotNumber = Mathf.Max(1, slotNumber);
        ResolveReferences();
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void ResolveReferences()
    {
        if (rectTransform == null)
        {
            rectTransform = transform as RectTransform;
        }
    }
}
