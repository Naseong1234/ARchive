using System;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class sh_ImagePickerService : MonoBehaviour
{
    [SerializeField] private string pickerTitle = "사진을 선택해주세요";
    public bool IsPicking { get; private set; }

    private Action<string> successCallback;
    private Action cancelCallback;
    private Action<string> failureCallback;

    public void PickImage(Action<string> onSuccess, Action onCancel, Action<string> onFailure)
    {
        if (IsPicking)
        {
            onFailure?.Invoke("이미지 선택이 이미 진행 중입니다.");
            return;
        }

        successCallback = onSuccess;
        cancelCallback = onCancel;
        failureCallback = onFailure;
        IsPicking = true;

#if UNITY_EDITOR
        OpenEditorPicker();
        return;
#elif UNITY_ANDROID
        OpenAndroidPicker();
        return;
#else
        FinishWithFailure("현재 플랫폼에서는 이미지 선택을 지원하지 않습니다.");
#endif
    }

#if UNITY_EDITOR
    private void OpenEditorPicker()
    {
        string selectedPath = EditorUtility.OpenFilePanelWithFilters(
            pickerTitle,
            string.Empty,
            new[]
            {
                "Image Files", "png,jpg,jpeg",
                "PNG", "png",
                "JPG", "jpg",
                "JPEG", "jpeg"
            });

        if (string.IsNullOrEmpty(selectedPath))
        {
            FinishWithCancel();
            return;
        }

        FinishWithSuccess(selectedPath);
    }
#endif

    private void OpenAndroidPicker()
    {
        if (!NativeGallery.CheckPermission(NativeGallery.PermissionType.Read, NativeGallery.MediaType.Image))
        {
            NativeGallery.RequestPermissionAsync(HandleAndroidPermissionResult, NativeGallery.PermissionType.Read, NativeGallery.MediaType.Image);
            return;
        }

        NativeGallery.GetImageFromGallery(HandleNativeGalleryResult, pickerTitle, "image/*");
    }

    private void HandleAndroidPermissionResult(NativeGallery.Permission permission)
    {
        if (permission != NativeGallery.Permission.Granted)
        {
            FinishWithFailure("갤러리 접근 권한이 거부되었습니다. 기기 권한 설정을 확인해주세요.");
            return;
        }

        NativeGallery.GetImageFromGallery(HandleNativeGalleryResult, pickerTitle, "image/*");
    }

    private void HandleNativeGalleryResult(string selectedPath)
    {
        if (string.IsNullOrEmpty(selectedPath))
        {
            FinishWithCancel();
            return;
        }

        FinishWithSuccess(selectedPath);
    }

    private void FinishWithSuccess(string selectedPath)
    {
        Action<string> callback = successCallback;
        ResetCallbacks();
        callback?.Invoke(selectedPath);
    }

    private void FinishWithCancel()
    {
        Action callback = cancelCallback;
        ResetCallbacks();
        callback?.Invoke();
    }

    private void FinishWithFailure(string errorMessage)
    {
        Action<string> callback = failureCallback;
        ResetCallbacks();
        callback?.Invoke(errorMessage);
    }

    private void ResetCallbacks()
    {
        IsPicking = false;
        successCallback = null;
        cancelCallback = null;
        failureCallback = null;
    }
}
