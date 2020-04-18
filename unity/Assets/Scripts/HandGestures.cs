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
#if !UNITY_EDITOR && UNITY_IOS
    // (N_HANDS #N, N_HLM #0, N_HLM #N-1, LM #0_0, .., LM#N-1_[N_HLM #N-1]-1)
    private const int HGD_HLM_PKT_HEADER_LEN = 3;
    private const int HGD_HLM_PKT_NUM_HANDS_OFFSET = 0;
    private const int HGD_HLM_PKT_NUM_HANDS = 2;
    private const int HGD_HLM_PKT_NUM_HAND_LANDMARKS_OFFSET = HGD_HLM_PKT_NUM_HANDS_OFFSET + 1;
    private const int HGD_HLM_PKT_NUM_HAND_LANDMARKS = 21;

    // LM(X, Y, Z)
    private const int HGD_HLM_PKT_HAND_LANDMARK_LEN = 3;
    private const int HGD_HLM_PKT_HAND_LANDMARK_X_OFFSET = 0;
    private const int HGD_HLM_PKT_HAND_LANDMARK_Y_OFFSET = 1;
    private const int HGD_HLM_PKT_HAND_LANDMARK_Z_OFFSET = 2;

    private const int HGD_HLM_PKT_LEN = HGD_HLM_PKT_HEADER_LEN + (HGD_HLM_PKT_NUM_HANDS * HGD_HLM_PKT_NUM_HAND_LANDMARKS * HGD_HLM_PKT_HAND_LANDMARK_LEN);

    private delegate void UnityIOSHandGestureDetectorCIntf_DidOutputPixelBufferCb(IntPtr pixelBuffer, int width, int height);
    
    private delegate void UnityIOSHandGestureDetectorCIntf_DidOutputHandLandmarksCb(IntPtr landmarkPkt);

    [DllImport("__Internal")]
    private static extern void UnityIOSHandGestureDetectorCIntf_SetDidOutputPixelBufferCb(UnityIOSHandGestureDetectorCIntf_DidOutputPixelBufferCb callback);

    [DllImport("__Internal")]
    private static extern void UnityIOSHandGestureDetectorCIntf_SetDidOutputHandLandmarksCb(UnityIOSHandGestureDetectorCIntf_DidOutputHandLandmarksCb callback);

    [DllImport("__Internal")]
    private static extern void UnityIOSHandGestureDetectorCIntf_ProcessSRGBImage(IntPtr imageBuffer, int width, int height);

    [MonoPInvokeCallback(typeof(UnityIOSHandGestureDetectorCIntf_DidOutputPixelBufferCb))]
    private static void DidOutputPixelBuffer(IntPtr pixelBuffer, int width, int height)
    {
        Debug.Log("HGD: Received PixelBuffer RGBA32 (" + width + ", " + height + ") @ " + pixelBuffer);
        byte[] managedPixelBuffer = new byte[width * height * 4];
        Marshal.Copy(pixelBuffer, managedPixelBuffer, 0, managedPixelBuffer.Length);
        m_MiniDisplay.UpdateDisplay(managedPixelBuffer, new Vector2Int(width, height));
    }

    [MonoPInvokeCallback(typeof(UnityIOSHandGestureDetectorCIntf_DidOutputHandLandmarksCb))]
    private static void DidOutputHandLandmarks(IntPtr hlmPkt)
    {
        Debug.Log("HGD: Received LandmarkPkt @ " + hlmPkt);
        float[] managedHlmPkt = new float[HGD_HLM_PKT_LEN];
        Marshal.Copy(hlmPkt, managedHlmPkt, 0, managedHlmPkt.Length);

        for (int i = 0; i < (int)managedHlmPkt[HGD_HLM_PKT_NUM_HANDS_OFFSET]; i++)
            Debug.Log("HGD: Number of landmarks for hand[" + i + "]: " +
                (int)managedHlmPkt[HGD_HLM_PKT_NUM_HAND_LANDMARKS_OFFSET + i]);
    }
#endif

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
            UnityIOSHandGestureDetectorCIntf_SetDidOutputPixelBufferCb(DidOutputPixelBuffer);
            UnityIOSHandGestureDetectorCIntf_SetDidOutputHandLandmarksCb(DidOutputHandLandmarks);
#endif
        }
    }

    unsafe void OnCameraFrameReceived(ARCameraFrameEventArgs eventArgs)
    {
        XRCameraImage image;
        if (!m_CameraManager.TryGetLatestImage(out image))
            return;

        //m_MiniDisplay.UpdateDisplay(image);

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

        UnityIOSHandGestureDetectorCIntf_ProcessSRGBImage(
            new IntPtr(buffer.GetUnsafePtr()),
            conversionParams.outputDimensions.x,
            conversionParams.outputDimensions.y);
#endif

        image.Dispose();
    }

    static private Camera m_Camera;

    static private ARCameraManager m_CameraManager;

    static private MiniDisplay m_MiniDisplay;
}
