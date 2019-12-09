using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class PhysicsExtensions
{
    public static double GetEnergy(this Collision collision)
    {
        float m0 = collision.GetContact(0).thisCollider.GetComponent<Rigidbody>().mass;
        if (collision.rigidbody != null)
        {
            float m1 = collision.rigidbody.mass;
            return 0.5f * (m0 * m1) / (m0 + m1) * collision.relativeVelocity.sqrMagnitude;
        }
        return 0.5f * m0 * collision.relativeVelocity.sqrMagnitude;
    }

    public static double GetEnergyLoss(this Collision collision)
    {
        float m0 = collision.GetContact(0).thisCollider.GetComponent<Rigidbody>().mass;
        if (collision.rigidbody != null)
        {
            float m1 = collision.rigidbody.mass;
            return 0.5f * (m0 + m1) / (m0 * m1) * Vector3.Dot(collision.impulse, collision.impulse);
        }
        return 0.5f / m0 * Vector3.Dot(collision.impulse, collision.impulse);
    }
}