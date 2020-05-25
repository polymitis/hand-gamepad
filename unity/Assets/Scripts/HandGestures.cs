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
    [Tooltip("Instantiate and move the prefab using detected hand.")]
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

    delegate void UnityIOSHandGestureDetectorCIntf_DidOutputHandRectsCb(IntPtr rectPkt);

    delegate void UnityIOSHandGestureDetectorCIntf_DidOutputPalmRectsCb(IntPtr rectPkt);

    [DllImport("__Internal")]
    static extern void UnityIOSHandGestureDetectorCIntf_SetDidOutputPixelBufferCb(UnityIOSHandGestureDetectorCIntf_DidOutputPixelBufferCb callback);

    [DllImport("__Internal")]
    static extern void UnityIOSHandGestureDetectorCIntf_SetDidOutputHandLandmarksCb(UnityIOSHandGestureDetectorCIntf_DidOutputHandLandmarksCb callback);
    
    [DllImport("__Internal")]
    static extern void UnityIOSHandGestureDetectorCIntf_SetDidOutputHandRectsCb(UnityIOSHandGestureDetectorCIntf_DidOutputHandRectsCb callback);
    
    [DllImport("__Internal")]
    static extern void UnityIOSHandGestureDetectorCIntf_SetDidOutputPalmRectsCb(UnityIOSHandGestureDetectorCIntf_DidOutputPalmRectsCb callback);

    [DllImport("__Internal")]
    static extern void UnityIOSHandGestureDetectorCIntf_ProcessSRGBImage(IntPtr imageBuffer, int width, int height);

    [MonoPInvokeCallback(typeof(UnityIOSHandGestureDetectorCIntf_DidOutputPixelBufferCb))]
    static void DidOutputPixelBuffer(IntPtr pixelBuffer, int width, int height)
    {
        while (Interlocked.Exchange(ref m_PixelBufferLock, 1) != 0) {}

        Debug.Log("HGD: Received PixelBuffer RGBA32 (" + width + ", " + height + ") @ " + pixelBuffer);

        m_PixelBuffer = new byte[width * height * 4];
        Marshal.Copy(pixelBuffer, m_PixelBuffer, 0, m_PixelBuffer.Length);
        m_PixelBufferSize = new Vector2Int(width, height);

        Interlocked.Exchange(ref m_PixelBufferLock, 0);
    }

    [MonoPInvokeCallback(typeof(UnityIOSHandGestureDetectorCIntf_DidOutputHandLandmarksCb))]
    static void DidOutputHandLandmarks(IntPtr hlmPkt)
    {
        while (Interlocked.Exchange(ref m_HandLandmarksLock, 1) != 0) {}

        Debug.Log("HGD: Received LandmarkPkt @ " + hlmPkt);

        Marshal.Copy(hlmPkt, m_HandLandmarks, 0, m_HandLandmarks.Length);

        Interlocked.Exchange(ref m_HandLandmarksLock, 0);
    }

    [MonoPInvokeCallback(typeof(UnityIOSHandGestureDetectorCIntf_DidOutputHandRectsCb))]
    static void DidOutputHandRects(IntPtr hrcPkt)
    {
        while (Interlocked.Exchange(ref m_HandRectsLock, 1) != 0) {}

        Debug.Log("HGD: Received hand RectPkt @ " + hrcPkt);

        Marshal.Copy(hrcPkt, m_HandRects, 0, m_HandRects.Length);

        Interlocked.Exchange(ref m_HandRectsLock, 0);
    }
    

    [MonoPInvokeCallback(typeof(UnityIOSHandGestureDetectorCIntf_DidOutputPalmRectsCb))]
    static void DidOutputPalmRects(IntPtr hrcPkt)
    {
        while (Interlocked.Exchange(ref m_PalmRectsLock, 1) != 0) {}

        Debug.Log("HGD: Received palm RectPkt @ " + hrcPkt);

        Marshal.Copy(hrcPkt, m_PalmRects, 0, m_PalmRects.Length);

        Interlocked.Exchange(ref m_PalmRectsLock, 0);
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
            UnityIOSHandGestureDetectorCIntf_SetDidOutputHandRectsCb(DidOutputHandRects);
            UnityIOSHandGestureDetectorCIntf_SetDidOutputPalmRectsCb(DidOutputPalmRects);
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
        m_LogDisplay.Clear();

        ProcessCameraFeedback();

        ProcessPalmRects();
        ProcessHandRects();
        ProcessHandLandmarks();
        UpdateHandDetectionLogs();

        UpdateSphere();
    }

    void UpdateHandDetectionLogs()
    {
        m_LogDisplay.Log("HGD: Hand visibility (accept > +" + HGD_HAND_LANDMARKS_VISIBILITY_RATIO + ") " + m_HandLandmarksVisibilityRatio);
        m_LogDisplay.Log("HGD: Hand is " + ((m_IsHandVisible) ? "VISIBLE" : "AWAY"));
        m_LogDisplay.Log("HGD: Wrist-Point/Wrist-Thumb (open hand > " + HGD_WRIST_POINT_TO_WRIST_THUMB_RATIO + ") " + m_WristPointToWristThumbRatio);
        m_LogDisplay.Log("HGD: Hand is " + ((m_IsHandOpen) ? "OPEN" : "CLOSED"));
        m_LogDisplay.Log("HGD: Hand size " + m_HandSize + ", palm size " + m_PalmSize);
        m_LogDisplay.Log("HGD: Screen pos of palm (" + m_PalmPos.x + ", " + m_PalmPos.y + "), camera dist " + ((m_IsHandOpen) ? m_OpenHandDist : m_ClosedHandDist));
        m_LogDisplay.Log("HGD: Screen pos of wrist (" + m_WristPos.x + ", " + m_WristPos.y + ") , rel depth " + m_WristPos.z + ", rel palm dist " + m_WristRelDist);
        m_LogDisplay.Log("HGD: Screen pos of index (" + m_IndexPos.x + ", " + m_IndexPos.y + "), rel depth " + m_IndexPos.z + ", rel palm dist " + m_IndexRelDist);
        m_LogDisplay.Log("HGD: Screen pos of little (" + m_LittlePos.x + ", " + m_LittlePos.y + "), rel depth " + m_LittlePos.z + ", rel palm dist " + m_PointRelDist);
        m_LogDisplay.Log("HGD: Screen pos of point (" + m_PointPos.x + ", " + m_PointPos.y + "), rel depth " + m_PointPos.z + ", rel palm dist " + m_ThumbRelDist);
        m_LogDisplay.Log("HGD: Screen pos of thumb (" + m_ThumbPos.x + ", " + m_ThumbPos.y + "), rel depth " + m_ThumbPos.z + ", rel palm dist " + m_LittleRelDist);
    }

    void UpdateSphere()
    {
        if (m_IsHandVisible && m_IsHandOpen)
        {
            var position = m_Camera.ScreenToWorldPoint(new Vector3(m_PalmPos.x * Screen.width, m_PalmPos.y * Screen.height, m_OpenHandDist));

            if (!sphere)
            {
                sphere = Instantiate(m_SpherePrefab, position, Quaternion.identity);
                m_LogDisplay.Log("World: Sphere instantiated at " + sphere.transform.position);
            }
            else
            {
                sphere.SetActive(true);
                sphere.transform.position = position;
                m_LogDisplay.Log("World: Sphere moved at " + sphere.transform.position);
            }
        }
        else
        {
            if (sphere)
                sphere.SetActive(false);
        }
    }

    void ProcessPalmRects()
    {
        if (Interlocked.Exchange(ref m_PalmRectsLock, 1) != 0)
            return;

        // Hand gesture debouncer
        int num_hands = (int)m_PalmRects[HGD_HLM_PKT_NUM_HANDS_OFFSET];
        if (num_hands == 0)
        {
            Interlocked.Exchange(ref m_PalmRectsLock, 0);
            m_PalmRectsAcceptCounter = HGD_ACCEPT_THRESHOLD;
            return;
        }
        else
        {
            m_PalmRectsAcceptCounter = (m_PalmRectsAcceptCounter - 1) % HGD_ACCEPT_THRESHOLD;
            if (m_PalmRectsAcceptCounter > 0)
            {
                Interlocked.Exchange(ref m_PalmRectsLock, 0);
                return;
            }
        }

        // Read packet buffer
        var rectOffset = HGD_HRC_PKT_HEADER_LEN + HGD_HRC_PKT_FIRST_RECT_OFFSET;
        var rectPos = new Vector2(1 - m_PalmRects[rectOffset + HGD_HRC_PKT_RECT_X_OFFSET], 1 - m_PalmRects[rectOffset + HGD_HRC_PKT_RECT_Y_OFFSET]);
        var rectSize = new Vector2(m_PalmRects[rectOffset + HGD_HRC_PKT_RECT_W_OFFSET], m_PalmRects[rectOffset + HGD_HRC_PKT_RECT_H_OFFSET]);

        Interlocked.Exchange(ref m_PalmRectsLock, 0);

        if (rectSize.x < HGD_PALM_RECT_SIDE_MIN || rectSize.y < HGD_PALM_RECT_SIDE_MIN || rectSize.x > HGD_PALM_RECT_SIDE_MAX || rectSize.y > HGD_PALM_RECT_SIDE_MAX)
            return;

        // Stabilize reported center
        if (m_PalmAvgSamples < HGD_AVG_PALM_NUM_SAMPLES)
            ++m_PalmAvgSamples;
        m_PalmPos += (rectPos - m_PalmPos) / m_PalmAvgSamples;

        // Calculate palm magnitude
        m_PalmSize = Mathf.Sqrt(Mathf.Pow(rectSize.x, 2) + Mathf.Pow(rectSize.y, 2));

        // Estimate distance from camera using an exponential function
        var openHandDist = Mathf.Clamp(HGD_DISTANCE_OPEN_HAND_PARAM_A + HGD_DISTANCE_OPEN_HAND_PARAM_B * Mathf.Pow(1 - m_PalmSize, HGD_DISTANCE_OPEN_HAND_PARAM_C + MATH_NATURAL_LOG_EPSILON), HGD_DISTANCE_MIN, HGD_DISTANCE_MAX);
        var closedHandDist = Mathf.Clamp(HGD_DISTANCE_CLOSED_HAND_PARAM_A + HGD_DISTANCE_CLOSED_HAND_PARAM_B * Mathf.Pow(1 - m_PalmSize, HGD_DISTANCE_CLOSED_HAND_PARAM_C + MATH_NATURAL_LOG_EPSILON), HGD_DISTANCE_MIN, HGD_DISTANCE_MAX);

        // Stabilize calculated distance
        if (m_HandDistAvgSamples < HGD_AVG_DISTANCE_NUM_SAMPLES)
            ++m_HandDistAvgSamples;
        m_OpenHandDist += (openHandDist - m_OpenHandDist) / m_HandDistAvgSamples;
        m_ClosedHandDist += (closedHandDist - m_ClosedHandDist) / m_HandDistAvgSamples;
    }

    void ProcessHandRects()
    {
        if (Interlocked.Exchange(ref m_HandRectsLock, 1) != 0)
            return;

        // Hand gesture debouncer
        int num_hands = (int)m_HandRects[HGD_HLM_PKT_NUM_HANDS_OFFSET];
        if (num_hands == 0)
        {
            Interlocked.Exchange(ref m_HandRectsLock, 0);
            m_HandRectsAcceptCounter = HGD_ACCEPT_THRESHOLD;
            return;
        }
        else
        {
            m_HandRectsAcceptCounter = (m_HandRectsAcceptCounter - 1) % HGD_ACCEPT_THRESHOLD;
            if (m_HandRectsAcceptCounter > 0)
            {
                Interlocked.Exchange(ref m_HandRectsLock, 0);
                return;
            }
        }

        // Read packet buffer
        var rectOffset = HGD_HRC_PKT_HEADER_LEN + HGD_HRC_PKT_FIRST_RECT_OFFSET;
        var rectSize = new Vector2(m_HandRects[rectOffset + HGD_HRC_PKT_RECT_W_OFFSET], m_HandRects[rectOffset + HGD_HRC_PKT_RECT_H_OFFSET]);

        Interlocked.Exchange(ref m_HandRectsLock, 0);

        if (rectSize.x < HGD_HAND_RECT_SIDE_MIN || rectSize.y < HGD_HAND_RECT_SIDE_MIN || rectSize.x > HGD_HAND_RECT_SIDE_MAX || rectSize.y > HGD_HAND_RECT_SIDE_MAX)
            return;

        // Calculate hand magnitude
        m_HandSize = Mathf.Sqrt(Mathf.Pow(rectSize.x, 2) + Mathf.Pow(rectSize.y, 2));
    }

    void ProcessHandLandmarks()
    {
        if (m_HandLandmarksVisibilityCounter > 0)
            m_HandLandmarksVisibilityCounter = (m_HandLandmarksVisibilityCounter - 1) % HGD_HAND_LANDMARKS_VISIBILITY_COUNT_MAX;
        m_HandLandmarksVisibilityRatio = 1.0f * m_HandLandmarksVisibilityCounter / HGD_HAND_LANDMARKS_VISIBILITY_COUNT_MAX;

        m_IsHandVisible = m_HandLandmarksVisibilityRatio > HGD_HAND_LANDMARKS_VISIBILITY_RATIO;

        if (Interlocked.Exchange(ref m_HandLandmarksLock, 1) != 0)
            return;

        // Hand gesture debouncer
        int num_hands = (int)m_HandLandmarks[HGD_HLM_PKT_NUM_HANDS_OFFSET];
        if (num_hands == 0)
        {
            m_HandLandmarksAcceptCounter = HGD_ACCEPT_THRESHOLD;
            Interlocked.Exchange(ref m_HandLandmarksLock, 0);
            return;
        }
        else
        {
            m_HandLandmarksAcceptCounter = (m_HandLandmarksAcceptCounter - 1) % HGD_ACCEPT_THRESHOLD;
            if (m_HandLandmarksAcceptCounter > 0)
            {
                Interlocked.Exchange(ref m_HandLandmarksLock, 0);
                return;
            }
        }

        // Read packet buffer
        var wristOffset = HGD_HLM_PKT_HEADER_LEN + HGD_HLM_PKT_FIRST_HAND_LANDMARKS_OFFSET + HGD_HLM_PKT_FIRST_HAND_LM0_OFFSET;
        var wristPos = new Vector3(1 - m_HandLandmarks[wristOffset + HGD_HLM_PKT_HAND_LANDMARK_X_OFFSET], 1 - m_HandLandmarks[wristOffset + HGD_HLM_PKT_HAND_LANDMARK_Y_OFFSET], m_HandLandmarks[wristOffset + HGD_HLM_PKT_HAND_LANDMARK_Z_OFFSET]);

        var indexOffset = HGD_HLM_PKT_HEADER_LEN + HGD_HLM_PKT_FIRST_HAND_LANDMARKS_OFFSET + HGD_HLM_PKT_FIRST_HAND_LM5_OFFSET;
        var indexPos = new Vector3(1 - m_HandLandmarks[indexOffset + HGD_HLM_PKT_HAND_LANDMARK_X_OFFSET], 1 - m_HandLandmarks[indexOffset + HGD_HLM_PKT_HAND_LANDMARK_Y_OFFSET], m_HandLandmarks[indexOffset + HGD_HLM_PKT_HAND_LANDMARK_Z_OFFSET]);

        var pointOffset = HGD_HLM_PKT_HEADER_LEN + HGD_HLM_PKT_FIRST_HAND_LANDMARKS_OFFSET + HGD_HLM_PKT_FIRST_HAND_LM8_OFFSET;
        var pointPos = new Vector3(1 - m_HandLandmarks[pointOffset + HGD_HLM_PKT_HAND_LANDMARK_X_OFFSET], 1 - m_HandLandmarks[pointOffset + HGD_HLM_PKT_HAND_LANDMARK_Y_OFFSET], m_HandLandmarks[pointOffset + HGD_HLM_PKT_HAND_LANDMARK_Z_OFFSET]);

        var thumbOffset = HGD_HLM_PKT_HEADER_LEN + HGD_HLM_PKT_FIRST_HAND_LANDMARKS_OFFSET + HGD_HLM_PKT_FIRST_HAND_LM4_OFFSET;
        var thumbPos = new Vector3(1 - m_HandLandmarks[thumbOffset + HGD_HLM_PKT_HAND_LANDMARK_X_OFFSET], 1 - m_HandLandmarks[thumbOffset + HGD_HLM_PKT_HAND_LANDMARK_Y_OFFSET], m_HandLandmarks[thumbOffset + HGD_HLM_PKT_HAND_LANDMARK_Z_OFFSET]);

        var littleOffset = HGD_HLM_PKT_HEADER_LEN + HGD_HLM_PKT_FIRST_HAND_LANDMARKS_OFFSET + HGD_HLM_PKT_FIRST_HAND_LM17_OFFSET;
        var littlePos = new Vector3(1 - m_HandLandmarks[littleOffset + HGD_HLM_PKT_HAND_LANDMARK_X_OFFSET], 1 - m_HandLandmarks[thumbOffset + HGD_HLM_PKT_HAND_LANDMARK_Y_OFFSET], m_HandLandmarks[littleOffset + HGD_HLM_PKT_HAND_LANDMARK_Z_OFFSET]);

        Interlocked.Exchange(ref m_HandLandmarksLock, 0);

        m_HandLandmarksVisibilityCounter = HGD_HAND_LANDMARKS_VISIBILITY_COUNT_MAX;

        // Stabilize landmark readings
        if (m_LmAvgSamples < HGD_AVG_LM_NUM_SAMPLES)
            ++m_LmAvgSamples;
        m_WristPos += (wristPos - m_WristPos) / m_LmAvgSamples;
        m_IndexPos += (indexPos - m_IndexPos) / m_LmAvgSamples;
        m_PointPos += (pointPos - m_PointPos) / m_LmAvgSamples;
        m_ThumbPos += (thumbPos - m_ThumbPos) / m_LmAvgSamples;
        m_LittlePos += (littlePos - m_LittlePos) / m_LmAvgSamples;

        // Estimate landmark relative distances for palm center
        m_WristRelDist = HGD_PALM_RELATIVE_DEPTH_MAX * m_WristPos.z;
        m_IndexRelDist = HGD_PALM_RELATIVE_DEPTH_MAX * m_IndexPos.z;
        m_PointRelDist = HGD_PALM_RELATIVE_DEPTH_MAX * m_PointPos.z;
        m_ThumbRelDist = HGD_PALM_RELATIVE_DEPTH_MAX * m_ThumbPos.z;
        m_LittleRelDist = HGD_PALM_RELATIVE_DEPTH_MAX * m_LittlePos.z;

        // Determine if the hand is open/closed
        m_WristPointToWristThumbRatio = Vector2.Distance(m_WristPos, m_PointPos) / Vector2.Distance(m_WristPos, m_ThumbPos);
        m_IsHandOpen = m_WristPointToWristThumbRatio > HGD_WRIST_POINT_TO_WRIST_THUMB_RATIO;
    }

    void ProcessCameraFeedback()
    {
        if (Interlocked.Exchange(ref m_PixelBufferLock, 1) != 0)
            return;

        if (m_PixelBuffer == null || m_PixelBuffer.Length == 0 || m_PixelBufferSize.x == 0 || m_PixelBufferSize.y == 0)
        {
            Interlocked.Exchange(ref m_PixelBufferLock, 0);
            return;
        }

        m_MiniDisplay.UpdateDisplay(m_PixelBuffer, m_PixelBufferSize);

        Interlocked.Exchange(ref m_PixelBufferLock, 0);
    }

    // Minimum number of successive hand detections
    const int HGD_ACCEPT_THRESHOLD = 10;

    // Landmarks packet HLM_PKT (N_HANDS #N, N_HLM #0, N_HLM #1, LM #0_0, .., LM #0_1, LM #1_0, .., LM #1_20)
    const int HGD_HLM_PKT_HEADER_LEN = 3;
    const int HGD_HLM_PKT_NUM_HANDS_OFFSET = 0;
    const int HGD_HLM_PKT_NUM_HANDS = 2;
    const int HGD_HLM_PKT_NUM_HAND_LANDMARKS_OFFSET = HGD_HLM_PKT_NUM_HANDS_OFFSET + 1;
    const int HGD_HLM_PKT_NUM_HAND_LANDMARKS = 21;
    // Landmark LM (X, Y, Z)
    const int HGD_HLM_PKT_HAND_LANDMARK_LEN = 3;
    const int HGD_HLM_PKT_HAND_LANDMARK_X_OFFSET = 0;
    const int HGD_HLM_PKT_HAND_LANDMARK_Y_OFFSET = 1;
    const int HGD_HLM_PKT_HAND_LANDMARK_Z_OFFSET = 2;
    // Size of landmarks packet
    const int HGD_HLM_PKT_LEN = HGD_HLM_PKT_HEADER_LEN + (HGD_HLM_PKT_NUM_HANDS * HGD_HLM_PKT_NUM_HAND_LANDMARKS * HGD_HLM_PKT_HAND_LANDMARK_LEN);

    // Landmarks offsets
    const int HGD_HLM_PKT_FIRST_HAND_LANDMARKS_OFFSET = 0;
    const int HGD_HLM_PKT_FIRST_HAND_LM0_OFFSET = 0;
    const int HGD_HLM_PKT_FIRST_HAND_LM4_OFFSET = 4;
    const int HGD_HLM_PKT_FIRST_HAND_LM5_OFFSET = 5;
    const int HGD_HLM_PKT_FIRST_HAND_LM8_OFFSET = 8;
    const int HGD_HLM_PKT_FIRST_HAND_LM17_OFFSET = 17;

    // Landmarks packet buffer
    static int m_HandLandmarksLock;
    static float[] m_HandLandmarks = new float[HGD_HLM_PKT_LEN];
    int m_HandLandmarksAcceptCounter;

    // Landmarks visibility
    const int HGD_HAND_LANDMARKS_VISIBILITY_COUNT_MAX = 20;
    const float HGD_HAND_LANDMARKS_VISIBILITY_RATIO = 0.3f;
    int m_HandLandmarksVisibilityCounter;
    float m_HandLandmarksVisibilityRatio;

    // Rects packet HRC_PKT (N_HANDS #N, RECT_ID #0, .., RECT_R #0, RECT_ID #1, .., RECT_R #1)
    const int HGD_HRC_PKT_HEADER_LEN = 1;
    const int HGD_HRC_PKT_NUM_HANDS_OFFSET = 0;
    const int HGD_HRC_PKT_NUM_HANDS = 2;
    const int HGD_HRC_PKT_RECT_NUM_PROP = 6;
    // Rect RECT (ID, X, Y, W, H, R)
    const int HGD_HRC_PKT_RECT_ID_OFFSET = 0;
    const int HGD_HRC_PKT_RECT_X_OFFSET = 1;
    const int HGD_HRC_PKT_RECT_Y_OFFSET = 2;
    const int HGD_HRC_PKT_RECT_W_OFFSET = 3;
    const int HGD_HRC_PKT_RECT_H_OFFSET = 4;
    const int HGD_HRC_PKT_RECT_R_OFFSET = 5;
    // Size of rects packet
    const int HGD_HRC_PKT_LEN = HGD_HRC_PKT_HEADER_LEN + (HGD_HRC_PKT_NUM_HANDS * HGD_HRC_PKT_RECT_NUM_PROP);

    // Rects offsets
    const int HGD_HRC_PKT_FIRST_RECT_OFFSET = 0;

    // Hand rects packet buffer
    static int m_HandRectsLock;
    static float[] m_HandRects = new float[HGD_HRC_PKT_LEN];
    int m_HandRectsAcceptCounter;

    // Palm rects packet buffer
    static int m_PalmRectsLock;
    static float[] m_PalmRects = new float[HGD_HRC_PKT_LEN];
    int m_PalmRectsAcceptCounter;

    // HGD Camera feedback
    static int m_PixelBufferLock;
    static byte[] m_PixelBuffer;
    static Vector2Int m_PixelBufferSize;

    // Hand visibility
    bool m_IsHandVisible;

    // Last detected hand landmarks
    const int HGD_AVG_LM_NUM_SAMPLES = 5;

    int m_LmAvgSamples = 0;
    Vector3 m_WristPos;
    Vector3 m_IndexPos;
    Vector3 m_PointPos;
    Vector3 m_ThumbPos;
    Vector3 m_LittlePos;

    const float HGD_PALM_RELATIVE_DEPTH_MAX = 0.08f;
    float m_WristRelDist;
    float m_IndexRelDist;
    float m_PointRelDist;
    float m_ThumbRelDist;
    float m_LittleRelDist;

    // Wrist-thumb / Wrist-point ratio
    const float HGD_WRIST_POINT_TO_WRIST_THUMB_RATIO = 1.1f;

    float m_WristPointToWristThumbRatio;

    bool m_IsHandOpen;

    // Hand rect detection constraints
    const float HGD_HAND_RECT_SIDE_MIN = 0.2f;
    const float HGD_HAND_RECT_SIDE_MAX = 1.5f;

    // Hand magnitude
    float m_HandSize;

    // Palm rect detection constraints
    const float HGD_PALM_RECT_SIDE_MIN = 0.08f;
    const float HGD_PALM_RECT_SIDE_MAX = 0.5f;

    // Palm center position detection
    const int HGD_AVG_PALM_NUM_SAMPLES = 5;

    int m_PalmAvgSamples;
    Vector2 m_PalmPos;

    // Palm magnitude
    float m_PalmSize;

    // Hand distance detection
    const float HGD_DISTANCE_OPEN_HAND_PARAM_A = 0.18f;
    const float HGD_DISTANCE_OPEN_HAND_PARAM_B = 1.4f;
    const float HGD_DISTANCE_OPEN_HAND_PARAM_C = 3.4f;
    const float HGD_DISTANCE_CLOSED_HAND_PARAM_A = 0.19f;
    const float HGD_DISTANCE_CLOSED_HAND_PARAM_B = 3.3f;
    const float HGD_DISTANCE_CLOSED_HAND_PARAM_C = 9.7f;
    const float HGD_DISTANCE_MIN = 0.2f;
    const float HGD_DISTANCE_MAX = 0.8f;
    const int HGD_AVG_DISTANCE_NUM_SAMPLES = 5;

    int m_HandDistAvgSamples;

    // Last estimated hand distance from camera
    float m_OpenHandDist;
    float m_ClosedHandDist;

    // Common math constants
    const float MATH_NATURAL_LOG_EPSILON = 2.71828f;

    Camera m_Camera;

    ARCameraManager m_CameraManager;

    LogDisplay m_LogDisplay;

    MiniDisplay m_MiniDisplay;
}
