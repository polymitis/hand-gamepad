using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

/// <summary>
/// Listens for touch events and performs an AR raycast from the screen touch point.
/// AR raycasts will only hit detected trackables like feature points and planes.
///
/// If a raycast hits a trackable, the <see cref="placedPrefab"/> is instantiated
/// and moved to the hit position.
/// </summary>
[RequireComponent(typeof(ARRaycastManager))]
public class NavigatePlane : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Instantiates this prefab on a plane at the touch location.")]
    GameObject m_PlacedPrefab;

    /// <summary>
    /// The prefab to instantiate on touch.
    /// </summary>
    public GameObject placedPrefab
    {
        get { return m_PlacedPrefab; }
        set { m_PlacedPrefab = value; }
    }

    /// <summary>
    /// The object instantiated as a result of a successful raycast intersection with a plane.
    /// </summary>
    public GameObject spawnedObject { get; private set; }

    void Awake()
    {
        m_RaycastManager = GetComponent<ARRaycastManager>();
    }

    bool TryGetTouchPosition(out Vector2 position)
    {
        if (Input.touchCount > 0)
        {
            position = Input.GetTouch(0).position;
            return true;
        }

        position = default;
        return false;
    }

    bool TryGetPinchDistance(out float zoom)
    {
        if (Input.touchCount > 1)
        {
            Debug.Log("NavigatePlane: Pinch detected");
            
            var touch0 = Input.GetTouch(0).position;
            var touch1 = Input.GetTouch(1).position;
            var dist0 = Vector2.Distance(touch0, touch1);
            Debug.Log("NavigatePlane: Pinch 0 distance " + dist0);

            var delta0 = Input.GetTouch(0).deltaPosition;
            var delta1 = Input.GetTouch(1).deltaPosition;
            var dist1 = Vector2.Distance(touch0 - delta0, touch1 - delta1);
            Debug.Log("NavigatePlane: Pinch -1 distance " + dist1);

            zoom = dist0 - dist1;
            Debug.Log("NavigatePlane: Pinch zoom " + zoom);

            return true;
        }

        zoom = default;
        return false;
    }

    void Update()
    {
        if (TryGetTouchPosition(out Vector2 position))
        {
            if (m_RaycastManager.Raycast(position, m_Hits, TrackableType.PlaneWithinPolygon))
            {
                // Raycast hits are sorted by distance, so the first one
                // will be the closest hit.
                var hitPose = m_Hits[0].pose;

                if (spawnedObject == null)
                {
                    spawnedObject = Instantiate(m_PlacedPrefab, hitPose.position, hitPose.rotation);
                }
            }
        }

        if (TryGetPinchDistance(out float zoom))
        {
            if (zoom > 0f)
            {
                Camera.main.transform.Translate(0, 0, 1 * m_zoomSpeed, Space.World);
                Debug.Log("NavigatePlane: Zoom in " + Camera.main.transform.localPosition);
            }
            else if (zoom < 0f)
            {
                Camera.main.transform.Translate(0, 0, -1 * m_zoomSpeed, Space.World);
                Debug.Log("NavigatePlane: Zoom out " + Camera.main.transform.localPosition);
            }
        }
    }

    private static readonly List<ARRaycastHit> m_Hits = new List<ARRaycastHit>();
    private ARRaycastManager m_RaycastManager;
    private static readonly float m_zoomSpeed = 1.01f;
}
