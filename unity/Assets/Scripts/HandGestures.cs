using System;
using System.Runtime.InteropServices;
using AOT;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[RequireComponent(typeof(ARCameraManager))]
public class HandGestures : MonoBehaviour
{
    private delegate void UnityIOSCharadesCIntf_DidOutputPixelBufferCb(IntPtr buffer, int width, int height);

    [DllImport("__Internal")]
    private static extern void UnityIOSCharadesCIntf_SetDidOutputPixelBufferCb(UnityIOSCharadesCIntf_DidOutputPixelBufferCb callback);

    [DllImport("__Internal")]
    private static extern void UnityIOSCharadesCIntf_ProcessSRGBImage(IntPtr buffer, int width, int height);

    [MonoPInvokeCallback(typeof(UnityIOSCharadesCIntf_DidOutputPixelBufferCb))]
    private static void DidOutputPixelBuffer(IntPtr buffer, int width, int height)
    {
        Debug.Log("OnCharadesDidOutputPixelBuffer called");
    }

    void Awake()
    {
        m_Camera = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Camera>();
        if (!m_Camera)
            throw new Exception("Missing MainCamera");

        m_CameraManager = GetComponent<ARCameraManager>();
        if (!m_CameraManager)
            throw new Exception("Missing ARCameraManager");

        m_MiniDisplay = GameObject.FindGameObjectWithTag("MiniDisplay").GetComponent<MiniDisplay>();
        if (!m_MiniDisplay)
            throw new Exception("Missing MiniDisplay");
    }

    void OnEnable()
    {
        if (m_CameraManager != null)
        {
            m_CameraManager.frameReceived += OnCameraFrameReceived;

#if !UNITY_EDITOR && UNITY_IOS
            UnityIOSCharadesCIntf_SetDidOutputPixelBufferCb(DidOutputPixelBuffer);
#endif
        }
    }

    void Update()
    {
        // Do nothing
    }

    unsafe void OnCameraFrameReceived(ARCameraFrameEventArgs eventArgs)
    {
        XRCameraImage image;
        if (!m_CameraManager.TryGetLatestImage(out image))
            return;

        m_MiniDisplay.UpdateDisplay(image);

#if !UNITY_EDITOR && UNITY_IOS
        var conversionParams = new XRCameraImageConversionParams
        {
            inputRect = new RectInt(0, 0, image.width, image.height),
            outputDimensions = new Vector2Int(image.width / 2, image.height / 2),
            outputFormat = TextureFormat.BGRA32,
            transformation = CameraImageTransformation.MirrorY
        };

        int size = image.GetConvertedDataSize(conversionParams);
        var buffer = new NativeArray<byte>(size, Allocator.Temp);
        image.Convert(conversionParams, new IntPtr(buffer.GetUnsafePtr()), buffer.Length);

        UnityIOSCharadesCIntf_ProcessSRGBImage(
            new IntPtr(buffer.GetUnsafePtr()),
            conversionParams.outputDimensions.x,
            conversionParams.outputDimensions.y);
#endif

        image.Dispose();
    }

    private Camera m_Camera;

    private ARCameraManager m_CameraManager;

    MiniDisplay m_MiniDisplay;
}
