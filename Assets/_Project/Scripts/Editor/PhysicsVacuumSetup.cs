#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Fizik vakumu tek tıkla kurar: Pullable layer, VacuumSettings asset'i, Tool_2'ye VacuumGun,
// sahneye test nesneleri ve OutOfBounds trigger'ı. Tekrar çalıştırılabilir.
public static class PhysicsVacuumSetup
{
    private const string DataFolder = "Assets/_Project/Data/Vacuum";
    private const string FxFolder = "Assets/_Project/FX/Vacuum";
    private const string SettingsPath = DataFolder + "/VacuumSettings.asset";
    private const string BeamMatPath = FxFolder + "/M_VacuumBeam.mat";
    private const string PropMatPath = FxFolder + "/M_PullableToon.mat";
    private const string PullableLayerName = "Pullable";
    private const string DemoRoot = "PullableDemo";
    private const string ZoneName = "OutOfBoundsZone";

    [MenuItem("PEST CO/Fizik Vakum Kur", priority = 51)]
    public static void Setup()
    {
        if (Application.isPlaying)
        {
            Debug.LogError("PEST CO: Fizik vakum Play modunda kurulamaz.");
            return;
        }

        int layer = EnsureLayer(PullableLayerName);
        if (layer < 0) return;

        EnsureFolder("Assets/_Project/Data", "Vacuum");
        EnsureFolder("Assets/_Project/FX", "Vacuum");

        VacuumSettings settings = EnsureSettings(layer);
        Material propMat = EnsureMaterial(PropMatPath, "PestCo/Toon", new Color(1f, 0.55f, 0.25f));

        GameObject tool = FindInScene("Tool_2");
        if (tool == null) { Debug.LogError("PEST CO: Tool_2 bulunamadi."); return; }
        VacuumGun gun = PestCoSetup.AddOrGet<VacuumGun>(tool);
        var so = new SerializedObject(gun);
        so.FindProperty("settings").objectReferenceValue = settings;
        so.FindProperty("cam").objectReferenceValue = Camera.main;
        Transform fx = tool.transform.Find("VacuumFX");
        so.FindProperty("muzzle").objectReferenceValue = fx != null ? fx : tool.transform;
        so.ApplyModifiedProperties();
        PestCoSetup.MarkDirty(tool);

        BuildDemoProps(layer, settings, propMat);
        BuildBoundsZone();

        EditorSceneManager.MarkSceneDirty(tool.scene);
        Selection.activeGameObject = tool;
        Debug.Log("PEST CO: Fizik vakum kuruldu. Sag tik basili = cek, sol tik = firlat, G = birak. Ctrl+S ile kaydet.", tool);
    }

    private static int EnsureLayer(string name)
    {
        int existing = LayerMask.NameToLayer(name);
        if (existing >= 0) return existing;

        var tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty layers = tagManager.FindProperty("layers");
        for (int i = 8; i < 32; i++)
        {
            SerializedProperty p = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(p.stringValue))
            {
                p.stringValue = name;
                tagManager.ApplyModifiedProperties();
                return i;
            }
        }
        Debug.LogError("PEST CO: Bos layer slotu yok.");
        return -1;
    }

    private static VacuumSettings EnsureSettings(int layer)
    {
        var s = AssetDatabase.LoadAssetAtPath<VacuumSettings>(SettingsPath);
        if (s == null)
        {
            s = ScriptableObject.CreateInstance<VacuumSettings>();
            AssetDatabase.CreateAsset(s, SettingsPath);
            s.Reset();
        }
        s.pullableLayers = 1 << layer;
        s.obstacleLayers = 1 << LayerMask.NameToLayer("Default");
        s.beamMaterial = EnsureMaterial(BeamMatPath, "Universal Render Pipeline/Particles/Unlit", Color.white);
        EditorUtility.SetDirty(s);
        AssetDatabase.SaveAssets();
        return s;
    }

    private static Material EnsureMaterial(string path, string shaderName, Color color)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;
        Shader sh = Shader.Find(shaderName);
        if (sh == null) sh = Shader.Find("Universal Render Pipeline/Unlit");
        mat = new Material(sh);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    // Oyuncunun önünde zemine 5 renkli test nesnesi (kutu/küre), farklı kütlelerde.
    private static void BuildDemoProps(int layer, VacuumSettings settings, Material mat)
    {
        GameObject old = FindInScene(DemoRoot);
        if (old != null) Object.DestroyImmediate(old);

        GameObject player = FindInScene("Player");
        Vector3 basePos = player != null ? player.transform.position : Vector3.zero;
        // Duvara bakmasın: oyuncunun etrafında en açık yönü seç.
        Vector3 fwd = player != null ? player.transform.forward : Vector3.forward;
        float bestFree = -1f;
        for (int a = 0; a < 16; a++)
        {
            Vector3 d = Quaternion.Euler(0f, a * 22.5f, 0f) * Vector3.forward;
            float free = Physics.Raycast(basePos + Vector3.up * 0.5f, d, out RaycastHit wall, 8f) ? wall.distance : 8f;
            if (free > bestFree + 0.01f) { bestFree = free; fwd = d; }
        }
        Vector3 right = Vector3.Cross(Vector3.up, fwd);
        float forwardDistance = Mathf.Clamp(bestFree * 0.6f, 2.5f, 5f);

        var root = new GameObject(DemoRoot);
        Undo.RegisterCreatedObjectUndo(root, "Create " + DemoRoot);
        PrimitiveType[] types = { PrimitiveType.Cube, PrimitiveType.Sphere, PrimitiveType.Cube, PrimitiveType.Sphere, PrimitiveType.Cube };
        float[] masses = { 0.5f, 1f, 3f, 0.3f, 8f };
        Color[] colors = { new Color(1f, 0.55f, 0.25f), new Color(0.4f, 0.8f, 1f), new Color(0.9f, 0.35f, 0.5f), new Color(0.6f, 0.9f, 0.4f), new Color(0.8f, 0.7f, 0.2f) };

        for (int i = 0; i < types.Length; i++)
        {
            Vector3 p = basePos + fwd * forwardDistance + right * ((i - 2) * 0.9f) + Vector3.up * 1.5f;
            if (Physics.Raycast(new Vector3(p.x, basePos.y + 0.5f, p.z), Vector3.down, out RaycastHit hit, 10f))
                p = hit.point + Vector3.up * 0.4f;

            var body = new GameObject("Pullable_" + i);
            body.layer = layer;
            body.transform.SetParent(root.transform, false);
            body.transform.position = p;
            var rb = body.AddComponent<Rigidbody>();
            rb.mass = masses[i];
            if (types[i] == PrimitiveType.Cube) body.AddComponent<BoxCollider>();
            else body.AddComponent<SphereCollider>();

            GameObject visual = GameObject.CreatePrimitive(types[i]);
            visual.name = "Visual";
            visual.layer = layer;
            Object.DestroyImmediate(visual.GetComponent<Collider>());
            visual.transform.SetParent(body.transform, false);
            visual.transform.localScale = Vector3.one * 0.6f;
            ResizeCollider(body, 0.6f);

            var r = visual.GetComponent<Renderer>();
            var inst = new Material(mat) { name = "M_Pullable_" + i };
            if (inst.HasProperty("_BaseColor")) inst.SetColor("_BaseColor", colors[i]);
            string matPath = FxFolder + "/M_Pullable_" + i + ".mat";
            AssetDatabase.CreateAsset(inst, matPath);
            r.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(matPath);

            var pull = body.AddComponent<PullableObject>();
            var pso = new SerializedObject(pull);
            pso.FindProperty("settings").objectReferenceValue = settings;
            pso.FindProperty("visual").objectReferenceValue = visual.transform;
            pso.ApplyModifiedProperties();
        }
    }

    private static void ResizeCollider(GameObject body, float size)
    {
        if (body.TryGetComponent(out BoxCollider box)) box.size = Vector3.one * size;
        else if (body.TryGetComponent(out SphereCollider sph)) sph.radius = size * 0.5f;
    }

    // Tüm renderer'ları saran büyük trigger kutusu: içinden çıkan Pullable spawn noktasına döner.
    private static void BuildBoundsZone()
    {
        GameObject old = FindInScene(ZoneName);
        if (old != null) Object.DestroyImmediate(old);

        bool first = true;
        Bounds b = new Bounds();
        foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
        {
            if (r is ParticleSystemRenderer || r is LineRenderer || r is TrailRenderer) continue;
            if (first) { b = r.bounds; first = false; }
            else b.Encapsulate(r.bounds);
        }
        if (first) return;
        b.Expand(new Vector3(6f, 20f, 6f));

        var go = new GameObject(ZoneName);
        Undo.RegisterCreatedObjectUndo(go, "Create " + ZoneName);
        go.layer = LayerMask.NameToLayer("Ignore Raycast");
        go.transform.position = b.center;
        var box = go.AddComponent<BoxCollider>();
        box.size = b.size;
        box.isTrigger = true;
        go.AddComponent<OutOfBoundsZone>();
    }

    private static void EnsureFolder(string parent, string name)
    {
        if (!AssetDatabase.IsValidFolder(parent + "/" + name))
            AssetDatabase.CreateFolder(parent, name);
    }

    private static GameObject FindInScene(string ad)
    {
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (t.name == ad) return t.gameObject;
        return null;
    }
}
#endif
