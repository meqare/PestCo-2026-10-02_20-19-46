using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Ev sahnesindeki (FamilyHouseBuilder) sabit kapı yapraklarını etkileşimli Door'a çevirir
// ve kapı çerçevelerini occlusion bake'inden çıkarır. Tekrar çalıştırmak güvenlidir (idempotent).
public static class HouseDoorSetup
{
    const string DoorsGroup = "DoorsWindows";
    const float OpenAngle = 90f;

    [MenuItem("PestCo/Ev Kapılarını Kur (Door + Portal)")]
    public static void Apply()
    {
        int layer = LayerMask.NameToLayer("Interactable");
        var group = GameObject.Find(DoorsGroup);
        if (group == null)
        {
            Debug.LogError("[DoorSetup] '" + DoorsGroup + "' grubu sahnede yok.");
            return;
        }

        // 1) Çerçeve/pervaz parçaları occluder olmasın: ince ve uzun oldukları için bake'de açıklığı tıkıyorlardı.
        int unflagged = 0;
        foreach (var t in group.GetComponentsInChildren<Transform>(true))
        {
            var f = GameObjectUtility.GetStaticEditorFlags(t.gameObject);
            if ((f & StaticEditorFlags.OccluderStatic) == 0) continue;
            GameObjectUtility.SetStaticEditorFlags(t.gameObject, f & ~StaticEditorFlags.OccluderStatic);
            unflagged++;
        }

        // 2) Her yaprak için Door + collider + portal.
        int doors = 0;
        foreach (var leaf in group.GetComponentsInChildren<Transform>(true))
        {
            if (leaf == null || !leaf.name.EndsWith("_Leaf")) continue; // önceki kapıda silinen çocuklar null olur
            SetupDoor(group.transform, leaf, layer);
            doors++;
        }

        // 3) Bake ayarı: kapı açıklığı (0.9 m) küçük delik sayılıp kapanmasın.
        StaticOcclusionCulling.smallestHole = 0.25f;

        EditorSceneManager_MarkDirty();
        Debug.Log("[DoorSetup] " + doors + " kapı kuruldu, " + unflagged + " parçadan OccluderStatic kaldırıldı. Occlusion'ı yeniden bake edin.");
    }

    static void SetupDoor(Transform parent, Transform leaf, int layer)
    {
        string baseName = leaf.name.Substring(0, leaf.name.Length - "_Leaf".Length);

        // Model zaten yerleştirilmişse (tekrar çalıştırma) dokunma.
        if (leaf.Find(ModelName) != null) return;

        // Yaprak panelinin boyutundan collider çıkar (Leaf çocuğu 1x1x1 küpün ölçeklenmişi, pivot yerel uzayında).
        Transform panel = leaf.Find("Leaf");
        Bounds lb;
        if (panel != null)
        {
            // Builder paneli ölçekle değil mesh köşeleriyle boyutlandırıyor: gerçek boyut mesh sınırlarında.
            var mf = panel.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;
            Bounds mb = mf.sharedMesh.bounds;
            Vector3 ps = panel.localScale;
            lb = new Bounds(panel.localPosition + Vector3.Scale(mb.center, ps), Vector3.Scale(mb.size, ps));
        }
        else
        {
            // Panel silinmişse (yarım kalmış önceki kurulum) boyutu önceki collider'dan al.
            BoxCollider old = null;
            foreach (var b in leaf.GetComponents<BoxCollider>()) if (!b.isTrigger) old = b;
            if (old == null) return;
            lb = new Bounds(old.center, old.size);
        }

        // Yaprak başlangıçta açık pozda (builder açıyı gömmüş). Yönü koru, kapalı açı = 0.
        float built = leaf.localEulerAngles.y;
        if (built > 180f) built -= 360f;
        float sign = built >= 0f ? 1f : -1f;

        var door = leaf.GetComponent<Door>();
        if (door == null) door = leaf.gameObject.AddComponent<Door>();

        // Collider'lar: kapalıyken engelleyen + her zaman açık etkileşim trigger'ı.
        var boxes = leaf.GetComponents<BoxCollider>();
        BoxCollider block = null, trigger = null;
        foreach (var b in boxes) { if (b.isTrigger) trigger = b; else block = b; }
        if (block == null) block = leaf.gameObject.AddComponent<BoxCollider>();
        if (trigger == null) trigger = leaf.gameObject.AddComponent<BoxCollider>();
        block.isTrigger = false;
        block.center = lb.center; block.size = lb.size;
        trigger.isTrigger = true;
        trigger.center = lb.center;
        trigger.size = new Vector3(lb.size.x, lb.size.y, Mathf.Max(lb.size.z, 0.3f));
        // Panel hangi ekseni ince ise onu kalınlaştır (builder ax=true: z ince, ax=false: x ince).
        if (lb.size.x < lb.size.z) trigger.size = new Vector3(Mathf.Max(lb.size.x, 0.3f), lb.size.y, lb.size.z);
        SetAllLayers(leaf, layer);

        // Portal: kapı açıklığının dünya sınırları (pervaz/eşik/başlık parçalarından).
        var portal = FindPortal(parent, baseName);
        if (portal == null)
        {
            var go = new GameObject(baseName + "_Portal");
            go.transform.SetParent(parent, false);
            portal = go.AddComponent<OcclusionPortal>();
        }
        Bounds ob = OpeningBounds(parent, baseName);
        portal.transform.position = ob.center;
        portal.transform.rotation = Quaternion.identity;
        portal.transform.localScale = Vector3.one;
        // OcclusionPortal.size yerel; parent ölçeği 1 varsayımı.
        var so = new SerializedObject(portal);
        // Not: OcclusionPortal'ın boyutu Unity API'sinde yok, serileştirilmiş alan "m_Center"/"m_Size".
        var size = so.FindProperty("m_Size");
        var center = so.FindProperty("m_Center");
        // Dünya boyutunu üst grubun ölçeğine böl (grup 1.12 ölçekli).
        Vector3 ls = parent.lossyScale;
        if (size != null) size.vector3Value = new Vector3(ob.size.x / ls.x, ob.size.y / ls.y, ob.size.z / ls.z);
        if (center != null) center.vector3Value = Vector3.zero;
        so.ApplyModifiedPropertiesWithoutUndo();

        // Door alanlarını bağla.
        var sd = new SerializedObject(door);
        sd.FindProperty("doorPivot").objectReferenceValue = leaf;
        sd.FindProperty("useExplicitAngles").boolValue = true;
        sd.FindProperty("closedAngle").floatValue = 0f;
        sd.FindProperty("openedAngle").floatValue = sign * OpenAngle;
        sd.FindProperty("blockingCollider").objectReferenceValue = block;
        sd.FindProperty("portal").objectReferenceValue = portal;
        sd.FindProperty("isOpen").boolValue = true; // sahne açık pozla başlar; Awake bunu uygular.
        sd.ApplyModifiedPropertiesWithoutUndo();

        SwapToModel(leaf, panel, lb);

        // Edit modunda da yaprağı açık pozda göster (Awake yalnız Play'de çalışır).
        leaf.localRotation = Quaternion.Euler(0f, sign * OpenAngle, 0f);
        portal.open = true;
        block.enabled = false;
    }

    // Builder'ın kutu yaprağını DOOR.fbx modeliyle değiştirir. Menteşe pivotu (leaf) aynı kalır.
    // Modelin kulpu -X tarafında (menteşe +X); yaprak ekseni ve yönüne göre döndürülür.
    static void SwapToModel(Transform leaf, Transform panel, Bounds lb)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (prefab == null) { Debug.LogError("[DoorSetup] Model yok: " + ModelPath); return; }

        // Eski kutu geometrisinin materyallerini al (kapı rengi ve pirinç kulp) — fbx'in Arnold shader'ı URP'de bozuk.
        Material leafMat = panel != null ? panel.GetComponent<Renderer>().sharedMaterial
            : AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/House/House_DoorSage.mat");
        var handle = leaf.Find("HandleA");
        Material handleMat = handle != null ? handle.GetComponent<Renderer>().sharedMaterial
            : AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/House/House_Brass.mat");

        for (int i = leaf.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(leaf.GetChild(i).gameObject);

        var model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, leaf);
        model.name = ModelName;

        // Eksen: yaprak X boyunca uzanıyorsa (ax) true. Yönü (d) panel merkezinin işaretinden bul.
        bool ax = lb.size.x > lb.size.z;
        float c = ax ? lb.center.x : lb.center.z;
        float d = c >= 0f ? 1f : -1f;
        float yaw = ax ? (d > 0f ? 180f : 0f) : (d > 0f ? 90f : -90f);

        float L = ax ? lb.size.x : lb.size.z;
        model.transform.localPosition = lb.center;
        model.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        // Model: 0.30 x 0.71 x 0.03 (genişlik, yükseklik, kalınlık). Kalınlığı sabit 2x tut.
        model.transform.localScale = new Vector3(L / ModelWidth, lb.size.y / ModelHeight, 2f);

        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
        {
            bool isLeaf = r.name == "Door" || r.name == "Prame1";
            r.sharedMaterial = isLeaf ? leafMat : handleMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }
        // Modelin kasası evin kendi çerçevesiyle çakışır; yaprakla birlikte gelen Prame1'i gizle.
        var frame = model.transform.Find("Prame1");
        if (frame != null) Object.DestroyImmediate(frame.gameObject);
    }

    const string ModelPath = "Assets/_Project/Models/DOOR.fbx";
    const string ModelName = "DoorModel";
    const float ModelWidth = 0.30f, ModelHeight = 0.71f;

    static OcclusionPortal FindPortal(Transform parent, string baseName)
    {
        var t = parent.Find(baseName + "_Portal");
        return t != null ? t.GetComponent<OcclusionPortal>() : null;
    }

    // Çerçeve parçalarının (Jamb/Head/Threshold) birleşik dünya sınırı = kapı açıklığı.
    static Bounds OpeningBounds(Transform parent, string baseName)
    {
        bool init = false;
        Bounds b = default;
        foreach (var r in parent.GetComponentsInChildren<Renderer>(true))
        {
            string n = r.name;
            if (n != baseName + "_JambL" && n != baseName + "_JambR" && n != baseName + "_Head" && n != baseName + "_Threshold") continue;
            if (!init) { b = r.bounds; init = true; } else b.Encapsulate(r.bounds);
        }
        // İnce eksene biraz pay ver ki portal duvar kalınlığını kapsasın.
        Vector3 s = b.size;
        if (s.x < s.z) s.x = Mathf.Max(s.x, 0.2f); else s.z = Mathf.Max(s.z, 0.2f);
        b.size = s;
        return b;
    }

    static void SetAllLayers(Transform root, int layer)
    {
        // Sadece yaprağın kendisi: etkileşim raycast'i collider'ın bulunduğu objeye bakar.
        root.gameObject.layer = layer;
    }

    static void EditorSceneManager_MarkDirty()
    {
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
    }
}
