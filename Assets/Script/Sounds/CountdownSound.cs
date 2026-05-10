using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CountdownSound : MonoBehaviour {

    protected AudioSource m_Source;
    protected float m_TimeToDisable;

    protected const float k_StartDelay = 0.5f;

    void OnEnable()
    {
        //得到声音组件
        m_Source = GetComponent<AudioSource>();
        m_TimeToDisable = m_Source.clip.length;

        Debug.Log("-----m_TimeToDisable:" + m_TimeToDisable);
        m_Source.PlayDelayed(k_StartDelay);
    }

    void Update()
    {
        //Time.deltaTime 0.016
        m_TimeToDisable -= Time.deltaTime;

        if (m_TimeToDisable < 0)
        {
            gameObject.SetActive(false);
        }    
    }
}
