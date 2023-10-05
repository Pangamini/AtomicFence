using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TranslateOverTime : MonoBehaviour
{
    [SerializeField] private Vector3 m_speed;
    
    // Update is called once per frame
    void Update()
    {
        transform.position += m_speed * Time.deltaTime;
    }
}
