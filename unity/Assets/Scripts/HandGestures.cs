using System;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[RequireComponent(typeof(ARCameraManager))]
public class HandGestures : MonoBehaviour
{
    [DllImport("__Internal")]
    private static extern void UnityIOSCharadesCIntf_ProcessVideoFrame(IntPtr buffer);

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

        //TODO Pass image to Charades instead of XRCameraFrame native pointer.

        image.Dispose();

#if !UNITY_EDITOR && UNITY_IOS
        var cameraParams = new XRCameraParams
        {
            zNear = m_Camera.nearClipPlane,
            zFar = m_Camera.farClipPlane,
            screenWidth = Screen.width,
            screenHeight = Screen.height,
            screenOrientation = Screen.orientation
        };

        XRCameraFrame frame;
        if (!m_CameraManager.subsystem.TryGetLatestFrame(cameraParams, out frame))
            return;

        UnityIOSCharadesCIntf_ProcessVideoFrame(frame.nativePtr);
#endif
    }

    private Camera m_Camera;

    private ARCameraManager m_CameraManager;

    MiniDisplay m_MiniDisplay;
}
