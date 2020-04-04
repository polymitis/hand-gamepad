using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

[RequireComponent(typeof(ARRaycastManager))]
public class NavigatePlane : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Instantiates this prefab on a plane at the touch location.")]
    GameObject m_PlanePrefab;

    public GameObject planePrefab
    {
        get { return m_PlanePrefab; }
        set { m_PlanePrefab = value; }
    }

    public GameObject plane { get; private set; }

    [SerializeField]
    [Tooltip("Instantiates this prefab on a plane at the touch location.")]
    GameObject m_CubePrefab;

    public GameObject cubePrefab
    {
        get { return m_CubePrefab; }
        set { m_CubePrefab = value; }
    }

    public GameObject cube { get; private set; }

    void Awake()
    {
        m_RaycastManager = GetComponent<ARRaycastManager>();
        m_SessionOrigin = GetComponent<ARSessionOrigin>();
        m_Camera = GameObject.FindGameObjectWithTag("MainCamera").GetComponent<Camera>();
    }

    bool TryAcceptingHaptic(in HapticType type)
    {
        if (m_CanditateHapticControl != type)
            m_NumHaptics = 0;

        m_CanditateHapticControl = type;

        if (m_PreviousHapticControl != type && (m_NumHaptics = ++m_NumHaptics % m_ValidMinHaptics) == 0)
            m_PreviousHapticControl = type;

        return (m_PreviousHapticControl == type);
    }

    bool TryGetTouch(out Vector2 position)
    {
        if (Input.touchCount == 1)
        {
            position = Input.GetTouch(0).position;

            return TryAcceptingHaptic(HapticType.Touch);
        }

        position = default;
        return false;
    }

    bool TryGetPinch(out float zoom, out float angle, out Vector2 center)
    {
        if (Input.touchCount == 2)
        {
            var touch0 = Input.GetTouch(0).position;
            var touch1 = Input.GetTouch(1).position;
            var dist0 = Vector2.Distance(touch0, touch1);
            center = (touch0 + touch1) / 2;

            var delta0 = Input.GetTouch(0).deltaPosition;
            var delta1 = Input.GetTouch(1).deltaPosition;
            var angle0 = 2 * Mathf.Atan2(Vector2.Distance(touch0, touch0 - delta0), Vector2.Distance(center, touch0));
            var angle1 = 2 * Mathf.Atan2(Vector2.Distance(touch1, touch1 - delta1), Vector2.Distance(center, touch1));
            angle = Mathf.FloorToInt((angle0 + angle1) * 100);
            angle = (angle > 5) ? angle : 0;
            if (touch0.x < (Screen.width / 2))
                angle = (touch0.y > (touch0 - delta0).y) ? angle : -angle;
            else
                angle = (touch0.y < (touch0 - delta0).y) ? angle : -angle;

            var dist1 = Vector2.Distance(touch0 - delta0, touch1 - delta1);
            zoom = (Mathf.Abs(dist0 - dist1) > 20) ? dist0 - dist1 : 0;

            return TryAcceptingHaptic(HapticType.Pinch);
        }

        angle = default;
        center = default;
        zoom = default;
        return false;
    }

    void Update()
    {
        if (!plane)
        {
            Vector2 screenCenter;
            screenCenter.x = Screen.width / 2;
            screenCenter.y = Screen.height / 2;

            if (m_RaycastManager.Raycast(screenCenter, m_Hits, TrackableType.PlaneWithinPolygon))
            {
                var hitPose = m_Hits[0].pose;

                plane = Instantiate(m_PlanePrefab, hitPose.position, hitPose.rotation);
                m_SessionOrigin.MakeContentAppearAt(plane.transform,
                    plane.transform.position,
                    plane.transform.rotation);
                Debug.Log("NavigatePlane: Plane instantiated at " + hitPose.position);
            }
        }

        if (TryGetTouch(out Vector2 touchPos))
        {
            var ray = m_Camera.ScreenPointToRay(touchPos);

            RaycastHit hit;
            if (Physics.Raycast(ray.origin, ray.direction, out hit))
            {
                var position = new Vector3(hit.transform.position.x, plane.transform.position.y, hit.transform.position.z);

                if (!cube)
                {
                    cube = Instantiate(cubePrefab, position, Quaternion.identity, plane.transform);
                    Debug.Log("NavigatePlane: Cube instantiated at " + cube.transform.position);
                }
                else
                {
                    cube.transform.position = position;
                    Debug.Log("NavigatePlane: Cube moved at " + cube.transform.position);
                }
            }
        }

        if (TryGetPinch(out float pinchZoom, out float pinchAngle, out Vector2 pinchCenter))
        {
            var ray = m_Camera.ScreenPointToRay(pinchCenter);
            
            if (pinchZoom > 0f)
            {
                m_SessionOrigin.transform.localPosition += ray.direction * m_zoomSpeed;
                Debug.Log("NavigatePlane: Zoom in " + m_SessionOrigin.transform.localPosition);
            }
            else if (pinchZoom < 0f)
            {
                m_SessionOrigin.transform.localPosition -= ray.direction * m_zoomSpeed;
                Debug.Log("NavigatePlane: Zoom out " + m_SessionOrigin.transform.localPosition);
            }
            else if (pinchAngle > 0f)
            {
                var angle = plane.transform.rotation.eulerAngles.y + m_angularSpeed;
                plane.transform.rotation = Quaternion.Euler(0, angle, 0);
                Debug.Log("NavigatePlane: Rotate +Y at angle " + plane.transform.rotation.eulerAngles.y);
            }
            else if (pinchAngle < 0f)
            {
                var angle = plane.transform.rotation.eulerAngles.y - m_angularSpeed;
                plane.transform.rotation = Quaternion.Euler(0, angle, 0);
                Debug.Log("NavigatePlane: Rotate -Y at angle " + plane.transform.rotation.eulerAngles.y);
            }
        }
    }

    private static readonly float m_angularSpeed = 2f;

    private Camera m_Camera;

    private static readonly List<ARRaycastHit> m_Hits = new List<ARRaycastHit>();

    private static int m_NumHaptics = 0;
    private static readonly int m_ValidMinHaptics = 10;

    private enum HapticType
    {
        None = 0,
        Touch,
        Pinch,
    };

    private static HapticType m_CanditateHapticControl = HapticType.None;
    private static HapticType m_PreviousHapticControl = HapticType.None;

    private ARRaycastManager m_RaycastManager;

    private ARSessionOrigin m_SessionOrigin;

    private static readonly float m_zoomSpeed = 0.05f;
}
