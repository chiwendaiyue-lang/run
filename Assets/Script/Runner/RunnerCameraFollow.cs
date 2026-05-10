using UnityEngine;

public class RunnerCameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0f, 5.5f, -8f);
    public float followSmooth = 8f;
    public float lookAhead = 4f;

    private void LateUpdate()
    {
        if (target == null)
        {
            return;
        }

        Vector3 desiredPosition = target.position + offset + Vector3.forward * lookAhead;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, followSmooth * Time.deltaTime);

        Vector3 lookTarget = target.position + Vector3.up * 1.2f + Vector3.forward * lookAhead;
        transform.rotation = Quaternion.Slerp(
            transform.rotation,
            Quaternion.LookRotation(lookTarget - transform.position, Vector3.up),
            followSmooth * Time.deltaTime
        );
    }
}
