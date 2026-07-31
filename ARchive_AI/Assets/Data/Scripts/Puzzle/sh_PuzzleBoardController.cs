using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Events;

public sealed class sh_PuzzleBoardController : MonoBehaviour
{
    [Header("Scene References")]
    [SerializeField] private sh_RotationRandomizer rotationRandomizer;
    [SerializeField] private sh_PuzzlePieceUI puzzlePiecePrefab;
    [SerializeField] private RectTransform pieceSpawnRoot;
    [SerializeField] private RectTransform[] pieceSpawnPoints = new RectTransform[9];
    [SerializeField] private sh_PuzzleSlot[] puzzleSlots = new sh_PuzzleSlot[9];
    [SerializeField] private Canvas puzzleCanvas;

    [Header("Board Settings")]
    [SerializeField] private bool generateOnStart = true;
    [SerializeField] private bool assignSlotNumbersFromArrayOrder = true;
    [SerializeField] private bool clearExistingPiecesOnGenerate = true;
    [SerializeField] private int expectedPieceCount = 9;
    [SerializeField] private float spritePixelsPerUnit = 100f;
    [SerializeField] private float slotSnapDistance = 120f;
    [SerializeField] private bool lockPieceWhenPlacedCorrectly = true;

    [Header("Events")]
    [SerializeField] private UnityEvent onPuzzleCompleted;

    [Header("Debug Data")]
    [SerializeField] private string rotatingPieceDirectoryPath = string.Empty;
    [SerializeField] private List<sh_PuzzlePieceData> generatedPieceData = new();
    [SerializeField] private List<sh_PuzzlePieceUI> spawnedPieces = new();
    [SerializeField] private bool hasInvokedPuzzleCompleted;

    private readonly List<Sprite> runtimeSprites = new();
    private readonly List<Texture2D> runtimeTextures = new();
    private sh_PuzzlePieceUI[] spawnPointOccupants;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Start()
    {
        if (!generateOnStart)
        {
            return;
        }

        GeneratePuzzleBoard();
    }

    private void Reset()
    {
        ResolveReferences();
    }

    [ContextMenu("Generate Puzzle Board")]
    public void GeneratePuzzleBoard()
    {
        ResolveReferences();

        if (!ValidateSetup())
        {
            return;
        }

        if (assignSlotNumbersFromArrayOrder)
        {
            ApplySlotNumbersFromArrayOrder();
        }

        if (!clearExistingPiecesOnGenerate && spawnedPieces.Count > 0)
        {
            Debug.LogWarning($"{nameof(sh_PuzzleBoardController)}: 기존 조각이 남아 있어서 새 조각 생성을 건너뜁니다.", this);
            return;
        }

        string[] pieceImagePaths = LoadRotatingPiecePaths();
        if (pieceImagePaths.Length != expectedPieceCount)
        {
            Debug.LogError(
                $"{nameof(sh_PuzzleBoardController)}: 퍼즐 조각 파일 개수가 {expectedPieceCount}개가 아닙니다. 현재 개수: {pieceImagePaths.Length}\n" +
                $"경로: {rotatingPieceDirectoryPath}",
                this);
            return;
        }

        sh_RotationRandomizer.RotationResult rotationResult = rotationRandomizer.CreatePieceDataList(pieceImagePaths);
        if (!rotationResult.IsSuccess)
        {
            Debug.LogError($"{nameof(sh_PuzzleBoardController)}: {rotationResult.ErrorMessage}", this);
            return;
        }

        ClearSpawnedPieces();
        ClearAllSlotOccupancy();
        EnsureSpawnPointOccupancyArray();
        ClearAllSpawnPointOccupancy();
        hasInvokedPuzzleCompleted = false;

        generatedPieceData = new List<sh_PuzzlePieceData>(rotationResult.PieceDataList);
        int[] shuffledSpawnPointIndexes = CreateShuffledIndexes(pieceSpawnPoints.Length);
        bool didFailToGenerateAllPieces = false;

        for (int index = 0; index < generatedPieceData.Count; index++)
        {
            sh_PuzzlePieceData pieceData = generatedPieceData[index];
            Sprite pieceSprite = CreateSpriteFromFile(pieceData.ImagePath);

            if (pieceSprite == null)
            {
                Debug.LogError($"{nameof(sh_PuzzleBoardController)}: 조각 이미지를 스프라이트로 변환하지 못했습니다.\n{pieceData.ImagePath}", this);
                didFailToGenerateAllPieces = true;
                continue;
            }

            sh_PuzzlePieceUI pieceInstance = Instantiate(puzzlePiecePrefab, pieceSpawnRoot);
            pieceInstance.Configure(pieceData, pieceSprite);
            int spawnPointIndex = shuffledSpawnPointIndexes[index];
            RectTransform spawnPoint = pieceSpawnPoints[spawnPointIndex];
            ApplySpawnPointLayout(pieceInstance.RectTransform, spawnPoint);
            sh_PuzzleDragHandler dragHandler = ConfigureDragHandler(pieceInstance);
            dragHandler.AssignToSpawnPoint(spawnPoint);
            SetSpawnPointOccupant(spawnPoint, pieceInstance);
            spawnedPieces.Add(pieceInstance);
        }

        if (didFailToGenerateAllPieces || spawnedPieces.Count != expectedPieceCount)
        {
            Debug.LogError(
                $"{nameof(sh_PuzzleBoardController)}: 퍼즐 UI를 완전하게 생성하지 못했습니다.\n" +
                $"예상 조각 수: {expectedPieceCount}\n" +
                $"실제 생성 수: {spawnedPieces.Count}",
                this);
            ClearSpawnedPieces();
            ClearAllSlotOccupancy();
            return;
        }

        Debug.Log(
            $"{nameof(sh_PuzzleBoardController)}: 퍼즐 UI 생성 완료.\n" +
            $"생성된 조각 수: {spawnedPieces.Count}\n" +
            $"슬롯 수: {puzzleSlots.Length}\n" +
            $"조각 폴더: {rotatingPieceDirectoryPath}",
            this);
    }

    public void HandlePieceBeginDrag(sh_PuzzleDragHandler dragHandler)
    {
        if (dragHandler == null)
        {
            return;
        }

        sh_PuzzleSlot currentSlot = dragHandler.CurrentSlot;
        RectTransform currentSpawnPoint = dragHandler.CurrentSpawnPoint;

        if (currentSlot == null && currentSpawnPoint == null)
        {
            return;
        }

        if (currentSlot != null)
        {
            currentSlot.ClearPiece(dragHandler.PuzzlePieceUI);
            dragHandler.ClearCurrentSlot();
        }

        if (currentSpawnPoint != null)
        {
            ClearSpawnPointOccupant(currentSpawnPoint, dragHandler.PuzzlePieceUI);
            dragHandler.ClearCurrentSpawnPoint();
        }

        dragHandler.SetPlacedCorrectly(false);
    }

    public void HandlePieceEndDrag(sh_PuzzleDragHandler dragHandler)
    {
        if (dragHandler == null)
        {
            return;
        }

        if (TryFindNearestSlot(dragHandler.RectTransform.position, out sh_PuzzleSlot nearestSlot))
        {
            PlacePieceOnSlot(dragHandler, nearestSlot);
            return;
        }

        if (TryFindNearestAvailableSpawnPoint(dragHandler.RectTransform.position, out RectTransform nearestSpawnPoint))
        {
            PlacePieceOnSpawnPoint(dragHandler, nearestSpawnPoint);
            return;
        }

        RestorePieceToPreviousPlacement(dragHandler);
    }

    public void HandlePieceTapped(sh_PuzzleDragHandler dragHandler)
    {
        if (dragHandler == null || dragHandler.PuzzlePieceUI == null)
        {
            return;
        }

        dragHandler.PuzzlePieceUI.RotateClockwise();

        sh_PuzzleSlot currentSlot = dragHandler.CurrentSlot;
        if (currentSlot == null)
        {
            dragHandler.SetPlacedCorrectly(false);
            return;
        }

        bool isCorrectPlacement = IsCorrectPlacement(dragHandler.PuzzlePieceUI, currentSlot);
        dragHandler.SetPlacedCorrectly(isCorrectPlacement);

        if (isCorrectPlacement && lockPieceWhenPlacedCorrectly)
        {
            dragHandler.SetLocked(true);
        }

        EvaluatePuzzleCompletion();
    }

    public void UseHint()
    {
        if (hasInvokedPuzzleCompleted)
        {
            return;
        }

        List<sh_PuzzleDragHandler> hintCandidates = new List<sh_PuzzleDragHandler>();

        for (int index = 0; index < spawnedPieces.Count; index++)
        {
            sh_PuzzlePieceUI pieceUI = spawnedPieces[index];
            sh_PuzzleDragHandler dragHandler = GetDragHandler(pieceUI);
            if (dragHandler == null || dragHandler.IsLocked)
            {
                continue;
            }

            sh_PuzzleSlot correctSlot = FindSlotByNumber(pieceUI.AnswerSlotNumber);
            if (correctSlot == null)
            {
                continue;
            }

            if (IsCorrectPlacement(pieceUI, correctSlot) && dragHandler.CurrentSlot == correctSlot)
            {
                continue;
            }

            hintCandidates.Add(dragHandler);
        }

        if (hintCandidates.Count == 0)
        {
            return;
        }

        int randomIndex = UnityEngine.Random.Range(0, hintCandidates.Count);
        sh_PuzzleDragHandler selectedDragHandler = hintCandidates[randomIndex];
        ApplyHintToPiece(selectedDragHandler);
    }

    private void ResolveReferences()
    {
        if (rotationRandomizer == null)
        {
            rotationRandomizer = GetComponent<sh_RotationRandomizer>();
        }

        if (pieceSpawnRoot == null)
        {
            pieceSpawnRoot = transform as RectTransform;
        }

        if (puzzleCanvas == null)
        {
            puzzleCanvas = GetComponentInParent<Canvas>();
        }

        if (string.IsNullOrWhiteSpace(rotatingPieceDirectoryPath))
        {
            rotatingPieceDirectoryPath = Path.Combine(Application.persistentDataPath, "Data", "Image", "MarkerImage", "RotatingImage");
        }
    }

    private bool ValidateSetup()
    {
        if (rotationRandomizer == null)
        {
            Debug.LogError($"{nameof(sh_PuzzleBoardController)}: {nameof(rotationRandomizer)} 참조가 필요합니다.", this);
            return false;
        }

        if (puzzlePiecePrefab == null)
        {
            Debug.LogError($"{nameof(sh_PuzzleBoardController)}: 퍼즐 조각 프리팹이 연결되지 않았습니다.", this);
            return false;
        }

        if (pieceSpawnRoot == null)
        {
            Debug.LogError($"{nameof(sh_PuzzleBoardController)}: 조각을 생성할 부모 RectTransform이 필요합니다.", this);
            return false;
        }

        if (puzzleCanvas == null)
        {
            Debug.LogError($"{nameof(sh_PuzzleBoardController)}: 드래그용 Canvas 참조가 필요합니다.", this);
            return false;
        }

        if (pieceSpawnPoints == null || pieceSpawnPoints.Length != expectedPieceCount)
        {
            Debug.LogError($"{nameof(sh_PuzzleBoardController)}: 조각 위치 참조는 {expectedPieceCount}개여야 합니다.", this);
            return false;
        }

        for (int index = 0; index < pieceSpawnPoints.Length; index++)
        {
            if (pieceSpawnPoints[index] == null)
            {
                Debug.LogError($"{nameof(sh_PuzzleBoardController)}: {index + 1}번 조각 위치 참조가 비어 있습니다.", this);
                return false;
            }
        }

        if (puzzleSlots == null || puzzleSlots.Length != expectedPieceCount)
        {
            Debug.LogError($"{nameof(sh_PuzzleBoardController)}: 슬롯 참조는 {expectedPieceCount}개여야 합니다.", this);
            return false;
        }

        for (int index = 0; index < puzzleSlots.Length; index++)
        {
            if (puzzleSlots[index] == null)
            {
                Debug.LogError($"{nameof(sh_PuzzleBoardController)}: {index + 1}번 슬롯 참조가 비어 있습니다.", this);
                return false;
            }
        }

        EnsureSpawnPointOccupancyArray();
        return true;
    }

    private void ApplySlotNumbersFromArrayOrder()
    {
        for (int index = 0; index < puzzleSlots.Length; index++)
        {
            puzzleSlots[index].SetSlotNumber(index + 1);
            puzzleSlots[index].gameObject.name = $"PuzzleSlot_{index + 1:00}";
        }
    }

    private string[] LoadRotatingPiecePaths()
    {
        if (!Directory.Exists(rotatingPieceDirectoryPath))
        {
            return Array.Empty<string>();
        }

        string[] filePaths = Directory.GetFiles(rotatingPieceDirectoryPath, "*.png", SearchOption.TopDirectoryOnly);
        Array.Sort(filePaths, StringComparer.OrdinalIgnoreCase);
        return filePaths;
    }

    private Sprite CreateSpriteFromFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return null;
        }

        byte[] imageBytes = File.ReadAllBytes(filePath);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);

        if (!ImageConversion.LoadImage(texture, imageBytes, false))
        {
            DestroyRuntimeTexture(texture);
            return null;
        }

        texture.name = Path.GetFileNameWithoutExtension(filePath);
        runtimeTextures.Add(texture);

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f),
            spritePixelsPerUnit);

        sprite.name = texture.name;
        runtimeSprites.Add(sprite);
        return sprite;
    }

    private static int[] CreateShuffledIndexes(int count)
    {
        int[] indexes = new int[count];

        for (int index = 0; index < count; index++)
        {
            indexes[index] = index;
        }

        for (int index = count - 1; index > 0; index--)
        {
            int randomIndex = UnityEngine.Random.Range(0, index + 1);
            (indexes[index], indexes[randomIndex]) = (indexes[randomIndex], indexes[index]);
        }

        return indexes;
    }

    private void ApplySpawnPointLayout(RectTransform pieceRectTransform, RectTransform spawnPoint)
    {
        if (pieceRectTransform == null || spawnPoint == null)
        {
            return;
        }

        Vector2 originalAnchorMin = pieceRectTransform.anchorMin;
        Vector2 originalAnchorMax = pieceRectTransform.anchorMax;
        Vector2 originalPivot = pieceRectTransform.pivot;
        Vector2 originalSizeDelta = pieceRectTransform.sizeDelta;
        RectTransform parentRectTransform = spawnPoint.parent as RectTransform;

        if (parentRectTransform != null)
        {
            pieceRectTransform.SetParent(parentRectTransform, false);
        }

        pieceRectTransform.anchorMin = originalAnchorMin;
        pieceRectTransform.anchorMax = originalAnchorMax;
        pieceRectTransform.pivot = originalPivot;
        pieceRectTransform.sizeDelta = originalSizeDelta;
        pieceRectTransform.position = spawnPoint.position;
        pieceRectTransform.localScale = Vector3.one;
    }

    private sh_PuzzleDragHandler ConfigureDragHandler(sh_PuzzlePieceUI pieceInstance)
    {
        if (pieceInstance == null)
        {
            return null;
        }

        sh_PuzzleDragHandler dragHandler = pieceInstance.GetComponent<sh_PuzzleDragHandler>();
        if (dragHandler == null)
        {
            dragHandler = pieceInstance.gameObject.AddComponent<sh_PuzzleDragHandler>();
        }

        dragHandler.Initialize(this);
        return dragHandler;
    }

    private bool TryFindNearestSlot(Vector3 pieceWorldPosition, out sh_PuzzleSlot nearestSlot)
    {
        nearestSlot = null;
        float nearestDistance = float.MaxValue;

        for (int index = 0; index < puzzleSlots.Length; index++)
        {
            sh_PuzzleSlot slot = puzzleSlots[index];
            if (slot == null || slot.RectTransform == null)
            {
                continue;
            }

            sh_PuzzleDragHandler occupantDragHandler = GetDragHandler(slot.CurrentPiece);
            if (occupantDragHandler != null && occupantDragHandler.IsLocked)
            {
                continue;
            }

            float distance = Vector2.Distance(pieceWorldPosition, slot.RectTransform.position);
            if (distance > slotSnapDistance || distance >= nearestDistance)
            {
                continue;
            }

            nearestDistance = distance;
            nearestSlot = slot;
        }

        return nearestSlot != null;
    }

    private bool TryFindNearestAvailableSpawnPoint(Vector3 pieceWorldPosition, out RectTransform nearestSpawnPoint)
    {
        nearestSpawnPoint = null;
        float nearestDistance = float.MaxValue;

        EnsureSpawnPointOccupancyArray();

        for (int index = 0; index < pieceSpawnPoints.Length; index++)
        {
            RectTransform spawnPoint = pieceSpawnPoints[index];
            if (spawnPoint == null || spawnPointOccupants[index] != null)
            {
                continue;
            }

            float distance = Vector2.Distance(pieceWorldPosition, spawnPoint.position);
            if (distance > slotSnapDistance || distance >= nearestDistance)
            {
                continue;
            }

            nearestDistance = distance;
            nearestSpawnPoint = spawnPoint;
        }

        return nearestSpawnPoint != null;
    }

    private void RestorePieceToPreviousPlacement(sh_PuzzleDragHandler dragHandler)
    {
        if (dragHandler == null)
        {
            return;
        }

        sh_PuzzleSlot previousSlot = dragHandler.PreviousSlotBeforeDrag;
        if (previousSlot != null && previousSlot.CanAssign(dragHandler.PuzzlePieceUI))
        {
            previousSlot.AssignPiece(dragHandler.PuzzlePieceUI);
            dragHandler.AssignToSlot(previousSlot);

            bool isCorrectPlacement = IsCorrectPlacement(dragHandler.PuzzlePieceUI, previousSlot);
            dragHandler.SetPlacedCorrectly(isCorrectPlacement);
            dragHandler.ClearPreviousSlotReference();
            dragHandler.ClearPreviousSpawnPointReference();
            return;
        }

        RectTransform previousSpawnPoint = dragHandler.PreviousSpawnPointBeforeDrag;
        if (previousSpawnPoint != null && GetSpawnPointOccupant(previousSpawnPoint) == null)
        {
            SetSpawnPointOccupant(previousSpawnPoint, dragHandler.PuzzlePieceUI);
            dragHandler.AssignToSpawnPoint(previousSpawnPoint);
            dragHandler.SetPlacedCorrectly(false);
            dragHandler.ClearPreviousSlotReference();
            dragHandler.ClearPreviousSpawnPointReference();
            return;
        }

        dragHandler.ReturnToStoredPosition();
        dragHandler.ClearPreviousSlotReference();
        dragHandler.ClearPreviousSpawnPointReference();
        dragHandler.SetPlacedCorrectly(false);
    }

    private void EvaluatePuzzleCompletion()
    {
        if (hasInvokedPuzzleCompleted)
        {
            return;
        }

        int correctCount = 0;

        for (int index = 0; index < puzzleSlots.Length; index++)
        {
            sh_PuzzleSlot slot = puzzleSlots[index];
            if (slot?.CurrentPiece == null)
            {
                return;
            }

            if (!IsCorrectPlacement(slot.CurrentPiece, slot))
            {
                return;
            }

            correctCount++;
        }

        if (correctCount != expectedPieceCount)
        {
            return;
        }

        hasInvokedPuzzleCompleted = true;
        Debug.Log($"{nameof(sh_PuzzleBoardController)}: 퍼즐 완료 판정 성공.", this);
        onPuzzleCompleted?.Invoke();
    }

    private void ClearAllSlotOccupancy()
    {
        for (int index = 0; index < puzzleSlots.Length; index++)
        {
            if (puzzleSlots[index] == null)
            {
                continue;
            }

            puzzleSlots[index].ClearPiece();
        }
    }

    private void ClearAllSpawnPointOccupancy()
    {
        EnsureSpawnPointOccupancyArray();

        for (int index = 0; index < spawnPointOccupants.Length; index++)
        {
            spawnPointOccupants[index] = null;
        }
    }

    private void ClearSpawnedPieces()
    {
        generatedPieceData.Clear();

        for (int index = spawnedPieces.Count - 1; index >= 0; index--)
        {
            if (spawnedPieces[index] == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(spawnedPieces[index].gameObject);
            }
            else
            {
                DestroyImmediate(spawnedPieces[index].gameObject);
            }
        }

        spawnedPieces.Clear();
        ClearRuntimeAssets();
    }

    private void ClearRuntimeAssets()
    {
        for (int index = runtimeSprites.Count - 1; index >= 0; index--)
        {
            if (runtimeSprites[index] == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(runtimeSprites[index]);
            }
            else
            {
                DestroyImmediate(runtimeSprites[index]);
            }
        }

        runtimeSprites.Clear();

        for (int index = runtimeTextures.Count - 1; index >= 0; index--)
        {
            DestroyRuntimeTexture(runtimeTextures[index]);
        }

        runtimeTextures.Clear();
    }

    private void DestroyRuntimeTexture(Texture2D texture)
    {
        if (texture == null)
        {
            return;
        }

        if (Application.isPlaying)
        {
            Destroy(texture);
        }
        else
        {
            DestroyImmediate(texture);
        }
    }

    private void OnDestroy()
    {
        ClearRuntimeAssets();
    }

    private void ApplyHintToPiece(sh_PuzzleDragHandler dragHandler)
    {
        if (dragHandler == null || dragHandler.PuzzlePieceUI == null)
        {
            return;
        }

        sh_PuzzlePieceUI movingPiece = dragHandler.PuzzlePieceUI;
        sh_PuzzleSlot targetSlot = FindSlotByNumber(movingPiece.AnswerSlotNumber);
        if (targetSlot == null)
        {
            return;
        }

        if (dragHandler.CurrentSlot != null)
        {
            dragHandler.CurrentSlot.ClearPiece(movingPiece);
            dragHandler.ClearCurrentSlot();
        }

        if (dragHandler.CurrentSpawnPoint != null)
        {
            ClearSpawnPointOccupant(dragHandler.CurrentSpawnPoint, movingPiece);
            dragHandler.ClearCurrentSpawnPoint();
        }

        sh_PuzzlePieceUI targetPiece = targetSlot.CurrentPiece;
        sh_PuzzleDragHandler targetDragHandler = GetDragHandler(targetPiece);

        if (targetDragHandler != null && targetPiece != null && targetPiece != movingPiece)
        {
            MovePieceToAvailableSpawnPointOrPrevious(targetDragHandler, dragHandler);
        }

        movingPiece.SetRotationValue(0);
        targetSlot.AssignPiece(movingPiece);
        dragHandler.AssignToSlot(targetSlot);
        dragHandler.SetPlacedCorrectly(true);

        if (lockPieceWhenPlacedCorrectly)
        {
            dragHandler.SetLocked(true);
        }

        dragHandler.ClearPreviousSlotReference();
        dragHandler.ClearPreviousSpawnPointReference();
        EvaluatePuzzleCompletion();
    }

    private void MovePieceToAvailableSpawnPointOrPrevious(sh_PuzzleDragHandler targetDragHandler, sh_PuzzleDragHandler sourceDragHandler)
    {
        if (targetDragHandler == null)
        {
            return;
        }

        targetDragHandler.SetLocked(false);

        if (TryFindAnyAvailableSpawnPoint(out RectTransform availableSpawnPoint))
        {
            SetSpawnPointOccupant(availableSpawnPoint, targetDragHandler.PuzzlePieceUI);
            targetDragHandler.AssignToSpawnPoint(availableSpawnPoint);
            targetDragHandler.SetPlacedCorrectly(false);
            return;
        }

        MovePieceToPreviousPlacement(targetDragHandler, sourceDragHandler);
    }

    private void PlacePieceOnSlot(sh_PuzzleDragHandler dragHandler, sh_PuzzleSlot targetSlot)
    {
        if (dragHandler == null || targetSlot == null)
        {
            return;
        }

        sh_PuzzlePieceUI movingPiece = dragHandler.PuzzlePieceUI;
        sh_PuzzlePieceUI targetPiece = targetSlot.CurrentPiece;
        sh_PuzzleDragHandler targetDragHandler = GetDragHandler(targetPiece);

        if (targetDragHandler != null && targetDragHandler.IsLocked)
        {
            RestorePieceToPreviousPlacement(dragHandler);
            return;
        }

        if (targetDragHandler != null && targetPiece != null && targetPiece != movingPiece)
        {
            MovePieceToPreviousPlacement(targetDragHandler, dragHandler);
        }

        targetSlot.AssignPiece(movingPiece);
        dragHandler.AssignToSlot(targetSlot);
        dragHandler.ClearPreviousSlotReference();
        dragHandler.ClearPreviousSpawnPointReference();

        bool isCorrectPlacement = IsCorrectPlacement(movingPiece, targetSlot);
        dragHandler.SetPlacedCorrectly(isCorrectPlacement);

        if (isCorrectPlacement && lockPieceWhenPlacedCorrectly)
        {
            dragHandler.SetLocked(true);
        }

        EvaluatePuzzleCompletion();
    }

    private void PlacePieceOnSpawnPoint(sh_PuzzleDragHandler dragHandler, RectTransform spawnPoint)
    {
        if (dragHandler == null || spawnPoint == null)
        {
            return;
        }

        SetSpawnPointOccupant(spawnPoint, dragHandler.PuzzlePieceUI);
        dragHandler.AssignToSpawnPoint(spawnPoint);
        dragHandler.SetPlacedCorrectly(false);
        dragHandler.ClearPreviousSlotReference();
        dragHandler.ClearPreviousSpawnPointReference();
    }

    private void MovePieceToPreviousPlacement(sh_PuzzleDragHandler targetDragHandler, sh_PuzzleDragHandler sourceDragHandler)
    {
        if (targetDragHandler == null)
        {
            return;
        }

        sh_PuzzleSlot sourcePreviousSlot = sourceDragHandler.PreviousSlotBeforeDrag;
        RectTransform sourcePreviousSpawnPoint = sourceDragHandler.PreviousSpawnPointBeforeDrag;

        if (sourcePreviousSlot != null)
        {
            sourcePreviousSlot.AssignPiece(targetDragHandler.PuzzlePieceUI);
            targetDragHandler.AssignToSlot(sourcePreviousSlot);
            bool isCorrectPlacement = IsCorrectPlacement(targetDragHandler.PuzzlePieceUI, sourcePreviousSlot);
            targetDragHandler.SetPlacedCorrectly(isCorrectPlacement);
            return;
        }

        if (sourcePreviousSpawnPoint != null)
        {
            SetSpawnPointOccupant(sourcePreviousSpawnPoint, targetDragHandler.PuzzlePieceUI);
            targetDragHandler.AssignToSpawnPoint(sourcePreviousSpawnPoint);
            targetDragHandler.SetPlacedCorrectly(false);
            return;
        }

        targetDragHandler.ReturnToStoredPosition();
        targetDragHandler.SetPlacedCorrectly(false);
    }

    private void EnsureSpawnPointOccupancyArray()
    {
        if (pieceSpawnPoints == null)
        {
            spawnPointOccupants = Array.Empty<sh_PuzzlePieceUI>();
            return;
        }

        if (spawnPointOccupants == null || spawnPointOccupants.Length != pieceSpawnPoints.Length)
        {
            spawnPointOccupants = new sh_PuzzlePieceUI[pieceSpawnPoints.Length];
        }
    }

    private void SetSpawnPointOccupant(RectTransform spawnPoint, sh_PuzzlePieceUI pieceUI)
    {
        int index = FindSpawnPointIndex(spawnPoint);
        if (index < 0)
        {
            return;
        }

        EnsureSpawnPointOccupancyArray();
        spawnPointOccupants[index] = pieceUI;
    }

    private void ClearSpawnPointOccupant(RectTransform spawnPoint, sh_PuzzlePieceUI pieceUI)
    {
        int index = FindSpawnPointIndex(spawnPoint);
        if (index < 0)
        {
            return;
        }

        EnsureSpawnPointOccupancyArray();
        if (spawnPointOccupants[index] == pieceUI)
        {
            spawnPointOccupants[index] = null;
        }
    }

    private sh_PuzzlePieceUI GetSpawnPointOccupant(RectTransform spawnPoint)
    {
        int index = FindSpawnPointIndex(spawnPoint);
        if (index < 0)
        {
            return null;
        }

        EnsureSpawnPointOccupancyArray();
        return spawnPointOccupants[index];
    }

    private int FindSpawnPointIndex(RectTransform spawnPoint)
    {
        if (spawnPoint == null || pieceSpawnPoints == null)
        {
            return -1;
        }

        for (int index = 0; index < pieceSpawnPoints.Length; index++)
        {
            if (pieceSpawnPoints[index] == spawnPoint)
            {
                return index;
            }
        }

        return -1;
    }

    private static sh_PuzzleDragHandler GetDragHandler(sh_PuzzlePieceUI pieceUI)
    {
        return pieceUI != null ? pieceUI.GetComponent<sh_PuzzleDragHandler>() : null;
    }

    private sh_PuzzleSlot FindSlotByNumber(int slotNumber)
    {
        for (int index = 0; index < puzzleSlots.Length; index++)
        {
            if (puzzleSlots[index] != null && puzzleSlots[index].SlotNumber == slotNumber)
            {
                return puzzleSlots[index];
            }
        }

        return null;
    }

    private bool TryFindAnyAvailableSpawnPoint(out RectTransform availableSpawnPoint)
    {
        EnsureSpawnPointOccupancyArray();

        for (int index = 0; index < pieceSpawnPoints.Length; index++)
        {
            if (pieceSpawnPoints[index] == null || spawnPointOccupants[index] != null)
            {
                continue;
            }

            availableSpawnPoint = pieceSpawnPoints[index];
            return true;
        }

        availableSpawnPoint = null;
        return false;
    }

    private static bool IsCorrectPlacement(sh_PuzzlePieceUI pieceUI, sh_PuzzleSlot slot)
    {
        return pieceUI != null &&
            slot != null &&
            pieceUI.AnswerSlotNumber == slot.SlotNumber &&
            pieceUI.GetNormalizedRotationValue() == 0;
    }
}
