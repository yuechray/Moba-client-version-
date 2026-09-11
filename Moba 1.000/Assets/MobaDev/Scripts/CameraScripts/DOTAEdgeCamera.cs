using UnityEngine;
using Unity.Cinemachine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CinemachineVirtualCamera))]
public class DOTAEdgeCamera : MonoBehaviour
{
    [Header("Edge Scroll")]
    [Tooltip("Скорость панорамирования при наведении на край экрана")]
    public float edgeSpeed = 18f;

    [Tooltip("Ширина зоны у края экрана (в пикселях)")]
    public float edgeZone = 40f;

    [Header("Drag Pan")]
    [Tooltip("Скорость перемещения при зажатой средней/правой кнопке мыши")]
    public float dragSpeed = 0.6f;

    [Header("Follow")]
    [Tooltip("Объект, за которым следует камера (капсула/герой)")]
    public Transform followTarget;

    [Tooltip("Сила следования за героем")]
    public float followLerp = 3.5f;

    [Header("Границы карты")]
    public Vector2 mapMin = new Vector2(-10, -10);
    public Vector2 mapMax = new Vector2(522, 522);

    private CinemachineVirtualCamera vcam;
    private CinemachineOrbitalTransposer transposer;
    private Vector3 desiredPosition;

    void Start()
    {
        vcam = GetComponent<CinemachineVirtualCamera>();
        transposer = vcam.GetCinemachineComponent<CinemachineOrbitalTransposer>();

        if (followTarget && transposer != null)
        {
            // Фиксированное расстояние и высота
            transposer.m_FollowOffset = new Vector3(0f, 22f, -32f); // Y=22 высота, Z=-32 назад

            // Начальная позиция
            desiredPosition = followTarget.position;
        }
        else
        {
            Debug.LogWarning("FollowTarget или transposer не найден!");
        }
    }

    void Update()
    {
        HandleEdgeScroll();
        HandleDrag();
        HandleFollow();
    }

    private void HandleEdgeScroll()
    {
        Vector2 mouse = Mouse.current.position.ReadValue();
        Vector2 screen = new Vector2(Screen.width, Screen.height);

        Vector3 move = Vector3.zero;

        if (mouse.x < edgeZone)           move.x = -1;
        if (mouse.x > screen.x - edgeZone) move.x = 1;
        if (mouse.y < edgeZone)           move.z = -1;
        if (mouse.y > screen.y - edgeZone) move.z = 1;

        if (move != Vector3.zero)
        {
            desiredPosition += (transform.right * move.x + transform.forward * move.z) * edgeSpeed * Time.deltaTime;
        }
    }

    private void HandleDrag()
    {
        if (Mouse.current.middleButton.isPressed || Mouse.current.rightButton.isPressed)
        {
            Vector2 delta = Mouse.current.delta.ReadValue();
            desiredPosition -= transform.right * delta.x * dragSpeed;
            desiredPosition -= transform.forward * delta.y * dragSpeed;
        }
    }

    private void HandleFollow()
    {
        if (followTarget)
        {
            desiredPosition = Vector3.Lerp(desiredPosition, followTarget.position, Time.deltaTime * followLerp);
        }
    }

    void LateUpdate()
    {
        // Границы карты
        desiredPosition.x = Mathf.Clamp(desiredPosition.x, mapMin.x, mapMax.x);
        desiredPosition.z = Mathf.Clamp(desiredPosition.z, mapMin.y, mapMax.y);

        // Обновляем позицию камеры
        if (followTarget)
        {
            vcam.transform.position = desiredPosition + transposer.m_FollowOffset;
        }
    }
}