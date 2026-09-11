using UnityEngine;
using MobaDev.Network;

public class MobaCamera : MonoBehaviour
{
    [Header("Угол и высота (Dota-стиль)")]
    [SerializeField] private Vector3 offset      = new Vector3(0f, 22f, -32f);
    [SerializeField] private float   smoothSpeed = 10f;

    private float   edgeScrollSpeed = 28f;
    private float   edgeZone        = 40f;
    private Vector2 mapMin          = new Vector2(-5f,   -5f);
    private Vector2 mapMax          = new Vector2(515f, 515f);

    private Vector3   _focusPoint;
    private Transform _target;

    void Start()
    {
        var brain = GetComponent("CinemachineBrain") as Behaviour;
        if (brain != null) brain.enabled = false;

        Cursor.visible   = true;
        Cursor.lockState = CursorLockMode.None;
    }

    void LateUpdate()
    {
        if (_target == null)
        {
            var ctrl = SpacetimeNetworkManager.Instance?.GetLocalChampion();
            if (ctrl == null) return;

            _target     = ctrl.transform;
            _focusPoint = _target.position;
            transform.position = _focusPoint + offset;
            transform.LookAt(_focusPoint);
        }

        if (Input.GetKeyDown(KeyCode.F1))
            _focusPoint = _target.position;

        HandleEdgeScroll();

        _focusPoint.x = Mathf.Clamp(_focusPoint.x, mapMin.x, mapMax.x);
        _focusPoint.z = Mathf.Clamp(_focusPoint.z, mapMin.y, mapMax.y);

        transform.position = Vector3.Lerp(transform.position, _focusPoint + offset, Time.deltaTime * smoothSpeed);
        transform.LookAt(_focusPoint);
    }

    private void HandleEdgeScroll()
    {
        var mouse = Input.mousePosition;
        var move  = Vector3.zero;

        if (mouse.x < edgeZone)                      move.x = -1f;
        else if (mouse.x > Screen.width  - edgeZone) move.x =  1f;

        if (mouse.y < edgeZone)                      move.z = -1f;
        else if (mouse.y > Screen.height - edgeZone) move.z =  1f;

        if (move != Vector3.zero)
            _focusPoint += move * edgeScrollSpeed * Time.deltaTime;
    }
}
