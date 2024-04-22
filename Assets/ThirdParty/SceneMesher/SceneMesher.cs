/*
 * Copyright (c) Meta Platforms, Inc. and affiliates.
 * All rights reserved.
 *
 * Licensed under the Oculus SDK License Agreement (the "License");
 * you may not use the Oculus SDK except in compliance with the License,
 * which is provided at the time of installation or download, or which
 * otherwise accompanies this software in either electronic or hard copy form.
 *
 * You may obtain a copy of the License at
 *
 * https://developer.oculus.com/licenses/oculussdk/
 *
 * Unless required by applicable law or agreed to in writing, the Oculus SDK
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Assertions;

public static class SceneMesher
{
    private const MeshColliderCookingOptions COOKING_OPTIONS = MeshColliderCookingOptions.UseFastMidphase | MeshColliderCookingOptions.CookForFasterSimulation | MeshColliderCookingOptions.WeldColocatedVertices | MeshColliderCookingOptions.EnableMeshCleaning;

    public static GameObject CreateMesh(OVRSceneRoom sceneRoom, GameObject sceneGameObject, float borderSize = 0.1f, bool worldUnits = false)
    {
        Assert.IsNotNull(sceneRoom);

        var classifications = sceneRoom.GetComponentsInChildren<OVRSemanticClassification>(true);

        var cornerPoints = GetClockwiseFloorOutline(sceneRoom);
        var volumes = new List<OVRSceneVolume>();
        var planes = new List<OVRScenePlane>();

        var ceilingHeight = sceneRoom.Ceiling.transform.position.y - sceneRoom.Floor.transform.position.y;

        foreach (var classification in classifications)
        {
            switch (classification.Labels[0])
            {
                case OVRSceneManager.Classification.Table:
                case OVRSceneManager.Classification.Couch:
                case OVRSceneManager.Classification.Other:
                case OVRSceneManager.Classification.Storage:
                case OVRSceneManager.Classification.Bed:
                case OVRSceneManager.Classification.Screen:
                case OVRSceneManager.Classification.Lamp:
                case OVRSceneManager.Classification.Plant:
                    if (classification.TryGetComponent<OVRSceneVolume>(out var volume))
                    {
                        volumes.Add(volume);
                    }
                    break;
                case OVRSceneManager.Classification.DoorFrame:
                case OVRSceneManager.Classification.WindowFrame:
                case OVRSceneManager.Classification.WallArt:
                    if (classification.TryGetComponent<OVRScenePlane>(out var plane))
                    {
                        planes.Add(plane);
                    }
                    break;
            }
        }

        PopulateSceneMesh(cornerPoints, volumes, planes, ceilingHeight, sceneGameObject, borderSize, worldUnits);

        sceneGameObject.transform.parent = sceneRoom.transform;
        return sceneGameObject;
    }

    /// <summary>
    /// The saved walls may not be clockwise, and they may not be in order.
    /// </summary>
    private static List<Vector3> GetClockwiseFloorOutline(OVRSceneRoom sceneRoom)
    {
        List<Vector3> cornerPoints = new List<Vector3>();

        var floor = sceneRoom.Floor;
        var floorTransform = floor.transform;

        foreach (var corner in floor.Boundary)
        {
            cornerPoints.Add(floorTransform.TransformPoint(corner));
        }
        cornerPoints.Reverse();

        return cornerPoints;
    }

    /// <summary>
    /// Create a single mesh of the Scene objects.
    /// Input points are required to be in clockwise order when viewed top-down.
    /// Each 4-edged wall has 8 vertices; 4 vertices for the corners, plus 4 for inset vertices (to make the border effect).
    /// The floor/ceiling mesh is similarly twice as many vertices; one set for the outline, another set for inset vertices.
    /// </summary>
    private static void PopulateSceneMesh(List<Vector3> cornerPoints, IReadOnlyList<OVRSceneVolume> sceneCubes,
        IReadOnlyList<OVRScenePlane> sceneQuads, float ceiling, GameObject sceneGameObject, float borderSize, bool mappingInWorldUnits)
    {
        var sceneMesh = new Mesh();
        if (!sceneGameObject.TryGetComponent<MeshFilter>(out var meshFilter))
        {
            meshFilter = sceneGameObject.AddComponent<MeshFilter>();
        }

        meshFilter.mesh = sceneMesh;

        // make sure the border width is no more than half the length of the shortest line
        borderSize = Mathf.Min(borderSize, ceiling * 0.5f);
        for (int i = 0; i < cornerPoints.Count; i++)
        {
            float lastEdge = (i == cornerPoints.Count - 1) ?
                Vector3.Distance(cornerPoints[i], cornerPoints[0]) : Vector3.Distance(cornerPoints[i], cornerPoints[i + 1]);
            borderSize = Mathf.Min(borderSize, lastEdge * 0.5f);
        }

        var furnishings = sceneCubes;

        // build meshes
        int capTriCount = cornerPoints.Count - 2;
        int wallVertCount = cornerPoints.Count * 8;
        int capVertCount = cornerPoints.Count * 2;
        int cubeVertCount = furnishings.Count * 48;
        int quadVertCount = sceneQuads.Count * 8;

        int totalVertices = wallVertCount + capVertCount * 2 + cubeVertCount + quadVertCount;
        Vector3[] meshVertices = new Vector3[totalVertices];
        Vector2[] meshUVs = new Vector2[totalVertices];
        Color32[] meshColors = new Color32[totalVertices];
        Vector3[] meshNormals = new Vector3[totalVertices];
        Vector4[] meshTangents = new Vector4[totalVertices];

        int wallIndexCount = cornerPoints.Count * 30;
        int capIndexCount = cornerPoints.Count * 6 + capTriCount * 3;
        int cubeIndexCount = furnishings.Count * 180;
        int quadIndexCount = sceneQuads.Count * 30;
        int totalIndices = wallIndexCount + capIndexCount * 2 + cubeIndexCount + quadIndexCount;
        int[] meshTriangles = new int[totalIndices];

        int vertCounter = 0;
        float uSpacing = 0.0f;
        int triCounter = 0;

        // create wall squares
        // each point has 8 vertices, forming 10 triangles
        for (int i = 0; i < cornerPoints.Count; i++)
        {
            Vector3 startPos = cornerPoints[i];
            Vector3 endPos = (i == cornerPoints.Count - 1) ? cornerPoints[0] : cornerPoints[i + 1];

            // direction to points
            Vector3 segmentDirection = (endPos - startPos).normalized;
            float thisSegmentLength = (endPos - startPos).magnitude / ceiling;

            Vector3 wallNorm = Vector3.Cross(Vector3.up, -segmentDirection);
            Vector4 wallTan = new Vector4(segmentDirection.x, segmentDirection.y, segmentDirection.z, 1);

            // outer vertices of wall
            for (int j = 0; j < 4; j++)
            {
                Vector3 basePos = (j / 2 == 0) ? startPos : endPos;
                float ceilingVert = (j == 1 || j == 2) ? 1.0f : 0.0f;
                meshVertices[vertCounter] = basePos + Vector3.up * (ceiling * ceilingVert);
                float uvX = (j / 2 == 0) ? uSpacing : uSpacing + thisSegmentLength;
                meshUVs[vertCounter] = new Vector2(uvX, ceilingVert) * (mappingInWorldUnits ? ceiling : 1.0f);
                meshColors[vertCounter] = Color.black;
                meshNormals[vertCounter] = wallNorm;
                meshTangents[vertCounter] = wallTan;
                vertCounter++;
            }
            // inner vertices of wall
            for (int j = 0; j < 4; j++)
            {
                float ceilingVert = (j == 1 || j == 2) ? 1.0f : 0.0f;
                Vector3 basePos = (j / 2 == 0) ? startPos : endPos;
                basePos += (j / 2 == 0) ? segmentDirection * borderSize : -segmentDirection * borderSize;
                basePos += Vector3.up * (ceiling * ceilingVert);
                basePos -= Vector3.up * (borderSize * Mathf.Sign(ceilingVert - 0.5f));
                meshVertices[vertCounter] = basePos;
                float worldScaleBorder = borderSize / ceiling;
                float uvX = (j / 2 == 0) ? uSpacing : uSpacing + thisSegmentLength;
                uvX -= worldScaleBorder * Mathf.Sign((j / 2) - 0.5f);
                float uvY = ceilingVert - worldScaleBorder * Mathf.Sign(ceilingVert - 0.5f);
                meshUVs[vertCounter] = new Vector2(uvX, uvY) * (mappingInWorldUnits ? ceiling : 1.0f);
                meshColors[vertCounter] = Color.white;
                meshNormals[vertCounter] = wallNorm;
                meshTangents[vertCounter] = wallTan;
                vertCounter++;
            }
            uSpacing += thisSegmentLength;

            CreateBorderedPolygon(ref meshTriangles, ref triCounter, i * 8, 4);
        }

        // top down mapping means tangent is X-axis
        Vector4 floorTangent = new Vector4(1, 0, 0, 1);
        Vector4 ceilingTangent = new Vector4(-1, 0, 0, 1);
        List<Vector3> insetPoints = new List<Vector3>();

        // create floor
        for (int i = 0; i < cornerPoints.Count; i++)
        {
            Vector3 startPos = cornerPoints[i];
            Vector3 endPos = (i == cornerPoints.Count - 1) ? cornerPoints[0] : cornerPoints[i + 1];
            Vector3 lastPos = (i == 0) ? cornerPoints[cornerPoints.Count - 1] : cornerPoints[i - 1];

            // direction to points
            Vector3 thisSegmentDirection = (endPos - startPos).normalized;
            Vector3 lastSegmentDirection = (lastPos - startPos).normalized;

            // outer points
            meshVertices[vertCounter] = cornerPoints[i];
            meshUVs[vertCounter] = (mappingInWorldUnits ? ceiling : 1.0f) * new Vector2(meshVertices[vertCounter].x, meshVertices[vertCounter].z) / ceiling;
            meshColors[vertCounter] = Color.black;
            meshNormals[vertCounter] = Vector3.up;
            meshTangents[vertCounter] = floorTangent;

            // inner points
            int newID = vertCounter + cornerPoints.Count;
            Vector3 insetDirection = GetInsetDirection(lastPos, startPos, endPos);
            // ensure that the border is the same width regardless of angle between walls
            float angle = Vector3.Angle(thisSegmentDirection, insetDirection);
            float adjacent = borderSize / Mathf.Tan(angle * Mathf.Deg2Rad);
            float adustedBorderSize = Mathf.Sqrt(adjacent * adjacent + borderSize * borderSize);
            Vector3 insetPoint = cornerPoints[i] + insetDirection * adustedBorderSize;
            insetPoints.Add(insetPoint);
            meshVertices[newID] = insetPoint;
            meshUVs[newID] = (mappingInWorldUnits ? ceiling : 1.0f) * new Vector2(meshVertices[newID].x, meshVertices[newID].z) / ceiling;
            meshColors[newID] = Color.white;
            meshNormals[newID] = Vector3.up;
            meshTangents[newID] = floorTangent;

            vertCounter++;
        }
        CreateBorderedPolygon(ref meshTriangles, ref triCounter, wallVertCount, cornerPoints.Count, cornerPoints, false, insetPoints);

        // because we do unique counting for the caps, need to offset it
        vertCounter += cornerPoints.Count;

        // ceiling
        insetPoints.Clear();
        for (int i = 0; i < cornerPoints.Count; i++)
        {
            Vector3 startPos = cornerPoints[i];
            Vector3 endPos = (i == cornerPoints.Count - 1) ? cornerPoints[0] : cornerPoints[i + 1];
            Vector3 lastPos = (i == 0) ? cornerPoints[cornerPoints.Count - 1] : cornerPoints[i - 1];

            // direction to points
            Vector3 thisSegmentDirection = (endPos - startPos).normalized;
            Vector3 lastSegmentDirection = (lastPos - startPos).normalized;

            // outer points
            meshVertices[vertCounter] = cornerPoints[i] + Vector3.up * ceiling;
            meshUVs[vertCounter] = (mappingInWorldUnits ? ceiling : 1.0f) * new Vector2(meshVertices[vertCounter].x, meshVertices[vertCounter].z) / ceiling;
            meshColors[vertCounter] = Color.black;
            meshNormals[vertCounter] = Vector3.down;
            meshTangents[vertCounter] = ceilingTangent;

            // inner points
            int newID = vertCounter + cornerPoints.Count;
            Vector3 insetDirection = GetInsetDirection(lastPos, startPos, endPos);
            // ensure that the border is the same width regardless of angle between walls
            float angle = Vector3.Angle(thisSegmentDirection, insetDirection);
            float adjacent = borderSize / Mathf.Tan(angle * Mathf.Deg2Rad);
            float adustedBorderSize = Mathf.Sqrt(adjacent * adjacent + borderSize * borderSize);
            Vector3 insetPoint = cornerPoints[i] + Vector3.up * ceiling + insetDirection * adustedBorderSize;
            insetPoints.Add(insetPoint);
            meshVertices[newID] = insetPoint;
            meshUVs[newID] = (mappingInWorldUnits ? ceiling : 1.0f) * new Vector2(meshVertices[newID].x, meshVertices[newID].z) / ceiling;
            meshColors[newID] = Color.white;
            meshNormals[newID] = Vector3.down;
            meshTangents[newID] = ceilingTangent;

            vertCounter++;
        }
        CreateBorderedPolygon(ref meshTriangles, ref triCounter, wallVertCount + capVertCount, cornerPoints.Count, cornerPoints, true, insetPoints);
        vertCounter += cornerPoints.Count;

        // furnishings
        for (int i = 0; i < furnishings.Count; i++)
        {
            var volume = furnishings[i];
            var cube = volume.transform;
            Vector3 dim = volume.Dimensions;

            var volumeOffset = volume.Offset;
            (volumeOffset.x, volumeOffset.y, volumeOffset.z) = (volumeOffset.x, volumeOffset.z, volumeOffset.y);

            Vector3 cubeCenter = cube.position + volumeOffset;

            // each cube face gets an 8-vertex mesh
            for (int j = 0; j < 6; j++)
            {
                Vector3 right = cube.right * dim.x;
                Vector3 up = cube.up * dim.y;
                Vector3 fwd = cube.forward * dim.z;
                switch (j)
                {
                    case 1:
                        right = cube.right * dim.x;
                        up = -cube.forward * dim.z;
                        fwd = cube.up * dim.y;
                        break;
                    case 2:
                        right = cube.right * dim.x;
                        up = -cube.up * dim.y;
                        fwd = -cube.forward * dim.z;
                        break;
                    case 3:
                        right = cube.right * dim.x;
                        up = cube.forward * dim.z;
                        fwd = -cube.up * dim.y;
                        break;
                    case 4:
                        right = -cube.forward * dim.z;
                        up = cube.up * dim.y;
                        fwd = cube.right * dim.x;
                        break;
                    case 5:
                        right = cube.forward * dim.z;
                        up = cube.up * dim.y;
                        fwd = -cube.right * dim.x;
                        break;
                }

                // outer verts of face
                for (int k = 0; k < 4; k++)
                {
                    Vector3 basePoint = cubeCenter + fwd * 0.5f + right * 0.5f - up * 0.5f;
                    switch (k)
                    {
                        case 1:
                            basePoint += up;
                            break;
                        case 2:
                            basePoint += up - right;
                            break;
                        case 3:
                            basePoint -= right;
                            break;
                    }
                    meshVertices[vertCounter] = basePoint - cube.forward * (dim.z * 0.5f);
                    meshUVs[vertCounter] = new Vector2(0, 0);
                    meshColors[vertCounter] = Color.black;
                    meshNormals[vertCounter] = cube.forward;
                    meshTangents[vertCounter] = cube.right;
                    vertCounter++;
                }
                // inner vertices of face
                for (int k = 0; k < 4; k++)
                {
                    Vector3 offset = up.normalized * borderSize - right.normalized * borderSize;
                    switch (k)
                    {
                        case 1:
                            offset = -up.normalized * borderSize - right.normalized * borderSize;
                            break;
                        case 2:
                            offset = -up.normalized * borderSize + right.normalized * borderSize;
                            break;
                        case 3:
                            offset = up.normalized * borderSize + right.normalized * borderSize;
                            break;
                    }
                    meshVertices[vertCounter] = meshVertices[vertCounter-4] + offset;
                    meshUVs[vertCounter] = new Vector2(0, 0);
                    meshColors[vertCounter] = Color.white;
                    meshNormals[vertCounter] = cube.forward;
                    meshTangents[vertCounter] = cube.right;
                    vertCounter++;
                }

                int baseVert = (wallVertCount + capVertCount * 2) + (i * 48) + (j * 8);
                CreateBorderedPolygon(ref meshTriangles, ref triCounter, baseVert, 4);
            }
        }

        // doors and windows
        for (int i = 0; i < sceneQuads.Count; i++)
        {
            var quad = sceneQuads[i];
            var quadTransform = quad.transform;

            Vector3 quadNorm = -quadTransform.forward;
            Vector4 quadTan = quadTransform.right;

            Vector2 localScale = quad.Dimensions;

            Vector3 xDim = localScale.x * quadTransform.right;
            Vector3 yDim = localScale.y * quadTransform.up;
            Vector3 leftBottom = quadTransform.position - xDim * 0.5f - yDim * 0.5f;

            // outer vertices of quad
            Vector3 vert = leftBottom;
            for (int j = 0; j < 4; j++)
            {
                // CW loop order, starting from bottom left
                switch (j)
                {
                    case 1:
                        vert += yDim;
                        break;
                    case 2:
                        vert += xDim;
                        break;
                    case 3:
                        vert -= yDim;
                        break;
                }
                float uvX = (j == 0 || j == 1) ? 0.0f : 1.0f;
                float uvY = (j == 1 || j == 2) ? 1.0f : 0.0f;
                meshVertices[vertCounter] = vert + quadNorm * 0.01f;
                meshUVs[vertCounter] = new Vector2(uvX, uvY);
                meshColors[vertCounter] = Color.black;
                meshNormals[vertCounter] = quadNorm;
                meshTangents[vertCounter] = quadTan;
                vertCounter++;
            }
            // inner vertices of quad
            for (int j = 0; j < 4; j++)
            {
                Vector3 offset = quadTransform.up * borderSize + quadTransform.right * borderSize;
                switch (j)
                {
                    case 1:
                        offset = -quadTransform.up * borderSize + quadTransform.right * borderSize;
                        break;
                    case 2:
                        offset = -quadTransform.up * borderSize - quadTransform.right * borderSize;
                        break;
                    case 3:
                        offset = quadTransform.up * borderSize - quadTransform.right * borderSize;
                        break;
                }
                meshVertices[vertCounter] = meshVertices[vertCounter - 4] + offset;
                meshUVs[vertCounter] = new Vector2(0, 0);
                meshColors[vertCounter] = Color.white;
                meshNormals[vertCounter] = quadNorm;
                meshTangents[vertCounter] = quadTan;
                vertCounter++;
            }

            int baseIndex = wallVertCount + capVertCount * 2 + cubeVertCount;
            CreateBorderedPolygon(ref meshTriangles, ref triCounter, baseIndex + i * 8, 4);
        }

        // after calculating all data for the mesh, assign it
        sceneMesh.Clear();
        sceneMesh.name = "SceneMesh";
        sceneMesh.vertices = meshVertices;
        sceneMesh.uv = meshUVs;
        sceneMesh.colors32 = meshColors;
        sceneMesh.triangles = meshTriangles;
        sceneMesh.normals = meshNormals;
        sceneMesh.tangents = meshTangents;

        if (sceneGameObject.TryGetComponent<MeshCollider>(out var meshCollider))
        {
            Physics.BakeMesh(sceneMesh.GetInstanceID(), false, COOKING_OPTIONS);
            meshCollider.sharedMesh = sceneMesh;
        }

        // Setting these values via reflection because there are no public setters.
        if (sceneGameObject.TryGetComponent<OVRSceneVolumeMeshFilter>(out var volumeMeshFilter))
        {
            volumeMeshFilter.enabled = false;

            var volumeMeshFilterType = volumeMeshFilter.GetType();
            var isCompletedProp = volumeMeshFilterType.GetProperty("IsCompleted",
                BindingFlags.Public | BindingFlags.Instance);
            isCompletedProp?.SetValue(volumeMeshFilter, true);
        }

        if (sceneGameObject.TryGetComponent<OVRSceneAnchor>(out var anchor))
        {
            anchor.enabled = false;

            var anchorType = anchor.GetType();
            var uuidProp = anchorType.GetProperty("Uuid",
                BindingFlags.Public | BindingFlags.Instance);
            uuidProp?.SetValue(anchor, Guid.NewGuid());

            var ovrSpace = new OVRSpace(ulong.MaxValue - 1);

            var spaceProp = anchorType.GetProperty("Space",
                BindingFlags.Public | BindingFlags.Instance);
            spaceProp?.SetValue(anchor, ovrSpace);
        }

        if (!sceneGameObject.TryGetComponent<OVRSemanticClassification>(out var classification))
        {
            classification = sceneGameObject.AddComponent<OVRSemanticClassification>();

            var list = new List<string> { OVRSceneManager.Classification.GlobalMesh };

            var labelsField = classification.GetType().GetField("_labels",
                BindingFlags.NonPublic | BindingFlags.Instance);
            labelsField?.SetValue(classification, list);
        }
    }

    /// <summary>
    /// For 2 walls defined by 3 corner points, get the inset direction from the inside corner.
    /// It will always point to the "inside" of the room
    /// </summary>
    private static Vector3 GetInsetDirection(Vector3 point1, Vector3 point2, Vector3 point3)
    {
        Vector3 vec1 = (point2 - point1).normalized;
        Vector3 vec2 = (point3 - point2).normalized;
        Vector3 insetDir = Vector3.Normalize((vec2 - vec1) * 0.5f);
        Vector3 wall1Normal = Vector3.Cross(Vector3.up, vec1);
        Vector3 wall2Normal = Vector3.Cross(Vector3.up, vec2);
        insetDir *= Vector3.Cross(vec1, vec2).y > 0 ? 1.0f : -1.0f;
        if (insetDir.magnitude <= Mathf.Epsilon)
        {
            insetDir = Vector3.forward;
        }

        return insetDir;
    }

    /// <summary>
    /// Given a clockwise set of points (outer then inner), set up triangle indices accordingly
    /// </summary>
    private static void CreateBorderedPolygon(ref int[] indexArray, ref int indexCounter, int baseCount, int pointsInLoop, List<Vector3> loopPoints = null, bool flipNormal = false, List<Vector3> insetPoints = null)
    {
        try {
            //int baseCount = baseIndex * 8; // 8 because each wall always has 8 vertices
            for (int j = 0; j < pointsInLoop; j++)
            {
                int id1 = ((j + 1) % pointsInLoop);
                int id2 = pointsInLoop + j;

                indexArray[indexCounter++] = baseCount + j;
                indexArray[indexCounter++] = baseCount + (flipNormal ? id2 : id1);
                indexArray[indexCounter++] = baseCount + (flipNormal ? id1 : id2);

                indexArray[indexCounter++] = baseCount + pointsInLoop + ((j + 1) % pointsInLoop);
                indexArray[indexCounter++] = baseCount + (flipNormal ? id1 : id2);
                indexArray[indexCounter++] = baseCount + (flipNormal ? id2 : id1);
            }

            int capTriCount = pointsInLoop - 2;

            if (loopPoints != null)
            {
                //use triangulator
                // WARNING: triangulator fails if any points are perfectly co-linear
                // in practice this is rare due to floating point imprecision
                List<Vector2> points2d = new List<Vector2>(loopPoints.Count);
                for (int i = 0; i < pointsInLoop; i++)
                {
                    Vector3 refP = insetPoints != null ? insetPoints[i] : loopPoints[i];
                    points2d.Add(new Vector2(refP.x, refP.z));
                }

                Triangulator triangulator = new Triangulator(points2d.ToArray());
                int[] indices = triangulator.Triangulate();
                for (int j = 0; j < capTriCount; j++)
                {
                    int id0 = pointsInLoop + indices[j * 3];
                    int id1 = pointsInLoop + indices[j * 3 + 1];
                    int id2 = pointsInLoop + indices[j * 3 + 2];

                    indexArray[indexCounter++] = baseCount + id0;
                    indexArray[indexCounter++] = baseCount + (flipNormal ? id2 : id1);
                    indexArray[indexCounter++] = baseCount + (flipNormal ? id1 : id2);
                }
            }
            else
            {
                //use simple triangle fan
                for (int j = 0; j < capTriCount; j++)
                {
                    int id1 = pointsInLoop + j + 1;
                    int id2 = pointsInLoop + j + 2;
                    indexArray[indexCounter++] = baseCount + pointsInLoop;
                    indexArray[indexCounter++] = baseCount + (flipNormal ? id2 : id1);
                    indexArray[indexCounter++] = baseCount + (flipNormal ? id1 : id2);
                }
            }
        }
        catch (IndexOutOfRangeException exception)
        {
            Debug.LogException(exception);
            Debug.LogError("Error parsing walls, are the walls intersecting? ");
        }
    }
}
