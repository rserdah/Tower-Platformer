using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class JumpBoost : MonoBehaviour
{
    [SerializeField]
    private float jumpBoost = 15f;
    [SerializeField]
    private bool boostLocalUp = true;
    public Vector3 m_boostDirection = Vector3.up;
    public Vector3 boostDirection
    {
        get
        {
            if(boostLocalUp)
                return transform.up;
            else
                return m_boostDirection;
        }
    }


    public float GetJumpBoost()
    {
        return jumpBoost;
    }

    public Vector3 GetJumpBoostDirection()
    {
        return boostDirection.normalized;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(transform.position, transform.position + boostDirection * jumpBoost);
    }
}
