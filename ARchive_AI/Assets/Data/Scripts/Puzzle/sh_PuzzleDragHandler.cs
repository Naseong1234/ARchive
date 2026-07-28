using UnityEngine;
using UnityEngine.EventSystems;

public sealed class sh_PuzzleDragHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Header("Runtime References")]
    [SerializeField] private sh_PuzzlePieceUI puzzlePieceUI;
    [SerializeField] private RectTransform rectTransform;
    [SerializeField] private CanvasGroup canvasGroup;

    [Header("Debug Data")]
    [SerializeField] private bool isLocked;
    [SerializeField] private bool isPlacedCorrectly;
    [SerializeField] private float tapMovementThreshold = 15f;

    private sh_PuzzleBoardController puzzleBoardController;
    private RectTransform dragParentRectTransform;
    private sh_PuzzleSlot currentSlot;
    private sh_PuzzleSlot previousSlotBeforeDrag;
    private RectTransform currentSpawnPoint;
    private RectTransform previousSpawnPointBeforeDrag;
    private Vector3 returnWorldPosition;
    private Vector3 pointerToPieceOffset;
    private Vector2 pointerDownScreenPosition;
    private bool didDragThisPress;
    private int originalSiblingIndex;

    public sh_PuzzlePieceUI PuzzlePieceUI => puzzlePieceUI;
    public RectTransform RectTransform => rectTransform;
    public sh_PuzzleSlot CurrentSlot => currentSlot;
    public sh_PuzzleSlot PreviousSlotBeforeDrag => previousSlotBeforeDrag;
    public RectTransform CurrentSpawnPoint => currentSpawnPoint;
    public RectTransform PreviousSpawnPointBeforeDrag => previousSpawnPointBeforeDrag;
    public bool IsLocked => isLocked;
    public bool IsPlacedCorrectly => isPlacedCorrectly;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Reset()
    {
        ResolveReferences();
    }

    public void Initialize(sh_PuzzleBoardController boardController)
    {
        ResolveReferences();
        puzzleBoardController = boardController;
        dragParentRectTransform = rectTransform.parent as RectTransform;
        CaptureCurrentPositionAsReturnPosition();
        SetLocked(false);
        SetPlacedCorrectly(false);
        currentSlot = null;
        previousSlotBeforeDrag = null;
        currentSpawnPoint = null;
        previousSpawnPointBeforeDrag = null;
        didDragThisPress = false;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (isLocked)
        {
            return;
        }

        pointerDownScreenPosition = eventData.position;
        didDragThisPress = false;
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isLocked || puzzleBoardController == null)
        {
            return;
        }

        didDragThisPress = true;
        previousSlotBeforeDrag = currentSlot;
        previousSpawnPointBeforeDrag = currentSpawnPoint;
        originalSiblingIndex = rectTransform.GetSiblingIndex();
        rectTransform.SetAsLastSibling();
        UpdatePointerOffset(eventData);

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = false;
        }

        puzzleBoardController.HandlePieceBeginDrag(this);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (isLocked || rectTransform == null)
        {
            return;
        }

        RectTransform parentRectTransform = dragParentRectTransform != null
            ? dragParentRectTransform
            : rectTransform.parent as RectTransform;

        if (parentRectTransform == null)
        {
            return;
        }

        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
            parentRectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out Vector3 worldPoint))
        {
            rectTransform.position = worldPoint + pointerToPieceOffset;
        }
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (isLocked)
        {
            return;
        }

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = true;
        }

        rectTransform.SetSiblingIndex(originalSiblingIndex);

        if (puzzleBoardController == null)
        {
            ReturnToStoredPosition();
            return;
        }

        puzzleBoardController.HandlePieceEndDrag(this);
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (isLocked || puzzleBoardController == null)
        {
            return;
        }

        if (didDragThisPress)
        {
            return;
        }

        if (Vector2.Distance(pointerDownScreenPosition, eventData.position) > tapMovementThreshold)
        {
            return;
        }

        puzzleBoardController.HandlePieceTapped(this);
    }

    public void CaptureCurrentPositionAsReturnPosition()
    {
        if (rectTransform != null)
        {
            returnWorldPosition = rectTransform.position;
        }
    }

    public void ReturnToStoredPosition()
    {
        if (rectTransform != null)
        {
            rectTransform.position = returnWorldPosition;
        }
    }

    public void AssignToSlot(sh_PuzzleSlot slot)
    {
        currentSlot = slot;
        currentSpawnPoint = null;

        if (rectTransform == null || slot?.RectTransform == null)
        {
            return;
        }

        rectTransform.position = slot.RectTransform.position;
        CaptureCurrentPositionAsReturnPosition();
    }

    public void AssignToSpawnPoint(RectTransform spawnPoint)
    {
        currentSpawnPoint = spawnPoint;
        currentSlot = null;

        if (rectTransform == null || spawnPoint == null)
        {
            return;
        }

        rectTransform.position = spawnPoint.position;
        CaptureCurrentPositionAsReturnPosition();
    }

    public void ClearCurrentSlot()
    {
        currentSlot = null;
    }

    public void ClearCurrentSpawnPoint()
    {
        currentSpawnPoint = null;
    }

    public void ClearPreviousSlotReference()
    {
        previousSlotBeforeDrag = null;
    }

    public void ClearPreviousSpawnPointReference()
    {
        previousSpawnPointBeforeDrag = null;
    }

    public void SetLocked(bool shouldLock)
    {
        isLocked = shouldLock;

        if (canvasGroup != null)
        {
            canvasGroup.blocksRaycasts = !shouldLock;
        }
    }

    public void SetPlacedCorrectly(bool isCorrect)
    {
        isPlacedCorrectly = isCorrect;

        if (puzzlePieceUI != null)
        {
            puzzlePieceUI.SetCorrectState(isCorrect);
        }
    }

    private void ResolveReferences()
    {
        if (puzzlePieceUI == null)
        {
            puzzlePieceUI = GetComponent<sh_PuzzlePieceUI>();
        }

        if (rectTransform == null)
        {
            rectTransform = transform as RectTransform;
        }

        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (canvasGroup == null)
        {
            canvasGroup = gameObject.AddComponent<CanvasGroup>();
        }
    }

    private void UpdatePointerOffset(PointerEventData eventData)
    {
        if (rectTransform == null)
        {
            pointerToPieceOffset = Vector3.zero;
            return;
        }

        RectTransform parentRectTransform = dragParentRectTransform != null
            ? dragParentRectTransform
            : rectTransform.parent as RectTransform;

        if (parentRectTransform == null)
        {
            pointerToPieceOffset = Vector3.zero;
            return;
        }

        if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
            parentRectTransform,
            eventData.position,
            eventData.pressEventCamera,
            out Vector3 worldPoint))
        {
            pointerToPieceOffset = rectTransform.position - worldPoint;
            return;
        }

        pointerToPieceOffset = Vector3.zero;
    }
}
