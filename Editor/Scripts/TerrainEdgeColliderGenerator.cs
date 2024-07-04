using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class TerrainEdgeColliderEditorTool : EditorWindow
{
    private Terrain targetTerrain;
    private float minY = 0f;
    private float maxY = 10f;
    private float extrusionDepth = 1f;
    private float simplificationEpsilon = 0.1f;
    private string newGameObjectName = "TerrainEdgeCollider";

    [MenuItem("Tools/Terrain Edge Collider Generator")]
    public static void ShowWindow()
    {
        GetWindow<TerrainEdgeColliderEditorTool>("Terrain Edge Collider Generator");
    }

    private void OnGUI()
    {
        GUILayout.Label("Terrain Edge Collider Generator", EditorStyles.boldLabel);

        targetTerrain = (Terrain)EditorGUILayout.ObjectField("Target Terrain", targetTerrain, typeof(Terrain), true);
        minY = EditorGUILayout.FloatField("Min Height", minY);
        maxY = EditorGUILayout.FloatField("Max Height", maxY);
        extrusionDepth = EditorGUILayout.FloatField("Extrusion Depth", extrusionDepth);
        simplificationEpsilon = EditorGUILayout.FloatField("Simplification Epsilon", simplificationEpsilon);
        newGameObjectName = EditorGUILayout.TextField("New GameObject Name", newGameObjectName);

        if (GUILayout.Button("Generate 2D Polygon"))
        {
            if (targetTerrain == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign a target terrain.", "OK");
                return;
            }

            Generate2DPolygon();
        }

        if (GUILayout.Button("Generate Edge Collider Mesh"))
        {
            if (targetTerrain == null)
            {
                EditorUtility.DisplayDialog("Error", "Please assign a target terrain.", "OK");
                return;
            }

            GenerateEdgeColliderMesh();
        }
        GUI.enabled = true;
    }

    private void Generate2DPolygon()
    {
        bool[,] heightSelection = SampleTerrainHeight();
        List<Vector2> contourPoints = GenerateContour(heightSelection);
        var orderedPoints = OrderPoints(contourPoints);
        var generatedObjects = new List<Object>();
        foreach (var points in orderedPoints)
        {
            var polygon = SimplifyPolygon(points);

            // Create a new GameObject and add a LineRenderer component
            GameObject polygonObject = new GameObject("2D Polygon Preview");
            Undo.RegisterCreatedObjectUndo(polygonObject, "Generate Preview");
            LineRenderer lineRenderer = polygonObject.AddComponent<LineRenderer>();

            // Set up the LineRenderer
            lineRenderer.positionCount = polygon.Count + 1; // +1 to close the loop
            lineRenderer.startWidth = lineRenderer.endWidth = 1f;
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.startColor = lineRenderer.endColor = Color.green;

            // Set the positions for the LineRenderer
            Vector3[] positions = new Vector3[polygon.Count + 1];
            for (int i = 0; i < polygon.Count; i++)
            {
                positions[i] = new Vector3(polygon[i].x, polygon[i].y, 0) + targetTerrain.transform.position;
            }

            positions[polygon.Count] = positions[0]; // Close the loop
            lineRenderer.SetPositions(positions);

            generatedObjects.Add(polygonObject);
        }

        // Select the newly created GameObject
        Selection.objects = generatedObjects.ToArray();

        EditorUtility.DisplayDialog("Success", "2D Polygon generated successfully!", "OK");
    }

    private void GenerateEdgeColliderMesh()
    {
        bool[,] heightSelection = SampleTerrainHeight();
        List<Vector2> contourPoints = GenerateContour(heightSelection);
        var orderedPoints = OrderPoints(contourPoints);
        var generatedObjects = new List<Object>();
        foreach (var points in orderedPoints)
        {
            var simplifiedContour = SimplifyPolygon(points);
            Mesh colliderMesh = ExtrudePolygon(simplifiedContour);

            // Create a new GameObject and add components
            GameObject newGameObject = new GameObject(newGameObjectName);
            Undo.RegisterCreatedObjectUndo(newGameObject, "Generate Preview");
            MeshFilter meshFilter = newGameObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = colliderMesh;

            MeshRenderer meshRenderer = newGameObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = new Material(Shader.Find("Standard"));

            MeshCollider meshCollider = newGameObject.AddComponent<MeshCollider>();
            meshCollider.sharedMesh = colliderMesh;

            // Position the new GameObject at the terrain's position
            newGameObject.transform.position = targetTerrain.transform.position;
            newGameObject.transform.rotation = Quaternion.Euler(90, 0, 0);
            var terrainData = targetTerrain.terrainData;
            newGameObject.transform.localScale = new Vector3(1 / (float)terrainData.heightmapResolution * terrainData.size.x, 1 / (float)terrainData.heightmapResolution * terrainData.size.z, 1);

            generatedObjects.Add(newGameObject);
        }

        // Select the newly created GameObject
        Selection.objects = generatedObjects.ToArray();

        EditorUtility.DisplayDialog("Success", "Edge Collider Mesh generated successfully!", "OK");
    }

    private bool[,] SampleTerrainHeight()
    {
        TerrainData terrainData = targetTerrain.terrainData;
        int width = terrainData.heightmapResolution;
        int height = terrainData.heightmapResolution;
        float[,] heights = terrainData.GetHeights(0, 0, width, height);
        bool[,] selection = new bool[width, height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float worldHeight = heights[y, x] * terrainData.size.y;
                selection[x, y] = worldHeight >= minY && worldHeight <= maxY;
            }
        }

        return selection;
    }

    private List<Vector2> GenerateContour(bool[,] heightSelection)
    {
        int width = heightSelection.GetLength(0);
        int height = heightSelection.GetLength(1);
        List<Vector2> contourPoints = new List<Vector2>();

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                if (heightSelection[x, y] && IsEdgePixel(heightSelection, x, y))
                {
                    contourPoints.Add(new Vector2(x, y));
                }
            }
        }

        return contourPoints;
    }

    private bool IsEdgePixel(bool[,] selection, int x, int y)
    {
        int width = selection.GetLength(0);
        int height = selection.GetLength(1);

        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                int nx = x + dx;
                int ny = y + dy;

                if (nx >= 0 && nx < width && ny >= 0 && ny < height)
                {
                    if (!selection[nx, ny])
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    private List<List<Vector2>> OrderPoints(List<Vector2> points)
    {
        if (points.Count <= 3)
            return new List<List<Vector2>>() { points };

        HashSet<Vector2> remainingPoints = new HashSet<Vector2>(points);
        var polygons = new List<List<Vector2>>();
        while (remainingPoints.Count > 0)
        {
            List<Vector2> orderedPoints = new List<Vector2>();
            polygons.Add(orderedPoints);

            // Start with the leftmost point
            Vector2 currentPoint = remainingPoints.OrderBy(p => p.x).First();
            orderedPoints.Add(currentPoint);
            remainingPoints.Remove(currentPoint);

            while (remainingPoints.Count > 0 && FindNextPoint(currentPoint, remainingPoints, out Vector2 nextPoint))
            {
                orderedPoints.Add(nextPoint);
                remainingPoints.Remove(nextPoint);
                currentPoint = nextPoint;
            }
        }

        return polygons;
    }

    private bool FindNextPoint(Vector2 currentPoint, HashSet<Vector2> remainingPoints, out Vector2 point)
    {
        return EnumerableHelper.TryFindFirst(remainingPoints.Where(p => Vector2.Distance(p, currentPoint) <= 10).OrderBy(p => Vector2.Distance(p, currentPoint)), out point);
    }

    private List<Vector2> SimplifyPolygon(List<Vector2> points)
    {
        if (points.Count <= 3)
            return points;

        var simplified = new List<Vector2> { points[0] };

        for (int i = 1; i < points.Count - 1; i++)
        {
            double area = TriangleArea(simplified.Last(), points[i], points[i + 1]);
            if (Mathf.Abs((float)area) > simplificationEpsilon)
            {
                simplified.Add(points[i]);
            }
        }

        simplified.Add(points.Last());
        return simplified;
    }

    private double TriangleArea(Vector2 a, Vector2 b, Vector2 c)
    {
        return Mathf.Abs((b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y)) / 2.0;
    }

    private Mesh ExtrudePolygon(List<Vector2> polygonPoints)
    {
        Mesh mesh = new Mesh();
        int vertexCount = polygonPoints.Count;
        Vector3[] vertices = new Vector3[vertexCount * 2];
        int[] triangles = new int[vertexCount * 12];

        // Create vertices
        for (int i = 0; i < vertexCount; i++)
        {
            vertices[i] = new Vector3(polygonPoints[i].x, polygonPoints[i].y, 0);
            vertices[i + vertexCount] = new Vector3(polygonPoints[i].x, polygonPoints[i].y, extrusionDepth);
        }

        // Create triangles with inverted normals
        int triangleIndex = 0;
        for (int i = 0; i < vertexCount; i++)
        {
            int nextI = (i + 1) % vertexCount;

            // Front face (inverted)
            triangles[triangleIndex++] = i + vertexCount;
            triangles[triangleIndex++] = nextI;
            triangles[triangleIndex++] = i;

            triangles[triangleIndex++] = i + vertexCount;
            triangles[triangleIndex++] = nextI + vertexCount;
            triangles[triangleIndex++] = nextI;

            // Side face (inverted)
            triangles[triangleIndex++] = nextI;
            triangles[triangleIndex++] = i + vertexCount;
            triangles[triangleIndex++] = i;

            triangles[triangleIndex++] = nextI + vertexCount;
            triangles[triangleIndex++] = i + vertexCount;
            triangles[triangleIndex++] = nextI;
        }

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();

        return mesh;
    }

    private static class EnumerableHelper
    {
        public static bool TryFindFirst<T>(IEnumerable<T> source, out T res)
        {
            var enumerator = source.GetEnumerator();
            if (enumerator.MoveNext())
            {
                res = enumerator.Current;
                return true;
            }

            res = default;
            return false;
        }
    }
}
