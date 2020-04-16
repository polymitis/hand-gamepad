using UnityEngine;
using System;
using System.Collections;

[RequireComponent(typeof(Animator))]
public class HandIKController : MonoBehaviour
{
    public Transform thumbIK = null;
    public Transform indexIK = null;
    public Transform middleIK = null;
    public Transform ringIK = null;
    public Transform littleIK = null;

    public void setThumbPos(Vector3 pos)
    {
        m_thumbPos = pos;
    }

    public void setIndexPos(Vector3 pos)
    {
        m_indexPos = pos;
    }

    public void setMiddlePos(Vector3 pos)
    {
        m_middlePos = pos;
    }

    public void setRingPos(Vector3 pos)
    {
        m_ringPos = pos;
    }

    public void setLittlePos(Vector3 pos)
    {
        m_littlePos = pos;
    }

    void Start()
    {
        m_Animator = GetComponent<Animator>();
    }

    void OnAnimatorIK(int layerIndex)
    {
        if (m_Animator && m_UpdateIK)
        {
            // TODO Set fingers position
        }
    }

    protected Animator m_Animator;

    private bool m_UpdateIK = false;

    private Vector3 m_thumbPos;
    private Vector3 m_indexPos;
    private Vector3 m_middlePos;
    private Vector3 m_ringPos;
    private Vector3 m_littlePos;

}