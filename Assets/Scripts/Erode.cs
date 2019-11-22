using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(PhysicsFracture))]
public class Erode : MonoBehaviour
{
    public float erosionRate = 10.0f;
    public float averageEnergy = 50.0f;
    // Update is called once per frame
    void FixedUpdate()
    {
        if (Random.value * erosionRate < Time.fixedDeltaTime)
        {
            GetComponent<PhysicsFracture>().Fracture(
                GetComponent<MeshCollider>().bounds.center 
                + Vector3.Scale(Random.insideUnitSphere, GetComponent<MeshCollider>().bounds.extents),
                averageEnergy * -Mathf.Log(Random.value));
        }
    }
}
