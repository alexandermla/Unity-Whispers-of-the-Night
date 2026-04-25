using UnityEngine;
using UnityEngine.AI;
using WoN.Interfaces;

public class EnemyIA2 : MonoBehaviour, ILightReactive
{
    [Header("Referencias")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource audioSource;
    
    [Header("Configuración de Persecución")]
    [SerializeField] private float chaseSpeed = 3.5f;
    [SerializeField] private float captureDistance = 1.5f;
    [SerializeField] private float breathingSoundInterval = 3.0f;
    [SerializeField] private float stunDuration = 3.0f;
    
    [Header("Audio")]
    [SerializeField] private AudioClip[] breathingSounds;
    [SerializeField] private AudioClip captureSound;
    [SerializeField] private AudioClip roarSound; // Añadido para el rugido

    [Header("Game Over")]
    [SerializeField] private GameObject gameOverPanel;
    
    private Transform playerTransform;
    private bool isChasing = false;
    private bool isStunned = false;
    private float breathingTimer = 0f;
    private float stunTimer = 0f;
    
    // Nombres de los parámetros del Animator exactamente como configurados
    private readonly int turnTriggerHash = Animator.StringToHash("Turn");
    private readonly int roarTriggerHash = Animator.StringToHash("Roar");
    private readonly int chaseTriggerHash = Animator.StringToHash("Chase");
    private readonly int walkFloatHash = Animator.StringToHash("Walk");
    private readonly int blockedBoolHash = Animator.StringToHash("Blocked");
    private readonly int attackTriggerHash = Animator.StringToHash("Attack");
    
    protected virtual void Awake()
    {
        if (agent == null)
        {
            agent = GetComponent<NavMeshAgent>();
        }
        
        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }
        
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
        }
        
        // Desactivar inicialmente
        if (agent != null)
        {
            agent.enabled = false;
        }
    }
    
    protected virtual void Start()
    {
        // Ocultar renderers inicialmente
        SetRenderersEnabled(false);
    }
    
    protected virtual void Update()
    {
        if (isStunned)
        {
            // Gestionar tiempo de aturdimiento
            stunTimer -= Time.deltaTime;
            if (stunTimer <= 0)
            {
                isStunned = false;
                
                // Reanudar persecución
                if (agent != null)
                {
                    agent.isStopped = false;
                    
                    // Actualizar animator - usar el parámetro "Blocked" para indicar que ya no está bloqueado
                    if (animator != null)
                    {
                        animator.SetBool(blockedBoolHash, false);
                        
                        // Reactivar la persecución
                        animator.SetTrigger(chaseTriggerHash);
                    }
                }
            }
            return;
        }
        
        if (isChasing && playerTransform != null && agent != null && agent.enabled)
        {
            // Actualizar destino
            agent.SetDestination(playerTransform.position);
            
            // Verificar si ha alcanzado al jugador
            if (Vector3.Distance(transform.position, playerTransform.position) <= captureDistance)
            {
                CapturePlayer();
            }
            
            // Reproducir sonido de respiración periódicamente
            breathingTimer -= Time.deltaTime;
            if (breathingTimer <= 0 && breathingSounds.Length > 0)
            {
                PlayBreathingSound();
                breathingTimer = breathingSoundInterval;
            }
            
            // Actualizar animaciones
            UpdateAnimations();
        }
    }
    
    public void StartChasing(Transform target)
    {
        playerTransform = target;
        isChasing = true;
        
        if (agent != null)
        {
            agent.enabled = true;
            agent.speed = chaseSpeed;
            agent.isStopped = false;
        }
        
        // Activar renderers
        SetRenderersEnabled(true);
        
        // Iniciar animación de persecución - activar trigger "Chase"
        if (animator != null)
        {
            animator.SetTrigger(chaseTriggerHash);
        }
        
        Debug.Log("Enemy started chasing player");
    }
    
    // Original method kept for backward compatibility
    public void OnLightExposure()
    {
        // Call the interface method with default parameters
        OnLightExposure(null, 1.0f);
    }
    
    // Implement the ILightReactive interface method
    public virtual void OnLightExposure(Transform lightSource = null, float intensity = 1.0f)
    {
        // Si es alcanzado por la luz de la linterna
        if (!isStunned && isChasing)
        {
            isStunned = true;
            stunTimer = stunDuration;
            
            // Detener movimiento
            if (agent != null)
            {
                agent.isStopped = true;
            }
            
            // Activar animación de aturdimiento - usar el parámetro "Blocked"
            if (animator != null)
            {
                animator.SetBool(blockedBoolHash, true);
            }
            
            Debug.Log("Enemy stunned by flashlight");
        }
    }
    
    private void CapturePlayer()
    {
        isChasing = false;
        
        // Detener agente
        if (agent != null)
        {
            agent.isStopped = true;
        }
        
        // Reproducir sonido de captura
        if (audioSource != null && captureSound != null)
        {
            audioSource.PlayOneShot(captureSound);
        }
        
        // Activar animación de captura - usar el trigger "Attack"
        if (animator != null)
        {
            animator.SetTrigger(attackTriggerHash);
        }
        
        // Mostrar Game Over
        if (gameOverPanel != null)
        {
            // Detener el tiempo
            Time.timeScale = 0;
            gameOverPanel.SetActive(true);
        }
        
        Debug.Log("Player captured");
    }
    
    private void UpdateAnimations()
    {
        if (animator != null && agent != null)
        {
            // Actualizar el parámetro Walk para controlar la velocidad de la animación Run
            float normalizedSpeed = agent.velocity.magnitude / agent.speed;
            animator.SetFloat(walkFloatHash, normalizedSpeed);
        }
    }
    
    // Método para activar la animación de giro desde la secuencia de la puerta
    public void PerformTurn()
    {
        if (animator != null)
        {
            animator.SetTrigger(turnTriggerHash);
            Debug.Log("Enemy turn animation triggered");
        }
    }

    public void TurnToTarget(Transform targetTransform, float turnDuration = 2.0f)
    {
        if (animator != null)
        {
            // Activar la animación de giro
            animator.SetTrigger(turnTriggerHash);
            Debug.Log("Enemy turn animation triggered towards specific target");
        }
        
        // Iniciar corrutina para girar suavemente hacia el objetivo
        StartCoroutine(RotateTowardsTarget(targetTransform, turnDuration));
    }

    private System.Collections.IEnumerator RotateTowardsTarget(Transform targetTransform, float duration)
    {
        float elapsedTime = 0;
        
        // Guardar rotación inicial
        Quaternion startRotation = transform.rotation;
        
        // Calcular rotación objetivo (mirando hacia el target)
        Vector3 directionToTarget = (targetTransform.position - transform.position).normalized;
        directionToTarget.y = 0; // Mantener el giro solo en el eje Y
        
        Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
        
        // Girar gradualmente
        while (elapsedTime < duration)
        {
            transform.rotation = Quaternion.Slerp(startRotation, targetRotation, elapsedTime / duration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        
        // Asegurar rotación final exacta
        transform.rotation = targetRotation;
        
        Debug.Log("Enemy completed turn towards target");
    }
    
    // Método para activar la animación de rugido desde la secuencia de la puerta
    public void PerformRoar()
    {
        if (animator != null)
        {
            animator.SetTrigger(roarTriggerHash);
            Debug.Log("Enemy roar animation triggered");

            // Reproducir sonido de rugido
            if (audioSource != null && roarSound != null)
            {
                audioSource.PlayOneShot(roarSound);
            }
        }
    }
    
    private void PlayBreathingSound()
    {
        if (audioSource != null && breathingSounds.Length > 0)
        {
            int index = Random.Range(0, breathingSounds.Length);
            audioSource.clip = breathingSounds[index];
            audioSource.Play();
        }
    }
    
    protected void SetRenderersEnabled(bool enabled)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        foreach (Renderer r in renderers)
        {
            r.enabled = enabled;
        }
    }
    
    // New methods for statue fountain sequence
    
    /// <summary>
    /// Increases the enemy's movement speed by the specified multiplier
    /// </summary>
    /// <param name="multiplier">Speed multiplier (e.g., 1.5 = 50% faster)</param>
    public void IncreaseSpeed(float multiplier)
    {
        if (agent != null)
        {
            agent.speed *= multiplier;
            chaseSpeed *= multiplier;
            
            Debug.Log($"Enemy {name} speed increased - New chase speed: {chaseSpeed}");
        }
    }
    
    /// <summary>
    /// Increases the enemy's detection radius by the specified multiplier
    /// </summary>
    /// <param name="multiplier">Radius multiplier (e.g., 1.5 = 50% larger detection radius)</param>
    public void IncreaseDetectionRadius(float multiplier)
    {
        captureDistance *= multiplier;
        
        Debug.Log($"Enemy {name} detection increased - New capture distance: {captureDistance}");
    }
    
    /// <summary>
    /// Resets the enemy's speed and detection values to their original settings
    /// </summary>
    public void ResetAggressiveness()
    {
        if (agent != null)
        {
            agent.speed = chaseSpeed;
        }
        
        // Reset to original values if needed
        // (Original values would need to be stored as separate fields)
    }
}