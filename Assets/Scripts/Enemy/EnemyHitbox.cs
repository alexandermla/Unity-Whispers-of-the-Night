using UnityEngine;

public class EnemyHitbox : MonoBehaviour
{
    [Tooltip("Referencia al script principal del enemigo (EnemyBase). Se busca en padres si no se asigna.")]
    public EnemyBase enemyBase; // <-- Cambiado a EnemyBase

    private Collider col;

    private bool canDamage = false;

    private void Awake()
    {
        if (enemyBase == null) {
            enemyBase = GetComponentInParent<EnemyBase>(); // <-- Buscar EnemyBase
        }
        if (enemyBase == null) {
            Debug.LogError($"EnemyHitbox en '{gameObject.name}' no pudo encontrar un script EnemyBase en sus padres.", this);
            enabled = false; return;
        }

        col = GetComponent<Collider>();
        if(col != null) { if (!col.isTrigger) { col.isTrigger = true; } }
        else { Debug.LogError($"EnemyHitbox en {gameObject.name} no tiene Collider!", this); enabled = false; }
        canDamage = false;
    }

    public void ActivateHitbox() {
        canDamage = true;
        col.enabled = true; // <-- Descomentar si se quiere activar el hitbox al activarse
        Debug.Log($"Hitbox Activado (canDamage = {canDamage}) en {gameObject.name}");
    }
    public void DeactivateHitbox() {
        canDamage = false;
        col.enabled = false; // <-- Descomentar si se quiere desactivar el hitbox al desactivarse
    }

    private void OnTriggerEnter(Collider other)
    {
        if (canDamage && other.CompareTag("Player")) {
            if (enemyBase != null) {
                enemyBase.RegisterHit(other); // <-- Llama al método de la clase base
                canDamage = false;
            }
            else { Debug.LogError($"¡ERROR CRÍTICO! enemyBase es null en OnTriggerEnter de '{gameObject.name}'.", this); }
        }
    }
}