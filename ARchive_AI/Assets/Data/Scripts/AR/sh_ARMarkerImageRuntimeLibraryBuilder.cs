using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public sealed class sh_ARMarkerImageRuntimeLibraryBuilder : MonoBehaviour
{
    [Header("AR References")]
    [SerializeField] private ARTrackedImageManager trackedImageManager;
    [SerializeField] private XRReferenceImageLibrary serializedReferenceImageLibrary;
    [SerializeField] private sh_ARTrackingResultHandler trackingResultHandler;

    [Header("Marker Settings")]
    [SerializeField] [Min(0.01f)] private float markerPhysicalWidthInMeters = 0.1f;
    [SerializeField] private bool assignRuntimeLibraryOnSuccess = true;

    [Header("Marker Piece Preview")]
    [SerializeField] private sh_PuzzlePieceUI markerPiecePrefab;
    [SerializeField] private RectTransform markerPieceSpawnRoot;
    [SerializeField] private RectTransform markerPieceSpawnPoint;
    [SerializeField] private float spritePixelsPerUnit = 100f;

    private sh_PuzzlePieceUI markerPiecePreviewInstance;
    private Sprite markerPiecePreviewSprite;
    private Texture2D markerPiecePreviewTexture;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void Start()
    {
        StartCoroutine(BuildRuntimeLibraryRoutine());
    }

    private IEnumerator BuildRuntimeLibraryRoutine()
    {
        ResolveReferences();

        if (trackedImageManager == null)
        {
            Debug.LogError($"{nameof(sh_ARMarkerImageRuntimeLibraryBuilder)}: {nameof(trackedImageManager)} 참조가 필요합니다.", this);
            yield break;
        }

        bool trackedImageManagerPreviousEnabledState = trackedImageManager.enabled;
        trackedImageManager.enabled = false;

        yield return EnsureArSessionReadyRoutine();

        if (ARSession.state < ARSessionState.Ready)
        {
            Debug.LogError(
                $"{nameof(sh_ARMarkerImageRuntimeLibraryBuilder)}: AR Session 준비가 완료되지 않아 마커 이미지 등록을 중단합니다. 현재 상태: {ARSession.state}",
                this);
            trackedImageManager.enabled = trackedImageManagerPreviousEnabledState;
            yield break;
        }

        while (trackedImageManager.subsystem == null)
        {
            yield return null;
        }

        if (!sh_ImageSliceService.TryLoadSelectedMarkerPieceData(out sh_ImageSliceService.SelectedMarkerPieceData markerPieceData))
        {
            Debug.LogError(
                $"{nameof(sh_ARMarkerImageRuntimeLibraryBuilder)}: 저장된 마커 조각 정보를 찾지 못했습니다. 로그인 씬에서 이미지를 다시 선택해주세요.",
                this);
            trackedImageManager.enabled = trackedImageManagerPreviousEnabledState;
            yield break;
        }

        string markerImagePath = markerPieceData.PiecePath;
        string markerImageName = markerPieceData.TrackingImageName;

        Texture2D markerTexture = LoadTextureFromFile(markerImagePath);
        if (markerTexture == null)
        {
            Debug.LogError(
                $"{nameof(sh_ARMarkerImageRuntimeLibraryBuilder)}: 마커 조각 이미지를 텍스처로 불러오지 못했습니다.\n{markerImagePath}",
                this);
            trackedImageManager.enabled = trackedImageManagerPreviousEnabledState;
            yield break;
        }

        RuntimeReferenceImageLibrary runtimeLibrary = serializedReferenceImageLibrary != null
            ? trackedImageManager.CreateRuntimeLibrary(serializedReferenceImageLibrary)
            : trackedImageManager.CreateRuntimeLibrary();

        if (runtimeLibrary is not MutableRuntimeReferenceImageLibrary mutableLibrary)
        {
            Destroy(markerTexture);
            Debug.LogError(
                $"{nameof(sh_ARMarkerImageRuntimeLibraryBuilder)}: 현재 기기 또는 시뮬레이션 환경이 Mutable Runtime Reference Image Library를 지원하지 않습니다.",
                this);
            trackedImageManager.enabled = trackedImageManagerPreviousEnabledState;
            yield break;
        }

        AddReferenceImageJobState addJobState;

        try
        {
            addJobState = mutableLibrary.ScheduleAddImageWithValidationJob(
                markerTexture,
                markerImageName,
                markerPhysicalWidthInMeters);
        }
        catch (Exception exception)
        {
            Destroy(markerTexture);
            Debug.LogError(
                $"{nameof(sh_ARMarkerImageRuntimeLibraryBuilder)}: 런타임 마커 이미지 등록 중 예외가 발생했습니다.\n{exception.Message}",
                this);
            trackedImageManager.enabled = trackedImageManagerPreviousEnabledState;
            yield break;
        }

        Destroy(markerTexture);

        while (!addJobState.status.IsComplete())
        {
            yield return null;
        }

        if (!addJobState.status.IsSuccess())
        {
            Debug.LogError(
                $"{nameof(sh_ARMarkerImageRuntimeLibraryBuilder)}: 마커 이미지 등록에 실패했습니다. 상태: {addJobState.status}",
                this);
            trackedImageManager.enabled = trackedImageManagerPreviousEnabledState;
            yield break;
        }

        if (assignRuntimeLibraryOnSuccess)
        {
            trackedImageManager.referenceLibrary = mutableLibrary;
        }

        trackedImageManager.enabled = true;

        if (trackingResultHandler != null)
        {
            trackingResultHandler.SetTargetReferenceImageName(markerImageName);
        }

        CreateMarkerPiecePreview(markerImagePath);

        Debug.Log(
            $"{nameof(sh_ARMarkerImageRuntimeLibraryBuilder)}: 런타임 마커 이미지 등록 완료.\n" +
            $"마커 이름: {markerImageName}\n" +
            $"마커 경로: {markerImagePath}\n" +
            $"마커 실제 가로 크기: {markerPhysicalWidthInMeters:0.###}m\n" +
            $"상태: {addJobState.status}",
            this);
    }

    private static IEnumerator EnsureArSessionReadyRoutine()
    {
        if (ARSession.state == ARSessionState.None || ARSession.state == ARSessionState.CheckingAvailability)
        {
            yield return ARSession.CheckAvailability();
        }

        if (ARSession.state == ARSessionState.NeedsInstall)
        {
            yield return ARSession.Install();
        }

        while (ARSession.state != ARSessionState.SessionInitializing &&
               ARSession.state != ARSessionState.SessionTracking &&
               ARSession.state != ARSessionState.Ready &&
               ARSession.state != ARSessionState.Unsupported)
        {
            yield return null;
        }
    }

    private void ResolveReferences()
    {
        if (trackedImageManager == null)
        {
            trackedImageManager = FindAnyObjectByType<ARTrackedImageManager>();
        }

        if (trackingResultHandler == null)
        {
            trackingResultHandler = GetComponent<sh_ARTrackingResultHandler>();
        }
    }

    private void CreateMarkerPiecePreview(string markerImagePath)
    {
        if (markerPiecePrefab == null || markerPieceSpawnRoot == null || markerPieceSpawnPoint == null)
        {
            return;
        }

        ClearMarkerPiecePreview();

        markerPiecePreviewSprite = CreateSpriteFromFile(markerImagePath, out markerPiecePreviewTexture);
        if (markerPiecePreviewSprite == null)
        {
            Debug.LogWarning($"{nameof(sh_ARMarkerImageRuntimeLibraryBuilder)}: 마커 조각 미리보기 스프라이트를 만들지 못했습니다.", this);
            return;
        }

        markerPiecePreviewInstance = Instantiate(markerPiecePrefab, markerPieceSpawnRoot);
        markerPiecePreviewInstance.Configure(
            new sh_PuzzlePieceData(1, 0, markerImagePath),
            markerPiecePreviewSprite);

        ApplySpawnPointLayout(markerPiecePreviewInstance.RectTransform, markerPieceSpawnPoint);
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

    private Sprite CreateSpriteFromFile(string filePath, out Texture2D loadedTexture)
    {
        loadedTexture = null;

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return null;
        }

        loadedTexture = LoadTextureFromFile(filePath);
        if (loadedTexture == null)
        {
            return null;
        }

        Sprite sprite = Sprite.Create(
            loadedTexture,
            new Rect(0f, 0f, loadedTexture.width, loadedTexture.height),
            new Vector2(0.5f, 0.5f),
            spritePixelsPerUnit);
        sprite.name = loadedTexture.name;
        return sprite;
    }

    private static Texture2D LoadTextureFromFile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            return null;
        }

        byte[] imageBytes = File.ReadAllBytes(filePath);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);

        if (!ImageConversion.LoadImage(texture, imageBytes, false))
        {
            Destroy(texture);
            return null;
        }

        texture.name = Path.GetFileNameWithoutExtension(filePath);
        return texture;
    }

    private void ClearMarkerPiecePreview()
    {
        if (markerPiecePreviewInstance != null)
        {
            Destroy(markerPiecePreviewInstance.gameObject);
            markerPiecePreviewInstance = null;
        }

        if (markerPiecePreviewSprite != null)
        {
            Destroy(markerPiecePreviewSprite);
            markerPiecePreviewSprite = null;
        }

        if (markerPiecePreviewTexture != null)
        {
            Destroy(markerPiecePreviewTexture);
            markerPiecePreviewTexture = null;
        }
    }
}
