using System;
using UnityEditor;
using UnityEngine;

// now it is for creating invisible colliders from terrain only - by flipping a mesh generated from terrain and add mesh collider to it
public class MeshFlipper : EditorWindow
{
    [MenuItem("Tools/FMB Toolset/Flip Mesh Upside Down")]
    private static void FlipSelectedMeshUpsideDown()
    {
        string path = EditorUtility.OpenFilePanel("Select Mesh to Flip", "Assets/", "fbx,asset,mesh,prefab");
        if (path.StartsWith(Application.dataPath)) path = "Assets" + path.Substring(Application.dataPath.Length);
        if (string.IsNullOrEmpty(path))
            return;

        Mesh originalMesh;

        GameObject selectedObject = AssetDatabase.LoadAssetAtPath<GameObject>(path);

        if (selectedObject != null)
        {
            if (selectedObject.GetComponent<MeshFilter>() == null) {
                Debug.LogError("Please select a GameObject with a MeshFilter component.");
                return;
            }

            originalMesh = selectedObject.GetComponent<MeshFilter>().sharedMesh;
        }
        else
        {
            originalMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        }


        if (originalMesh == null) {
            Debug.LogError("Selected GameObject does not have a valid mesh.");
            return;
        }

        Mesh flippedMesh = FlipMesh(originalMesh);

        string savePath = EditorUtility.SaveFilePanel("Save Flipped Mesh", "Assets/", "FlippedMesh", "asset");

        if (savePath.StartsWith(Application.dataPath))
        {
            savePath = "Assets" + savePath.Substring(Application.dataPath.Length);
        }
        if (!string.IsNullOrEmpty(savePath))
        {
            AssetDatabase.CreateAsset(flippedMesh, savePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("Flipped mesh saved at: " + savePath);
        }
    }

    private static Mesh FlipMesh(Mesh originalMesh)
    {
        Mesh flippedMesh = new Mesh();
        flippedMesh.vertices = Array.ConvertAll(originalMesh.vertices, v => new Vector3(v.x, -v.y, v.z));
        flippedMesh.normals = Array.ConvertAll(originalMesh.normals, n => new Vector3(n.x, n.y, n.z)); // not flipping normals to make the collision right
        flippedMesh.triangles = Array.ConvertAll(originalMesh.triangles, t => t);

        flippedMesh.RecalculateNormals();
        flippedMesh.RecalculateBounds();

        return flippedMesh;
    }
}
