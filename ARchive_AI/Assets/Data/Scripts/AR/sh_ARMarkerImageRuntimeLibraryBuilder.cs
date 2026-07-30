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

    [Header("Debug Data")]
    [SerializeField] private string lastMarkerImagePath = string.Empty;
    [SerializeField] private string lastMarkerImageName = string.Empty;
    [SerializeField] private AddReferenceImageJobStatus lastAddJobStatus = AddReferenceImageJobStatus.None;

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
        ReportDebugStatus("런타임 마커 등록 시작");

        if (trackedImageManager == null)
        {
            ReportDebugStatus("실패: AR Tracked Image Manager 참조 없음");
            Debug.LogError($"{nameof(sh_ARMarkerImageRuntimeLibraryBuilder)}: {nameof(trackedImageManager)} 참조가 필요합니다.", this);
            yield break;
        }

        bool trackedImageManagerPreviousEnabledState = trackedImageManager.enabled;
        trackedImageManager.enabled = false;
        ReportDebugStatus("AR Session 준비 상태 확인 중");

        yield return EnsureArSessionReadyRoutine();

        if (ARSession.state < ARSessionState.Ready)
        {
            ReportDebugStatus($"실패: AR Session 준비 안됨 ({ARSession.state})");
            Debug.LogError(
                $"{nameof(sh_ARMarkerImageRuntimeLibraryBuilder)}: AR Session 준비가 완료되지 않아 마커 이미지 등록을 중단합니다. 현재 상태: {ARSession.state}",
                this);
            trackedImageManager.enabled = trackedImageManagerPreviousEnabledState;
            yield break;
        }

        ReportDebugStatus($"AR Session 준비 완료 ({ARSession.state})");

        while (trackedImageManager.subsystem == null)
        {
            yield return null;
        }

        ReportDebugStatus("마커 조각 경로 확인 중");

        if (!sh_ImageSliceService.TryLoadSelectedMarkerPieceData(out sh_ImageSliceService.SelectedMarkerPieceData markerPieceData))
        {
            ReportDebugStatus("실패: 저장된 마커 조각 경로 없음");
            Debug.LogError(
                $"{nameof(sh_ARMarkerImageRuntimeLibraryBuilder)}: 저장된 마커 조각 정보를 찾지 못했습니다. 로그인 씬에서 이미지를 다시 선택해주세요.",
                this);
            trackedImageManager.enabled = trackedImageManagerPreviousEnabledState;
            yield break;
        }

        lastMarkerImagePath = markerPieceData.PiecePath;
        lastMarkerImageName = markerPieceData.TrackingImageName;

        Texture2D markerTexture = LoadTextureFromFile(markerPieceData.PiecePath);
        if (markerTexture == null)
        {
            ReportDebugStatus("실패: 마커 조각 이미지를 텍스처로 로드하지 못함");
            Debug.LogError(
                $"{nameof(sh_ARMarkerImageRuntimeLibraryBuilder)}: 마커 조각 이미지를 텍스처로 불러오지 못했습니다.\n{markerPieceData.PiecePath}",
                this);
            trackedImageManager.enabled = trackedImageManagerPreviousEnabledState;
            yield break;
        }

        ReportDebugStatus("런타임 이미지 라이브러리 생성 중");

        RuntimeReferenceImageLibrary runtimeLibrary = serializedReferenceImageLibrary != null
            ? trackedImageManager.CreateRuntimeLibrary(serializedReferenceImageLibrary)
            : trackedImageManager.CreateRuntimeLibrary();

        if (runtimeLibrary is not MutableRuntimeReferenceImageLibrary mutableLibrary)
        {
            Destroy(markerTexture);
            ReportDebugStatus("실패: Mutable Runtime Reference Image Library 미지원");
            Debug.LogError(
                $"{nameof(sh_ARMarkerImageRuntimeLibraryBuilder)}: 현재 기기 또는 시뮬레이션 환경이 Mutable Runtime Reference Image Library를 지원하지 않습니다.",
                this);
            trackedImageManager.enabled = trackedImageManagerPreviousEnabledState;
            yield break;
        }

        AddReferenceImageJobState addJobState;

        try
        {
            ReportDebugStatus("마커 조각 이미지를 런타임 마커로 등록 중");
            addJobState = mutableLibrary.ScheduleAddImageWithValidationJob(
                markerTexture,
                lastMarkerImageName,
                markerPhysicalWidthInMeters);
        }
        catch (Exception exception)
        {
            Destroy(markerTexture);
            ReportDebugStatus($"실패: 마커 등록 예외 - {exception.Message}");
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

        lastAddJobStatus = addJobState.status;

        if (!addJobState.status.IsSuccess())
        {
            ReportDebugStatus($"실패: 마커 등록 상태 {addJobState.status}");
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
            trackingResultHandler.SetTargetReferenceImageName(lastMarkerImageName);
        }

        ReportDebugStatus($"마커 등록 완료, 트래킹 대기 중: {lastMarkerImageName}");

        Debug.Log(
            $"{nameof(sh_ARMarkerImageRuntimeLibraryBuilder)}: 런타임 마커 이미지 등록 완료.\n" +
            $"마커 이름: {lastMarkerImageName}\n" +
            $"마커 경로: {markerPieceData.PiecePath}\n" +
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

    private void ReportDebugStatus(string message)
    {
        if (trackingResultHandler != null)
        {
            trackingResultHandler.ReportDebugStatus(message);
            return;
        }

        sh_ARSceneController arSceneController = GetComponent<sh_ARSceneController>();
        if (arSceneController != null)
        {
            arSceneController.SetDebugStatus(message);
        }
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
}
