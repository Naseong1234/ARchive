using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public sealed class sh_ARTrackingResultHandler : MonoBehaviour
{
    [Header("AR References")]
    [SerializeField] private ARTrackedImageManager trackedImageManager;
    [SerializeField] private sh_ARSceneController arSceneController;

    [Header("Tracking Settings")]
    [SerializeField] private string targetReferenceImageName = string.Empty;
    [SerializeField] private bool triggerOnlyOnce = true;
    [SerializeField] private bool requireTrackingStateTracking = true;

    private bool hasTriggeredTrackingSuccess;

    private void Awake()
    {
        ResolveReferences();
    }

    private void Reset()
    {
        ResolveReferences();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (trackedImageManager == null)
        {
            ReportDebugStatus("실패: AR Tracked Image Manager 참조 없음");
            Debug.LogError($"{nameof(sh_ARTrackingResultHandler)}: {nameof(trackedImageManager)} 참조가 필요합니다.", this);
            return;
        }

        trackedImageManager.trackablesChanged.AddListener(HandleTrackablesChanged);
        ReportDebugStatus("AR 트래킹 이벤트 구독 완료");
    }

    private void OnDisable()
    {
        if (trackedImageManager == null)
        {
            return;
        }

        trackedImageManager.trackablesChanged.RemoveListener(HandleTrackablesChanged);
    }

    private void HandleTrackablesChanged(ARTrackablesChangedEventArgs<ARTrackedImage> eventArgs)
    {
        for (int index = 0; index < eventArgs.added.Count; index++)
        {
            TryHandleTrackedImage(eventArgs.added[index]);
        }

        for (int index = 0; index < eventArgs.updated.Count; index++)
        {
            TryHandleTrackedImage(eventArgs.updated[index]);
        }
    }

    private void TryHandleTrackedImage(ARTrackedImage trackedImage)
    {
        if (trackedImage == null)
        {
            return;
        }

        if (triggerOnlyOnce && hasTriggeredTrackingSuccess)
        {
            return;
        }

        if (requireTrackingStateTracking && trackedImage.trackingState != TrackingState.Tracking)
        {
            return;
        }

        string referenceImageName = trackedImage.referenceImage.name;
        ReportDebugStatus($"이미지 감지: {referenceImageName}");

        if (!IsTargetReferenceImage(referenceImageName))
        {
            ReportDebugStatus($"감지됨, 그러나 대상 아님: {referenceImageName}");
            return;
        }

        hasTriggeredTrackingSuccess = true;

        if (arSceneController == null)
        {
            ReportDebugStatus("실패: AR Scene Controller 참조 없음");
            Debug.LogError($"{nameof(sh_ARTrackingResultHandler)}: {nameof(arSceneController)} 참조가 필요합니다.", this);
            return;
        }

        arSceneController.HandleTrackingSuccess(referenceImageName);
    }

    public void SetTargetReferenceImageName(string referenceImageName)
    {
        targetReferenceImageName = string.IsNullOrWhiteSpace(referenceImageName) ?
            string.Empty :
            referenceImageName.Trim();

        ReportDebugStatus($"트래킹 대상 설정: {targetReferenceImageName}");
    }

    private bool IsTargetReferenceImage(string referenceImageName)
    {
        if (string.IsNullOrWhiteSpace(targetReferenceImageName))
        {
            return true;
        }

        return string.Equals(
            targetReferenceImageName.Trim(),
            referenceImageName,
            System.StringComparison.OrdinalIgnoreCase);
    }

    private void ResolveReferences()
    {
        if (trackedImageManager == null)
        {
            trackedImageManager = FindAnyObjectByType<ARTrackedImageManager>();
        }

        if (arSceneController == null)
        {
            arSceneController = GetComponent<sh_ARSceneController>();
        }
    }

    public void ReportDebugStatus(string message)
    {
        if (arSceneController != null)
        {
            arSceneController.SetDebugStatus(message);
        }
    }
}
