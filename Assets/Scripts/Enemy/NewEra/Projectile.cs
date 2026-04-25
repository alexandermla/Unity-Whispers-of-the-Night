using UnityEngine;

public class Projectile : MonoBehaviour
{
    public float speed = 15f;
    public float lifeTime = 5f; // Tiempo antes de autodestruirse
    public float damage = 10f;
    public GameObject owner; // Quién disparó este proyectil (para evitar auto-daño)

    private Rigidbody rb;
    private Vector3 direction;

    // Usar Initialize en lugar de Start para pasar datos al instanciar
    public void Initialize(Vector3 dir, float projSpeed, float dmg, GameObject projectileOwner)
    {
        direction = dir;
        speed = projSpeed;
        damage = dmg;
        owner = projectileOwner;

        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = direction * speed;
        } else { // Si no tiene Rigidbody, moverlo manualmente
             StartCoroutine(MoveProjectile());
        }

        // Destruir después de un tiempo
        Destroy(gameObject, lifeTime);
    }

    // Movimiento manual si no usa Rigidbody
    System.Collections.IEnumerator MoveProjectile() {
         while(true) {
             transform.position += direction * speed * Time.deltaTime;
             yield return null;
         }
    }


    void OnCollisionEnter(Collision collision)
    {
         // Evitar colisionar con quien lo lanzó inmediatamente
        if (collision.gameObject == owner)
        {
            // Podrías ignorar la colisión por un breve instante si es necesario
            // Physics.IgnoreCollision(GetComponent<Collider>(), collision.collider);
            // return;
        }


        // Intenta aplicar daño si colisiona con algo que tenga vida (ej. PlayerHealth)
        // PlayerHealth playerHealth = collision.gameObject.GetComponent<PlayerHealth>();
        // if (playerHealth != null)
        // {
        //     playerHealth.TakeDamage(damage);
        //     Debug.Log("Proyectil golpeó al jugador!");
        // } else {
             // Podrías dañar otros enemigos si tienen un script similar
              EnemyBase enemyHit = collision.gameObject.GetComponent<EnemyBase>();
              if (enemyHit != null && collision.gameObject != owner) // No dañar a otros enemigos del mismo tipo? O sí?
              {
                   // enemyHit.TakeDamage(damage);
                   // Debug.Log($"Proyectil golpeó a {enemyHit.name}");
              } else {
                    Debug.Log($"Proyectil golpeó a {collision.gameObject.name}");
              }

        // }


        // Opcional: Instanciar efecto de impacto
        // if (impactEffectPrefab != null) Instantiate(impactEffectPrefab, transform.position, Quaternion.identity);

        // Destruir el proyectil al impactar con casi cualquier cosa (excepto quizás otros proyectiles o triggers)
        // if (collision.gameObject.layer != LayerMask.NameToLayer("Projectile")) // Evita chocar con otros proyectiles
        // {
            Destroy(gameObject);
        // }
    }

     void OnTriggerEnter(Collider other) {
          // Podrías usar Triggers si el proyectil no debe tener colisión física
          // La lógica sería similar a OnCollisionEnter
          // Asegúrate de que el Rigidbody sea Kinematic si usas triggers para detectar colisiones
     }
}