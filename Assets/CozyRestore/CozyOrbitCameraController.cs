using System.IO;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class CozyOrbitCameraController : MonoBehaviour
{
    public Camera viewCamera;
    public Vector3 target = new Vector3(0f, 1f, 0f);
    public float distance = 14f;
    public float minDistance = 8f;
    public float maxDistance = 17.5f;
    public float yaw = 50f;
    public float pitch = 21f;
    public float orbitSensitivity = 2.5f;
    public float panSpeed = 5.5f;
    public float zoomSpeed = 2f;
    public Vector3 panMin = new Vector3(-4.8f, 0.8f, -2.8f);
    public Vector3 panMax = new Vector3(3.4f, 2.4f, 3.7f);

    private void Awake()
    {
        if (viewCamera == null)
        {
            viewCamera = Camera.main;
        }
        RefreshTransform();
    }

    private void Update()
    {
        HandleKeyboardPan();
        HandleMouseOrbit();
        HandleZoom();
        RefreshTransform();
    }

    private void HandleKeyboardPan()
    {
        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");
        Vector3 forward = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
        Vector3 right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
        forward.y = 0f;
        right.y = 0f;
        target += (forward.normalized * vertical + right.normalized * horizontal) * panSpeed * Time.deltaTime;

        if (Input.GetKey(KeyCode.R))
        {
            target += Vector3.up * panSpeed * 0.5f * Time.deltaTime;
        }
        if (Input.GetKey(KeyCode.F))
        {
            target += Vector3.down * panSpeed * 0.5f * Time.deltaTime;
        }
    }

    private void HandleMouseOrbit()
    {
        CozyToolController tools = GetComponent<CozyToolController>();
        if (tools != null && tools.BlocksRightMouseOrbit)
        {
            return;
        }

        if (Input.GetMouseButton(1))
        {
            yaw += Input.GetAxis("Mouse X") * orbitSensitivity * 6f;
            pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * orbitSensitivity * 3f, 12f, 55f);
        }

        if (Input.GetMouseButton(2))
        {
            Vector3 right = viewCamera.transform.right;
            Vector3 up = Vector3.up;
            target -= right * Input.GetAxis("Mouse X") * orbitSensitivity * 0.08f * distance;
            target -= up * Input.GetAxis("Mouse Y") * orbitSensitivity * 0.05f * distance;
        }
    }

    private void HandleZoom()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f)
        {
            distance = Mathf.Clamp(distance - scroll * zoomSpeed, minDistance, maxDistance);
        }

        target = new Vector3(
            Mathf.Clamp(target.x, panMin.x, panMax.x),
            Mathf.Clamp(target.y, panMin.y, panMax.y),
            Mathf.Clamp(target.z, panMin.z, panMax.z));
    }

    private void RefreshTransform()
    {
        if (viewCamera == null)
        {
            return;
        }

        Quaternion rotation = Quaternion.Euler(pitch, yaw, 0f);
        transform.position = target;
        transform.rotation = rotation;
        viewCamera.transform.position = target - rotation * Vector3.forward * distance;
        viewCamera.transform.rotation = rotation;
    }
}
