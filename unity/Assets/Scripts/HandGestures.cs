using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using AOT;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[RequireComponent(typeof(ARCameraManager))]
public class HandGestures : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Instantiate the prefab on the tip of the detected index finger.")]
    GameObject m_SpherePrefab;

    public GameObject spherePrefab
    {
        get { return m_SpherePrefab; }
        set { m_SpherePrefab = value; }
    }

    public GameObject sphere { get; private set; }

#if !UNITY_EDITOR && UNITY_IOS
    delegate void UnityIOSHandGestureDetectorCIntf_DidOutputPixelBufferCb(IntPtr pixelBuffer, int width, int height);
    
    delegate void UnityIOSHandGestureDetectorCIntf_DidOutputHandLandmarksCb(IntPtr landmarkPkt);

    [DllImport("__Internal")]
    static extern void UnityIOSHandGestureDetectorCIntf_SetDidOutputPixelBufferCb(UnityIOSHandGestureDetectorCIntf_DidOutputPixelBufferCb callback);

    [DllImport("__Internal")]
    static extern void UnityIOSHandGestureDetectorCIntf_SetDidOutputHandLandmarksCb(UnityIOSHandGestureDetectorCIntf_DidOutputHandLandmarksCb callback);

    [DllImport("__Internal")]
    static extern void UnityIOSHandGestureDetectorCIntf_ProcessSRGBImage(IntPtr imageBuffer, int width, int height);

    [MonoPInvokeCallback(typeof(UnityIOSHandGestureDetectorCIntf_DidOutputPixelBufferCb))]
    static void DidOutputPixelBuffer(IntPtr pixelBuffer, int width, int height)
    {
        //Debug.Log("HGD: Received PixelBuffer RGBA32 (" + width + ", " + height + ") @ " + pixelBuffer);

        byte[] managedPixelBuffer = new byte[width * height * 4];
        Marshal.Copy(pixelBuffer, managedPixelBuffer, 0, managedPixelBuffer.Length);
        m_MiniDisplay.UpdateDisplay(managedPixelBuffer, new Vector2Int(width, height));
    }

    [MonoPInvokeCallback(typeof(UnityIOSHandGestureDetectorCIntf_DidOutputHandLandmarksCb))]
    static void DidOutputHandLandmarks(IntPtr hlmPkt)
    {
        while (Interlocked.Exchange(ref m_HandLandmarksLock, 1) != 0) {}

        //Debug.Log("HGD: Received LandmarkPkt @ " + hlmPkt);

        Marshal.Copy(hlmPkt, m_HandLandmarks, 0, m_HandLandmarks.Length);

        Interlocked.Exchange(ref m_HandLandmarksLock, 0);
}
#endif // !UNITY_EDITOR && UNITY_IOS

    void Awake()
    {
        m_Camera = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Camera>();
        if (!m_Camera)
            throw new Exception("Missing MainCamera");

        m_CameraManager = GetComponent<ARCameraManager>();
        if (!m_CameraManager)
            throw new Exception("Missing ARCameraManager");

        m_RaycastManager = GetComponent<ARRaycastManager>();
        if (!m_RaycastManager)
            throw new Exception("Missing RaycastManager");

        m_LogDisplay = GameObject.FindGameObjectWithTag("LogDisplay").GetComponent<LogDisplay>();
        if (!m_LogDisplay)
            throw new Exception("Missing LogDisplay");

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

    void Update()
    {
        ProcessTouches();
        ProcessHandLandmarks();
    }

    bool TryGetTouchPosition(out Vector2 touchPosition)
    {
#if !UNITY_EDITOR && UNITY_IOS
        if (Input.touchCount > 0)
        {
            touchPosition = Input.GetTouch(0).position;
            return true;
        }
#endif
        touchPosition = default;
        return false;
    }

    void ProcessTouches()
    {
        if (!TryGetTouchPosition(out Vector2 touchPosition))
            return;

        if (m_RaycastManager.Raycast(touchPosition, s_Hits, TrackableType.PlaneWithinPolygon))
        {
            var hitPose = s_Hits[0].pose;

            if (!sphere)
            {
                sphere = Instantiate(m_SpherePrefab, hitPose.position, hitPose.rotation);

                Debug.Log("Touch: Sphere instantiated at " + sphere.transform.position);
            }
            else
            {
                sphere.transform.position = hitPose.position;

                Debug.Log("Touch: Sphere moved at " + sphere.transform.position);
            }
        }
    }

    void ProcessHandLandmarks()
    {
        if (Interlocked.Exchange(ref m_HandLandmarksLock, 1) != 0)
            return;

        // Hand gesture debouncer
        int num_hands = (int)m_HandLandmarks[HGD_HLM_PKT_NUM_HANDS_OFFSET];
        if (num_hands == 0)
        {
            Interlocked.Exchange(ref m_HandLandmarksLock, 0);

            m_HandDetectionAcceptCounter = HGD_ACCEPT_THRESHOLD;

            return;
        }
        else
        {
            m_HandDetectionAcceptCounter = (m_HandDetectionAcceptCounter - 1) % HGD_ACCEPT_THRESHOLD;
            if (m_HandDetectionAcceptCounter > 0)
            {
                Interlocked.Exchange(ref m_HandLandmarksLock, 0);

                return;
            }
        }
        m_LogDisplay.Log("Hand: Detected (" + m_HandDetectionAcceptCounter + " > 0)");

        Vector2 screenCenter;
        screenCenter.x = Screen.width / 2;
        screenCenter.y = Screen.height / 2;

        var handBaseOffset = HGD_HLM_PKT_HEADER_LEN + HGD_HLM_PKT_HAND_LANDMARK_HAND_BASE_OFFSET;
        var handBaseScreenPos = new Vector2(
            (1 - m_HandLandmarks[handBaseOffset + HGD_HLM_PKT_HAND_LANDMARK_X_OFFSET]) * Screen.width,
            (1 - m_HandLandmarks[handBaseOffset + HGD_HLM_PKT_HAND_LANDMARK_Y_OFFSET]) * Screen.height);
        var handBaseDepth = m_HandLandmarks[handBaseOffset + HGD_HLM_PKT_HAND_LANDMARK_Z_OFFSET];

        var thumbBaseOffset = HGD_HLM_PKT_HEADER_LEN + HGD_HLM_PKT_HAND_LANDMARK_THUMB_BASE_OFFSET;
        var thumbBaseScreenPos = new Vector2(
            (1 - m_HandLandmarks[thumbBaseOffset + HGD_HLM_PKT_HAND_LANDMARK_X_OFFSET]) * Screen.width,
            (1 - m_HandLandmarks[thumbBaseOffset + HGD_HLM_PKT_HAND_LANDMARK_Y_OFFSET]) * Screen.height);
        var thumbBaseDepth = m_HandLandmarks[thumbBaseOffset + HGD_HLM_PKT_HAND_LANDMARK_Z_OFFSET];

        var thumbTipOffset = HGD_HLM_PKT_HEADER_LEN + HGD_HLM_PKT_HAND_LANDMARK_THUMB_TIP_OFFSET;
        var thumbTipScreenPos = new Vector2(
            (1 - m_HandLandmarks[thumbTipOffset + HGD_HLM_PKT_HAND_LANDMARK_X_OFFSET]) * Screen.width,
            (1 - m_HandLandmarks[thumbTipOffset + HGD_HLM_PKT_HAND_LANDMARK_Y_OFFSET]) * Screen.height);
        var thumbTipDepth = m_HandLandmarks[thumbTipOffset + HGD_HLM_PKT_HAND_LANDMARK_Z_OFFSET];

        var indexBaseOffset = HGD_HLM_PKT_HEADER_LEN + HGD_HLM_PKT_HAND_LANDMARK_INDEX_BASE_OFFSET;
        var indexBaseScreenPos = new Vector2(
            (1 - m_HandLandmarks[indexBaseOffset + HGD_HLM_PKT_HAND_LANDMARK_X_OFFSET]) * Screen.width,
            (1 - m_HandLandmarks[indexBaseOffset + HGD_HLM_PKT_HAND_LANDMARK_Y_OFFSET]) * Screen.height);
        var indexBaseDepth = m_HandLandmarks[indexBaseOffset + HGD_HLM_PKT_HAND_LANDMARK_Z_OFFSET];

        var indexTipOffset = HGD_HLM_PKT_HEADER_LEN + HGD_HLM_PKT_HAND_LANDMARK_INDEX_TIP_OFFSET; 
        var indexTipScreenPos = new Vector2(
            (1 - m_HandLandmarks[indexTipOffset + HGD_HLM_PKT_HAND_LANDMARK_X_OFFSET]) * Screen.width,
            (1 - m_HandLandmarks[indexTipOffset + HGD_HLM_PKT_HAND_LANDMARK_Y_OFFSET]) * Screen.height);
        var indexTipDepth = m_HandLandmarks[indexTipOffset + HGD_HLM_PKT_HAND_LANDMARK_Z_OFFSET];

        Interlocked.Exchange(ref m_HandLandmarksLock, 0);

        m_LogDisplay.Log("Hand: HandBase ScreenPos " + handBaseScreenPos + ", HlmDepth " + handBaseDepth);
        m_LogDisplay.Log("Hand: ThumbBase ScreenPos " + thumbBaseScreenPos + ", HlmDepth " + thumbBaseDepth);
        m_LogDisplay.Log("Hand: ThumbTip ScreenPos " + thumbTipScreenPos + ", HlmDepth " + thumbTipDepth);
        m_LogDisplay.Log("Hand: IndexBase ScreenPos " + indexBaseScreenPos + ", HlmDepth " + indexBaseDepth);
        m_LogDisplay.Log("Hand: IndexTip ScreenPos " + indexTipScreenPos + ", HlmDepth " + indexTipDepth);

        if (m_RaycastManager.Raycast(indexTipScreenPos, s_Hits, TrackableType.PlaneWithinPolygon))
        {
            var hitPose = s_Hits[0].pose;

            if (!sphere)
            {
                sphere = Instantiate(m_SpherePrefab, hitPose.position, hitPose.rotation);

                m_LogDisplay.Log("Hand: Sphere instantiated at " + sphere.transform.position);
            }
            else
            {
                sphere.transform.position = hitPose.position;

                m_LogDisplay.Log("Hand: Sphere moved at " + sphere.transform.position);
            }
        }
    }

    // LM_PKT (N_HANDS #N, N_HLM #0, N_HLM #N-1, LM #0_0, .., LM#N-1_[N_HLM #N-1]-1)
    const int HGD_HLM_PKT_HEADER_LEN = 3;
    const int HGD_HLM_PKT_NUM_HANDS_OFFSET = 0;
    const int HGD_HLM_PKT_NUM_HANDS = 2;
    const int HGD_HLM_PKT_NUM_HAND_LANDMARKS_OFFSET = HGD_HLM_PKT_NUM_HANDS_OFFSET + 1;
    const int HGD_HLM_PKT_NUM_HAND_LANDMARKS = 21;

    // LM_PKT/LM(X, Y, Z)
    const int HGD_HLM_PKT_HAND_LANDMARK_LEN = 3;
    const int HGD_HLM_PKT_HAND_LANDMARK_X_OFFSET = 0;
    const int HGD_HLM_PKT_HAND_LANDMARK_Y_OFFSET = 1;
    const int HGD_HLM_PKT_HAND_LANDMARK_Z_OFFSET = 2;

    // Size of LM_PKT
    const int HGD_HLM_PKT_LEN = HGD_HLM_PKT_HEADER_LEN + (HGD_HLM_PKT_NUM_HANDS * HGD_HLM_PKT_NUM_HAND_LANDMARKS * HGD_HLM_PKT_HAND_LANDMARK_LEN);

    // Landmarks offsets
    const int HGD_HLM_PKT_HAND_LANDMARK_HAND_BASE_OFFSET = 0;
    const int HGD_HLM_PKT_HAND_LANDMARK_THUMB_BASE_OFFSET = 1;
    const int HGD_HLM_PKT_HAND_LANDMARK_THUMB_TIP_OFFSET = 4;
    const int HGD_HLM_PKT_HAND_LANDMARK_INDEX_BASE_OFFSET = 5;
    const int HGD_HLM_PKT_HAND_LANDMARK_INDEX_TIP_OFFSET = 8;

    static int m_HandLandmarksLock = 0;
    static float[] m_HandLandmarks = new float[HGD_HLM_PKT_LEN];

    // Minimum number of successive hand detections
    const int HGD_ACCEPT_THRESHOLD = 30;

    static int m_HandDetectionAcceptCounter = 0;

    static List<ARRaycastHit> s_Hits = new List<ARRaycastHit>();

    static Camera m_Camera;

    static ARCameraManager m_CameraManager;

    static ARRaycastManager m_RaycastManager;

    static LogDisplay m_LogDisplay;

    static MiniDisplay m_MiniDisplay;
}
