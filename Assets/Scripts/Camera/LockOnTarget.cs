// LockOnTarget.cs (Modificado)
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Events;

[RequireComponent(typeof(Collider))]
public class LockOnTarget : MonoBehaviour
{
    private ThirdPersonCamera thirdPersonCamera;
    // --- MODIFICADO: Referencia a la interfaz ---
    private ILockOnVisuals lockOnVisualsTarget;
    // --- FIN MODIFICADO ---

    // --- Eliminada referencia específica a EnemyIAVillage ---
    // private EnemyIAVillage enemyAI;
    // ---------------------------------------------------

    [Header("Settings")]
    public float Radius = 15f;
    public LayerMask TargetMask;
    public LayerMask ObstructionMask;
    public float viewOffset = 1.0f;

    [Header("Targeting")]
    public Transform TargetLocked { get; private set; }
    public List<Transform> Targets { get; private set; } = new List<Transform>();

    [Header("Events")]
    public UnityEvent<Transform> OnTargetLocked;
    public UnityEvent OnTargetCleared;

    [SerializeField] private Transform targetPoint;
    [SerializeField] private bool useCustomTargetPoint = false;
    [SerializeField] private Vector3 targetOffset = Vector3.up;
    [SerializeField] private bool autoAdjustTargetPoint = true;
    [SerializeField] [Range(0.3f, 0.8f)] private float heightRatio = 0.65f;

    [Header("Camera Control")]
    [SerializeField] private Vector3 cameraAdditionalOffset = Vector3.zero;

    private Transform cachedTransform;
    private Vector3 actualTargetPoint;
    private Collider myCollider;
    private float colliderHeight;

    private void Awake()
    {
        cachedTransform = transform;
        myCollider = GetComponent<Collider>();

        // --- MODIFICADO: Obtener referencia a la interfaz ---
        lockOnVisualsTarget = GetComponent<ILockOnVisuals>();
        if (lockOnVisualsTarget == null)
        {
            // Intentar buscar en hijos por si acaso, aunque normalmente estará en el mismo objeto
            lockOnVisualsTarget = GetComponentInChildren<ILockOnVisuals>();
            if (lockOnVisualsTarget == null)
            {
                Debug.LogWarning($"LockOnTarget en {gameObject.name} no encontró un componente que implemente ILockOnVisuals (como EnemyIAVillage o SleepingEnemyController). El cambio de material no funcionará.", this);
            }
        }
        // --- FIN MODIFICADO ---


        // --- Eliminada obtención específica de EnemyIAVillage ---
        // enemyAI = GetComponent<EnemyIAVillage>();
        // if (enemyAI == null) { ... }
        // ---------------------------------------------------

        CalculateTargetPointPosition();
    }

    void Start()
    {
        thirdPersonCamera = FindAnyObjectByType<ThirdPersonCamera>();
        if (thirdPersonCamera == null)
        {
             Debug.LogError("LockOnTarget: No se pudo encontrar el script ThirdPersonCamera.", this);
        }
    }

    private void CalculateTargetPointPosition()
    {
        if (useCustomTargetPoint && targetPoint != null) {
            actualTargetPoint = targetPoint.position;
            return;
        }
        if (autoAdjustTargetPoint && myCollider != null) {
            Vector3 adjustedOffset = targetOffset;
            Bounds bounds = myCollider.bounds;
            colliderHeight = bounds.size.y;
            adjustedOffset.y = colliderHeight * heightRatio;
            adjustedOffset.y = Mathf.Max(adjustedOffset.y, 0.5f);
            actualTargetPoint = bounds.center + new Vector3(0, adjustedOffset.y - (colliderHeight * 0.5f), 0);
        } else {
            actualTargetPoint = cachedTransform.position + targetOffset;
        }
    }

    private void Update() { CalculateTargetPointPosition(); }
    public Vector3 GetTargetPoint() { return actualTargetPoint; }
    public Vector3 GetCameraAdditionalOffset() { return cameraAdditionalOffset; }

    public bool IsValidTarget()
    {
        // --- MODIFICADO: Comprobación genérica (si es un MonoBehaviour) ---
        // Comprobamos si el componente que implementa la interfaz es un MonoBehaviour
        // y si su gameObject está activo. Puedes añadir lógica más específica si necesitas
        // chequear estados concretos de diferentes tipos de enemigos.
        MonoBehaviour visualTargetMono = lockOnVisualsTarget as MonoBehaviour;
        if (visualTargetMono != null)
        {
            return visualTargetMono.enabled && visualTargetMono.gameObject.activeInHierarchy;
        }
        // Si no es un MonoBehaviour o no hay target, no es válido
        return lockOnVisualsTarget != null; // Es válido si existe la interfaz (luego la cámara puede decidir si se ve, etc.)
        // ----------------------------------------------------------------
    }

    // --- Modificado para usar la interfaz ---
    public void UpdateLockOnVisual(bool isActive)
    {
        if (lockOnVisualsTarget != null)
        {
            lockOnVisualsTarget.SetLockOnMaterial(isActive);
        }
    }
    // ------------------------------------

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 targetPos = useCustomTargetPoint && targetPoint != null
            ? targetPoint.position
            : (Application.isPlaying ? actualTargetPoint : transform.position + targetOffset);
        Gizmos.DrawWireSphere(targetPos, 0.2f);
        Gizmos.DrawLine(transform.position, targetPos);
        if (myCollider != null) {
             Gizmos.color = new Color(0, 1, 0, 0.3f);
             Gizmos.DrawWireCube(myCollider.bounds.center, myCollider.bounds.size);
        }
    }
}