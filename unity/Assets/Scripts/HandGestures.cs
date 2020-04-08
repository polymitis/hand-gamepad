using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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
        m_CameraManager = GetComponent<ARCameraManager>();
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
        if (m_CameraManager.subsystem.TryGetLatestFrame(cameraParams, out frame))
            UnityIOSCharadesCIntf_ProcessVideoFrame(frame.nativePtr);
#endif
    }

    private Camera m_Camera;

    private ARCameraManager m_CameraManager;
}
