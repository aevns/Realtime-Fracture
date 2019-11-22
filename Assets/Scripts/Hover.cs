using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Hover : MonoBehaviour
{
    public float height = 5f;
    public LayerMask mask;

    void FixedUpdate()
    {
        foreach (Rigidbody rb in GetComponentsInChildren<Rigidbody>())
        {
            RaycastHit hitInfo;
            if (Physics.Raycast(rb.transform.position + (rb.centerOfMass), Physics.gravity, out hitInfo, float.PositiveInfinity, mask))
            {
                rb.AddForce(
                    -Physics.gravity * height / hitInfo.distance,
                    ForceMode.Acceleration
                );
            }
        }
    }
}