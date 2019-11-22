using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

// TODO: use a custom editor instead of a fake checkbox 'button'.
// Also, look into a way to listen to changes in the MeshFilter object (if possible)

/// <summary>
/// NMesh data for single-submesh objects.
/// </summary>
[RequireComponent(typeof(MeshFilter))]
[RequireComponent(typeof(MeshRenderer))]
public class NMesh : MonoBehaviour
{
    public bool generateNgons = false;
    public const float planeTolerance = 3.16E-3f;

    [SerializeField]
    [HideInInspector]
    public List<int> ngons;


    /// <summary>
    /// Generates the NMesh's ngon data from the object's meshfilter data.
    /// Restricted to, and fully functional for, convexhull objects.
    /// </summary>
    private void GenerateNgonsFromMesh()
    {
        Vector3[] vertices = GetComponent<MeshFilter>().sharedMesh.vertices;
        int[] tris = GetComponent<MeshFilter>().sharedMesh.triangles;

        Dictionary<Plane, HashSet<int>> vertexLoops = new Dictionary<Plane, HashSet<int>>();

        // Create sets of coplanar triangles
        for (int i = 0; i < tris.Length - 2; i += 3)
        {
            Plane plane = new Plane(vertices[tris[i]], vertices[tris[i + 1]], vertices[tris[i + 2]]);
            bool preExisting = false;
            foreach (Plane p in vertexLoops.Keys)
            {
                if ((p.normal - plane.normal).sqrMagnitude <= planeTolerance)
                {
                    plane = p;
                    preExisting = true;
                    break;
                }
            }

            if (!preExisting)
            {
                vertexLoops.Add(plane, new HashSet<int>());
            }

            vertexLoops[plane].Add(tris[i]);
            vertexLoops[plane].Add(tris[i + 1]);
            vertexLoops[plane].Add(tris[i + 2]);
        }

        ngons = new List<int>();

        // Add convex loops of triangle sets to the ngon list
        // ngon loops are terminated by indices of -1
        foreach (KeyValuePair<Plane, HashSet<int>> vertexLoop in vertexLoops)
        {
            ngons = ngons.Concat(GrahamScan(vertexLoop)).ToList();
            ngons.Add(-1);
        }

        Debug.Log("Recalculated Ngon Count: " + ngons.Count);
    }

    /// <summary>
    /// Generates a convex vertex loop from geometry defining an ngon surface.
    /// </summary>
    /// <param name="vIndexSet">A key value pair containing the plane of a surface, and a hashset of vertices on the surface.</param>
    /// <returns>The convex ngon defining the surface.</returns>
    private List<int> GrahamScan(KeyValuePair<Plane, HashSet<int>> vIndexSet)
    {
        Vector3[] vertices = GetComponent<MeshFilter>().sharedMesh.vertices;

        // Defines the u,v axes desccribing the surface in 2D.
        Vector3 u = Vector3.Cross(Vector3.up, vIndexSet.Key.normal);
        if (u.sqrMagnitude < 0.5f)
        {
            u = Vector3.Cross(Vector3.left, vIndexSet.Key.normal);
        }
        Vector3 v = Vector3.Cross(u, vIndexSet.Key.normal);
        u.Normalize();
        v.Normalize();

        // Monotone increases with angle around the surface normal.
        float SortCriterion(Vector3 vec)
        {
            return Vector3.SignedAngle(u, vec, vIndexSet.Key.normal);
        }

        // Sorting vertices along the v, then u axes, so that the minimum v axis vertex (at p0) can be found.
        List<int> vIndices = vIndexSet.Value.OrderBy(vi => Vector3.Dot(vertices[vi], v)).ThenBy(vi => Vector3.Dot(vertices[vi], u)).ToList();
        int vI0 = vIndices[0];
        Vector3 p0 = vertices[vI0];
        vIndices.RemoveAt(0);

        // Sorting vertices (without p0) according to the angle from p0 to each vertex.
        vIndices = vIndices.OrderBy(vi => SortCriterion(vertices[vi] - p0)).ToList();

        // Ensure we only include counter-clockwise rotations in the vertex loop, creating a convex loop.
        List<int> loop = new List<int>();
        loop.Add(vI0);
        for (int i = 0; i < vIndices.Count; i++)
        {
            int vi = vIndices[i];
            while (loop.Count > 1
                && SortCriterion(vertices[vi] - vertices[loop[loop.Count - 1]]) < SortCriterion(vertices[loop[loop.Count - 1]] - vertices[loop[loop.Count - 2]]))
            {
                loop.RemoveAt(loop.Count - 1);
            }
            loop.Add(vi);
        }
        return loop;
    }

    private void OnValidate()
    {
        if (generateNgons)
        {
            generateNgons = false;
            GenerateNgonsFromMesh();
        }
    }

    /// <summary>
    /// Draw ngon outlines in random colors for debugging purposes.
    /// </summary>
    void OnDrawGizmosSelected()
    {
        if (ngons == null || ngons.Count < 1)
            return;
        
        Random.InitState(ngons[0].GetHashCode());
        Gizmos.color = Random.ColorHSV(0, 1, 1, 1, 0.5f, 0.5f, 1, 1);
        Gizmos.matrix = transform.localToWorldMatrix;
        Mesh m = GetComponent<MeshFilter>().sharedMesh;

        int loopStart = 0;
        for (int i = 1; i < ngons.Count; i++)
        {
            if (ngons[i] == -1)
            {
                Gizmos.DrawLine(m.vertices[ngons[i - 1]], m.vertices[ngons[loopStart]]);
                i++;
                loopStart = i;
                Gizmos.color = Random.ColorHSV(0, 1, 1, 1, 0.5f, 0.5f, 1, 1);
                continue;
            }
            Gizmos.DrawLine(m.vertices[ngons[i - 1]], m.vertices[ngons[i]]);
        }
    }
}