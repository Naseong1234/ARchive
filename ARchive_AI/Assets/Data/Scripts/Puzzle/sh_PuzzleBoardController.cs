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
            ApplySpawnPointLayout(pieceInstance.RectTransform, pieceSpawnPoints[shuffledSpawnPointIndexes[index]]);
            ConfigureDragHandler(pieceInstance);
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

    public IReadOnlyList<sh_PuzzlePieceData> GetGeneratedPieceData()
    {
        return generatedPieceData;
    }

    public IReadOnlyList<sh_PuzzleSlot> GetPuzzleSlots()
    {
        return puzzleSlots;
    }

    public void HandlePieceBeginDrag(sh_PuzzleDragHandler dragHandler)
    {
        if (dragHandler == null)
        {
            return;
        }

        sh_PuzzleSlot currentSlot = dragHandler.CurrentSlot;
        if (currentSlot == null)
        {
            return;
        }

        currentSlot.ClearPiece(dragHandler.PuzzlePieceUI);
        dragHandler.ClearCurrentSlot();
        dragHandler.SetPlacedCorrectly(false);
    }

    public void HandlePieceEndDrag(sh_PuzzleDragHandler dragHandler)
    {
        if (dragHandler == null)
        {
            return;
        }

        if (!TryFindNearestAvailableSlot(dragHandler.RectTransform.position, out sh_PuzzleSlot nearestSlot))
        {
            RestorePieceToPreviousPlacement(dragHandler);
            return;
        }

        nearestSlot.AssignPiece(dragHandler.PuzzlePieceUI);
        dragHandler.AssignToSlot(nearestSlot);
        dragHandler.ClearPreviousSlotReference();

        bool isCorrectSlot = dragHandler.PuzzlePieceUI != null &&
            dragHandler.PuzzlePieceUI.AnswerSlotNumber == nearestSlot.SlotNumber;

        dragHandler.SetPlacedCorrectly(isCorrectSlot);

        if (isCorrectSlot && lockPieceWhenPlacedCorrectly)
        {
            dragHandler.SetLocked(true);
        }

        EvaluatePuzzleCompletion();
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

    private void ConfigureDragHandler(sh_PuzzlePieceUI pieceInstance)
    {
        if (pieceInstance == null)
        {
            return;
        }

        sh_PuzzleDragHandler dragHandler = pieceInstance.GetComponent<sh_PuzzleDragHandler>();
        if (dragHandler == null)
        {
            dragHandler = pieceInstance.gameObject.AddComponent<sh_PuzzleDragHandler>();
        }

        dragHandler.Initialize(this);
    }

    private bool TryFindNearestAvailableSlot(Vector3 pieceWorldPosition, out sh_PuzzleSlot nearestSlot)
    {
        nearestSlot = null;
        float nearestDistance = float.MaxValue;

        for (int index = 0; index < puzzleSlots.Length; index++)
        {
            sh_PuzzleSlot slot = puzzleSlots[index];
            if (slot == null || slot.RectTransform == null || slot.IsOccupied)
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

            bool isCorrectSlot = dragHandler.PuzzlePieceUI != null &&
                dragHandler.PuzzlePieceUI.AnswerSlotNumber == previousSlot.SlotNumber;
            dragHandler.SetPlacedCorrectly(isCorrectSlot);
            dragHandler.ClearPreviousSlotReference();
            return;
        }

        dragHandler.ReturnToStoredPosition();
        dragHandler.ClearPreviousSlotReference();
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

            if (slot.CurrentPiece.AnswerSlotNumber != slot.SlotNumber)
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
}
