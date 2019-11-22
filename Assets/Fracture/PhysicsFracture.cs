using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(NMesh))]
[RequireComponent(typeof(MeshCollider))]
[RequireComponent(typeof(Rigidbody))]
public class PhysicsFracture : MonoBehaviour
{
    private const int POOLSIZE = 1 << 11;
    private static Stack<GameObject> freeFragments;

    [Tooltip("Average distance between fault points.")]
    [Min(0f)]
    public float meanFaultDistance = 0.1f;

    [Tooltip("Energy per square meter required to fracture the material.")]
    [Min(0f)]
    public float fractureEnergy = 10.0f;

    [Tooltip("Energy per square meter released due to fracturing.")]
    [Min(0f)]
    public float surfaceEnergy = 0.0f;

    [Tooltip("Minimum fragment mass; fragments below this limit will cease fracturing.")]
    [Min(0f)]
    public float fragmentMinMass = 0.1f;

    [Tooltip("Ignore collisions with insufficient energy to bisect a sphere of this radius.\nIncrease this value to avoid unnecessary computations.")]
    [Min(0f)]
    public float minFractureRadius = 0.0f;

    /// <summary>
    /// Creates an object pool of fragments.
    /// </summary>
    private static void InitializeObjectPool()
    {
        freeFragments = new Stack<GameObject>(POOLSIZE);
        for (int i = 0; i < POOLSIZE; i++)
        {
            GameObject obj = new GameObject("Fragment " + i, typeof(PhysicsFracture));
            obj.GetComponent<MeshCollider>().convex = true;
            obj.SetActive(false);
            freeFragments.Push(obj);
        }
    }

    /// <summary>
    /// Random variate describing the distance to the nearest fault point.
    /// </summary>
    private float GetFaultDistance()
    {
        // Fault points are a 3D poisson process in distance from source.
        // Thus, if we assume faults are distributed over the volume,
        // The nearest fault is located according to the PDF
        // PDF(r) = (rho 4 Pi r^2) E^-(rho 4/3 Pi r^3)
        // With the CDF
        // CDF(r) = 1 - E^-(rho 4/3 π r^3)
        // Using the inverse CDF,
        // r(P) = ((3 / π) ^ (1 / 3) log(-1 / (P - 1)) ^ (1 / 3))/ (2 ^ (2 / 3) ρ ^ (1 / 3))
        // Allows us to get a variable distributed according to the PDF
        return meanFaultDistance * Mathf.Pow(-Mathf.Log(Random.value), 0.3333333333333333333f) * 1.119846521722185685f;
    }

    /// <summary>
    /// The initial kinetic energy associated with a collision.
    /// <para>TODO: consider implementing a setting to effectively deal with highly elastic collisions, which have low energy loss.</para>
    /// </summary>
    private float GetCollisionEnergy(Collision collision)
    {
        float m0 = GetComponent<Rigidbody>().mass;
        Rigidbody otherRigidbody;
        if (collision.gameObject.TryGetComponent<Rigidbody>(out otherRigidbody))
        {
            float m1 = otherRigidbody.mass;
            return 0.5f * (m0 * m1) / (m0 + m1) * collision.relativeVelocity.sqrMagnitude;
        }
        return 0.5f * m0 * collision.relativeVelocity.sqrMagnitude;
    }

    /// <summary>
    /// The actual energy lost in a given collision.
    /// </summary>
    private float GetCollisionEnergyLoss(Collision collision)
    {
        float m0 = GetComponent<Rigidbody>().mass;
        Rigidbody otherRigidbody;
        if (false && collision.gameObject.TryGetComponent<Rigidbody>(out otherRigidbody))
        {
            float m1 = otherRigidbody.mass;
            return 0.5f * (m0 + m1) / (m0 * m1) * Vector3.Dot(collision.impulse, collision.impulse);
        }
        return 0.5f / m0 * Vector3.Dot(collision.impulse, collision.impulse);
    }

    /// <summary>
    /// Performs fractal calls of MeshSplit.Split (no dependence on direction yet)
    /// </summary>
    public void Fracture(Vector3 point, float energy)
    {
        #region Cancellation Checks

        // If the mass is too small for further fragmentation, disable fractures
        if (GetComponent<Rigidbody>().mass < fragmentMinMass)
        {
            enabled = false;
            return;
        }
        // If the energy is insufficient for fragmentation, return
        else if (energy < Mathf.PI * minFractureRadius * minFractureRadius * fractureEnergy)
        {
            return;
        }

        #endregion Cancellation Checks

        #region Fracture Properties

        MeshCollider meshCollider = GetComponent<MeshCollider>();

        // breaks along lower energy planes should be more common
        Vector3 extents = meshCollider.bounds.extents;
        Vector3 normal = Vector3.Scale(Random.onUnitSphere, extents);

        Plane plane = new Plane(normal, point + Random.onUnitSphere * GetFaultDistance());

        // falloff is based on an ellipsoid that encomasses the entire volume
        Ray ray = new Ray(meshCollider.bounds.center, -Vector3.Scale(plane.normal, extents));
        plane.Raycast(ray, out float rayDist);
        Vector3 rPos = ray.GetPoint(rayDist) - meshCollider.bounds.center;
        float radialFactor = 1f - 0.33f * (
            rPos.x * rPos.x / (extents.x * extents.x)
            + rPos.y * rPos.y / (extents.y * extents.y)
            + rPos.z * rPos.z / (extents.z * extents.z)
            );

        // Approximate area of an ellipse section cut
        float centerArea = Mathf.PI * extents.x * extents.y * extents.z / Vector3.Scale(plane.normal, extents).magnitude;
        float crossSectionArea = radialFactor * centerArea;

        // if the energy is too low to fracture, pr the plane cannot intersect the object, cancel fragmentation
        if (crossSectionArea <= 0f || energy <= fractureEnergy * crossSectionArea)
        {
            return;
        }

        #endregion Fracture Properties

        #region Fracturing

        // set energy value for further fragmentation steps
        energy = (energy - fractureEnergy + surfaceEnergy * crossSectionArea) * 0.5f;

        // try to pull the other fragment from the object pool
        GameObject other = freeFragments.Count > 0 ? freeFragments.Pop() : Instantiate(gameObject);

        // attempt to fracture the object
        bool split = MeshSplit.Split(GetComponent<NMesh>(), other.GetComponent<NMesh>(), plane, Space.World);

        if (split)
        {
            other.SetActive(true);
            other.layer = gameObject.layer;

            Rigidbody rigidbody = GetComponent<Rigidbody>();
            PhysicsFracture otherFracture = other.GetComponent<PhysicsFracture>();
            MeshCollider otherMeshCollider = other.GetComponent<MeshCollider>();
            Rigidbody otherRigidbody = other.GetComponent<Rigidbody>();

            otherFracture.meanFaultDistance = meanFaultDistance;
            otherFracture.fractureEnergy = fractureEnergy;
            otherFracture.surfaceEnergy = surfaceEnergy;
            otherFracture.fragmentMinMass = fragmentMinMass;

            meshCollider.sharedMesh = GetComponent<MeshFilter>().mesh;
            otherMeshCollider.sharedMesh = other.GetComponent<MeshFilter>().mesh;
            otherMeshCollider.sharedMaterial = meshCollider.sharedMaterial;

            Vector3 b0 = meshCollider.bounds.size;
            Vector3 b1 = otherMeshCollider.bounds.size;
            float massFraction = 1.0f / (1.0f + ((b1.x / b0.x) * (b1.y / b0.y) * (b1.z / b0.z)));

            otherRigidbody.mass = rigidbody.mass * (1 - massFraction);
            rigidbody.mass *= massFraction;

            //TODO: Consider copying all component properties. Possible without reflection?
            otherRigidbody.drag = rigidbody.drag;

            rigidbody.WakeUp();
            otherRigidbody.WakeUp();

            //TODO: Consider adding normal force from mesh split.
            //How much of the surface energy goes toward future fractures, and how much toward normal forces? Why?
            otherRigidbody.velocity = rigidbody.velocity;

            otherFracture.Fracture(point, energy);
        }
        else
        {
            other.SetActive(false);
            freeFragments.Push(other);
        }

        Fracture(point, energy);

        #endregion Fracturing
    }

    private void Awake()
    {
        if (freeFragments == null)
            InitializeObjectPool();
    }

    private void OnCollisionEnter(Collision collision)
    {
        //Vector3 meanPoint = Vector3.zero;
        //for (int i = 0; i < collision.contactCount; i++)
        //{
        //    meanPoint += collision.GetContact(i).point;
        //}
        //meanPoint /= collision.contactCount;
        float energy = GetCollisionEnergyLoss(collision);
        if (energy >= Mathf.PI * minFractureRadius * minFractureRadius * fractureEnergy)
        {
            Fracture(GetComponent<Collider>().ClosestPointOnBounds(collision.GetContact(0).point), energy);
        }
    }
}
