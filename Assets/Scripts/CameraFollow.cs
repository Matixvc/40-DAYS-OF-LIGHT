using UnityEngine;

public class CameraFollow : MonoBehaviour
{
    [Header("Objetivo")]
    [SerializeField] private Transform target;

    [Header("Configuración de Posición")]
    [SerializeField] private Vector3 offset = new Vector3(0f, 12f, -8f);
    [SerializeField] private float smoothSpeed = 5f;

    private void Start()
    {
        if (target == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null) target = player.transform;
        }
    }

    private void LateUpdate()
    {
        if (target == null) return;

        // Posición deseada con el Offset isometrico/Top-Down
        Vector3 desiredPosition = target.position + offset;

        // Movimiento suave hacia la posición objetivo
        Vector3 smoothedPosition = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        transform.position = smoothedPosition;

        // Mantener el ángulo de visión fijo hacia el objetivo
        transform.LookAt(target.position + Vector3.up * 1f);
    }
}