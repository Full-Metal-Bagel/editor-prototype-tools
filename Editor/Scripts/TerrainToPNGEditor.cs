using UnityEngine;
using UnityEditor;

public class TerrainToPNGEditor : EditorWindow
{
    [SerializeField]
    private Terrain _terrain;
    [SerializeField]
    private Gradient _outputGradient = new Gradient();
    [SerializeField]
    private Vector2 _inputRange;

    [SerializeField]
    private Texture2D _previewTexture;

    [MenuItem("Tools/FMB Toolset/TerrainToPNG")]
    private static void Init()
    {
        TerrainToPNGEditor window = GetWindow<TerrainToPNGEditor>();
        window.titleContent = new GUIContent("TerrainToPNG");
        window.Show();
    }

    private void OnEnable() {
        SceneView.duringSceneGui += OnSceneGUI;
    }

    private void OnDisable() {
        SceneView.duringSceneGui -= OnSceneGUI;
        DestroyImmediate(_previewTexture);
    }

    private void OnSceneGUI(SceneView sceneView) {
        if (_terrain != null) {
            Handles.color = new Color(0, 0.5f, 1);
            Vector3 terrainSize = _terrain.terrainData.size;
            Handles.DrawWireCube(
                _terrain.transform.position + new Vector3(terrainSize.x / 2, (_inputRange.y + _inputRange.x) / 2, terrainSize.z / 2)
                , new Vector3(terrainSize.x, _inputRange.y - _inputRange.x, terrainSize.z));
        }
    }

    void OnGUI()
    {
        GUILayout.Label("Terrain to PNG Converter", EditorStyles.boldLabel);

        _terrain = EditorGUILayout.ObjectField("Terrain", _terrain, typeof(Terrain), true) as Terrain;
        _outputGradient = EditorGUILayout.GradientField("Output Gradient", _outputGradient);
        EditorGUILayout.MinMaxSlider("Use Terrain Height Range", ref _inputRange.x, ref _inputRange.y,
            0, _terrain == null ? 1 : _terrain.terrainData.heightmapScale.y);
        EditorGUILayout.LabelField($"{_inputRange.x} - {_inputRange.y}");

        if (GUILayout.Button("Convert"))
        {
            UpdatePreviewTexture(ConvertToTexture());
        }

        GUILayout.Space(10);

        // Display preview texture
        if (_previewTexture != null)
        {
            GUILayout.Label("Preview:");
            GUILayout.Label(_previewTexture, GUILayout.MaxHeight(200), GUILayout.MaxWidth(200));
        }

        using (new EditorGUI.DisabledScope(_previewTexture == null))
        {
            if (GUILayout.Button("Save As PNG"))
            {
                SaveAsPNG(_previewTexture);
            }
        }
    }

    private Texture2D ConvertToTexture()
    {
        if (_terrain != null)
        {
            TerrainData terrainData = _terrain.terrainData;
            int width = terrainData.heightmapResolution;
            int height = terrainData.heightmapResolution;

            // Get the heights of the terrain
            float[,] heights = terrainData.GetHeights(0, 0, width, height);

            // Create a Texture2D to store the heightmap data
            Texture2D texture = new Texture2D(width, height);

            // Convert the heights to color values using the gradient
            Color[] colors = new Color[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float heightValue = heights[y, x];
                    float lowBound = _inputRange.x / terrainData.heightmapScale.y;
                    float highBound = _inputRange.y / terrainData.heightmapScale.y;
                    Color color = _outputGradient.Evaluate((heightValue - lowBound) / (highBound - lowBound));
                    colors[y * width + x] = color;
                }
            }

            // Set the colors to the texture
            texture.SetPixels(colors);
            texture.Apply();

            float aspectRatio = terrainData.heightmapScale.x / terrainData.heightmapScale.z;
            (int newTextureWidth, int newTextureHeight) = aspectRatio >= 1 ? (texture.width, (int)(texture.width / aspectRatio)) : ((int)(texture.height * aspectRatio), texture.height);
            Texture2D scaledTexture = CreateScaledTexture(texture, newTextureWidth, newTextureHeight);
            DestroyImmediate(texture);

            return scaledTexture;
        }
        else
        {
            Debug.LogError("Terrain reference is missing. Please assign the terrain.");
            return null;
        }
    }

    private void UpdatePreviewTexture(Texture2D newTexture = null)
    {
        if (_previewTexture != null)
        {
            DestroyImmediate(_previewTexture); // Clean up previous preview texture
        }

        if (newTexture != null)
        {
            // Clone the texture to avoid modifying the original texture
            _previewTexture = Instantiate(newTexture);
        }
    }

    private Texture2D CreateScaledTexture(Texture2D sourceTexture, int newWidth, int newHeight)
    {
        var rt = RenderTexture.GetTemporary(newWidth, newHeight, 0, RenderTextureFormat.ARGB32);
        Graphics.Blit(sourceTexture, rt);
        Texture2D scaledTexture = new Texture2D(newWidth, newHeight, TextureFormat.ARGB32, false);
        RenderTexture.active = rt;
        scaledTexture.ReadPixels(new Rect(0, 0, newWidth, newHeight), 0, 0);
        scaledTexture.Apply();
        RenderTexture.active = null;
        RenderTexture.ReleaseTemporary(rt);
        return scaledTexture;
    }

    private void SaveAsPNG(Texture2D texture)
    {
        // Encode the texture to a PNG file
        byte[] bytes = texture.EncodeToPNG();
        string path = EditorUtility.SaveFilePanel("Save PNG", "Assets", "TerrainHeightmap", "png");
        if (!string.IsNullOrEmpty(path))
        {
            System.IO.File.WriteAllBytes(path, bytes);
            AssetDatabase.Refresh();
            Debug.Log("Terrain heightmap saved as " + path);
        }
    }
}
