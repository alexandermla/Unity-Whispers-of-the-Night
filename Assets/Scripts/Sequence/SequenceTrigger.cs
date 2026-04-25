using UnityEngine;

public class SequenceTrigger : MonoBehaviour
{
    // Arrastra aquí el objeto que tiene CentralFountainController
    public CentralFountainController goddessController;

    private void OnTriggerEnter(Collider other)
    {
        // Verifica si es el jugador y si el controlador está asignado
        if (other.CompareTag("Player") && goddessController != null)
        {
            // Llama al método público en el controlador para iniciar la secuencia
            goddessController.StartRevealSequenceFromTrigger();
            // Opcional: Desactivar este trigger después de usarlo una vez
            // gameObject.SetActive(false);
        }
    }

    // Opcional: Dibujar Gizmo para ver el trigger en el editor
    private void OnDrawGizmos()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            Gizmos.color = new Color(0f, 1f, 0f, 0.3f); // Verde semitransparente
            if (col is BoxCollider box)
            {
                Gizmos.DrawCube(transform.position + box.center, Vector3.Scale(transform.lossyScale, box.size));
            }
            else if (col is SphereCollider sphere)
            {
                Gizmos.DrawSphere(transform.position + sphere.center, sphere.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.y, transform.lossyScale.z));
            }
        }
    }
}