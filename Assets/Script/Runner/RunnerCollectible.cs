using UnityEngine;

public class RunnerCollectible : MonoBehaviour
{
    public int coinValue = 1;
    public float rotateSpeed = 180f;

    private bool m_Collected;

    private void Update()
    {
        transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
    }

    public void Collect()
    {
        if (m_Collected)
        {
            return;
        }

        m_Collected = true;
        if (RunnerGameManager.Instance != null)
        {
            RunnerGameManager.Instance.AddCoin(coinValue);
        }

        Destroy(gameObject);
    }
}
