using System;
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

    public GameObject SpherePrefab
    {
        get { return m_SpherePrefab; }
        set { m_SpherePrefab = value; }
    }

    public GameObject sphere { get; private set; }

#if !UNITY_EDITOR && UNITY_IOS
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
        while (Interlocked.Exchange(ref m_HandLandmarksLock, 1) != 0) {}

        Debug.Log("HGD: Received LandmarkPkt @ " + hlmPkt);
        Marshal.Copy(hlmPkt, m_HandLandmarks, 0, m_HandLandmarks.Length);

        Interlocked.Exchange(ref m_HandLandmarksLock, 0);

        int num_hands = (int)m_HandLandmarks[HGD_HLM_PKT_NUM_HANDS_OFFSET];
        Debug.Log("\tNumber of hand instances with landmarks: " + num_hands);

#if DEBUG
        for (int hand_index = 0; hand_index < num_hands; hand_index++)
        {
            int num_hlm = (int)m_HandLandmarks[HGD_HLM_PKT_NUM_HAND_LANDMARKS_OFFSET + hand_index];
            Debug.Log("\tNumber of landmarks for hand[" + hand_index + "]: " + num_hlm);

            for (int i = 0; i < num_hlm; i++)
            {
                int lm_index = (int)(HGD_HLM_PKT_HEADER_LEN + (hand_index * HGD_HLM_PKT_NUM_HAND_LANDMARKS * HGD_HLM_PKT_HAND_LANDMARK_LEN) + i);
                Debug.Log(@"\t\tLandmark[" + i + "]: (" + m_HandLandmarks[lm_index + HGD_HLM_PKT_HAND_LANDMARK_X_OFFSET] + ", " + m_HandLandmarks[lm_index + HGD_HLM_PKT_HAND_LANDMARK_Y_OFFSET] + ", " + m_HandLandmarks[lm_index + HGD_HLM_PKT_HAND_LANDMARK_Z_OFFSET] + ")");
            }
        }
#endif // DEBUG
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
        ProcessHandLandmarks();
    }

    private void ProcessHandLandmarks()
    {
        if (Interlocked.Exchange(ref m_HandLandmarksLock, 1) != 0)
            return;

        int num_hands = (int)m_HandLandmarks[HGD_HLM_PKT_NUM_HANDS_OFFSET];
        if (num_hands == 0)
        {
            Interlocked.Exchange(ref m_HandLandmarksLock, 0);
            return;
        }

        Vector2 screenCenter;
        screenCenter.x = Screen.width / 2;
        screenCenter.y = Screen.height / 2;

        var indexTipOffset = HGD_HLM_PKT_HEADER_LEN + HGD_HLM_PKT_HAND_LANDMARK_INDEX_TIP_OFFSET; 
        var indexTipScreenPos = new Vector2(
            (1 - m_HandLandmarks[indexTipOffset + HGD_HLM_PKT_HAND_LANDMARK_X_OFFSET]) * Screen.width,
            (1 - m_HandLandmarks[indexTipOffset + HGD_HLM_PKT_HAND_LANDMARK_Y_OFFSET]) * Screen.height);
        indexTipScreenPos -= screenCenter;

        Interlocked.Exchange(ref m_HandLandmarksLock, 0);

        var position = new Vector3(indexTipScreenPos.x, indexTipScreenPos.y, 20f);

        if (!sphere)
        {
            sphere = Instantiate(SpherePrefab, position, Quaternion.identity);
            Debug.Log("HGD: Sphere instantiated at " + sphere.transform.position);
        }
        else
        {
            sphere.transform.position = position;
            Debug.Log("HGD: Sphere moved at " + sphere.transform.position);
        }

    }

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

    // LMs
    private const int HGD_HLM_PKT_HAND_LANDMARK_INDEX_TIP_OFFSET = 8;

    private const int HGD_HLM_PKT_LEN = HGD_HLM_PKT_HEADER_LEN + (HGD_HLM_PKT_NUM_HANDS * HGD_HLM_PKT_NUM_HAND_LANDMARKS * HGD_HLM_PKT_HAND_LANDMARK_LEN);

    private static int m_HandLandmarksLock = 0;
    private static float[] m_HandLandmarks = new float[HGD_HLM_PKT_LEN];

    private static Camera m_Camera;

    private static ARCameraManager m_CameraManager;

    private static MiniDisplay m_MiniDisplay;
}
