using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FireBoulders : MonoBehaviour
{
    public GameObject boulder;
    public float velocity = 10.0f;
    public float energy = 50.0f;
    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            GameObject obj = Instantiate(boulder, Camera.main.transform.position, Quaternion.identity);
            obj.GetComponent<Rigidbody>().velocity = Camera.main.ScreenPointToRay(Input.mousePosition).direction * velocity;
        }

        if (Input.GetMouseButtonDown(1))
        {
            RaycastHit hit;
            PhysicsFracture frac;
            if (
                Physics.Raycast(Camera.main.ScreenPointToRay(Input.mousePosition), hitInfo: out hit)
                && hit.collider.TryGetComponent<PhysicsFracture>(out frac))
            {
                Debug.DrawRay(hit.point, hit.normal, Color.red, 10);
                frac.Fracture(hit.point, energy);
            }
        }
    }
}
