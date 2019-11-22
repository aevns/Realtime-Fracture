using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/*
 * ngons are convex, therefore there's either 0 or 2 intersections per ngon; no more.
 *	Thus, if and only if there's 1 intersect by the end of a loop, it can be assumed
 *	that there's an intersection between the first and last element as well.
 */

public static class MeshSplit
{
    private struct MeshData
    {
        public List<Vector3> vertices;
        public List<Vector3> normals;
        public List<Vector2> uvs;
        public List<int> ngons;
        public List<int> tris;

        public MeshData(int capacity)
        {
            vertices = new List<Vector3>(capacity);
            normals = new List<Vector3>(capacity);
            uvs = new List<Vector2>(capacity);
            ngons = new List<int>(capacity);
            tris = new List<int>(capacity);
        }
    }

    public static bool Split(NMesh original, NMesh copy, Plane plane, Space space = Space.Self)
    {
        // Basic setup. [COMPLETE]
        #region Setup

        if (space == Space.World)
        {
            // TransformPlane doesn't seem to account for scale correctly. worldToLocalMatrix also seems to have issues.
            plane = original.transform.worldToLocalMatrix.TransformPlane(plane);
            //plane.distance /= Vector3.Dot(plane.normal, original.transform.lossyScale);
        }
        copy.transform.parent = original.transform.parent;
        copy.transform.localPosition = original.transform.localPosition;
        copy.transform.localScale = original.transform.localScale;
        copy.transform.localRotation = original.transform.localRotation;
        copy.GetComponent<MeshRenderer>().sharedMaterials = original.GetComponent<MeshRenderer>().sharedMaterials;

        #endregion Setup

        // Original mesh data. [COMPLETE]
        #region Input and Object Data

        Mesh originalMesh = original.GetComponent<MeshFilter>().sharedMesh;

        Vector3[] vertices = originalMesh.vertices;
        Vector3[] normals = originalMesh.normals;
        Vector2[] uvs = originalMesh.uv;
        List<int> ngons = original.ngons;

        #endregion Input and Object Data

        // Output data collections. Data sets are indexed to avoid conditionals.
        #region Output Data

        MeshData[] data = new[] { new MeshData(vertices.Length), new MeshData(vertices.Length) };

        #endregion Output Data

        // Generate ngon and vertex data for each new object.
        #region Ngon Recalculation

        // This list keeps track of new vertices, which then need to be ordered into a new ngon.
        List<Vector3> sliceVerts = new List<Vector3>(16);

        // Loops through each ngon from the original object, which are seperated by values of -1.
        int ngonIndex = 0;
        while (ngonIndex < ngons.Count)
        {
            Vector3 crossPoint0 = Vector3.positiveInfinity;
            Vector3 crossPoint1 = Vector3.positiveInfinity;

            // First vertex's data must be recalled to close the loop
            int firstNode = ngons[ngonIndex++];
            float firstDist = plane.GetDistanceToPoint(vertices[firstNode]);
            int firstSide = System.Convert.ToInt32(firstDist > 0);

            data[firstSide].vertices.Add(vertices[firstNode]);
            data[firstSide].normals.Add(normals[firstNode]);
            data[firstSide].uvs.Add(uvs[firstNode]);
            data[firstSide].ngons.Add(data[firstSide].vertices.Count - 1);

            int lastNode = firstNode;
            float lastDist = firstDist;
            int lastSide = firstSide;

            int node;
            while ((node = ngons[ngonIndex++]) >= 0)
            {
                float dist = plane.GetDistanceToPoint(vertices[node]);
                int side = System.Convert.ToInt32(dist > 0);

                // If the vertices are seperated by the plane, add the midpoint node to both new ngons.
                if (side != lastSide)
                {
                    float interpolator = lastDist / (lastDist - dist);
                    Vector3 midVertex = vertices[lastNode] + (vertices[node] - vertices[lastNode]) * interpolator;
                    Vector3 midNormal = normals[lastNode] + (normals[node] - normals[lastNode]) * interpolator;
                    Vector2 midUV = uvs[lastNode] + (uvs[node] - uvs[lastNode]) * interpolator;

                    for (int i = 0; i < 2; i++)
                    {
                        data[i].vertices.Add(midVertex);
                        data[i].normals.Add(midNormal);
                        data[i].uvs.Add(midUV);
                        data[i].ngons.Add(data[i].vertices.Count - 1);
                    }

                    if (side == 1) crossPoint0 = midVertex;
                    else crossPoint1 = midVertex;
                }

                data[side].vertices.Add(vertices[node]);
                data[side].normals.Add(normals[node]);
                data[side].uvs.Add(uvs[node]);
                data[side].ngons.Add(data[side].vertices.Count - 1);

                lastNode = node;
                lastDist = dist;
                lastSide = side;
            }

            // Check the final edge, to see if another midpoint node needs to be added to each object.
            if (firstSide != lastSide)
            {
                float interpolator = lastDist / (lastDist - firstDist);
                Vector3 midVertex = vertices[lastNode] + (vertices[firstNode] - vertices[lastNode]) * interpolator;
                Vector3 midNormal = normals[lastNode] + (normals[firstNode] - normals[lastNode]) * interpolator;
                Vector2 midUV = uvs[lastNode] + (uvs[firstNode] - uvs[lastNode]) * interpolator;

                for (int i = 0; i < 2; i++)
                {
                    data[i].vertices.Add(midVertex);
                    data[i].normals.Add(midNormal);
                    data[i].uvs.Add(midUV);
                    data[i].ngons.Add(data[i].vertices.Count - 1);
                }

                if (firstSide == 1) crossPoint0 = midVertex;
                else crossPoint1 = midVertex;
            }

            // Check if we've undergone a split. If so, add new sliceVerts.
            if (!float.IsInfinity(crossPoint0.sqrMagnitude))
            {
                sliceVerts.Add(crossPoint0);
                sliceVerts.Add(crossPoint1);
            }

            // Check if we've added a new ngon for each object. If so, terminate with -1.
            for (int i = 0; i < 2; i++)
            {
                if (data[i].ngons.Count > 0 && data[i].ngons[data[i].ngons.Count - 1] != -1)
                    data[i].ngons.Add(-1);
            }
        }

        // Cancel the operation if no split took place.
        if (sliceVerts.Count == 0)
            return false;

        #endregion Ngon Recalculation

        // Generate ngon data for the new face of each object.
        #region New Ngon Surface Creation

        // Get the position of each vertex in UV space.
        Vector3 u = Vector3.Cross(Vector3.up, plane.normal);
        if (u.sqrMagnitude < 0.5f)
        {
            u = Vector3.Cross(Vector3.left, plane.normal);
        }
        Vector3 v = Vector3.Cross(u, plane.normal);

        // Scale UV coordinates according to input (TODO)
        //u = Vector3.Scale(u, original.transform.lossyScale);
        //v = Vector3.Scale(v, original.transform.lossyScale);
        u *= 1;// 0.0254f/2;
        v *= 1;// 0.0254f/2;

        // Create the ngon of the new section cut.
        int currentIndex = 0;
        List<Vector3> loopVerts = new List<Vector3>(sliceVerts.Count);
        List<Vector2> loopUVs = new List<Vector2>(sliceVerts.Count);
        int ittr = 0;
        do
        {
            loopVerts.Add(sliceVerts[currentIndex]);
            loopUVs.Add(new Vector2(Vector3.Dot(sliceVerts[currentIndex], u), Vector3.Dot(sliceVerts[currentIndex], v)));

            for (int i = 0; i < sliceVerts.Count; i += 2)
            {
                if (sliceVerts[i] == sliceVerts[currentIndex + 1])
                {
                    currentIndex = i;
                    break;
                }
            }
            ittr++;
        } while (currentIndex != 0 && ittr <= sliceVerts.Count / 2);

        // Cancel the operation if a valid edge loop could not be constructed.
        if (ittr != sliceVerts.Count / 2)
            return false;

        data[0].vertices.AddRange(loopVerts);
        data[0].uvs.AddRange(loopUVs);

        data[1].vertices.AddRange(loopVerts);
        data[1].uvs.AddRange(loopUVs);

        for (int i = 0; i < loopVerts.Count; i++)
        {
            data[0].normals.Add(plane.normal);
            data[1].normals.Add(-plane.normal);

            data[0].ngons.Add(data[0].vertices.Count - 1 - i);
            data[1].ngons.Add(data[1].vertices.Count - loopVerts.Count + i);
        }

        data[0].ngons.Add(-1);
        data[1].ngons.Add(-1);

        #endregion New Ngon Surface Creation

        // Build triangle data from ngon data.
        #region Mesh Generation

        for (int i = 0; i < 2; i++)
        {
            int startPoint = 0;
            int prevPoint = 0;
            for (int n = 0; n < data[i].ngons.Count - 1; n++)
            {
                if (data[i].ngons[n] == -1)
                {
                    startPoint = data[i].ngons[++n];
                    prevPoint = data[i].ngons[++n];
                    continue;
                }
                data[i].tris.Add(startPoint);
                data[i].tris.Add(prevPoint);
                data[i].tris.Add(data[i].ngons[n]);
                prevPoint = data[i].ngons[n];
            }
        }

        #endregion Mesh Generation

        // Set data, regenerate tangent and collision data.
        #region Set Data

        original.ngons = data[0].ngons;
        Mesh newMesh = new Mesh();
        newMesh.SetVertices(data[0].vertices);
        newMesh.SetNormals(data[0].normals);
        newMesh.SetUVs(0, data[0].uvs);
        newMesh.SetTriangles(data[0].tris, 0);
        newMesh.RecalculateTangents();
        original.GetComponent<MeshFilter>().sharedMesh = newMesh;

        copy.ngons = data[1].ngons;
        Mesh copyMesh = new Mesh();
        copyMesh.SetVertices(data[1].vertices);
        copyMesh.SetNormals(data[1].normals);
        copyMesh.SetUVs(0, data[1].uvs);
        copyMesh.SetTriangles(data[1].tris, 0);
        copyMesh.RecalculateTangents();
        copy.GetComponent<MeshFilter>().sharedMesh = copyMesh;

        #endregion Set Data

        return true;
    }
}