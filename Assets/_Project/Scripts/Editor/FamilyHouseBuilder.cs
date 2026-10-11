using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Menu: PestCo > Aile Evi Olusturucu
// Evi "HousePlacementAnchor" altinda "SpaciousFamilyHouse" olarak kurar.
// Yerel eksenler: X genislik, Y yukseklik, Z derinlik. On giris yerel -Z yonune bakar.
// Tekrar calistirildiginda yalnizca anchor altindaki onceki "SpaciousFamilyHouse" yenilenir.
public partial class FamilyHouseBuilder : EditorWindow
{
    const string RootName = "SpaciousFamilyHouse";
    const string AnchorName = "HousePlacementAnchor";
    const string MatFolder = "Assets/_Project/Materials/House";
    static readonly Vector3 DefaultAnchorPos = new Vector3(32f, 0f, -6f);

    const float H = 2.8f;     // duvar yuksekligi
    const float TO = 0.2f;    // dis duvar kalinligi
    const float TI = 0.12f;   // ic duvar kalinligi
    const float Z1 = 4.5f;    // on oda sirasi / koridor siniri
    const float Z2 = 5.8f;    // koridor / arka oda sirasi siniri

    public Transform anchor;

    [MenuItem("PestCo/Aile Evi Olusturucu")]
    static void Open() { GetWindow<FamilyHouseBuilder>("Aile Evi"); }

    void OnGUI()
    {
        EditorGUILayout.HelpBox("Ev, Anchor noktasinin altinda olusur (yerel 0,0,0 = evin on-sol zemin kosesi). " +
            "Anchor'u sahnede tasirsan ev de tasinir. Bos birakirsan 'HousePlacementAnchor' bulunur/olusturulur.", MessageType.Info);
        anchor = (Transform)EditorGUILayout.ObjectField("Anchor", anchor, typeof(Transform), true);
        if (GUILayout.Button("Anchor noktasinda olustur / yenile")) Build(anchor);
        EditorGUILayout.Space();
        if (GUILayout.Button("Cati: ac / kapat")) ToggleChild("Roof");
        if (GUILayout.Button("Preview isiklari: ac / kapat")) ToggleChild("Preview/PreviewLights");
        if (GUILayout.Button("Preview kamera: ac / kapat")) TogglePreviewCamera();
        EditorGUILayout.Space();
        if (GUILayout.Button("Secili noktaya olustur"))
        {
            if (Selection.activeTransform == null) { Debug.LogWarning("Hierarchy'den bir nesne sec."); return; }
            anchor = Selection.activeTransform;
            Build(anchor);
        }
    }

    // ---------- durum ----------
    class Room { public string name; public float x0, z0, x1, z1; public Material floor; }
    struct Gap { public bool ax; public float f, c, w; }
    struct Op { public float c, w, sill, head; public Op(float c, float w, float sill, float head) { this.c = c; this.w = w; this.sill = sill; this.head = head; } }

    static List<Room> rooms;
    static List<Gap> gaps;
    static Dictionary<string, Material> mats;
    static Transform gS, gDW, gLiv, gKit, gMas, gKid, gBath, gWC, gSto, gEnt, gCor, gLight, gRoof, gCeil;

    // materyaller
    static Material wallM, trim, oak, oakLight, oakDark, sage, sageDark, cream, greige, mustard, terra, rugM, rugB,
        tileGray, tileWhite, stone, concrete, chrome, brass, black, glass, mirror, marble, ceramic, plant, pot, soil,
        emis, screen, roofM, gableM, kBlue, kYellow, kPink, bookA, bookB, bookC, bookD, cardboard, steel, water,
        floorOak, floorOakL, floorTile, floorStone, floorConc, doorSage, towelA, towelB, fabricDark, sagePaint, subway;

    // ====================== GIRIS ======================
    public static void Build(Transform a)
    {
        if (a == null)
        {
            var ex = GameObject.Find(AnchorName);
            if (ex == null)
            {
                ex = new GameObject(AnchorName);
                ex.transform.position = DefaultAnchorPos;
                Undo.RegisterCreatedObjectUndo(ex, "Create House Anchor");
            }
            a = ex.transform;
        }
        for (var t = a; t != null; t = t.parent)
        {
            if (t.name == RootName)
            {
                if (t.parent == null) { Debug.LogWarning("Ev kokunu anchor olarak secemezsin. Bir anchor nesnesi sec."); return; }
                a = t.parent; break;
            }
        }
        for (int i = a.childCount - 1; i >= 0; i--)
            if (a.GetChild(i).name == RootName) Undo.DestroyObjectImmediate(a.GetChild(i).gameObject);

        var root = new GameObject(RootName);
        Undo.RegisterCreatedObjectUndo(root, "Build Family House");
        root.transform.SetParent(a, false);

        Init(root.transform);
        bev = 0.008f;
        BuildStructure();
        BuildWallsAndOpenings();
        BuildLiving();
        BuildKitchen();
        BuildMaster();
        BuildKids();
        BuildBath();
        BuildWC();
        BuildStorage();
        BuildEntrance();
        BuildCorridor();
        BuildDetails();
        BuildLighting();
        BuildRoof();
        OptimizeHouse(root.transform);
        gRoof.gameObject.SetActive(false);

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(a.gameObject.scene);
        Selection.activeTransform = a;
        Debug.Log("SpaciousFamilyHouse olusturuldu: " + a.name + " altinda, dunya konumu " + a.position);
    }

    // ====================== MATERYALLER ======================
    static Material Mt(string name, Color c, float smooth = 0.3f, float metal = 0f, string tex = null, float nScale = 1f)
    {
        Material m;
        if (mats.TryGetValue(name, out m)) return m;
        string path = MatFolder + "/House_" + name + ".mat";
        m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            AssetDatabase.CreateAsset(m, path);
        }
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", smooth);
        m.SetFloat("_Metallic", metal);
        ApplyTex(m, tex, nScale);
        EditorUtility.SetDirty(m);
        mats[name] = m;
        return m;
    }
    static Color Cc(float r, float g, float b, float a = 1f) { return new Color(r, g, b, a); }

    static Material GlassMat()
    {
        var m = Mt("Glass", Cc(0.75f, 0.88f, 0.92f, 0.18f), 0.95f);
        m.SetFloat("_Surface", 1f);
        m.SetFloat("_Blend", 0f);
        m.SetFloat("_ZWrite", 0f);
        m.SetFloat("_SrcBlend", 5f);
        m.SetFloat("_DstBlend", 10f);
        m.SetOverrideTag("RenderType", "Transparent");
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.renderQueue = 3000;
        return m;
    }
    static Material EmisMat(string name, Color c, float k)
    {
        var m = Mt(name, c, 0.2f);
        m.EnableKeyword("_EMISSION");
        m.SetColor("_EmissionColor", c * k);
        m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        return m;
    }

    static void InitMats()
    {
        if (!AssetDatabase.IsValidFolder(MatFolder)) AssetDatabase.CreateFolder("Assets/_Project/Materials", "House");
        mats = new Dictionary<string, Material>();
        texSets = new Dictionary<string, TexSet>();
        meshCache = new Dictionary<string, Mesh>();
        wallM = Mt("Wall", Cc(0.94f, 0.93f, 0.89f), 0.12f, 0f, "plaster", 0.15f);
        trim = Mt("Trim", Cc(0.97f, 0.96f, 0.93f), 0.4f);
        oak = Mt("Oak", Cc(0.95f, 0.88f, 0.80f), 0.4f, 0f, "oakGrain", 0.8f);
        oakLight = Mt("OakLight", Cc(1f, 0.97f, 0.92f), 0.42f, 0f, "oakGrain", 0.8f);
        oakDark = Mt("OakDark", Cc(0.58f, 0.50f, 0.45f), 0.38f, 0f, "oakGrain", 0.8f);
        sage = Mt("Sage", Cc(0.66f, 0.76f, 0.64f), 0.12f, 0f, "fabric", 1f);
        sagePaint = Mt("SagePaint", Cc(0.62f, 0.72f, 0.60f), 0.42f);
        sageDark = Mt("SageDark", Cc(0.45f, 0.56f, 0.46f), 0.12f, 0f, "fabric", 1f);
        doorSage = Mt("DoorSage", Cc(0.55f, 0.67f, 0.56f), 0.4f);
        cream = Mt("Cream", Cc(0.93f, 0.89f, 0.81f), 0.1f, 0f, "fabric", 1f);
        greige = Mt("Greige", Cc(0.80f, 0.76f, 0.68f), 0.1f, 0f, "fabric", 1.2f);
        fabricDark = Mt("FabricDark", Cc(0.38f, 0.42f, 0.40f), 0.1f, 0f, "fabric", 1f);
        mustard = Mt("Mustard", Cc(0.90f, 0.72f, 0.33f), 0.1f, 0f, "fabric", 1f);
        terra = Mt("Terracotta", Cc(0.80f, 0.47f, 0.34f), 0.12f, 0f, "fabric", 1f);
        pot = Mt("Pot", Cc(0.78f, 0.45f, 0.32f), 0.2f, 0f, "concrete", 0.5f);
        rugM = Mt("Rug", Cc(0.95f, 0.91f, 0.82f), 0.02f, 0f, "rug", 1.2f);
        rugB = Mt("RugBorder", Cc(0.66f, 0.76f, 0.64f), 0.02f, 0f, "rug", 1.2f);
        tileGray = Mt("TileGray", Cc(0.72f, 0.72f, 0.70f), 0.55f, 0f, "floorTile", 1f);
        tileWhite = Mt("TileWhite", Cc(0.95f, 0.95f, 0.94f), 0.75f, 0f, "subway", 1f);
        subway = Mt("Subway", Cc(1f, 1f, 1f), 0.8f, 0f, "subway", 1f);
        stone = Mt("Stone", Cc(0.9f, 0.88f, 0.85f), 0.45f, 0f, "stone", 1f);
        concrete = Mt("Concrete", Cc(0.85f, 0.85f, 0.83f), 0.1f, 0f, "concrete", 1f);
        chrome = Mt("Chrome", Cc(0.85f, 0.86f, 0.88f), 0.92f, 1f);
        brass = Mt("Brass", Cc(0.85f, 0.68f, 0.32f), 0.78f, 1f, "brushed", 0.5f);
        black = Mt("Black", Cc(0.07f, 0.07f, 0.08f), 0.5f, 0.3f);
        glass = GlassMat();
        mirror = Mt("Mirror", Cc(0.78f, 0.84f, 0.90f), 0.97f, 0f);
        mirror.EnableKeyword("_EMISSION");
        mirror.SetColor("_EmissionColor", new Color(0.30f, 0.34f, 0.38f));
        marble = Mt("Marble", Cc(1f, 1f, 1f), 0.88f, 0f, "marble", 0.5f);
        ceramic = Mt("Ceramic", Cc(0.97f, 0.97f, 0.96f), 0.92f);
        plant = Mt("Plant", Cc(0.55f, 0.85f, 0.50f), 0.3f, 0f, "leaf", 1f);
        soil = Mt("Soil", Cc(0.2f, 0.14f, 0.1f), 0.05f, 0f, "concrete", 1f);
        emis = EmisMat("EmissiveWarm", Cc(1f, 0.85f, 0.6f), 2.5f);
        screen = Mt("Screen", Cc(0.03f, 0.03f, 0.04f), 0.92f, 0.2f);
        roofM = Mt("Roof", Cc(0.30f, 0.30f, 0.32f), 0.25f, 0f, "concrete", 1f);
        gableM = Mt("Gable", Cc(0.94f, 0.93f, 0.89f), 0.12f, 0f, "plaster", 0.15f);
        gableM.SetFloat("_Cull", 0f);
        kBlue = Mt("KidBlue", Cc(0.50f, 0.68f, 0.88f), 0.1f, 0f, "fabric", 1f);
        kYellow = Mt("KidYellow", Cc(0.97f, 0.83f, 0.33f), 0.1f, 0f, "fabric", 1f);
        kPink = Mt("KidPink", Cc(0.92f, 0.64f, 0.68f), 0.1f, 0f, "fabric", 1f);
        bookA = Mt("BookA", Cc(0.70f, 0.30f, 0.28f), 0.3f);
        bookB = Mt("BookB", Cc(0.30f, 0.45f, 0.65f), 0.3f);
        bookC = Mt("BookC", Cc(0.85f, 0.75f, 0.35f), 0.3f);
        bookD = Mt("BookD", Cc(0.35f, 0.55f, 0.40f), 0.3f);
        cardboard = Mt("Cardboard", Cc(0.78f, 0.62f, 0.44f), 0.05f, 0f, "cardboard", 1f);
        steel = Mt("Steel", Cc(0.78f, 0.80f, 0.83f), 0.55f, 0.55f, "brushed", 0.5f);
        water = Mt("Water", Cc(0.75f, 0.88f, 0.95f), 0.95f);
        towelA = Mt("TowelA", Cc(0.90f, 0.87f, 0.80f), 0.05f, 0f, "fabric", 1.3f);
        towelB = Mt("TowelB", Cc(0.60f, 0.72f, 0.62f), 0.05f, 0f, "fabric", 1.3f);
        floorOak = Mt("FloorOak", Cc(0.92f, 0.86f, 0.80f), 0.5f, 0f, "oakFloor", 0.8f);
        floorOakL = Mt("FloorOakLight", Cc(1f, 0.97f, 0.93f), 0.5f, 0f, "oakFloor", 0.8f);
        floorTile = Mt("FloorTile", Cc(1f, 1f, 1f), 0.65f, 0f, "floorTile", 1f);
        floorStone = Mt("FloorStone", Cc(1f, 1f, 1f), 0.5f, 0f, "stone", 1f);
        floorConc = Mt("FloorConcrete", Cc(0.9f, 0.9f, 0.88f), 0.2f, 0f, "concrete", 1f);
    }

    // ====================== YAPI TASLARI ======================
    static GameObject B(Transform p, string n, float x0, float y0, float z0, float x1, float y1, float z1, Material m,
        float ex = 0f, float ey = 0f, float ez = 0f, bool col = true)
    {
        float sx = Mathf.Max(Mathf.Abs(x1 - x0), 0.001f), sy = Mathf.Max(Mathf.Abs(y1 - y0), 0.001f), sz = Mathf.Max(Mathf.Abs(z1 - z0), 0.001f);
        var g = new GameObject(n);
        g.AddComponent<MeshFilter>().sharedMesh = BoxMesh(sx, sy, sz, bev, uvHoriz);
        g.AddComponent<MeshRenderer>().sharedMaterial = m;
        if (col) g.AddComponent<BoxCollider>().size = new Vector3(sx, sy, sz);
        var t = g.transform;
        t.SetParent(p, false);
        t.localPosition = new Vector3((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f);
        t.localRotation = Quaternion.Euler(ex, ey, ez);
        return g;
    }
    // dekor: collider yok
    static GameObject D(Transform p, string n, float x0, float y0, float z0, float x1, float y1, float z1, Material m,
        float ex = 0f, float ey = 0f, float ez = 0f)
    {
        return B(p, n, x0, y0, z0, x1, y1, z1, m, ex, ey, ez, false);
    }
    static GameObject Cy(Transform p, string n, float cx, float y0, float cz, float r, float h, Material m,
        bool col = false, float ex = 0f, float ey = 0f, float ez = 0f)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        g.name = n;
        if (!col) Object.DestroyImmediate(g.GetComponent<Collider>());
        var t = g.transform;
        t.SetParent(p, false);
        t.localPosition = new Vector3(cx, y0 + h * 0.5f, cz);
        t.localRotation = Quaternion.Euler(ex, ey, ez);
        t.localScale = new Vector3(r * 2f, h * 0.5f, r * 2f);
        g.GetComponent<MeshRenderer>().sharedMaterial = m;
        return g;
    }
    // yatay silindir (x ekseni boyunca uzanir): merkez noktasi verilir
    static GameObject CyH(Transform p, string n, float cx, float cy, float cz, float r, float len, Material m, float ex, float ey, float ez)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        g.name = n;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        var t = g.transform;
        t.SetParent(p, false);
        t.localPosition = new Vector3(cx, cy, cz);
        t.localRotation = Quaternion.Euler(ex, ey, ez);
        t.localScale = new Vector3(r * 2f, len * 0.5f, r * 2f);
        g.GetComponent<MeshRenderer>().sharedMaterial = m;
        return g;
    }
    static GameObject Sp(Transform p, string n, float cx, float cy, float cz, float sx, float sy, float sz, Material m,
        float ex = 0f, float ey = 0f, float ez = 0f)
    {
        var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        g.name = n;
        Object.DestroyImmediate(g.GetComponent<Collider>());
        var t = g.transform;
        t.SetParent(p, false);
        t.localPosition = new Vector3(cx, cy, cz);
        t.localRotation = Quaternion.Euler(ex, ey, ez);
        t.localScale = new Vector3(sx, sy, sz);
        g.GetComponent<MeshRenderer>().sharedMaterial = m;
        return g;
    }
    static Transform Grp(Transform p, string n, float x, float z, float yaw = 0f, float y = 0f)
    {
        var t = new GameObject(n).transform;
        t.SetParent(p, false);
        t.localPosition = new Vector3(x, y, z);
        t.localRotation = Quaternion.Euler(0f, yaw, 0f);
        return t;
    }
    static void Lamp(Transform p, string n, float x, float y, float z, float range, float intensity, Color c)
    {
        var g = new GameObject(n);
        g.transform.SetParent(p, false);
        g.transform.localPosition = new Vector3(x, y, z);
        var l = g.AddComponent<Light>();
        l.type = LightType.Point;
        l.color = c;
        l.range = range;
        l.intensity = intensity;
        l.shadows = LightShadows.None;
    }

    static void Init(Transform root)
    {
        InitMats();
        gaps = new List<Gap>();
        gS = Grp(root, "Structure", 0, 0);
        gDW = Grp(root, "DoorsWindows", 0, 0);
        gLiv = Grp(root, "LivingRoom", 0, 0);
        gKit = Grp(root, "Kitchen", 0, 0);
        gMas = Grp(root, "MasterBedroom", 0, 0);
        gKid = Grp(root, "KidsBedroom", 0, 0);
        gBath = Grp(root, "Bathroom", 0, 0);
        gWC = Grp(root, "WC", 0, 0);
        gSto = Grp(root, "Storage", 0, 0);
        gEnt = Grp(root, "Entrance", 0, 0);
        gCor = Grp(root, "Corridor", 0, 0);
        gLight = Grp(root, "Lighting", 0, 0);
        gRoof = Grp(root, "Roof", 0, 0);
        gCeil = Grp(gRoof, "Ceiling", 0, 0);

        rooms = new List<Room>();
        rooms.Add(new Room { name = "Depo", x0 = 0, z0 = 0, x1 = 2.2f, z1 = Z1, floor = floorConc });
        rooms.Add(new Room { name = "WC", x0 = 2.2f, z0 = 0, x1 = 3.8f, z1 = Z1, floor = floorTile });
        rooms.Add(new Room { name = "Banyo", x0 = 3.8f, z0 = 0, x1 = 6.4f, z1 = Z1, floor = floorTile });
        rooms.Add(new Room { name = "Antre", x0 = 6.4f, z0 = 0, x1 = 9.0f, z1 = Z1, floor = floorStone });
        rooms.Add(new Room { name = "AnaYatak", x0 = 9.0f, z0 = 0, x1 = 14f, z1 = Z1, floor = floorOak });
        rooms.Add(new Room { name = "Koridor", x0 = 0, z0 = Z1, x1 = 14f, z1 = Z2, floor = floorOak });
        rooms.Add(new Room { name = "Salon", x0 = 0, z0 = Z2, x1 = 7f, z1 = 12f, floor = floorOakL });
        rooms.Add(new Room { name = "Mutfak", x0 = 7f, z0 = Z2, x1 = 10f, z1 = 12f, floor = floorTile });
        rooms.Add(new Room { name = "Cocuk", x0 = 10f, z0 = Z2, x1 = 14f, z1 = 12f, floor = floorOak });
    }

    // ====================== YAPI ======================
    static void BuildStructure()
    {
        bev = 0f;
        B(gS, "Foundation", -0.1f, -0.35f, -0.1f, 14.1f, -0.05f, 12.1f, concrete);
        foreach (var r in rooms)
            B(gS, "Floor_" + r.name, r.x0, -0.05f, r.z0, r.x1, 0f, r.z1, r.floor);

        // zemin gecis profilleri (antre-koridor, salon girisi)
        D(gS, "Transition_Antre", 6.46f, 0f, Z1 - 0.02f, 8.94f, 0.012f, Z1 + 0.02f, brass);
        D(gS, "Transition_Salon", 2.7f, 0f, Z2 - 0.02f, 4.3f, 0.012f, Z2 + 0.02f, brass);

        // dis plint (cerceve)
        B(gS, "Plinth_Left", -0.2f, -0.3f, -0.2f, -0.1f, 0.12f, 12.2f, concrete);
        B(gS, "Plinth_Right", 14.1f, -0.3f, -0.2f, 14.2f, 0.12f, 12.2f, concrete);
        B(gS, "Plinth_Back", -0.1f, -0.3f, 12.1f, 14.1f, 0.12f, 12.2f, concrete);
        B(gS, "Plinth_FrontL", -0.1f, -0.3f, -0.2f, 5.4f, 0.12f, -0.1f, concrete);
        B(gS, "Plinth_FrontR", 10.0f, -0.3f, -0.2f, 14.1f, 0.12f, -0.1f, concrete);

        // supurgelikler
        bev = 0.006f;
        foreach (var r in rooms) Skirt(r);
        bev = 0.008f;
    }

    static void Skirt(Room r)
    {
        float iw = r.x0 < 0.01f ? 0.1f : 0.06f;
        float ie = Mathf.Abs(r.x1 - 14f) < 0.01f ? 0.1f : 0.06f;
        float isz = r.z0 < 0.01f ? 0.1f : 0.06f;
        float inn = Mathf.Abs(r.z1 - 12f) < 0.01f ? 0.1f : 0.06f;
        SkirtLine(true, r.z0, r.x0 + iw, r.x1 - ie, 1, isz);
        SkirtLine(true, r.z1, r.x0 + iw, r.x1 - ie, -1, inn);
        SkirtLine(false, r.x0, r.z0 + isz, r.z1 - inn, 1, iw);
        SkirtLine(false, r.x1, r.z0 + isz, r.z1 - inn, -1, ie);
    }
    static void SkirtLine(bool ax, float wl, float a0, float a1, int inward, float inset)
    {
        float face = wl + inward * inset;
        float lo = Mathf.Min(face, face + inward * 0.015f), hi = Mathf.Max(face, face + inward * 0.015f);
        var gl = new List<Gap>();
        foreach (var g in gaps) if (g.ax == ax && Mathf.Abs(g.f - wl) < 0.01f) gl.Add(g);
        gl.Sort((u, v) => u.c.CompareTo(v.c));
        float cur = a0;
        foreach (var g in gl)
        {
            float s = g.c - g.w / 2 - 0.07f, e = g.c + g.w / 2 + 0.07f;
            if (e < a0 || s > a1) continue;
            if (s > cur) SkirtSeg(ax, cur, s, lo, hi);
            cur = Mathf.Max(cur, e);
        }
        if (a1 > cur) SkirtSeg(ax, cur, a1, lo, hi);
    }
    static void SkirtSeg(bool ax, float s, float e, float lo, float hi)
    {
        if (ax) D(gS, "Skirting", s, 0f, lo, e, 0.1f, hi, trim);
        else D(gS, "Skirting", lo, 0f, s, hi, 0.1f, e, trim);
    }

    // duvar yardimcilari
    static void WB(Transform p, string n, bool ax, float f, float t, float a0, float a1, float y0, float y1, Material m)
    {
        if (a1 - a0 < 0.001f) return;
        if (ax) B(p, n, a0, y0, f - t / 2, a1, y1, f + t / 2, m);
        else B(p, n, f - t / 2, y0, a0, f + t / 2, y1, a1, m);
    }
    static void Wall(string n, bool ax, float f, float a0, float a1, float t, params Op[] ops)
    {
        float oldBev = bev; bev = 0f;
        var list = new List<Op>(ops);
        list.Sort((u, v) => u.c.CompareTo(v.c));
        float cur = a0; int i = 0;
        foreach (var o in list)
        {
            float s = o.c - o.w / 2, e = o.c + o.w / 2;
            WB(gS, n + "_" + i++, ax, f, t, cur, s, 0f, H, wallM);
            if (o.sill > 0.001f) WB(gS, n + "_Sill" + i, ax, f, t, s, e, 0f, o.sill, wallM);
            if (o.head < H - 0.001f) WB(gS, n + "_Head" + i, ax, f, t, s, e, o.head, H, wallM);
            cur = e;
        }
        WB(gS, n + "_" + i, ax, f, t, cur, a1, 0f, H, wallM);
        bev = oldBev;
    }
    // duvar eksenine gore kutu: a = duvar boyunca, q = duvara dik (duvar merkezinden)
    static GameObject WA(Transform p, string n, bool ax, float f, float a0, float a1, float q0, float q1, float y0, float y1, Material m)
    {
        if (ax) return D(p, n, a0, y0, f + q0, a1, y1, f + q1, m);
        return D(p, n, f + q0, y0, a0, f + q1, y1, a1, m);
    }

    static void BuildWallsAndOpenings()
    {
        // dis duvarlar
        Wall("Outer_Front", true, 0f, -0.1f, 14.1f, TO,
            new Op(7.7f, 1.1f, 0f, 2.2f), new Op(5.1f, 0.6f, 1.5f, 2.0f), new Op(3.0f, 0.5f, 1.5f, 2.0f), new Op(11.5f, 1.6f, 0.9f, 2.1f));
        Wall("Outer_Back", true, 12f, -0.1f, 14.1f, TO,
            new Op(3.5f, 3.6f, 0.5f, 2.3f), new Op(8.5f, 1.2f, 1.05f, 2.1f), new Op(12.0f, 1.6f, 0.9f, 2.1f));
        Wall("Outer_Left", false, 0f, 0.1f, 11.9f, TO, new Op(9.0f, 2.2f, 0.9f, 2.3f));
        Wall("Outer_Right", false, 14f, 0.1f, 11.9f, TO, new Op(2.3f, 1.6f, 0.9f, 2.1f), new Op(9.0f, 1.8f, 0.9f, 2.1f));

        // ic duvarlar
        Wall("Inner_ZA", true, Z1, 0.1f, 13.9f, TI,
            new Op(1.12f, 0.9f, 0f, 2.1f), new Op(3.0f, 0.9f, 0f, 2.1f), new Op(5.1f, 0.9f, 0f, 2.1f),
            new Op(7.7f, 2.48f, 0f, 2.3f), new Op(11.5f, 0.9f, 0f, 2.1f));
        Wall("Inner_ZB", true, Z2, 0.1f, 13.9f, TI,
            new Op(3.5f, 1.6f, 0f, 2.2f), new Op(8.5f, 0.9f, 0f, 2.1f), new Op(12.0f, 0.9f, 0f, 2.1f));
        float zs = Z1 - 0.06f, zn = Z2 + 0.06f;
        Wall("Inner_X2", false, 2.2f, 0.1f, zs, TI);
        Wall("Inner_X3", false, 3.8f, 0.1f, zs, TI);
        Wall("Inner_X6", false, 6.4f, 0.1f, zs, TI);
        Wall("Inner_X9", false, 9.0f, 0.1f, zs, TI);
        Wall("Inner_X7", false, 7.0f, zn, 11.9f, TI, new Op(9.6f, 0.9f, 0f, 2.1f));
        Wall("Inner_X10", false, 10.0f, zn, 11.9f, TI);

        // dis cephe detaylari: ahsap kaplama ve sundurma
        for (float x = 5.65f; x < 7.0f; x += 0.1f) D(gEnt, "Slat", x, 0.12f, -0.16f, x + 0.055f, 2.55f, -0.1f, oakDark);
        for (float x = 8.45f; x < 9.8f; x += 0.1f) D(gEnt, "Slat", x, 0.12f, -0.16f, x + 0.055f, 2.55f, -0.1f, oakDark);
        B(gEnt, "Porch", 5.4f, -0.3f, -1.9f, 10.0f, 0f, -0.1f, stone);
        D(gEnt, "PorchStep", 5.9f, -0.3f, -2.3f, 9.5f, 0.005f, -1.9f, stone);
        B(gEnt, "Canopy", 5.2f, 2.55f, -1.95f, 10.2f, 2.7f, -0.1f, trim);
        D(gEnt, "CanopyBeam", 5.2f, 2.45f, -1.95f, 10.2f, 2.55f, -1.85f, oak);
        B(gEnt, "Post_L", 5.35f, 0f, -1.85f, 5.47f, 2.55f, -1.73f, oak);
        B(gEnt, "Post_R", 9.93f, 0f, -1.85f, 10.05f, 2.55f, -1.73f, oak);
        D(gEnt, "HouseNumber", 9.0f, 1.55f, -0.115f, 9.25f, 1.75f, -0.1f, black);
        D(gEnt, "WallLantern", 6.05f, 1.7f, -0.18f, 6.2f, 1.95f, -0.1f, emis);
        Cy(gEnt, "PorchLight", 7.7f, 2.52f, -1.0f, 0.1f, 0.03f, emis);

        // kapilar
        Door("Door_Entry", true, 0f, 7.7f, 1.1f, 2.2f, 1, true, 30f, TO, doorSage);
        Door("Door_Depo", true, Z1, 1.12f, 0.9f, 2.1f, -1, true, 35f, TI, trim);
        Door("Door_WC", true, Z1, 3.0f, 0.9f, 2.1f, -1, true, 35f, TI, trim);
        Door("Door_Banyo", true, Z1, 5.1f, 0.9f, 2.1f, -1, true, 35f, TI, trim);
        Door("Door_Master", true, Z1, 11.5f, 0.9f, 2.1f, -1, true, 35f, TI, doorSage);
        Door("Door_Mutfak", true, Z2, 8.5f, 0.9f, 2.1f, 1, true, 35f, TI, trim);
        Door("Door_Cocuk", true, Z2, 12.0f, 0.9f, 2.1f, 1, true, 35f, TI, doorSage);
        Door("Door_SalonMutfak", false, 7.0f, 9.6f, 0.9f, 2.1f, 1, false, 35f, TI, trim);
        CasedOpening("Opening_Salon", true, Z2, 3.5f, 1.6f, 2.2f, TI);
        gaps.Add(new Gap { ax = true, f = Z1, c = 7.7f, w = 2.48f });
        B(gDW, "Beam_Antre", 6.46f, 2.3f, Z1 - 0.07f, 8.94f, 2.42f, Z1 + 0.07f, trim);

        // pencereler
        Window("Win_Banyo", true, 0f, 5.1f, 0.6f, 1.5f, 2.0f, TO, 1, false);
        Window("Win_WC", true, 0f, 3.0f, 0.5f, 1.5f, 2.0f, TO, 1, false);
        Window("Win_Master_Front", true, 0f, 11.5f, 1.6f, 0.9f, 2.1f, TO, 1, true);
        Window("Win_Master_East", false, 14f, 2.3f, 1.6f, 0.9f, 2.1f, TO, -1, true);
        Window("Win_Salon_North", true, 12f, 3.5f, 3.6f, 0.5f, 2.3f, TO, -1, true);
        Window("Win_Salon_West", false, 0f, 9.0f, 2.2f, 0.9f, 2.3f, TO, 1, false);
        Window("Win_Mutfak", true, 12f, 8.5f, 1.2f, 1.05f, 2.1f, TO, -1, false);
        Window("Win_Cocuk_North", true, 12f, 12.0f, 1.6f, 0.9f, 2.1f, TO, -1, true);
        Window("Win_Cocuk_East", false, 14f, 9.0f, 1.8f, 0.9f, 2.1f, TO, -1, false);
    }

    static void CasedOpening(string n, bool ax, float f, float c, float w, float h, float t)
    {
        gaps.Add(new Gap { ax = ax, f = f, c = c, w = w });
        float a0 = c - w / 2, a1 = c + w / 2;
        for (int sd = -1; sd <= 1; sd += 2)
        {
            float q0 = sd > 0 ? t / 2 : -t / 2 - 0.012f, q1 = sd > 0 ? t / 2 + 0.012f : -t / 2;
            WA(gDW, n + "_CasL", ax, f, a0 - 0.08f, a0, q0, q1, 0f, h + 0.08f, trim);
            WA(gDW, n + "_CasR", ax, f, a1, a1 + 0.08f, q0, q1, 0f, h + 0.08f, trim);
            WA(gDW, n + "_CasT", ax, f, a0 - 0.08f, a1 + 0.08f, q0, q1, h, h + 0.08f, trim);
        }
        WA(gDW, n + "_Reveal", ax, f, a0, a1, -t / 2, t / 2, h - 0.04f, h, trim);
        WA(gDW, n + "_Threshold", ax, f, a0, a1, -t / 2, t / 2, 0f, 0.012f, brass);
    }

    static void Door(string n, bool ax, float f, float c, float w, float h, int s, bool hLow, float open, float t, Material leaf)
    {
        var p = gDW;
        gaps.Add(new Gap { ax = ax, f = f, c = c, w = w });
        float a0 = c - w / 2, a1 = c + w / 2;
        WA(p, n + "_JambL", ax, f, a0, a0 + 0.04f, -t / 2, t / 2, 0f, h, trim);
        WA(p, n + "_JambR", ax, f, a1 - 0.04f, a1, -t / 2, t / 2, 0f, h, trim);
        WA(p, n + "_Head", ax, f, a0, a1, -t / 2, t / 2, h - 0.04f, h, trim);
        for (int sd = -1; sd <= 1; sd += 2)
        {
            float q0 = sd > 0 ? t / 2 : -t / 2 - 0.012f, q1 = sd > 0 ? t / 2 + 0.012f : -t / 2;
            WA(p, n + "_CasL", ax, f, a0 - 0.07f, a0, q0, q1, 0f, h + 0.07f, trim);
            WA(p, n + "_CasR", ax, f, a1, a1 + 0.07f, q0, q1, 0f, h + 0.07f, trim);
            WA(p, n + "_CasT", ax, f, a0 - 0.07f, a1 + 0.07f, q0, q1, h, h + 0.07f, trim);
        }
        WA(p, n + "_Threshold", ax, f, a0, a1, -t / 2, t / 2, 0f, 0.012f, brass);

        float hinge = hLow ? a0 + 0.04f : a1 - 0.04f;
        float d = hLow ? 1f : -1f;
        float L = w - 0.09f, lh = h - 0.05f;
        var piv = new GameObject(n + "_Leaf").transform;
        piv.SetParent(p, false);
        piv.localPosition = ax ? new Vector3(hinge, 0f, f) : new Vector3(f, 0f, hinge);
        float th = ax ? -s * d * open : s * d * open;
        piv.localRotation = Quaternion.Euler(0f, th, 0f);
        float lo = Mathf.Min(0f, d * L), hi = Mathf.Max(0f, d * L);
        var panelM = (leaf == trim) ? wallM : trim;
        if (ax)
        {
            D(piv, "Leaf", lo, 0.01f, -0.02f, hi, lh, 0.02f, leaf);
            D(piv, "PanelUp", lo + 0.1f, lh - 0.75f, -0.026f, hi - 0.1f, lh - 0.12f, 0.026f, panelM);
            D(piv, "PanelLow", lo + 0.1f, 0.2f, -0.026f, hi - 0.1f, lh - 0.9f, 0.026f, panelM);
            float hx = d * (L - 0.1f);
            D(piv, "HandleA", hx - 0.06f, 0.97f, -0.05f, hx + 0.06f, 1.0f, -0.02f, brass);
            D(piv, "HandleB", hx - 0.06f, 0.97f, 0.02f, hx + 0.06f, 1.0f, 0.05f, brass);
            D(piv, "Plate", hx - 0.02f, 0.9f, -0.024f, hx + 0.02f, 1.08f, 0.024f, brass);
        }
        else
        {
            D(piv, "Leaf", -0.02f, 0.01f, lo, 0.02f, lh, hi, leaf);
            D(piv, "PanelUp", -0.026f, lh - 0.75f, lo + 0.1f, 0.026f, lh - 0.12f, hi - 0.1f, panelM);
            D(piv, "PanelLow", -0.026f, 0.2f, lo + 0.1f, 0.026f, lh - 0.9f, hi - 0.1f, panelM);
            float hz = d * (L - 0.1f);
            D(piv, "HandleA", -0.05f, 0.97f, hz - 0.06f, -0.02f, 1.0f, hz + 0.06f, brass);
            D(piv, "HandleB", 0.02f, 0.97f, hz - 0.06f, 0.05f, 1.0f, hz + 0.06f, brass);
            D(piv, "Plate", -0.024f, 0.9f, hz - 0.02f, 0.024f, 1.08f, hz + 0.02f, brass);
        }
        foreach (float hy in new[] { 0.25f, h * 0.5f, h - 0.3f })
            Cy(piv, "Hinge", 0f, hy - 0.05f, 0f, 0.012f, 0.1f, chrome);
    }

    static void Window(string n, bool ax, float f, float c, float w, float sill, float head, float t, int inS, bool curtains)
    {
        var p = gDW;
        float a0 = c - w / 2, a1 = c + w / 2, fr = 0.05f;
        WA(p, n + "_FrL", ax, f, a0, a0 + fr, -0.045f, 0.045f, sill, head, trim);
        WA(p, n + "_FrR", ax, f, a1 - fr, a1, -0.045f, 0.045f, sill, head, trim);
        WA(p, n + "_FrB", ax, f, a0, a1, -0.045f, 0.045f, sill, sill + fr, trim);
        WA(p, n + "_FrT", ax, f, a0, a1, -0.045f, 0.045f, head - fr, head, trim);
        var gl = WA(p, n + "_Glass", ax, f, a0 + fr, a1 - fr, -0.006f, 0.006f, sill + fr, head - fr, glass);
        gl.name = n + "_Glass";
        int bars = w > 2.5f ? 2 : (w > 1.0f ? 1 : 0);
        for (int i = 0; i < bars; i++)
        {
            float bx = a0 + (i + 1) * w / (bars + 1);
            WA(p, n + "_Mullion", ax, f, bx - 0.02f, bx + 0.02f, -0.03f, 0.03f, sill + fr, head - fr, trim);
        }
        if (head - sill > 1.3f) WA(p, n + "_Transom", ax, f, a0 + fr, a1 - fr, -0.03f, 0.03f, head - 0.45f, head - 0.41f, trim);

        float iq0 = inS > 0 ? 0f : -(t / 2 + 0.06f), iq1 = inS > 0 ? (t / 2 + 0.06f) : 0f;
        WA(p, n + "_Stool", ax, f, a0 - 0.07f, a1 + 0.07f, iq0, iq1, sill, sill + 0.03f, trim);
        float hy0 = (sill + head) * 0.5f;
        WA(p, n + "_Handle", ax, f, a1 - 0.14f, a1 - 0.1f, inS > 0 ? t / 2 : -t / 2 - 0.05f, inS > 0 ? t / 2 + 0.05f : -t / 2, hy0 - 0.06f, hy0 + 0.06f, brass);
        float oq0 = inS > 0 ? -(t / 2 + 0.05f) : 0f, oq1 = inS > 0 ? 0f : (t / 2 + 0.05f);
        WA(p, n + "_ExtSill", ax, f, a0 - 0.05f, a1 + 0.05f, oq0, oq1, sill - 0.03f, sill + 0.01f, concrete);
        float cq0 = inS > 0 ? t / 2 : -t / 2 - 0.012f, cq1 = inS > 0 ? t / 2 + 0.012f : -t / 2;
        WA(p, n + "_CasL", ax, f, a0 - 0.06f, a0, cq0, cq1, sill, head + 0.06f, trim);
        WA(p, n + "_CasR", ax, f, a1, a1 + 0.06f, cq0, cq1, sill, head + 0.06f, trim);
        WA(p, n + "_CasT", ax, f, a0 - 0.06f, a1 + 0.06f, cq0, cq1, head, head + 0.06f, trim);

        if (curtains)
        {
            float k0 = inS > 0 ? t / 2 + 0.05f : -t / 2 - 0.1f, k1 = inS > 0 ? t / 2 + 0.1f : -t / 2 - 0.05f;
            CurtainFolds(n + "_CurtainL", ax, f, a0 - 0.45f, a0 + 0.2f, k0, k1, inS);
            CurtainFolds(n + "_CurtainR", ax, f, a1 - 0.2f, a1 + 0.45f, k0, k1, inS);
            WA(gDW, n + "_Rod", ax, f, a0 - 0.55f, a1 + 0.55f, inS > 0 ? t / 2 + 0.03f : -t / 2 - 0.13f, inS > 0 ? t / 2 + 0.13f : -t / 2 - 0.03f, 2.55f, 2.6f, brass);
        }
    }

    // ====================== MOBILYA PARCALARI ======================
    static void Plant(Transform p, float x, float z, float s)
    {
        var g = Grp(p, "Plant", x, z);
        Cy(g, "Pot", 0, 0, 0, 0.2f * s, 0.38f * s, pot, true);
        Cy(g, "Soil", 0, 0.37f * s, 0, 0.17f * s, 0.03f * s, soil);
        Cy(g, "Trunk", 0, 0.4f * s, 0, 0.018f * s, 0.8f * s, oakDark);
        for (int i = 0; i < 10; i++)
        {
            float a = i * 36f * Mathf.Deg2Rad;
            float r = 0.2f * s * (0.5f + 0.5f * (i % 2));
            float y = (0.8f + 0.1f * (i % 4)) * s;
            Sp(g, "Leaf", Mathf.Cos(a) * r, y, Mathf.Sin(a) * r, 0.34f * s, 0.09f * s, 0.2f * s, plant, 0f, -i * 36f, 18f);
        }
        Sp(g, "Crown", 0, 1.1f * s, 0, 0.34f * s, 0.22f * s, 0.34f * s, plant);
    }

    static void Frame(Transform p, string n, float x, float y, float z, float w, float h, float yaw, Material art, Material art2)
    {
        var g = Grp(p, n, x, z, yaw);
        D(g, "Border", -w / 2, y, 0f, w / 2, y + h, 0.03f, oak);
        D(g, "Canvas", -w / 2 + 0.04f, y + 0.04f, 0.028f, w / 2 - 0.04f, y + h - 0.04f, 0.034f, wallM);
        D(g, "Art1", -w * 0.3f, y + h * 0.2f, 0.034f, w * 0.1f, y + h * 0.75f, 0.038f, art);
        D(g, "Art2", w * 0.0f, y + h * 0.35f, 0.038f, w * 0.32f, y + h * 0.82f, 0.042f, art2);
    }

    static void Armchair(Transform p, float x, float z, float yaw, Material m)
    {
        var g = Grp(p, "Armchair", x, z, yaw);
        B(g, "Seat", -0.38f, 0.2f, -0.4f, 0.38f, 0.42f, 0.4f, m);
        D(g, "Cushion", -0.32f, 0.42f, -0.28f, 0.32f, 0.52f, 0.36f, m);
        B(g, "Back", -0.38f, 0.42f, -0.4f, 0.38f, 0.95f, -0.25f, m, -8f, 0f, 0f);
        B(g, "ArmL", -0.42f, 0.2f, -0.4f, -0.32f, 0.62f, 0.4f, m);
        B(g, "ArmR", 0.32f, 0.2f, -0.4f, 0.42f, 0.62f, 0.4f, m);
        foreach (var lx in new[] { -0.34f, 0.34f })
            foreach (var lz in new[] { -0.34f, 0.34f })
                D(g, "Leg", lx - 0.025f, 0f, lz - 0.025f, lx + 0.025f, 0.2f, lz + 0.025f, oakDark);
        D(g, "Pillow", -0.15f, 0.5f, -0.2f, 0.15f, 0.8f, -0.1f, mustard, -15f, 15f, 0f);
    }

    static void Chair(Transform p, float x, float z, float yaw, Material m)
    {
        var g = Grp(p, "Chair", x, z, yaw);
        B(g, "Seat", -0.21f, 0.43f, -0.21f, 0.21f, 0.47f, 0.21f, m);
        B(g, "BackRest", -0.21f, 0.47f, -0.21f, 0.21f, 0.9f, -0.17f, m);
        foreach (var lx in new[] { -0.18f, 0.18f })
            foreach (var lz in new[] { -0.18f, 0.18f })
                D(g, "Leg", lx - 0.015f, 0f, lz - 0.015f, lx + 0.015f, 0.43f, lz + 0.015f, oakDark);
    }

    static void Bed(Transform g, float w, Material frame, Material head, Material sheet, Material duvet, Material accent, int pillows)
    {
        float hw = w / 2;
        B(g, "BedFrame", -hw - 0.04f, 0.15f, -1.05f, hw + 0.04f, 0.33f, 1.07f, frame);
        D(g, "Headboard", -hw - 0.08f, 0.1f, -1.14f, hw + 0.08f, 1.2f, -1.02f, head);
        for (int i = 0; i < 5; i++)
        {
            float sx = -hw + (i + 0.5f) * (w / 5f);
            D(g, "HeadPanel", sx - 0.01f, 0.45f, -1.025f, sx + 0.01f, 1.1f, -1.0f, frame);
        }
        D(g, "Footboard", -hw - 0.04f, 0.1f, 1.04f, hw + 0.04f, 0.45f, 1.1f, frame);
        foreach (var lx in new[] { -hw - 0.02f, hw + 0.02f })
            foreach (var lz in new[] { -1.08f, 1.07f })
                D(g, "Leg", lx - 0.03f, 0f, lz - 0.03f, lx + 0.03f, 0.15f, lz + 0.03f, oakDark);
        bev = 0.03f;
        B(g, "Mattress", -hw, 0.33f, -1.0f, hw, 0.58f, 1.0f, sheet);
        D(g, "Duvet", -hw - 0.02f, 0.58f, -0.15f, hw + 0.02f, 0.65f, 1.02f, duvet);
        D(g, "DuvetFold", -hw - 0.02f, 0.65f, -0.15f, hw + 0.02f, 0.68f, 0.1f, accent);
        D(g, "Throw", -hw - 0.03f, 0.6f, 0.6f, hw + 0.03f, 0.68f, 0.9f, accent);
        float pw = w / pillows;
        for (int i = 0; i < pillows; i++)
        {
            float cx = -hw + (i + 0.5f) * pw;
            D(g, "Pillow", cx - pw * 0.42f, 0.58f, -0.95f, cx + pw * 0.42f, 0.72f, -0.5f, sheet, -12f, 0f, 0f);
        }
        if (w > 1.2f)
        {
            D(g, "Cushion1", -0.35f, 0.66f, -0.45f, -0.05f, 0.9f, -0.35f, accent, -25f, 10f, 0f);
            D(g, "Cushion2", 0.1f, 0.66f, -0.45f, 0.4f, 0.9f, -0.35f, mustard, -25f, -10f, 0f);
        }
        bev = 0.008f;
    }

    static void Nightstand(Transform p, float x, float z, float yaw)
    {
        var g = Grp(p, "Nightstand", x, z, yaw);
        B(g, "Body", -0.25f, 0.12f, -0.2f, 0.25f, 0.55f, 0.2f, oak);
        D(g, "Drawer", -0.22f, 0.3f, 0.2f, 0.22f, 0.5f, 0.215f, oakLight);
        D(g, "Knob", -0.02f, 0.38f, 0.215f, 0.02f, 0.42f, 0.24f, brass);
        D(g, "Shelf", -0.22f, 0.15f, 0.2f, 0.22f, 0.27f, 0.215f, oakLight);
        foreach (var lx in new[] { -0.21f, 0.21f })
            foreach (var lz in new[] { -0.16f, 0.16f })
                D(g, "Leg", lx - 0.015f, 0f, lz - 0.015f, lx + 0.015f, 0.12f, lz + 0.015f, oakDark);
        Cy(g, "LampBase", 0, 0.55f, 0, 0.06f, 0.18f, brass);
        Cy(g, "LampShade", 0, 0.7f, 0, 0.13f, 0.18f, emis);
        D(g, "Book", -0.2f, 0.55f, 0.02f, -0.02f, 0.58f, 0.16f, bookB);
    }

    static void Wardrobe(Transform p, string n, float x, float z, float yaw, float w, float h, Material body, bool mirrored)
    {
        var g = Grp(p, n, x, z, yaw);
        B(g, "Body", 0, 0.08f, 0, w, h, 0.6f, body);
        D(g, "Plinth", 0.02f, 0, 0.03f, w - 0.02f, 0.08f, 0.55f, oakDark);
        int doors = Mathf.Max(2, Mathf.RoundToInt(w / 0.6f));
        float dw = w / doors;
        for (int i = 0; i < doors; i++)
        {
            float x0 = i * dw + 0.01f, x1 = (i + 1) * dw - 0.01f;
            D(g, "Door", x0, 0.1f, 0.6f, x1, h - 0.02f, 0.625f, (mirrored && i == doors - 1) ? mirror : oakLight);
            float hx = (i % 2 == 0) ? x1 - 0.06f : x0 + 0.06f;
            D(g, "Handle", hx - 0.01f, h * 0.45f, 0.625f, hx + 0.01f, h * 0.45f + 0.25f, 0.65f, brass);
        }
        D(g, "Crown", -0.01f, h, 0f, w + 0.01f, h + 0.04f, 0.63f, trim);
    }

    static void Rug(Transform p, string n, float cx, float cz, float sx, float sz, Material m, Material border)
    {
        D(p, n + "_Border", cx - sx / 2, 0.001f, cz - sz / 2, cx + sx / 2, 0.016f, cz + sz / 2, border);
        D(p, n, cx - sx / 2 + 0.1f, 0.001f, cz - sz / 2 + 0.1f, cx + sx / 2 - 0.1f, 0.02f, cz + sz / 2 - 0.1f, m);
    }

    // ====================== SALON ======================
    static void BuildLiving()
    {
        var p = gLiv;
        Rug(p, "Rug_Salon", 3.3f, 8.7f, 3.8f, 3.2f, rugM, rugB);

        // L koltuk (arkasi bati duvarinda, +X'e bakar)
        var s = Grp(p, "Sofa_L", 0.8f, 8.3f, 90f);
        B(s, "Base", -1.4f, 0.12f, -0.47f, 1.4f, 0.42f, 0.47f, greige);
        B(s, "BackMain", -1.4f, 0.42f, -0.47f, 1.4f, 0.92f, -0.2f, greige);
        B(s, "ArmL", -1.4f, 0.12f, -0.47f, -1.2f, 0.66f, 0.47f, greige);
        B(s, "Chaise", 0.4f, 0.12f, 0.47f, 1.4f, 0.42f, 1.6f, greige);
        B(s, "ArmChaise", 1.2f, 0.12f, 0.47f, 1.4f, 0.62f, 1.6f, greige);
        bev = 0.03f;
        for (int i = 0; i < 3; i++)
        {
            float x0 = -1.2f + i * 0.87f;
            D(s, "SeatCushion", x0 + 0.01f, 0.42f, -0.2f, x0 + 0.85f, 0.55f, 0.47f, cream);
            D(s, "BackCushion", x0 + 0.03f, 0.55f, -0.37f, x0 + 0.83f, 0.88f, -0.2f, cream, -10f, 0f, 0f);
        }
        D(s, "ChaiseCushion", 0.41f, 0.42f, 0.47f, 1.19f, 0.55f, 1.58f, cream);
        D(s, "Pillow1", -1.05f, 0.55f, -0.2f, -0.65f, 0.9f, -0.05f, sage, -15f, 15f, 5f);
        D(s, "Pillow2", -0.55f, 0.55f, -0.2f, -0.2f, 0.88f, -0.05f, mustard, -15f, -10f, -5f);
        D(s, "Pillow3", 0.9f, 0.55f, -0.2f, 1.25f, 0.88f, -0.05f, sageDark, -15f, 10f, 5f);
        D(s, "Throw", 0.5f, 0.55f, 0.9f, 1.15f, 0.58f, 1.5f, terra);
        bev = 0.008f;
        foreach (var lx in new[] { -1.35f, 1.35f })
            foreach (var lz in new[] { -0.42f, 0.42f })
                D(s, "Leg", lx - 0.03f, 0f, lz - 0.03f, lx + 0.03f, 0.12f, lz + 0.03f, brass);
        D(s, "LegC1", 0.45f, 0f, 1.5f, 0.51f, 0.12f, 1.56f, brass);
        D(s, "LegC2", 1.29f, 0f, 1.5f, 1.35f, 0.12f, 1.56f, brass);

        // sehpa
        var ct = Grp(p, "CoffeeTable", 3.1f, 8.5f);
        Cy(ct, "Top", 0, 0.38f, 0, 0.5f, 0.04f, marble, true);
        Cy(ct, "Shelf", 0, 0.14f, 0, 0.4f, 0.03f, oakDark);
        foreach (var a in new[] { 0f, 120f, 240f })
        {
            float r = a * Mathf.Deg2Rad;
            Cy(ct, "Leg", Mathf.Cos(r) * 0.32f, 0, Mathf.Sin(r) * 0.32f, 0.02f, 0.38f, brass);
        }
        D(ct, "BookStack1", -0.2f, 0.42f, -0.1f, 0.05f, 0.46f, 0.1f, bookA);
        D(ct, "BookStack2", -0.18f, 0.46f, -0.08f, 0.03f, 0.49f, 0.08f, bookC);
        Cy(ct, "Vase", 0.2f, 0.42f, 0.05f, 0.05f, 0.18f, ceramic);
        Sp(ct, "Flowers", 0.2f, 0.66f, 0.05f, 0.18f, 0.14f, 0.18f, sage);
        Cy(ct, "Candle", 0.05f, 0.42f, -0.22f, 0.03f, 0.08f, cream);

        // koltuklar
        Armchair(p, 2.9f, 10.6f, 180f, sage);
        Armchair(p, 4.9f, 9.9f, 233f, sage);
        var side = Grp(p, "SideTable", 3.95f, 10.7f);
        Cy(side, "Top", 0, 0.5f, 0, 0.22f, 0.03f, brass);
        Cy(side, "Stem", 0, 0, 0, 0.02f, 0.5f, brass);
        Cy(side, "Base", 0, 0, 0, 0.14f, 0.02f, brass);
        Cy(side, "Cup", 0.05f, 0.53f, 0, 0.04f, 0.08f, ceramic);

        // TV unitesi (dogu duvarinda, -X'e bakar)
        var tv = Grp(p, "TVUnit", 6.7f, 7.55f, 270f);
        B(tv, "Body", -1.1f, 0.2f, -0.22f, 1.1f, 0.55f, 0.22f, oak);
        for (int i = 0; i < 4; i++)
        {
            float x0 = -1.08f + i * 0.54f;
            D(tv, "DoorFront", x0 + 0.01f, 0.22f, 0.22f, x0 + 0.52f, 0.53f, 0.235f, (i % 2 == 0) ? oakLight : oak);
            D(tv, "Pull", x0 + 0.24f, 0.4f, 0.235f, x0 + 0.28f, 0.5f, 0.25f, brass);
        }
        foreach (var lx in new[] { -1.0f, 1.0f })
            D(tv, "Leg", lx - 0.025f, 0f, -0.15f, lx + 0.025f, 0.2f, 0.15f, black);
        D(tv, "Bezel", -0.78f, 0.95f, -0.2f, 0.78f, 1.7f, -0.14f, black);
        D(tv, "Screen", -0.75f, 0.98f, -0.14f, 0.75f, 1.67f, -0.135f, screen);
        D(tv, "Soundbar", -0.5f, 0.58f, -0.12f, 0.5f, 0.66f, 0.0f, black);
        Cy(tv, "Vase", 0.9f, 0.55f, 0f, 0.06f, 0.22f, ceramic);
        Sp(tv, "Branch", 0.9f, 0.9f, 0f, 0.22f, 0.3f, 0.22f, plant);
        D(tv, "Books", -1.0f, 0.55f, -0.08f, -0.6f, 0.62f, 0.12f, bookC);
        for (float z = 6.25f; z < 8.85f; z += 0.085f)
            D(p, "WallSlat", 6.86f, 0.55f, z, 6.94f, 2.6f, z + 0.05f, oakDark);

        // bitkiler, lamba
        Plant(p, 0.65f, 11.25f, 1.6f);
        Plant(p, 6.45f, 11.3f, 1.4f);
        Plant(p, 6.5f, 6.25f, 1.1f);
        var fl = Grp(p, "FloorLamp", 0.5f, 6.3f);
        Cy(fl, "Base", 0, 0, 0, 0.14f, 0.03f, brass);
        Cy(fl, "Pole", 0, 0.03f, 0, 0.015f, 1.55f, brass);
        Cy(fl, "Shade", 0, 1.35f, 0, 0.17f, 0.3f, emis);

        // duvar dekoru
        Frame(p, "Art_North1", 0.75f, 1.15f, 11.9f, 0.8f, 1.1f, 180f, sage, terra);
        Frame(p, "Art_South1", 1.1f, 1.2f, Z2 + 0.06f, 0.9f, 0.7f, 0f, terra, sage);
        Frame(p, "Art_South2", 5.9f, 1.2f, Z2 + 0.06f, 0.9f, 0.7f, 0f, sageDark, mustard);

        // avize
        var ch = Grp(p, "Chandelier", 3.3f, 8.7f);
        Cy(ch, "Chain", 0, 2.1f, 0, 0.01f, 0.7f, brass);
        Cy(ch, "Canopy", 0, 2.77f, 0, 0.09f, 0.03f, brass);
        for (int ring = 0; ring < 2; ring++)
        {
            float rr = ring == 0 ? 0.55f : 0.32f, yy = ring == 0 ? 2.0f : 2.22f;
            int segs = 18;
            for (int i = 0; i < segs; i++)
            {
                float a = i * 360f / segs;
                float ar = a * Mathf.Deg2Rad;
                D(ch, "RingSeg", Mathf.Cos(ar) * rr - 0.1f, yy, Mathf.Sin(ar) * rr - 0.012f,
                    Mathf.Cos(ar) * rr + 0.1f, yy + 0.025f, Mathf.Sin(ar) * rr + 0.012f, brass, 0f, -a - 90f, 0f);
                // not: kutu merkezde olusturulur, yerel donus kutunun merkezi etrafindadir
                if (ring == 0 && i % 3 == 0)
                {
                    Sp(ch, "Bulb", Mathf.Cos(ar) * rr, yy + 0.07f, Mathf.Sin(ar) * rr, 0.08f, 0.1f, 0.08f, emis);
                    Sp(ch, "Crystal", Mathf.Cos(ar) * rr, yy - 0.12f, Mathf.Sin(ar) * rr, 0.04f, 0.1f, 0.04f, glass);
                }
            }
        }
        for (int i = 0; i < 3; i++)
        {
            float a = i * 120f;
            CyH(ch, "Spoke", Mathf.Cos(a * Mathf.Deg2Rad) * 0.28f, 2.1f, Mathf.Sin(a * Mathf.Deg2Rad) * 0.28f, 0.008f, 0.58f, brass, 90f, -a, 0f);
        }
        Sp(ch, "CoreBulb", 0, 2.0f, 0, 0.14f, 0.14f, 0.14f, emis);

        // konsol (koridor acikligi yaninda)
        var con = Grp(p, "Console", 5.9f, Z2 + 0.06f);
        B(con, "Top", -0.6f, 0.78f, 0f, 0.6f, 0.82f, 0.3f, oak);
        foreach (var lx in new[] { -0.55f, 0.55f })
            D(con, "Leg", lx - 0.02f, 0f, 0.03f, lx + 0.02f, 0.78f, 0.27f, black);
        D(con, "Tray", -0.2f, 0.82f, 0.05f, 0.2f, 0.84f, 0.25f, brass);
        Cy(con, "Vase", 0.4f, 0.82f, 0.15f, 0.05f, 0.2f, ceramic);
        Sp(con, "Plant", 0.4f, 1.1f, 0.15f, 0.25f, 0.2f, 0.25f, plant);
    }

    // ====================== MUTFAK ======================
    static void BaseCab(Transform g, string n, float x0, float x1, int doors, bool drawers)
    {
        B(g, n + "_Body", x0, 0.1f, 0f, x1, 0.86f, 0.58f, sagePaint);
        D(g, n + "_Plinth", x0, 0f, 0.03f, x1, 0.1f, 0.5f, black);
        float w = (x1 - x0) / doors;
        for (int i = 0; i < doors; i++)
        {
            float a = x0 + i * w + 0.01f, b = x0 + (i + 1) * w - 0.01f;
            if (drawers)
            {
                D(g, n + "_Drawer", a, 0.55f, 0.58f, b, 0.85f, 0.6f, sagePaint);
                ShakerFrame(g, a, 0.55f, b, 0.85f, 0.6f);
                D(g, n + "_Door", a, 0.11f, 0.58f, b, 0.53f, 0.6f, sagePaint);
                ShakerFrame(g, a, 0.11f, b, 0.53f, 0.6f);
                D(g, n + "_DrawerPull", (a + b) / 2 - 0.1f, 0.78f, 0.6f, (a + b) / 2 + 0.1f, 0.8f, 0.63f, brass);
                D(g, n + "_Pull", (a + b) / 2 - 0.01f, 0.4f, 0.6f, (a + b) / 2 + 0.01f, 0.5f, 0.63f, brass);
            }
            else
            {
                D(g, n + "_Door", a, 0.11f, 0.58f, b, 0.85f, 0.6f, sagePaint);
                ShakerFrame(g, a, 0.11f, b, 0.85f, 0.6f);
                D(g, n + "_Pull", b - 0.07f, 0.55f, 0.6f, b - 0.05f, 0.75f, 0.63f, brass);
            }
        }
        D(g, n + "_Top", x0 - 0.0f, 0.86f, -0.0f, x1, 0.9f, 0.62f, marble);
    }
    static void UpperCab(Transform g, string n, float x0, float x1)
    {
        B(g, n + "_Body", x0, 1.5f, 0f, x1, 2.2f, 0.33f, sagePaint);
        int doors = Mathf.Max(1, Mathf.RoundToInt((x1 - x0) / 0.5f));
        float w = (x1 - x0) / doors;
        for (int i = 0; i < doors; i++)
        {
            float a = x0 + i * w + 0.01f, b = x0 + (i + 1) * w - 0.01f;
            D(g, n + "_Door", a, 1.51f, 0.33f, b, 2.19f, 0.35f, sagePaint);
            ShakerFrame(g, a, 1.51f, b, 2.19f, 0.35f);
            D(g, n + "_Pull", b - 0.07f, 1.55f, 0.35f, b - 0.05f, 1.75f, 0.38f, brass);
        }
    }

    static void BuildKitchen()
    {
        var p = gKit;
        // dogu duvari boyunca sira: yerel x = dunya z
        var e = Grp(p, "Run_East", 9.94f, 0f, 270f);
        BaseCab(e, "E1", 6.62f, 8.22f, 2, true);
        BaseCab(e, "E2", 8.82f, 11.9f - 0.0f, 3, true);
        UpperCab(e, "EU1", 6.7f, 8.15f);
        UpperCab(e, "EU2", 8.9f, 11.3f);
        // buzdolabi
        B(e, "Fridge", 5.92f, 0.03f, 0f, 6.62f, 1.85f, 0.68f, steel);
        D(e, "FridgeSplit", 5.92f, 1.2f, 0.68f, 6.62f, 1.215f, 0.69f, black);
        D(e, "FridgeHandleU", 6.5f, 1.3f, 0.69f, 6.53f, 1.7f, 0.72f, chrome);
        D(e, "FridgeHandleL", 6.5f, 0.6f, 0.69f, 6.53f, 1.1f, 0.72f, chrome);
        D(e, "FridgeTopCab", 5.92f, 1.86f, 0f, 6.62f, 2.3f, 0.5f, sage);
        // ocak ve firin
        B(e, "Oven", 8.22f, 0.1f, 0f, 8.82f, 0.86f, 0.58f, black);
        D(e, "OvenWindow", 8.28f, 0.25f, 0.58f, 8.76f, 0.65f, 0.595f, screen);
        D(e, "OvenHandle", 8.26f, 0.7f, 0.6f, 8.78f, 0.725f, 0.64f, chrome);
        for (int i = 0; i < 4; i++) Cy(e, "Knob", 8.3f + i * 0.14f, 0.77f, 0.6f, 0.02f, 0.03f, chrome, false, 90f, 0f, 0f);
        D(e, "Cooktop", 8.22f, 0.9f, 0f, 8.82f, 0.915f, 0.58f, screen);
        Cy(e, "BurnerA", 8.42f, 0.915f, 0.16f, 0.07f, 0.01f, black);
        Cy(e, "BurnerB", 8.62f, 0.915f, 0.16f, 0.06f, 0.01f, black);
        Cy(e, "BurnerC", 8.42f, 0.915f, 0.42f, 0.06f, 0.01f, black);
        Cy(e, "BurnerD", 8.62f, 0.915f, 0.42f, 0.07f, 0.01f, black);
        // davlumbaz
        D(e, "HoodBody", 8.22f, 1.55f, 0f, 8.82f, 1.75f, 0.5f, steel);
        D(e, "HoodChimney", 8.4f, 1.75f, 0f, 8.64f, 2.8f, 0.24f, steel);
        // tezgah arasi seramik
        uvHoriz = true; D(e, "Backsplash1", 6.62f, 0.9f, 0f, 8.22f, 1.5f, 0.012f, subway); uvHoriz = false;
        uvHoriz = true; D(e, "Backsplash2", 8.82f, 0.9f, 0f, 11.3f, 1.5f, 0.012f, subway); uvHoriz = false;
        uvHoriz = true; D(e, "BacksplashHood", 8.22f, 0.9f, 0f, 8.82f, 2.0f, 0.012f, subway); uvHoriz = false;

        // kuzey duvari: evye (pencere altinda). yerel x = 9.34 - dunya x
        var n = Grp(p, "Run_North", 9.34f, 11.9f, 180f);
        BaseCab(n, "N1", 0f, 0.34f, 1, false);
        BaseCab(n, "N2", 0.34f, 1.34f, 2, false);
        BaseCab(n, "N3", 1.34f, 2.24f, 2, false);
        D(n, "SinkBasin", 0.45f, 0.895f, 0.1f, 1.23f, 0.905f, 0.5f, steel);
        D(n, "SinkDrain", 0.82f, 0.905f, 0.28f, 0.86f, 0.91f, 0.32f, black);
        Cy(n, "TapStem", 0.84f, 0.9f, 0.04f, 0.015f, 0.28f, chrome);
        D(n, "TapSpout", 0.835f, 1.14f, 0.04f, 0.845f, 1.16f, 0.22f, chrome);
        D(n, "TapHandle", 0.9f, 1.0f, 0.03f, 0.95f, 1.06f, 0.05f, chrome);
        UpperCab(n, "NU1", 1.52f, 2.24f);
        uvHoriz = true; D(n, "Backsplash", 0f, 0.9f, 0f, 2.24f, 1.5f, 0.012f, subway); uvHoriz = false;

        // masa ve sandalyeler
        var t = Grp(p, "KitchenTable", 7.95f, 7.7f);
        Cy(t, "Top", 0, 0.72f, 0, 0.42f, 0.04f, oak, true);
        Cy(t, "Stem", 0, 0, 0, 0.04f, 0.72f, oakDark);
        Cy(t, "Base", 0, 0, 0, 0.22f, 0.03f, oakDark);
        Cy(t, "Bowl", 0, 0.76f, 0, 0.12f, 0.05f, ceramic);
        Sp(t, "Fruit1", -0.03f, 0.82f, 0.02f, 0.07f, 0.07f, 0.07f, terra);
        Sp(t, "Fruit2", 0.04f, 0.82f, -0.02f, 0.07f, 0.07f, 0.07f, mustard);
        Chair(p, 7.95f, 7.05f, 0f, sage);
        Chair(p, 7.95f, 8.4f, 180f, sage);
        Plant(p, 7.35f, 11.55f, 0.6f);
        Frame(p, "Art", 7.06f, 1.3f, 6.9f, 0.5f, 0.7f, 90f, sage, mustard);
        var sh = Grp(p, "WallShelf", 7.06f, 7.4f, 90f);
        D(sh, "Board", 0f, 1.5f, 0f, 1.0f, 1.54f, 0.22f, oak);
        D(sh, "Jar1", 0.1f, 1.54f, 0.05f, 0.25f, 1.7f, 0.18f, ceramic);
        D(sh, "Jar2", 0.35f, 1.54f, 0.05f, 0.5f, 1.66f, 0.18f, glass);
        D(sh, "Cookbook", 0.65f, 1.54f, 0.04f, 0.9f, 1.8f, 0.07f, bookC);
    }

    // ====================== ANA YATAK ODASI ======================
    static void BuildMaster()
    {
        var p = gMas;
        Rug(p, "Rug_Master", 11.0f, 2.3f, 3.2f, 2.8f, rugM, rugB);
        var b = Grp(p, "Bed_King", 10.22f, 2.3f, 90f);
        Bed(b, 1.8f, oak, sageDark, cream, cream, sage, 4);
        // basucu mekani: cerceve panel
        D(p, "HeadWallPanel", 9.06f, 0.6f, 1.0f, 9.1f, 2.3f, 3.6f, sage);
        var bench = Grp(b, "Bench", 0f, 1.35f);
        B(bench, "Seat", -0.7f, 0.35f, -0.22f, 0.7f, 0.5f, 0.22f, fabricDark);
        foreach (var lx in new[] { -0.65f, 0.65f }) foreach (var lz in new[] { -0.16f, 0.16f })
            D(bench, "Leg", lx - 0.02f, 0f, lz - 0.02f, lx + 0.02f, 0.35f, lz + 0.02f, brass);
        NightstandAsset(b, -1.2f, -0.88f);
        NightstandAsset(b, 1.2f, -0.88f);

        // gardirop (kuzey duvari, dunya x 12.2-13.86)
        Wardrobe(p, "Wardrobe_North", 13.86f, 4.44f, 180f, 1.66f, 2.4f, oak, false);
        // gardirop (guney duvari)
        Wardrobe(p, "Wardrobe_South", 12.85f, 0.1f, 0f, 1.05f, 2.4f, oak, true);
        // komod ve ayna (kuzey duvari, x 9.1-10.9)
        var dr = Grp(p, "Dresser", 10.9f, 4.44f, 180f);
        B(dr, "Body", 0, 0.1f, 0, 1.8f, 0.85f, 0.5f, oak);
        for (int i = 0; i < 3; i++)
        {
            D(dr, "Drawer", i * 0.6f + 0.02f, 0.14f, 0.5f, (i + 1) * 0.6f - 0.02f, 0.82f, 0.52f, oakLight);
            D(dr, "Knob", i * 0.6f + 0.28f, 0.46f, 0.52f, i * 0.6f + 0.32f, 0.5f, 0.55f, brass);
        }
        D(dr, "Top", -0.02f, 0.85f, -0.0f, 1.82f, 0.88f, 0.52f, marble);
        D(dr, "MirrorFrame", 0.3f, 1.0f, 0.0f, 1.5f, 2.1f, 0.04f, brass);
        D(dr, "Mirror", 0.34f, 1.04f, 0.04f, 1.46f, 2.06f, 0.05f, mirror);
        Cy(dr, "Vase", 0.2f, 0.88f, 0.2f, 0.05f, 0.2f, ceramic);
        Sp(dr, "Flowers", 0.2f, 1.15f, 0.2f, 0.2f, 0.16f, 0.2f, plant);
        D(dr, "JewelBox", 1.3f, 0.88f, 0.15f, 1.6f, 0.98f, 0.32f, oakDark);
        Cy(dr, "Perfume", 0.95f, 0.88f, 0.2f, 0.04f, 0.12f, glass);

        // koltuk + yan sehpa
        Armchair(p, 13.2f, 3.2f, 225f, sage);
        var st = Grp(p, "SideTable", 13.45f, 3.85f);
        Cy(st, "Top", 0, 0.5f, 0, 0.17f, 0.03f, brass);
        Cy(st, "Stem", 0, 0, 0, 0.015f, 0.5f, brass);
        Cy(st, "Base", 0, 0, 0, 0.1f, 0.02f, brass);
        Plant(p, 9.45f, 0.55f, 1.0f);
        Frame(p, "Art_Over_Bed", 9.1f, 1.45f, 2.3f, 0.9f, 0.7f, 90f, mustard, terra);
        Frame(p, "Art_South", 9.9f, 1.4f, 0.1f, 0.6f, 0.8f, 0f, sageDark, sage);
    }

    // ====================== COCUK ODASI ======================
    static void BuildKids()
    {
        var p = gKid;
        // yataklar: bas kuzey duvarinda, -Z'ye bakar
        var b1 = Grp(p, "Bed_A", 10.55f, 10.76f, 180f);
        Bed(b1, 0.9f, oakLight, kBlue, cream, kBlue, mustard, 1);
        var b2 = Grp(p, "Bed_B", 13.38f, 10.76f, 180f);
        Bed(b2, 0.9f, oakLight, kYellow, cream, kPink, sage, 1);
        // aradaki sandik (pencere altinda)
        var tb = Grp(p, "ToyChest", 11.98f, 11.65f, 180f);
        B(tb, "Body", -0.55f, 0f, -0.2f, 0.55f, 0.45f, 0.2f, oakLight);
        D(tb, "Lid", -0.57f, 0.45f, -0.22f, 0.57f, 0.5f, 0.22f, kBlue);
        D(tb, "Strap", -0.3f, 0.2f, 0.2f, -0.24f, 0.45f, 0.21f, brass);
        D(tb, "Strap2", 0.24f, 0.2f, 0.2f, 0.3f, 0.45f, 0.21f, brass);
        // yildiz ve bulut duvar dekorlari
        D(p, "Star1", 10.4f, 1.6f, 11.84f, 10.7f, 1.9f, 11.86f, kYellow, 0f, 0f, 45f);
        D(p, "Star2", 10.9f, 1.95f, 11.84f, 11.1f, 2.15f, 11.86f, kYellow, 0f, 0f, 20f);
        D(p, "Star3", 13.1f, 1.6f, 11.84f, 13.4f, 1.9f, 11.86f, kYellow, 0f, 0f, 30f);
        Sp(p, "Cloud1", 13.5f, 2.2f, 11.86f, 0.4f, 0.2f, 0.04f, cream);
        Sp(p, "Cloud2", 13.3f, 2.15f, 11.86f, 0.3f, 0.18f, 0.04f, cream);
        for (int i = 0; i < 12; i++)
            Sp(p, "FairyLight", 10.2f + i * 0.3f, 2.25f - 0.06f * Mathf.Sin(i * 0.9f), 11.82f, 0.05f, 0.05f, 0.05f, emis);

        // cift calisma masasi (bati duvari)
        var d = Grp(p, "DoubleDesk", 10.06f, 8.4f, 90f);
        B(d, "Top", 0f, 0.72f, 0f, 2.0f, 0.76f, 0.65f, oakLight);
        B(d, "SideL", 0.0f, 0.0f, 0.02f, 0.04f, 0.72f, 0.63f, oak);
        B(d, "SideR", 1.96f, 0.0f, 0.02f, 2.0f, 0.72f, 0.63f, oak);
        B(d, "Divider", 0.98f, 0.0f, 0.02f, 1.02f, 0.72f, 0.63f, oak);
        D(d, "Drawer1", 1.04f, 0.45f, 0.62f, 1.94f, 0.7f, 0.64f, sage);
        D(d, "Drawer2", 1.04f, 0.15f, 0.62f, 1.94f, 0.4f, 0.64f, sage);
        D(d, "Drawer3", 0.06f, 0.45f, 0.62f, 0.96f, 0.7f, 0.64f, kBlue);
        D(d, "Monitor1", 0.35f, 0.76f, 0.1f, 0.85f, 1.1f, 0.14f, black);
        D(d, "Monitor2", 1.15f, 0.76f, 0.1f, 1.65f, 1.1f, 0.14f, black);
        Cy(d, "DeskLamp1", 0.15f, 0.76f, 0.2f, 0.05f, 0.3f, kYellow);
        Cy(d, "DeskLamp2", 1.85f, 0.76f, 0.2f, 0.05f, 0.3f, kPink);
        Cy(d, "PencilCup", 0.95f, 0.76f, 0.15f, 0.04f, 0.1f, kBlue);
        D(d, "Notebook", 0.4f, 0.76f, 0.35f, 0.7f, 0.78f, 0.55f, bookC);
        D(d, "Shelf", 0.1f, 1.5f, 0f, 1.9f, 1.54f, 0.24f, oak);
        for (int i = 0; i < 8; i++) D(d, "ShelfBook", 0.15f + i * 0.2f, 1.54f, 0.04f, 0.15f + i * 0.2f + 0.16f, 1.54f + 0.2f + 0.03f * (i % 3), 0.2f, (i % 2 == 0) ? bookB : bookA);
        Chair(d, 0.5f, 0.95f, 180f, kBlue);
        Chair(d, 1.5f, 0.95f, 180f, kYellow);

        // kitaplik (dogu duvari)
        var bs = Grp(p, "Bookshelf", 13.9f, 6.2f, 270f);
        B(bs, "SideL", 0f, 0f, 0f, 0.03f, 1.9f, 0.35f, oak);
        B(bs, "SideR", 1.57f, 0f, 0f, 1.6f, 1.9f, 0.35f, oak);
        D(bs, "Back", 0f, 0f, 0f, 1.6f, 1.9f, 0.015f, oakDark);
        for (int i = 0; i <= 5; i++) D(bs, "ShelfBoard", 0.03f, 0.05f + i * 0.37f, 0.01f, 1.57f, 0.08f + i * 0.37f, 0.35f, oak);
        for (int r = 0; r < 5; r++)
            for (int i = 0; i < 9; i++)
            {
                var m = (i + r) % 4 == 0 ? bookA : ((i + r) % 4 == 1 ? bookB : ((i + r) % 4 == 2 ? bookC : bookD));
                float x0 = 0.06f + i * 0.165f;
                D(bs, "Book", x0, 0.08f + r * 0.37f, 0.08f, x0 + 0.13f, 0.08f + r * 0.37f + 0.22f + 0.03f * (i % 3), 0.3f, m);
            }

        // dolap (bati duvari, desk ile yatak arasi)
        Wardrobe(p, "Wardrobe_Kids", 10.06f, 9.7f, 90f, 1.2f, 2.2f, oakLight, false);
        // oyuncak dolabi (koridor duvari)
        var tc = Grp(p, "ToyCabinet", 13.86f, Z2 + 0.06f, 0f);
        B(tc, "Body", -1.2f, 0.05f, 0f, 0f, 0.9f, 0.45f, oakLight);
        for (int i = 0; i < 3; i++)
        {
            D(tc, "Cube", -1.18f + i * 0.4f, 0.1f, 0.45f, -0.82f + i * 0.4f, 0.42f, 0.47f, (i == 0) ? kBlue : (i == 1 ? kYellow : kPink));
            D(tc, "Cube2", -1.18f + i * 0.4f, 0.5f, 0.45f, -0.82f + i * 0.4f, 0.85f, 0.47f, (i == 0) ? kPink : (i == 1 ? kBlue : kYellow));
        }
        D(tc, "TopBear", -0.7f, 0.9f, 0.1f, -0.45f, 1.15f, 0.35f, mustard);

        // hali ve oyuncaklar
        Rug(p, "Rug_Kids", 12.0f, 8.3f, 1.9f, 1.5f, kYellow, kBlue);
        D(p, "Block1", 11.7f, 0.02f, 8.0f, 11.85f, 0.17f, 8.15f, kBlue, 0f, 20f, 0f);
        D(p, "Block2", 11.95f, 0.02f, 8.1f, 12.1f, 0.17f, 8.25f, kPink, 0f, -15f, 0f);
        D(p, "Block3", 11.82f, 0.17f, 8.05f, 11.97f, 0.32f, 8.2f, sage, 0f, 10f, 0f);
        Sp(p, "TeddyBody", 12.5f, 0.15f, 8.5f, 0.28f, 0.3f, 0.26f, terra);
        Sp(p, "TeddyHead", 12.5f, 0.38f, 8.5f, 0.18f, 0.18f, 0.18f, terra);
        Sp(p, "TeddyEar1", 12.43f, 0.46f, 8.5f, 0.07f, 0.07f, 0.04f, terra);
        Sp(p, "TeddyEar2", 12.57f, 0.46f, 8.5f, 0.07f, 0.07f, 0.04f, terra);
    }

    // ====================== BANYO ======================
    static void BuildBath()
    {
        var p = gBath;
        // dus kabini (guney-bati kose)
        B(p, "ShowerTray", 3.86f, 0f, 0.1f, 4.76f, 0.05f, 1.0f, ceramic);
        D(p, "ShowerFloorDrain", 4.25f, 0.05f, 0.5f, 4.35f, 0.055f, 0.6f, chrome);
        D(p, "GlassE", 4.755f, 0.05f, 0.1f, 4.765f, 2.0f, 1.0f, glass);
        D(p, "GlassN", 3.86f, 0.05f, 0.995f, 4.76f, 2.0f, 1.005f, glass);
        D(p, "GlassFrameTop", 4.75f, 1.99f, 0.1f, 4.77f, 2.03f, 1.01f, chrome);
        D(p, "GlassFrameTopN", 3.86f, 1.99f, 0.99f, 4.77f, 2.03f, 1.01f, chrome);
        D(p, "GlassHandle", 4.72f, 0.95f, 0.99f, 4.74f, 1.15f, 1.03f, chrome);
        Cy(p, "ShowerPipe", 3.92f, 0.05f, 0.14f, 0.012f, 2.05f, chrome);
        CyH(p, "ShowerArm", 4.05f, 2.05f, 0.14f, 0.012f, 0.3f, chrome, 0f, 0f, 90f);
        Cy(p, "RainHead", 4.2f, 2.03f, 0.14f, 0.1f, 0.02f, chrome);
        D(p, "ShowerNiche", 3.88f, 1.1f, 0.5f, 3.9f, 1.4f, 0.9f, ceramic);
        Cy(p, "ShampooBottle", 3.9f, 1.1f, 0.6f, 0.025f, 0.15f, sage);

        // kucuk banyo kuveti (guney-dogu kose)
        B(p, "Tub", 5.55f, 0f, 0.15f, 6.28f, 0.55f, 1.85f, ceramic);
        D(p, "TubInner", 5.62f, 0.5f, 0.22f, 6.21f, 0.56f, 1.78f, water);
        D(p, "TubWoodSkirt", 5.5f, 0f, 0.1f, 6.28f, 0.1f, 1.9f, oak);
        Cy(p, "TubTap", 5.92f, 0.55f, 0.18f, 0.015f, 0.12f, chrome);
        D(p, "TubSpout", 5.915f, 0.66f, 0.1f, 5.925f, 0.68f, 0.18f, chrome);

        // lavabo dolabi + ayna (bati duvari, dunya z 2.4-3.6, yerel x = 3.6 - z)
        var v = Grp(p, "Vanity", 3.86f, 3.6f, 90f);
        B(v, "Body", 0f, 0.15f, 0f, 1.2f, 0.8f, 0.5f, oak);
        D(v, "Plinth", 0.02f, 0f, 0.03f, 1.18f, 0.15f, 0.45f, black);
        D(v, "DoorA", 0.02f, 0.2f, 0.5f, 0.59f, 0.78f, 0.52f, oakLight);
        D(v, "DoorB", 0.61f, 0.2f, 0.5f, 1.18f, 0.78f, 0.52f, oakLight);
        D(v, "PullA", 0.5f, 0.55f, 0.52f, 0.52f, 0.7f, 0.55f, brass);
        D(v, "PullB", 0.68f, 0.55f, 0.52f, 0.7f, 0.7f, 0.55f, brass);
        D(v, "Top", -0.01f, 0.8f, 0f, 1.21f, 0.84f, 0.52f, marble);
        D(v, "Basin", 0.35f, 0.84f, 0.1f, 0.85f, 0.9f, 0.45f, ceramic);
        Cy(v, "Tap", 0.6f, 0.9f, 0.08f, 0.015f, 0.2f, chrome);
        D(v, "TapSpout", 0.595f, 1.07f, 0.08f, 0.605f, 1.09f, 0.2f, chrome);
        D(v, "MirrorFrame", 0.15f, 1.1f, 0.0f, 1.05f, 2.1f, 0.03f, brass);
        D(v, "Mirror", 0.18f, 1.13f, 0.03f, 1.02f, 2.07f, 0.04f, mirror);
        D(v, "SconceL", 0.05f, 1.9f, 0.02f, 0.12f, 2.0f, 0.1f, emis);
        D(v, "SconceR", 1.08f, 1.9f, 0.02f, 1.15f, 2.0f, 0.1f, emis);
        Cy(v, "SoapDispenser", 0.15f, 0.84f, 0.2f, 0.03f, 0.12f, sage);
        Cy(v, "Toothbrush", 1.05f, 0.84f, 0.2f, 0.03f, 0.1f, glass);

        // havlu askisi ve havlular (dogu duvari)
        var tw = Grp(p, "TowelRail", 6.28f, 2.2f, 270f);
        D(tw, "Bar1", 0.0f, 1.3f, 0.0f, 0.5f, 1.32f, 0.04f, chrome);
        D(tw, "Bar2", 0.0f, 1.1f, 0.0f, 0.5f, 1.12f, 0.04f, chrome);
        D(tw, "Bar3", 0.0f, 0.9f, 0.0f, 0.5f, 0.92f, 0.04f, chrome);
        D(tw, "Post1", 0.0f, 0.85f, 0.0f, 0.02f, 1.35f, 0.04f, chrome);
        D(tw, "Post2", 0.48f, 0.85f, 0.0f, 0.5f, 1.35f, 0.04f, chrome);
        D(tw, "TowelA", 0.05f, 0.9f, 0.04f, 0.45f, 1.32f, 0.07f, towelA);
        D(tw, "TowelB", 0.1f, 0.95f, 0.07f, 0.4f, 1.12f, 0.1f, towelB);
        // camasir sepeti, hali, bitki
        Cy(p, "LaundryBasket", 6.0f, 0f, 3.9f, 0.2f, 0.55f, oakLight, true);
        D(p, "BathMat", 4.6f, 0.001f, 1.2f, 5.4f, 0.02f, 1.7f, towelB);
        Plant(p, 6.05f, 2.9f, 0.55f);
    }

    // ====================== WC ======================
    static void BuildWC()
    {
        var p = gWC;
        var t = Grp(p, "Toilet", 3.0f, 0.1f);
        B(t, "Cistern", -0.2f, 0.38f, 0f, 0.2f, 0.85f, 0.2f, ceramic);
        D(t, "CisternLid", -0.21f, 0.85f, -0.01f, 0.21f, 0.88f, 0.21f, ceramic);
        D(t, "FlushButton", -0.05f, 0.88f, 0.08f, 0.05f, 0.9f, 0.12f, chrome);
        B(t, "BowlBase", -0.15f, 0f, 0.05f, 0.15f, 0.38f, 0.35f, ceramic);
        Sp(t, "Bowl", 0f, 0.38f, 0.38f, 0.38f, 0.2f, 0.58f, ceramic);
        D(t, "Seat", -0.17f, 0.46f, 0.15f, 0.17f, 0.49f, 0.6f, trim);
        D(t, "Lid", -0.16f, 0.49f, 0.15f, 0.16f, 0.5f, 0.55f, trim, -8f, 0f, 0f);
        // klozet kagidi tutucu (bati duvari)
        D(p, "PaperHolder", 2.26f, 0.75f, 0.9f, 2.32f, 0.82f, 1.1f, chrome);
        D(p, "PaperRoll", 2.32f, 0.75f, 0.92f, 2.4f, 0.83f, 1.08f, cream);
        // el yikama lavabosu (dogu duvari)
        var s = Grp(p, "HandBasin", 3.74f, 2.4f, 270f);
        D(s, "Bracket", -0.2f, 0.78f, 0f, 0.2f, 0.84f, 0.26f, ceramic);
        D(s, "Bowl", -0.17f, 0.84f, 0.03f, 0.17f, 0.9f, 0.23f, ceramic);
        Cy(s, "Tap", 0f, 0.9f, 0.05f, 0.012f, 0.14f, chrome);
        D(s, "Spout", -0.005f, 1.02f, 0.05f, 0.005f, 1.04f, 0.14f, chrome);
        D(s, "MirrorFrame", -0.2f, 1.1f, 0f, 0.2f, 1.7f, 0.025f, brass);
        D(s, "Mirror", -0.18f, 1.12f, 0.025f, 0.18f, 1.68f, 0.03f, mirror);
        D(s, "TowelRing", 0.25f, 0.95f, 0f, 0.35f, 1.05f, 0.02f, brass);
        D(s, "Towel", 0.27f, 0.7f, 0.02f, 0.33f, 1.0f, 0.05f, towelB);
    }

    // ====================== DEPO ======================
    static void Shelf(Transform p, string n, float x, float z, float yaw, float w, float d, float h, int levels)
    {
        var g = Grp(p, n, x, z, yaw);
        foreach (var lx in new[] { 0f, w - 0.04f })
            foreach (var lz in new[] { 0f, d - 0.04f })
                B(g, "Post", lx, 0f, lz, lx + 0.04f, h, lz + 0.04f, steel);
        for (int i = 0; i < levels; i++)
        {
            float y = 0.1f + i * (h - 0.15f) / (levels - 1);
            B(g, "Board", 0f, y, 0f, w, y + 0.03f, d, oakLight);
            if (i < levels - 1)
            {
                int cnt = Mathf.Max(2, Mathf.RoundToInt(w / 0.5f));
                float bw = w / cnt;
                for (int k = 0; k < cnt; k++)
                    D(g, "Box", k * bw + 0.04f, y + 0.03f, 0.04f, (k + 1) * bw - 0.04f, y + 0.03f + 0.22f + 0.04f * ((k + i) % 3), d - 0.04f, ((k + i) % 2 == 0) ? cardboard : sage);
            }
        }
    }
    static void BuildStorage()
    {
        var p = gSto;
        Shelf(p, "Shelf_West", 0.1f, 2.9f, 90f, 2.6f, 0.4f, 1.9f, 4);
        Shelf(p, "Shelf_East", 2.14f, 0.3f, 270f, 2.6f, 0.4f, 1.9f, 4);
        // zemin kutulari, kova, paspas
        D(p, "FloorBox1", 0.6f, 0f, 0.25f, 1.1f, 0.4f, 0.75f, cardboard);
        D(p, "FloorBox2", 0.65f, 0.4f, 0.3f, 1.05f, 0.7f, 0.7f, cardboard, 0f, 12f, 0f);
        Cy(p, "Bucket", 1.45f, 0f, 0.45f, 0.17f, 0.3f, steel);
        Cy(p, "Mop", 1.45f, 0.1f, 0.45f, 0.012f, 1.4f, oakDark, false, 6f, 0f, 0f);
        D(p, "StorageCrate", 1.55f, 0f, 3.2f, 2.0f, 0.3f, 3.55f, oakDark);
    }

    // ====================== ANTRE ======================
    static void BuildEntrance()
    {
        var p = gEnt;
        // ayakkabilik (bati duvari, z 1.0-2.2; yerel x = 2.2 - z)
        var sc = Grp(p, "ShoeCabinet", 6.46f, 2.2f, 90f);
        B(sc, "Body", 0f, 0.1f, 0f, 1.2f, 0.95f, 0.35f, oak);
        D(sc, "Plinth", 0.02f, 0f, 0.03f, 1.18f, 0.1f, 0.3f, black);
        D(sc, "FlapA", 0.02f, 0.6f, 0.35f, 0.59f, 0.93f, 0.37f, oakLight, -8f, 0f, 0f);
        D(sc, "FlapB", 0.61f, 0.6f, 0.35f, 1.18f, 0.93f, 0.37f, oakLight, -8f, 0f, 0f);
        D(sc, "DoorLow", 0.02f, 0.14f, 0.35f, 1.18f, 0.56f, 0.37f, oakLight);
        D(sc, "Pull", 0.3f, 0.45f, 0.37f, 0.9f, 0.47f, 0.4f, brass);
        D(sc, "Cushion", 0.05f, 0.95f, 0.02f, 1.15f, 1.02f, 0.33f, sage);
        Cy(sc, "Vase", 1.05f, 1.02f, 0.18f, 0.045f, 0.16f, ceramic);
        // ayna (bati duvari)
        D(p, "MirrorFrame", 6.46f, 1.25f, 1.0f, 6.5f, 2.25f, 2.2f, brass);
        D(p, "Mirror", 6.5f, 1.28f, 1.04f, 6.52f, 2.22f, 2.16f, mirror);
        // portmanto (dogu duvari; yerel x = z - 0.9)
        var cr = Grp(p, "CoatRack", 8.94f, 0.9f, 270f);
        D(cr, "Board", 0f, 1.5f, 0f, 1.5f, 1.75f, 0.03f, oak);
        for (int i = 0; i < 5; i++)
        {
            float hx = 0.15f + i * 0.3f;
            D(cr, "Hook", hx - 0.01f, 1.55f, 0.03f, hx + 0.01f, 1.6f, 0.11f, brass);
        }
        D(cr, "Coat1", 0.05f, 0.75f, 0.05f, 0.4f, 1.62f, 0.14f, sageDark);
        D(cr, "Coat2", 0.5f, 0.85f, 0.05f, 0.85f, 1.62f, 0.13f, terra);
        D(cr, "Scarf", 0.95f, 1.0f, 0.05f, 1.1f, 1.6f, 0.1f, mustard);
        D(cr, "Bag", 1.2f, 0.9f, 0.05f, 1.45f, 1.3f, 0.14f, oakDark);
        B(cr, "Bench", 0f, 0f, 0.0f, 1.5f, 0.45f, 0.35f, oakLight);
        D(cr, "BenchCushion", 0.05f, 0.45f, 0.03f, 1.45f, 0.5f, 0.32f, cream);
        D(cr, "ShoeA", 0.2f, 0.0f, 0.38f, 0.32f, 0.1f, 0.58f, black);
        D(cr, "ShoeB", 0.36f, 0.0f, 0.38f, 0.48f, 0.1f, 0.58f, terra);
        // sirsemsiye standi
        Cy(p, "UmbrellaStand", 8.7f, 0f, 3.2f, 0.1f, 0.4f, brass);
        Cy(p, "Umbrella", 8.7f, 0.2f, 3.2f, 0.02f, 0.7f, black);
        // paspas ve yolluk
        D(p, "Doormat", 7.2f, 0.001f, 0.15f, 8.2f, 0.02f, 0.7f, oakDark);
        Rug(p, "Runner_Antre", 7.7f, 2.6f, 0.9f, 2.9f, rugM, rugB);
        Plant(p, 8.65f, 4.0f, 0.8f);
    }

    // ====================== KORIDOR ======================
    static void BuildCorridor()
    {
        var p = gCor;
        Rug(p, "Runner_Koridor", 7.0f, (Z1 + Z2) / 2, 12.0f, 0.7f, rugM, rugB);
        Frame(p, "Art1", 1.5f, 1.3f, Z2 - 0.06f, 0.6f, 0.8f, 180f, sage, terra);
        Frame(p, "Art2", 10.2f, 1.3f, Z2 - 0.06f, 0.6f, 0.8f, 180f, mustard, sageDark);
        Frame(p, "Art3", 6.3f, 1.3f, Z2 - 0.06f, 0.7f, 0.5f, 180f, terra, sage);
        D(p, "WallLight1", 0.9f, 1.7f, 4.5f + 0.06f, 1.0f, 1.9f, 4.5f + 0.1f, emis);
        D(p, "WallLight2", 13.0f, 1.7f, 4.5f + 0.06f, 13.1f, 1.9f, 4.5f + 0.1f, emis);
        D(p, "WallLight3", 13.0f, 1.7f, Z2 - 0.1f, 13.1f, 1.9f, Z2 - 0.06f, emis);
    }

    // ====================== ISIK ======================
    static void BuildLighting()
    {
        var warm = new Color(1f, 0.82f, 0.62f);
        var cool = new Color(1f, 0.95f, 0.88f);
        Lamp(gLight, "Light_Salon", 3.3f, 2.4f, 8.7f, 8f, 2.2f, warm);
        Lamp(gLight, "Light_Mutfak", 8.5f, 2.5f, 8.8f, 6f, 1.8f, cool);
        Lamp(gLight, "Light_Master", 11.5f, 2.5f, 2.3f, 6f, 1.8f, warm);
        Lamp(gLight, "Light_Cocuk", 12.0f, 2.5f, 8.8f, 6f, 1.8f, warm);
        Lamp(gLight, "Light_Banyo", 5.1f, 2.5f, 2.3f, 5f, 1.6f, cool);
        Lamp(gLight, "Light_WC", 3.0f, 2.5f, 2.3f, 3.5f, 1.2f, cool);
        Lamp(gLight, "Light_Depo", 1.12f, 2.5f, 2.3f, 3.5f, 1.2f, cool);
        Lamp(gLight, "Light_Antre", 7.7f, 2.5f, 2.3f, 5f, 1.6f, warm);
        Lamp(gLight, "Light_Koridor_B", 7.0f, 2.5f, 5.15f, 4f, 1.0f, warm);

        // tavan spotlari (Roof/Ceiling altinda, cati ile birlikte gizlenir)
        float[][] spots = {
            new[]{1.12f,2.3f}, new[]{3.0f,2.3f}, new[]{5.1f,2.3f}, new[]{7.7f,2.3f}, new[]{11.5f,2.3f},
            new[]{8.5f,7.5f}, new[]{8.5f,10.0f}, new[]{12f,7.2f}, new[]{12f,10.3f},
            new[]{1.5f,7.2f}, new[]{5.5f,7.2f}, new[]{1.5f,10.8f}, new[]{5.5f,10.8f},
            new[]{2.5f,5.15f}, new[]{7.0f,5.15f}, new[]{11.5f,5.15f}
        };
        foreach (var s in spots) Cy(gCeil, "Downlight", s[0], 2.77f, s[1], 0.08f, 0.03f, emis);
    }

    // ====================== CATI ======================
    static void BuildRoof()
    {
        // tavan levhasi
        D(gCeil, "CeilingSlab", -0.1f, 2.8f, -0.1f, 14.1f, 2.88f, 12.1f, wallM);
        D(gCeil, "CeilingCove", 0.1f, 2.74f, 0.1f, 13.9f, 2.8f, 0.2f, trim);

        float rise = Mathf.Tan(25f * Mathf.Deg2Rad);
        float eave = H - 0.5f * rise;                    // z=-0.6 noktasindaki yukseklik
        float apex = H + 6.1f * rise;
        float cy = (eave + apex) / 2f + 0.08f;
        float len = 6.6f / Mathf.Cos(25f * Mathf.Deg2Rad);
        D(gRoof, "RoofSlope_Front", -0.6f, cy - 0.09f, 2.7f - len / 2, 14.6f, cy + 0.09f, 2.7f + len / 2, roofM, -25f, 0f, 0f);
        D(gRoof, "RoofSlope_Back", -0.6f, cy - 0.09f, 9.3f - len / 2, 14.6f, cy + 0.09f, 9.3f + len / 2, roofM, 25f, 0f, 0f);
        D(gRoof, "RoofRidge", -0.65f, apex + 0.02f, 5.85f, 14.65f, apex + 0.14f, 6.15f, trim);
        Gable("Gable_Left", -0.1f, 0.1f);
        Gable("Gable_Right", 13.9f, 14.1f);
    }

    static void Gable(string n, float x0, float x1)
    {
        float apex = H + 6.1f * Mathf.Tan(25f * Mathf.Deg2Rad);
        var vs = new List<Vector3>();
        var tr = new List<int>();
        System.Action<Vector3, Vector3, Vector3> tri = (a, b, c) =>
        {
            int i = vs.Count; vs.Add(a); vs.Add(b); vs.Add(c); tr.Add(i); tr.Add(i + 1); tr.Add(i + 2);
        };
        Vector3 p0 = new Vector3(x0, H, -0.1f), p1 = new Vector3(x0, H, 12.1f), p2 = new Vector3(x0, apex, 6f);
        Vector3 q0 = new Vector3(x1, H, -0.1f), q1 = new Vector3(x1, H, 12.1f), q2 = new Vector3(x1, apex, 6f);
        tri(p0, p2, p1);
        tri(q0, q1, q2);
        tri(p0, q0, q2); tri(p0, q2, p2);
        tri(p1, p2, q2); tri(p1, q2, q1);
        tri(p0, p1, q1); tri(p0, q1, q0);
        var mesh = new Mesh { name = n };
        mesh.SetVertices(vs);
        mesh.SetTriangles(tr, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        var g = new GameObject(n);
        g.transform.SetParent(gRoof, false);
        g.AddComponent<MeshFilter>().sharedMesh = mesh;
        g.AddComponent<MeshRenderer>().sharedMaterial = gableM;
    }
}
