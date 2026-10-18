using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Oyundaki gerçek modellerin (sprey tabancası, vakum, hamamböceği) şeffaf arka planlı fotoğrafını çeker,
/// PNG simge olarak kaydeder ve ToolData/BugData'nın icon alanına bağlar.
/// Böylece envanter ve HUD simgeleri oyundaki modellerle birebir aynı çizgi film görünümünde olur.
/// </summary>
public static class InventoryIconBaker
{
    private const string Folder = "Assets/_Project/UI/Icons";
    private const int Resolution = 256;
    private const int TempLayer = 31;

    [MenuItem("PEST CO/Envanter Simgelerini Oluştur")]
    public static void BakeAll()
    {
        Bake("SprayGunModel", "Icon_Spray", new Vector3(15f, -135f, 0f), "t:ToolData Tool_Spray", null);
        Bake("VacuumGunModel", "Icon_Vacuum", new Vector3(10f, 90f, 0f), "t:ToolData Tool_Vacuum", null);
        Bake("Roach_1", "Icon_Roach", new Vector3(20f, 150f, 0f), null, "t:BugData Hamamboecegi");
        BakeDrawnIcons();
        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// Modeli olmayan eşyaların çizgi film simgelerini PNG olarak kaydeder ve bağlar.
    /// (FPS: oyun sırasında ilk alındıklarında kodla çizilmeleri ~200 ms takılma yapıyordu.)
    /// </summary>
    public static void BakeDrawnIcons()
    {
        AssignIcon(SaveSprite(CartoonIcons.Tool("trap"), "Icon_Trap"), "t:ToolData Tool_Trap", null);
        AssignIcon(SaveSprite(CartoonIcons.Tool("racket"), "Icon_Racket"), "t:ToolData Tool_Racket", null);

        Sprite soda = SaveSprite(CartoonIcons.Food(FoodType.NuclearSoda), "Icon_Soda");
        Sprite candy = SaveSprite(CartoonIcons.Food(FoodType.ExplosiveCandy), "Icon_Candy");
        Sprite cookie = SaveSprite(CartoonIcons.Food(FoodType.CursedCookie), "Icon_Cookie");

        foreach (var hotbar in Object.FindObjectsByType<InventoryHotbar>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var so = new SerializedObject(hotbar);
            so.FindProperty("_nuclearSodaIcon").objectReferenceValue = soda;
            so.FindProperty("_explosiveCandyIcon").objectReferenceValue = candy;
            so.FindProperty("_cursedCookieIcon").objectReferenceValue = cookie;
            so.ApplyModifiedProperties();
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(hotbar.gameObject.scene);
        }
    }

    private static Sprite SaveSprite(Sprite sprite, string fileName)
    {
        string path = SavePng(sprite.texture, fileName);
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    /// <summary>
    /// Sahnedeki nesnenin bir kopyasını uzakta, geçici katmanda, verilen açıyla çeker.
    /// </summary>
    public static Sprite Bake(string sourceName, string fileName, Vector3 viewEuler, string toolFilter, string bugFilter)
    {
        GameObject source = FindInScene(sourceName);
        if (source == null)
        {
            Debug.LogWarning($"PEST CO: '{sourceName}' sahnede bulunamadı, simgesi çekilemedi.");
            return null;
        }

        GameObject copy = Object.Instantiate(source);
        copy.hideFlags = HideFlags.HideAndDontSave;
        foreach (var behaviour in copy.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
        copy.SetActive(true);
        copy.transform.SetPositionAndRotation(new Vector3(0f, -2000f, 0f), Quaternion.identity);
        copy.transform.localScale = source.transform.lossyScale;
        SetLayer(copy, TempLayer);

        // Sağlık barı gibi düz yardımcı objeler simgeye girmesin.
        foreach (var particle in copy.GetComponentsInChildren<ParticleSystem>(true)) particle.gameObject.SetActive(false);

        Bounds bounds = GetBounds(copy);
        var cameraGo = new GameObject("IconCamera") { hideFlags = HideFlags.HideAndDontSave };
        var cam = cameraGo.AddComponent<Camera>();
        cam.cullingMask = 1 << TempLayer;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        cam.orthographic = true;
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 100f;
        cam.allowHDR = false;
        cam.allowMSAA = true;
        var urp = cam.GetUniversalAdditionalCameraData();
        urp.renderPostProcessing = false;
        urp.renderShadows = false;

        Quaternion view = Quaternion.Euler(viewEuler);
        float radius = bounds.extents.magnitude;
        cam.transform.SetPositionAndRotation(bounds.center - view * Vector3.forward * (radius * 4f), view);
        cam.orthographicSize = FitSize(cam, bounds) * 1.08f;

        var rt = RenderTexture.GetTemporary(Resolution, Resolution, 24, RenderTextureFormat.ARGB32);
        rt.antiAliasing = 8;
        cam.targetTexture = rt;
        cam.Render();

        RenderTexture previous = RenderTexture.active;
        RenderTexture.active = rt;
        var texture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false);
        texture.ReadPixels(new Rect(0, 0, Resolution, Resolution), 0, 0);
        texture.Apply();
        RenderTexture.active = previous;

        cam.targetTexture = null;
        RenderTexture.ReleaseTemporary(rt);
        Object.DestroyImmediate(cameraGo);
        Object.DestroyImmediate(copy);

        Texture2D trimmed = TrimToSquare(texture, 0.06f);
        string path = SavePng(trimmed, fileName);
        Object.DestroyImmediate(texture);
        Object.DestroyImmediate(trimmed);

        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
        AssignIcon(sprite, toolFilter, bugFilter);
        Debug.Log($"PEST CO: '{sourceName}' simgesi oluşturuldu -> {path}");
        return sprite;
    }

    /// <summary>
    /// Şeffaf kenar boşluklarını atar ve modeli ortalanmış kare bir simgeye yeniden ölçekler
    /// (slotu dolduran büyük simge).
    /// </summary>
    private static Texture2D TrimToSquare(Texture2D source, float padding)
    {
        int w = source.width, h = source.height;
        Color[] pixels = source.GetPixels();
        int minX = w, minY = h, maxX = -1, maxY = -1;
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            if (pixels[y * w + x].a < 0.05f) continue;
            minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
            minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
        }
        if (maxX < 0) return Object.Instantiate(source);

        float side = Mathf.Max(maxX - minX + 1, maxY - minY + 1) * (1f + padding * 2f);
        float cx = (minX + maxX + 1) * 0.5f, cy = (minY + maxY + 1) * 0.5f;

        var result = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32, false);
        var output = new Color[Resolution * Resolution];
        for (int y = 0; y < Resolution; y++)
        for (int x = 0; x < Resolution; x++)
        {
            float sx = cx + ((x + 0.5f) / Resolution - 0.5f) * side;
            float sy = cy + ((y + 0.5f) / Resolution - 0.5f) * side;
            output[y * Resolution + x] = sx < 0 || sy < 0 || sx >= w || sy >= h
                ? Color.clear
                : source.GetPixelBilinear(sx / w, sy / h);
        }
        result.SetPixels(output);
        result.Apply();
        return result;
    }

    /// <summary>Kamera açısından bakınca nesnenin ekrana sığacağı ortografik boyutu hesaplar.</summary>
    private static float FitSize(Camera cam, Bounds bounds)
    {
        float maxX = 0f, maxY = 0f;
        Vector3 ext = bounds.extents;
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = bounds.center + Vector3.Scale(ext, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            Vector3 local = cam.transform.InverseTransformPoint(corner);
            maxX = Mathf.Max(maxX, Mathf.Abs(local.x));
            maxY = Mathf.Max(maxY, Mathf.Abs(local.y));
        }
        return Mathf.Max(maxX, maxY);
    }

    private static Bounds GetBounds(GameObject go)
    {
        var renderers = go.GetComponentsInChildren<Renderer>();
        Bounds bounds = new Bounds(go.transform.position, Vector3.zero);
        bool first = true;
        foreach (var r in renderers)
        {
            if (r is ParticleSystemRenderer || !r.gameObject.activeInHierarchy) continue;
            if (first) { bounds = r.bounds; first = false; }
            else bounds.Encapsulate(r.bounds);
        }
        return bounds;
    }

    private static string SavePng(Texture2D texture, string fileName)
    {
        Directory.CreateDirectory(Folder);
        string path = $"{Folder}/{fileName}.png";
        File.WriteAllBytes(path, texture.EncodeToPNG());
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

        var importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        return path;
    }

    private static void AssignIcon(Sprite sprite, string toolFilter, string bugFilter)
    {
        if (sprite == null) return;

        if (!string.IsNullOrEmpty(toolFilter))
        {
            foreach (string guid in AssetDatabase.FindAssets(toolFilter))
            {
                var data = AssetDatabase.LoadAssetAtPath<ToolData>(AssetDatabase.GUIDToAssetPath(guid));
                if (data == null) continue;
                data.icon = sprite;
                EditorUtility.SetDirty(data);
            }
        }

        if (!string.IsNullOrEmpty(bugFilter))
        {
            foreach (string guid in AssetDatabase.FindAssets(bugFilter))
            {
                var data = AssetDatabase.LoadAssetAtPath<BugData>(AssetDatabase.GUIDToAssetPath(guid));
                if (data == null) continue;
                data.icon = sprite;
                EditorUtility.SetDirty(data);
            }
        }
    }

    private static GameObject FindInScene(string name)
    {
        foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == name && t.gameObject.scene.IsValid()) return t.gameObject;
        return null;
    }

    private static void SetLayer(GameObject go, int layer)
    {
        go.layer = layer;
        foreach (Transform child in go.transform) SetLayer(child.gameObject, layer);
    }
}
