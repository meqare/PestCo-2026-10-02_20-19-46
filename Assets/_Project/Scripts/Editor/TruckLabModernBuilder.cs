using System.Collections.Generic;
using PestCo.Truck;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PestCo.Truck.EditorTools
{
    /// <summary>
    /// Modern "mobil böcek laboratuvarı" kamyon içi sahnesi (URP). TruckLabBuilder'dan bağımsızdır:
    /// kendi menüsü, kendi sahnesi ve kendi asset klasörü vardır.
    /// Menü: PestCo > Truck Lab Modern
    /// Koordinatlar: oyuncu arka kapıdan (-Z) girer ve +Z'ye (kabine) bakar.
    ///   Sağ duvar (+X): arkadan aydınlatmalı fanus vitrini (cam raflar, 3 farklı fanus boyu)
    ///   Ön sağ köşe   : tavana kadar uzanan büyük silindir cam tank
    ///   Ön duvar (+Z) : PC iş istasyonu (çift monitör, cam panelli kasa, ofis koltuğu)
    ///   Sol duvar (-X): cam kapılı numune dolabı, duvar ekranı, kontrol paneli
    ///   Orta          : ada tipi laboratuvar masası (mikroskop, petri, tablet, fanus tepsisi)
    /// </summary>
    public static class TruckLabModernBuilder
    {
        private const string Kok = "Assets/_Project/TruckLabModern";
        private const string SahneYolu = "Assets/Scenes/TruckLab_Modern.unity";

        // İç ölçüler (metre)
        private const float Genislik = 4.2f;  // X
        private const float Uzunluk = 6.4f;   // Z
        private const float Yukseklik = 2.7f; // Y
        private const float Kalinlik = 0.1f;
        private const float MasaZ = -0.3f;    // masa merkezi (ön/arka boşluk: 1.9 m / 1.9 m)

        private const StaticEditorFlags TamStatic =
            StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic |
            StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic;

        // Küçük / şeffaf / değişken objeler: lightmap'e katılmaz, occluder olmaz
        private const StaticEditorFlags GISiz =
            StaticEditorFlags.OccludeeStatic | StaticEditorFlags.BatchingStatic;

        private class Materyaller
        {
            public Material Duvar, Tavan, Zemin, Kablo, KoyuMetal, Aluminyum, Beyaz, Kumas, PCKasa, Toprak, Dal, BocekGovde;
            public Material Led, FanusIc, RafLed, ArkaIsik, Ekran, Buton;
            public Material Cam, CamLit;
        }

        private struct FanusTipi
        {
            public float Yaricap, Govde, Kubbe;
            public Mesh Mesh;
        }

        private static FanusTipi kucuk, orta, uzun;
        private static int etiketSayaci;

        // ------------------------------------------------------------------ MENÜ

        [MenuItem("PestCo/Truck Lab Modern/1 - Sahneyi Kur")]
        public static void SahneyiKur()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            KlasorleriHazirla();
            AssetDatabase.Refresh();
            Random.InitState(7); // her kurulumda aynı "doğal" dağılım

            var m = MateryalleriOlustur();
            kucuk = FanusTipiOlustur("FanusCam_Kucuk", 0.09f, 0.14f, 0.06f);
            orta = FanusTipiOlustur("FanusCam_Orta", 0.12f, 0.18f, 0.08f);
            uzun = FanusTipiOlustur("FanusCam_Uzun", 0.10f, 0.24f, 0.07f);
            Mesh tankMesh = LatheMesh("TankCam", 0.30f, 1.9f, 0f, 32);
            etiketSayaci = 1;

            VolumeProfile tabanProfil = TabanProfiliOlustur();
            VolumeProfile korkuProfil = KorkuProfiliOlustur();

            var sahne = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            OrtamAyarlari();

            int interactableLayer = LayerMask.NameToLayer("Interactable");
            if (interactableLayer < 0)
            {
                Debug.LogWarning("[TruckLabModern] 'Interactable' layer bulunamadı, Default kullanılıyor.");
                interactableLayer = 0;
            }

            var ortam = new GameObject("Ortam").transform;
            var mobilya = new GameObject("Mobilya").transform;
            var fanuslar = new GameObject("Fanuslar").transform;
            var isiklar = new GameObject("Aydinlatma").transform;
            var etkilesim = new GameObject("Etkilesim").transform;

            KabuguKur(ortam, m);
            ArkaKapilariKur(ortam, m);
            MasayiKur(mobilya, fanuslar, m);
            VitriniKur(mobilya, fanuslar, m);
            TankKur(mobilya, m, tankMesh);
            var atmosfer = PostProcessKur(tabanProfil, korkuProfil);
            PCIstasyonunuKur(mobilya, etkilesim, m, interactableLayer);
            SolDuvariKur(mobilya, fanuslar, etkilesim, m, interactableLayer, atmosfer);
            AydinlatmayiKur(isiklar, m);
            ReflectionProbeKur(isiklar);
            OyuncuyuKur(interactableLayer);
            GolgeMesafesiniAyarla(25f);
            LightingAyarlariniOlustur();

            EditorSceneManager.SaveScene(sahne, SahneYolu);
            AssetDatabase.SaveAssets();
            Debug.Log($"[TruckLabModern] Sahne kuruldu: {SahneYolu} ({etiketSayaci - 1} fanus)  → Sonra '2 - Bake' çalıştırın.");
        }

        [MenuItem("PestCo/Truck Lab Modern/2 - Occlusion + Lightmap Bake")]
        public static void Bake()
        {
            StaticOcclusionCulling.smallestOccluder = 0.5f;
            StaticOcclusionCulling.smallestHole = 0.1f;
            StaticOcclusionCulling.Compute();
            Lightmapping.BakeAsync(); // Reflection Probe'lar da bu bake ile üretilir
            Debug.Log("[TruckLabModern] Occlusion culling hesaplandı, lightmap bake başladı.");
        }

        // ------------------------------------------------------------------ KLASÖR / AYAR

        private static void KlasorleriHazirla()
        {
            if (!AssetDatabase.IsValidFolder(Kok)) AssetDatabase.CreateFolder("Assets/_Project", "TruckLabModern");
            foreach (var alt in new[] { "Materials", "Textures", "Meshes", "Profiles" })
                if (!AssetDatabase.IsValidFolder($"{Kok}/{alt}"))
                    AssetDatabase.CreateFolder(Kok, alt);
        }

        private static void OrtamAyarlari()
        {
            RenderSettings.skybox = null;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = Hex("#0E1417");
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogColor = Hex("#0B1114");
            RenderSettings.fogDensity = 0.03f;
        }

        /// <summary>Global URP asset'ine dokunmaz; sahneye ShadowDistanceOverride bileşeni ekler.</summary>
        private static void GolgeMesafesiniAyarla(float mesafe)
        {
            var go = new GameObject("GolgeMesafesi");
            var bilesen = go.AddComponent<ShadowDistanceOverride>();
            var so = new SerializedObject(bilesen);
            var prop = so.FindProperty("golgeMesafesi");
            if (prop != null) { prop.floatValue = mesafe; so.ApplyModifiedPropertiesWithoutUndo(); }

            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp && !urp.supportsAdditionalLightShadows)
                Debug.LogWarning("[TruckLabModern] URP asset'te ek ışık gölgeleri kapalı; titreyen lambalar gölge vermez.");
        }

        private static void LightingAyarlariniOlustur()
        {
            string yol = $"{Kok}/TruckLabModern.lighting";
            var ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(yol);
            if (ls == null)
            {
                ls = new LightingSettings { name = "TruckLabModern" };
                AssetDatabase.CreateAsset(ls, yol);
            }
            ls.bakedGI = true;
            ls.realtimeGI = false;
            ls.mixedBakeMode = MixedLightingMode.IndirectOnly; // Mixed: dolaylı baked, doğrudan + gölge realtime
            ls.lightmapper = LightingSettings.Lightmapper.ProgressiveGPU;
            EditorUtility.SetDirty(ls);
            Lightmapping.lightingSettings = ls;
        }

        // ------------------------------------------------------------------ MATERYALLER

        private static Materyaller MateryalleriOlustur()
        {
            var normal = FircalanmisNormalOlustur();
            var m = new Materyaller
            {
                // İstenen temel değerler
                Duvar = Lit("M_FircalanmisCelik", "#8A9196", 1f, 0.6f, normal),
                Tavan = Lit("M_TavanPaneli", "#1E2326", 0f, 0.25f),
                Zemin = Lit("M_EpoksiZemin", "#3A4248", 0f, 0.85f),
                Kablo = Lit("M_KabloKanali", "#0B0D0E", 0.2f, 0.4f),

                // Modern iç mekan için ek yüzeyler
                KoyuMetal = Lit("M_KoyuMetal", "#202528", 1f, 0.45f),
                Aluminyum = Lit("M_Aluminyum", "#B9C0C6", 1f, 0.75f, normal),
                Beyaz = Lit("M_BeyazPanel", "#D5DBDF", 0f, 0.55f),
                Kumas = Lit("M_KoltukKumas", "#1A1F23", 0f, 0.15f),
                PCKasa = Lit("M_PCKasa", "#111416", 0.6f, 0.7f),
                Toprak = Lit("M_Toprak", "#2B2119", 0f, 0.1f),
                Dal = Lit("M_Dal", "#4A3A2A", 0f, 0.2f),
                BocekGovde = Lit("M_BocekGovde", "#1C1410", 0.3f, 0.8f),

                Led = Emissive("M_TavanLED", "#D9E8F0", 2.0f),
                FanusIc = Emissive("M_FanusIc", "#6FD6C8", 1.5f),
                RafLed = Emissive("M_RafAltiLED", "#6FD6C8", 0.8f),
                ArkaIsik = Emissive("M_VitrinArkaIsik", "#8FD9CF", 0.35f),
                Ekran = Emissive("M_PCEkran", "#0F3A44", 1.0f),
                Buton = Emissive("M_PanelButon", "#C83A3A", 1.0f),
            };
            m.CamLit = CamLitOlustur();   // Tablodaki URP Lit cam (raflar, kapaklar, petri)
            m.Cam = CamFresnelOlustur(m.CamLit);
            return m;
        }

        private static Material Lit(string ad, string hex, float metallic, float smoothness, Texture2D normal = null)
        {
            var mat = MateryalYukle(ad, "Universal Render Pipeline/Lit");
            mat.SetColor("_BaseColor", Hex(hex));
            mat.SetFloat("_Metallic", metallic);
            mat.SetFloat("_Smoothness", smoothness);
            if (normal != null)
            {
                mat.SetTexture("_BumpMap", normal);
                mat.SetFloat("_BumpScale", 0.6f);
                mat.SetTextureScale("_BaseMap", new Vector2(2f, 2f));
                mat.EnableKeyword("_NORMALMAP");
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material Emissive(string ad, string hex, float yogunluk)
        {
            var mat = Lit(ad, "#0B0D0E", 0f, 0.5f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Hex(hex) * yogunluk);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material CamLitOlustur()
        {
            var mat = MateryalYukle("M_FanusCam_Lit", "Universal Render Pipeline/Lit");
            var c = Hex("#BFE8E2"); c.a = 0.12f;
            mat.SetColor("_BaseColor", c);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_Smoothness", 0.95f);
            mat.SetFloat("_Surface", 1f);             // Transparent
            mat.SetFloat("_Blend", 1f);               // Premultiply
            mat.SetFloat("_SrcBlend", (float)BlendMode.One);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.SetFloat("_SpecularHighlights", 1f);  // Specular açık
            mat.SetFloat("_EnvironmentReflections", 1f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.EnableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.DisableKeyword("_SPECULARHIGHLIGHTS_OFF");
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.renderQueue = (int)RenderQueue.Transparent;
            mat.SetShaderPassEnabled("ShadowCaster", false);
            mat.SetShaderPassEnabled("DepthOnly", false);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material CamFresnelOlustur(Material yedek)
        {
            if (Shader.Find("PestCo/FanusGlass") == null)
            {
                Debug.LogWarning("[TruckLabModern] PestCo/FanusGlass shader bulunamadı, Lit cam kullanılıyor.");
                return yedek;
            }
            var mat = MateryalYukle("M_FanusCam_Fresnel", "PestCo/FanusGlass");
            var c = Hex("#BFE8E2"); c.a = 0.12f;
            mat.SetColor("_BaseColor", c);
            mat.SetColor("_EdgeColor", Hex("#9FEDE0"));
            mat.SetFloat("_Smoothness", 0.95f);
            mat.SetFloat("_FresnelPower", 3f);
            mat.SetFloat("_FresnelIntensity", 1f);
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material MateryalYukle(string ad, string shaderAdi)
        {
            string yol = $"{Kok}/Materials/{ad}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(yol);
            var shader = Shader.Find(shaderAdi);
            if (mat == null)
            {
                mat = new Material(shader) { name = ad };
                AssetDatabase.CreateAsset(mat, yol);
            }
            else if (shader != null && mat.shader != shader) mat.shader = shader;
            return mat;
        }

        /// <summary>
        /// URP Lit gerçek anizotropi desteklemez; bu yüzden tek yönde uzatılmış çizgilerden
        /// oluşan bir normal map üretilir (fırçalanmış metal görünümü).
        /// </summary>
        private static Texture2D FircalanmisNormalOlustur()
        {
            string yol = $"{Kok}/Textures/T_FircalanmisCelik_N.png";
            var mevcut = AssetDatabase.LoadAssetAtPath<Texture2D>(yol);
            if (mevcut != null) return mevcut;

            const int n = 512;
            var h = new float[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    // X yönünde çok yavaş, Y yönünde çok hızlı değişen gürültü = yatay fırça çizgileri
                    float a = Mathf.PerlinNoise(x * 0.004f, y * 0.9f);
                    float b = Mathf.PerlinNoise(x * 0.015f + 31.7f, y * 2.3f + 11.1f);
                    h[y * n + x] = a * 0.65f + b * 0.35f;
                }

            var tex = new Texture2D(n, n, TextureFormat.RGB24, false, true);
            var px = new Color[n * n];
            const float guc = 2.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = h[y * n + (x + 1) % n] - h[y * n + (x - 1 + n) % n];
                    float dy = h[((y + 1) % n) * n + x] - h[((y - 1 + n) % n) * n + x];
                    var nv = new Vector3(-dx * guc, -dy * guc, 1f).normalized;
                    px[y * n + x] = new Color(nv.x * 0.5f + 0.5f, nv.y * 0.5f + 0.5f, nv.z * 0.5f + 0.5f);
                }
            tex.SetPixels(px);
            tex.Apply();
            System.IO.File.WriteAllBytes(yol, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);

            AssetDatabase.ImportAsset(yol);
            var imp = (TextureImporter)AssetImporter.GetAtPath(yol);
            imp.textureType = TextureImporterType.NormalMap;
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(yol);
        }

        // ------------------------------------------------------------------ FANUS MESH

        private static FanusTipi FanusTipiOlustur(string ad, float r, float govde, float kubbe)
            => new FanusTipi { Yaricap = r, Govde = govde, Kubbe = kubbe, Mesh = LatheMesh(ad, r, govde, kubbe, 24) };

        /// <summary>Silindir gövde + (kubbe > 0 ise) kubbeli kapak. Profil Y ekseni etrafında döndürülür.</summary>
        private static Mesh LatheMesh(string ad, float r, float govde, float kubbe, int segment)
        {
            string yol = $"{Kok}/Meshes/{ad}.asset";
            var mevcut = AssetDatabase.LoadAssetAtPath<Mesh>(yol);
            if (mevcut != null) return mevcut;

            var profil = new List<Vector2> { new Vector2(r, 0f), new Vector2(r, govde * 0.5f), new Vector2(r, govde) };
            if (kubbe > 0f)
            {
                const int kubbeAdim = 8;
                for (int i = 1; i <= kubbeAdim; i++)
                {
                    float a = i / (float)kubbeAdim * Mathf.PI * 0.5f;
                    profil.Add(new Vector2(r * Mathf.Cos(a), govde + kubbe * Mathf.Sin(a)));
                }
            }

            var v = new List<Vector3>();
            var nrm = new List<Vector3>();
            var uv = new List<Vector2>();
            var tri = new List<int>();

            for (int j = 0; j < profil.Count; j++)
            {
                Vector2 onceki = profil[Mathf.Max(j - 1, 0)];
                Vector2 sonraki = profil[Mathf.Min(j + 1, profil.Count - 1)];
                Vector2 teg = (sonraki - onceki).normalized;
                Vector2 n2 = new Vector2(teg.y, -teg.x); // profil yukarı giderken dışa bakan normal

                for (int s = 0; s <= segment; s++)
                {
                    float aci = s / (float)segment * Mathf.PI * 2f;
                    var yon = new Vector3(Mathf.Cos(aci), 0f, Mathf.Sin(aci));
                    v.Add(yon * profil[j].x + Vector3.up * profil[j].y);
                    nrm.Add((yon * n2.x + Vector3.up * n2.y).normalized);
                    uv.Add(new Vector2(s / (float)segment, j / (float)(profil.Count - 1)));
                }
            }

            int halka = segment + 1;
            for (int j = 0; j < profil.Count - 1; j++)
                for (int s = 0; s < segment; s++)
                {
                    int a = j * halka + s, b = a + halka;
                    tri.Add(a); tri.Add(b); tri.Add(a + 1);
                    tri.Add(a + 1); tri.Add(b); tri.Add(b + 1);
                }

            var mesh = new Mesh { name = ad };
            mesh.SetVertices(v);
            mesh.SetNormals(nrm);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(tri, 0);
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, yol);
            return mesh;
        }

        // ------------------------------------------------------------------ KABUK

        private static void KabuguKur(Transform p, Materyaller m)
        {
            float hx = Genislik * 0.5f, hz = Uzunluk * 0.5f, k = Kalinlik;
            Kutu("Zemin", p, new Vector3(0, -k / 2, 0), new Vector3(Genislik + 2 * k, k, Uzunluk + 2 * k), m.Zemin);
            Kutu("Tavan", p, new Vector3(0, Yukseklik + k / 2, 0), new Vector3(Genislik + 2 * k, k, Uzunluk + 2 * k), m.Tavan);
            Kutu("Duvar_Sag", p, new Vector3(hx + k / 2, Yukseklik / 2, 0), new Vector3(k, Yukseklik, Uzunluk), m.Duvar);
            Kutu("Duvar_Sol", p, new Vector3(-hx - k / 2, Yukseklik / 2, 0), new Vector3(k, Yukseklik, Uzunluk), m.Duvar);
            Kutu("Duvar_On", p, new Vector3(0, Yukseklik / 2, hz + k / 2), new Vector3(Genislik, Yukseklik, k), m.Duvar);
            Kutu("Duvar_Arka", p, new Vector3(0, Yukseklik / 2, -hz - k / 2), new Vector3(Genislik, Yukseklik, k), m.Duvar);

            // Duvar panel derzleri (her 0.8 m'de ince koyu çizgi) — modern panel kaplama hissi
            for (float z = -hz + 0.8f; z < hz - 0.1f; z += 0.8f)
                foreach (float sx in new[] { -1f, 1f })
                    Kutu("PanelDerz", p, new Vector3(sx * (hx - 0.004f), Yukseklik / 2, z), new Vector3(0.008f, Yukseklik - 0.2f, 0.012f), m.Kablo, false);

            // Tavan: koyu panel bölmeleri
            for (int i = 0; i < 4; i++)
            {
                float z = -hz + Uzunluk / 4f * (i + 0.5f);
                Kutu($"TavanPanel_{i}", p, new Vector3(0, Yukseklik - 0.02f, z), new Vector3(Genislik - 0.3f, 0.03f, Uzunluk / 4f - 0.08f), m.Tavan, false);
            }

            // Kablo kanalları: zemin ve tavan kenarları
            foreach (float sx in new[] { -1f, 1f })
            {
                Kutu("KabloKanali_Zemin", p, new Vector3(sx * (hx - 0.06f), 0.05f, 0), new Vector3(0.12f, 0.1f, Uzunluk), m.Kablo, false);
                Kutu("KabloKanali_Tavan", p, new Vector3(sx * (hx - 0.05f), Yukseklik - 0.04f, 0), new Vector3(0.1f, 0.08f, Uzunluk), m.Kablo, false);
            }
            Kutu("KabloKanali_OnDikey", p, new Vector3(0.75f, Yukseklik / 2, hz - 0.05f), new Vector3(0.1f, Yukseklik, 0.1f), m.Kablo, false);

            // Zemin yön şeritleri (ince emissive çizgiler, koridoru belirler)
            foreach (float x in new[] { -1.5f, 1.6f })
                Kutu("ZeminYonSeridi", p, new Vector3(x, 0.002f, -0.1f), new Vector3(0.025f, 0.004f, Uzunluk - 1.6f), m.RafLed, false);
        }

        private static void ArkaKapilariKur(Transform p, Materyaller m)
        {
            // Kamyonun çift kanatlı arka kapısı (iç yüz)
            float z = -Uzunluk * 0.5f + 0.02f;
            var kapi = new GameObject("ArkaKapi").transform;
            kapi.SetParent(p, false);
            foreach (float s in new[] { -1f, 1f })
            {
                Kutu("Kanat", kapi, new Vector3(s * 0.62f, 1.15f, z), new Vector3(1.2f, 2.2f, 0.03f), m.Duvar, false);
                Kutu("Kol", kapi, new Vector3(s * 0.12f, 1.1f, z + 0.04f), new Vector3(0.03f, 0.6f, 0.03f), m.Aluminyum, false);
                Kutu("KilitCubugu", kapi, new Vector3(s * 0.85f, 1.15f, z + 0.03f), new Vector3(0.025f, 2.1f, 0.025f), m.Aluminyum, false);
            }
            Kutu("OrtaDerz", kapi, new Vector3(0, 1.15f, z + 0.016f), new Vector3(0.02f, 2.2f, 0.005f), m.Kablo, false);
            Kutu("UstLED", kapi, new Vector3(0, 2.32f, z + 0.02f), new Vector3(1.6f, 0.015f, 0.02f), m.RafLed, false);
        }

        // ------------------------------------------------------------------ ORTA MASA

        private static void MasayiKur(Transform p, Transform fanusKoku, Materyaller m)
        {
            // 2.0 (Z) x 0.8 (X), üst yüzey 0.9 m. Boşluk: sol 1.2 m, sağ 1.3 m, ön/arka 1.9 m.
            var masa = new GameObject("CalismaMasasi").transform;
            masa.SetParent(p, false);
            masa.localPosition = new Vector3(0, 0, MasaZ);

            Kutu("Ust", masa, new Vector3(0, 0.88f, 0), new Vector3(0.8f, 0.04f, 2.0f), m.Beyaz);
            Kutu("Govde", masa, new Vector3(0, 0.45f, 0), new Vector3(0.6f, 0.82f, 1.7f), m.Duvar);
            Kutu("Supurgelik", masa, new Vector3(0, 0.04f, 0), new Vector3(0.56f, 0.08f, 1.66f), m.Kablo, false);
            Kutu("AltLED", masa, new Vector3(0, 0.003f, 0), new Vector3(0.62f, 0.004f, 1.72f), m.RafLed, false);
            foreach (float s in new[] { -1f, 1f })
                Kutu("UstKenarLED", masa, new Vector3(s * 0.401f, 0.865f, 0), new Vector3(0.004f, 0.008f, 1.96f), m.RafLed, false);

            // Mikroskop (primitiflerden)
            var mik = new GameObject("Mikroskop").transform;
            mik.SetParent(masa, false);
            mik.localPosition = new Vector3(-0.1f, 0.9f, 0.55f);
            mik.localRotation = Quaternion.Euler(0, 90f, 0);
            Kutu("Taban", mik, new Vector3(0, 0.02f, 0), new Vector3(0.16f, 0.04f, 0.22f), m.Beyaz, false);
            Kutu("Kol", mik, new Vector3(0, 0.16f, -0.07f), new Vector3(0.05f, 0.26f, 0.05f), m.Beyaz, false);
            Kutu("Tabla", mik, new Vector3(0, 0.12f, 0.01f), new Vector3(0.12f, 0.012f, 0.12f), m.KoyuMetal, false);
            var tup = Prim(PrimitiveType.Cylinder, "Tup", mik, new Vector3(0, 0.26f, -0.02f), new Vector3(0.04f, 0.08f, 0.04f), m.KoyuMetal, false, GISiz);
            tup.transform.localRotation = Quaternion.Euler(-25f, 0, 0);
            Prim(PrimitiveType.Cylinder, "Objektif", mik, new Vector3(0, 0.17f, 0.01f), new Vector3(0.02f, 0.03f, 0.02f), m.Aluminyum, false, GISiz);
            Prim(PrimitiveType.Cylinder, "IsikHalkasi", mik, new Vector3(0, 0.045f, 0.01f), new Vector3(0.05f, 0.004f, 0.05f), m.FanusIc, false, GISiz);

            // Petri kapları
            for (int i = 0; i < 4; i++)
            {
                var petri = Prim(PrimitiveType.Cylinder, "Petri", masa, new Vector3(0.18f, 0.905f, 0.2f + i * 0.13f), new Vector3(0.1f, 0.006f, 0.1f), m.CamLit, false, GISiz);
                petri.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                Prim(PrimitiveType.Cylinder, "Kultur", masa, new Vector3(0.18f, 0.9f, 0.2f + i * 0.13f), new Vector3(0.085f, 0.002f, 0.085f), i % 2 == 0 ? m.FanusIc : m.Toprak, false, GISiz);
            }

            // Tablet
            var tablet = Kutu("Tablet", masa, new Vector3(0.12f, 0.905f, -0.35f), new Vector3(0.18f, 0.008f, 0.26f), m.PCKasa, false);
            tablet.transform.localRotation = Quaternion.Euler(0, 12f, 0);
            var tabletEkran = Kutu("TabletEkran", tablet.transform, new Vector3(0, 0.6f, 0), new Vector3(0.9f, 0.3f, 0.9f), m.Ekran, false);
            GameObjectUtility.SetStaticEditorFlags(tabletEkran, GISiz);

            // Küçük fanus tepsisi (masanın arka ucunda, 3 adet; etiketler arka kapıya bakar)
            Kutu("FanusTepsisi", masa, new Vector3(0, 0.905f, -0.75f), new Vector3(0.5f, 0.01f, 0.32f), m.Aluminyum, false);
            for (int i = -1; i <= 1; i++)
                FanusKur(fanusKoku, new Vector3(i * 0.16f, 0.91f, MasaZ - 0.75f), m, kucuk, Quaternion.Euler(0, -90f, 0));
        }

        // ------------------------------------------------------------------ SAĞ DUVAR VİTRİNİ

        private static void VitriniKur(Transform mobilya, Transform fanusKoku, Materyaller m)
        {
            const float derinlik = 0.4f;
            float duvarX = Genislik * 0.5f;
            float x = duvarX - derinlik * 0.5f;                // vitrin merkezi
            float onKenar = duvarX - derinlik;                 // 1.7 m → masaya 1.3 m
            float[] katlar = { 1.2f, 1.65f, 2.10f };           // zemin üstü 1.2 m, katlar arası 0.45 m
            float[] gruplar = { -1.7f, 0f, 1.7f };             // 3 grup, aralarında 0.5 m boşluk
            const float grupGenislik = 1.2f;

            // Her katta farklı bir ritim: orta-uzun-orta / 4 küçük / uzun-orta-uzun
            var desenler = new[]
            {
                new[] { orta, uzun, orta },
                new[] { kucuk, kucuk, kucuk, kucuk },
                new[] { uzun, orta, uzun },
            };

            for (int g = 0; g < gruplar.Length; g++)
            {
                var grup = new GameObject($"Vitrin_{g + 1}").transform;
                grup.SetParent(mobilya, false);
                float gz = gruplar[g];

                // Alt dolap: beyaz kapaklar, kulpsuz (gizli tutamak derzi), süpürgelik LED
                Kutu("AltDolap", grup, new Vector3(x, 0.55f, gz), new Vector3(derinlik, 1.1f, grupGenislik), m.Beyaz);
                Kutu("TutamakDerzi", grup, new Vector3(onKenar - 0.001f, 1.04f, gz), new Vector3(0.004f, 0.02f, grupGenislik - 0.04f), m.Kablo, false);
                Kutu("KapakDerzi", grup, new Vector3(onKenar - 0.001f, 0.55f, gz), new Vector3(0.004f, 1.0f, 0.006f), m.Kablo, false);
                Kutu("SupurgelikLED", grup, new Vector3(onKenar + 0.02f, 0.012f, gz), new Vector3(0.01f, 0.008f, grupGenislik - 0.06f), m.RafLed, false);
                Kutu("Tezgah", grup, new Vector3(x - 0.01f, 1.115f, gz), new Vector3(derinlik + 0.02f, 0.03f, grupGenislik), m.KoyuMetal);

                // Arkadan aydınlatmalı panel + alüminyum çerçeve
                Kutu("ArkaIsikPaneli", grup, new Vector3(duvarX - 0.012f, 1.8f, gz), new Vector3(0.01f, 1.25f, grupGenislik - 0.06f), m.ArkaIsik, false);
                Kutu("CerceveUst", grup, new Vector3(x, 2.46f, gz), new Vector3(derinlik, 0.04f, grupGenislik), m.Aluminyum);
                foreach (float s in new[] { -1f, 1f })
                    Kutu("CerceveYan", grup, new Vector3(x, 1.8f, gz + s * (grupGenislik * 0.5f - 0.015f)), new Vector3(derinlik, 1.3f, 0.03f), m.Aluminyum);

                for (int k = 0; k < katlar.Length; k++)
                {
                    float ky = katlar[k];

                    // Cam raf + ön alüminyum profil + raf altı LED
                    var raf = Kutu($"CamRaf_{k + 1}", grup, new Vector3(x, ky - 0.006f, gz), new Vector3(derinlik - 0.02f, 0.012f, grupGenislik - 0.06f), m.CamLit, false);
                    GameObjectUtility.SetStaticEditorFlags(raf, GISiz);
                    raf.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                    Kutu("RafProfil", grup, new Vector3(onKenar + 0.01f, ky - 0.02f, gz), new Vector3(0.02f, 0.03f, grupGenislik - 0.06f), m.Aluminyum, false);
                    Kutu("RafAltiLED", grup, new Vector3(onKenar + 0.03f, ky - 0.04f, gz), new Vector3(0.012f, 0.006f, grupGenislik - 0.1f), m.RafLed, false);

                    var desen = desenler[k];
                    float kullanilan = grupGenislik - 0.2f;
                    float aralik = kullanilan / desen.Length;
                    for (int f = 0; f < desen.Length; f++)
                    {
                        float fz = gz - kullanilan * 0.5f + aralik * (f + 0.5f);
                        // Hafif derinlik kaydırması: zikzak dizilim daha doğal durur
                        float fx = x - 0.02f + (f % 2 == 0 ? 0f : 0.04f);
                        FanusKur(fanusKoku, new Vector3(fx, ky, fz), m, desen[f], Quaternion.identity);
                    }
                }
            }

            // Vitrin grupları arasındaki boşluklarda dikey LED kolonlar
            foreach (float z in new[] { -0.85f, 0.85f })
                Kutu("AraLEDKolon", mobilya, new Vector3(duvarX - 0.02f, 1.8f, z), new Vector3(0.02f, 1.3f, 0.02f), m.RafLed, false);
        }

        /// <summary>
        /// Fanus: koyu metal taban + toprak + dal + böcek + iç ışık diski + cam gövde/kubbe + iç Point Light + etiket.
        /// donus = identity iken etiket -X'e (koridora) bakar.
        /// </summary>
        private static void FanusKur(Transform p, Vector3 poz, Materyaller m, FanusTipi tip, Quaternion donus)
        {
            string etiket = $"B-{etiketSayaci++:00}";
            float r = tip.Yaricap;
            const float tabanH = 0.05f;
            float olc = r / 0.12f;

            var kok = new GameObject("Fanus_" + etiket).transform;
            kok.SetParent(p, false);
            kok.localPosition = poz;
            kok.localRotation = donus;

            // İç düzen için ayrı, rastgele döndürülmüş alt kök (etiket sabit kalsın)
            var ic = new GameObject("Ic").transform;
            ic.SetParent(kok, false);
            ic.localRotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);

            // Koyu metal taban (Unity silindiri 24 segment) + alüminyum halka + taban LED
            Prim(PrimitiveType.Cylinder, "Taban", kok, new Vector3(0, tabanH * 0.5f, 0), new Vector3(2 * r + 0.03f, tabanH * 0.5f, 2 * r + 0.03f), m.KoyuMetal, false, TamStatic);
            Prim(PrimitiveType.Cylinder, "TabanHalka", kok, new Vector3(0, tabanH, 0), new Vector3(2 * r + 0.012f, 0.003f, 2 * r + 0.012f), m.Aluminyum, false, GISiz);
            Prim(PrimitiveType.Cylinder, "TabanLED", kok, new Vector3(0, tabanH * 0.35f, 0), new Vector3(2 * r + 0.034f, 0.003f, 2 * r + 0.034f), m.RafLed, false, GISiz);

            // İç düzen: emissive taban diski, toprak, dal, böcek
            Prim(PrimitiveType.Cylinder, "IcIsikDiski", ic, new Vector3(0, tabanH + 0.002f, 0), new Vector3(2 * r - 0.01f, 0.002f, 2 * r - 0.01f), m.FanusIc, false, GISiz);
            Prim(PrimitiveType.Sphere, "Toprak", ic, new Vector3(0, tabanH + 0.01f, 0), new Vector3(2 * r - 0.04f, 0.03f * olc, 2 * r - 0.04f), m.Toprak, false, GISiz);
            var dal = Prim(PrimitiveType.Cube, "Dal", ic, new Vector3(0.02f * olc, tabanH + 0.06f * olc, 0), new Vector3(0.012f, 0.13f * olc, 0.012f), m.Dal, false, GISiz);
            dal.transform.localRotation = Quaternion.Euler(0, 0, Random.Range(25f, 45f));
            var bocek = Prim(PrimitiveType.Sphere, "Bocek", ic, new Vector3(-0.03f * olc, tabanH + 0.03f, 0.02f * olc), new Vector3(0.035f * olc, 0.015f * olc, 0.05f * olc), m.BocekGovde, false, GISiz);
            bocek.transform.localRotation = Quaternion.Euler(0, Random.Range(0f, 360f), 0);

            // Cam gövde + kubbe
            var cam = new GameObject("Cam");
            cam.transform.SetParent(kok, false);
            cam.transform.localPosition = new Vector3(0, tabanH, 0);
            cam.AddComponent<MeshFilter>().sharedMesh = tip.Mesh;
            var mr = cam.AddComponent<MeshRenderer>();
            mr.sharedMaterial = m.Cam;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(cam, GISiz);

            // Kubbe tepesinde küçük alüminyum topuz
            Prim(PrimitiveType.Sphere, "KubbeTopuz", kok, new Vector3(0, tabanH + tip.Govde + tip.Kubbe, 0), Vector3.one * 0.018f, m.Aluminyum, false, GISiz);

            // İç ışık (baked: onlarca realtime ışık maliyetinden kaçınmak için)
            var isik = new GameObject("IcIsik").AddComponent<Light>();
            isik.transform.SetParent(kok, false);
            isik.transform.localPosition = new Vector3(0, tabanH + tip.Govde * 0.6f, 0);
            isik.type = LightType.Point;
            isik.range = 0.8f;
            isik.intensity = 0.6f;
            isik.color = Hex("#6FD6C8");
            isik.shadows = LightShadows.None;
            isik.lightmapBakeType = LightmapBakeType.Baked;

            // Taban etiketi (TextMeshPro, tabanın yerel -X yüzünde)
            var yazi = new GameObject("Etiket").AddComponent<TextMeshPro>();
            yazi.transform.SetParent(kok, false);
            yazi.transform.localPosition = new Vector3(-(r + 0.016f), tabanH * 0.55f, 0);
            yazi.transform.localRotation = Quaternion.Euler(0, 90f, 0);
            yazi.rectTransform.sizeDelta = new Vector2(0.2f, 0.04f);
            yazi.text = etiket;
            yazi.fontSize = 0.3f;
            yazi.alignment = TextAlignmentOptions.Center;
            yazi.color = Hex("#9FEDE0");
        }

        // ------------------------------------------------------------------ BÜYÜK TANK

        private static void TankKur(Transform p, Materyaller m, Mesh tankMesh)
        {
            // Ön sağ köşe: tavana kadar uzanan silindir cam tank (sahnenin odak noktası)
            var tank = new GameObject("BuyukTank").transform;
            tank.SetParent(p, false);
            tank.localPosition = new Vector3(1.5f, 0, 2.7f);

            Prim(PrimitiveType.Cylinder, "AltKaide", tank, new Vector3(0, 0.2f, 0), new Vector3(0.72f, 0.2f, 0.72f), m.KoyuMetal, true, TamStatic);
            Prim(PrimitiveType.Cylinder, "AltHalkaLED", tank, new Vector3(0, 0.405f, 0), new Vector3(0.64f, 0.005f, 0.64f), m.FanusIc, false, GISiz);
            Prim(PrimitiveType.Cylinder, "UstKaide", tank, new Vector3(0, 2.52f, 0), new Vector3(0.72f, 0.18f, 0.72f), m.KoyuMetal, false, TamStatic);
            Prim(PrimitiveType.Cylinder, "UstHalkaLED", tank, new Vector3(0, 2.335f, 0), new Vector3(0.64f, 0.005f, 0.64f), m.FanusIc, false, GISiz);

            var cam = new GameObject("TankCam");
            cam.transform.SetParent(tank, false);
            cam.transform.localPosition = new Vector3(0, 0.42f, 0);
            cam.AddComponent<MeshFilter>().sharedMesh = tankMesh;
            var mr = cam.AddComponent<MeshRenderer>();
            mr.sharedMaterial = m.Cam;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(cam, GISiz);
            var col = cam.AddComponent<CapsuleCollider>();
            col.radius = 0.3f;
            col.height = 1.9f;
            col.center = new Vector3(0, 0.95f, 0);

            // İçerik: dikey dallar ve büyük bir numune
            Prim(PrimitiveType.Sphere, "Toprak", tank, new Vector3(0, 0.44f, 0), new Vector3(0.52f, 0.06f, 0.52f), m.Toprak, false, GISiz);
            for (int i = 0; i < 4; i++)
            {
                var d = Prim(PrimitiveType.Cube, "Dal", tank, new Vector3(Mathf.Cos(i * 1.6f) * 0.1f, 0.85f, Mathf.Sin(i * 1.6f) * 0.1f), new Vector3(0.02f, 0.85f, 0.02f), m.Dal, false, GISiz);
                d.transform.localRotation = Quaternion.Euler(Random.Range(-12f, 12f), i * 45f, Random.Range(-12f, 12f));
            }
            var numune = Prim(PrimitiveType.Sphere, "BuyukNumune", tank, new Vector3(0.04f, 1.2f, -0.06f), new Vector3(0.12f, 0.05f, 0.2f), m.BocekGovde, false, GISiz);
            numune.transform.localRotation = Quaternion.Euler(70f, 30f, 0);

            // Tavana giden borular
            foreach (float s in new[] { -1f, 1f })
                Prim(PrimitiveType.Cylinder, "Boru", tank, new Vector3(s * 0.2f, 2.65f, 0.15f), new Vector3(0.05f, 0.05f, 0.05f), m.Aluminyum, false, GISiz);

            // İç ışık: Mixed, gölgesiz (realtime maliyeti düşük tutmak için)
            var isik = new GameObject("TankIsigi").AddComponent<Light>();
            isik.transform.SetParent(tank, false);
            isik.transform.localPosition = new Vector3(0, 1.3f, 0);
            isik.type = LightType.Point;
            isik.range = 2.5f;
            isik.intensity = 1.4f;
            isik.color = Hex("#6FD6C8");
            isik.shadows = LightShadows.None;
            isik.lightmapBakeType = LightmapBakeType.Mixed;
        }

        // ------------------------------------------------------------------ PC İŞ İSTASYONU

        private static void PCIstasyonunuKur(Transform mobilya, Transform etkilesim, Materyaller m, int layer)
        {
            // Ön duvar, sol taraf. Masa 1.6 (X) x 0.65 (Z), yükseklik 0.75 m. Monitörler -Z'ye (oyuncuya) bakar.
            float duvarZ = Uzunluk * 0.5f;
            var pc = new GameObject("PC_IsIstasyonu").transform;
            pc.SetParent(mobilya, false);
            pc.localPosition = new Vector3(-0.6f, 0, duvarZ - 0.325f);

            // Masa: beyaz üst, alüminyum yan ayaklar, kablo tavası
            Kutu("MasaUst", pc, new Vector3(0, 0.735f, 0), new Vector3(1.6f, 0.03f, 0.65f), m.Beyaz);
            foreach (float s in new[] { -1f, 1f })
                Kutu("YanAyak", pc, new Vector3(s * 0.77f, 0.36f, 0), new Vector3(0.04f, 0.72f, 0.6f), m.Aluminyum);
            Kutu("KabloTavasi", pc, new Vector3(0, 0.66f, 0.22f), new Vector3(1.3f, 0.06f, 0.12f), m.Kablo, false);
            Kutu("MasaAltiLED", pc, new Vector3(0, 0.718f, -0.3f), new Vector3(1.5f, 0.004f, 0.01f), m.RafLed, false);

            // İki monitör (kenarları oyuncuya doğru açılı)
            var ekranlar = new List<Object>();
            var icerikler = new List<Object>();
            string[] yazilar =
            {
                "<b>PESTCO BIO-LAB</b>\n<size=70%>NUMUNE TAKIBI  //  ARAC 07</size>\n\n<color=#6FD6C8>AKTIF: 46</color>   KARANTINA: 2\nSICAKLIK 21.4 C   NEM %68",
                "<b>CANLI IZLEME</b>\n<size=70%>VITRIN B  //  KAT 2</size>\n\n<color=#C83A3A>! B-14  HAREKET ALGILANDI</color>\n<color=#6FD6C8>KAYIT ..........  ACIK</color>",
            };
            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? -1f : 1f;
                var mon = new GameObject($"Monitor_{i + 1}").transform;
                mon.SetParent(pc, false);
                mon.localPosition = new Vector3(s * 0.34f, 0.75f, 0.1f);
                mon.localRotation = Quaternion.Euler(0, s * 14f, 0);

                Kutu("AyakTaban", mon, new Vector3(0, 0.008f, 0.05f), new Vector3(0.22f, 0.012f, 0.16f), m.Aluminyum, false);
                Kutu("AyakKol", mon, new Vector3(0, 0.17f, 0.08f), new Vector3(0.04f, 0.32f, 0.02f), m.Aluminyum, false);
                Kutu("Kasa", mon, new Vector3(0, 0.38f, 0.06f), new Vector3(0.62f, 0.37f, 0.025f), m.PCKasa, false);
                var ekran = Kutu("Ekran", mon, new Vector3(0, 0.385f, 0.046f), new Vector3(0.6f, 0.34f, 0.004f), m.Ekran, false);
                GameObjectUtility.SetStaticEditorFlags(ekran, GISiz); // açılıp kapandığı için GI'ya katılmaz
                ekranlar.Add(ekran.GetComponent<Renderer>());

                var yazi = new GameObject("EkranYazisi").AddComponent<TextMeshPro>();
                yazi.transform.SetParent(mon, false);
                yazi.transform.localPosition = new Vector3(0, 0.385f, 0.042f);
                yazi.rectTransform.sizeDelta = new Vector2(0.54f, 0.3f);
                yazi.text = yazilar[i];
                yazi.fontSize = 0.32f;
                yazi.alignment = TextAlignmentOptions.TopLeft;
                yazi.color = Hex("#CFE6EE");
                icerikler.Add(yazi.gameObject);
            }

            // Klavye, fare, mouse pad
            Kutu("MousePad", pc, new Vector3(0.05f, 0.752f, -0.12f), new Vector3(0.8f, 0.003f, 0.3f), m.Kablo, false);
            Kutu("Klavye", pc, new Vector3(-0.05f, 0.76f, -0.12f), new Vector3(0.44f, 0.015f, 0.14f), m.PCKasa, false);
            Kutu("KlavyeIsik", pc, new Vector3(-0.05f, 0.768f, -0.12f), new Vector3(0.42f, 0.002f, 0.12f), m.RafLed, false);
            Prim(PrimitiveType.Sphere, "Fare", pc, new Vector3(0.32f, 0.762f, -0.12f), new Vector3(0.06f, 0.025f, 0.1f), m.PCKasa, false, GISiz);

            // PC kasası: masanın altında sağda, cam yan panel + iç LED + fanlar
            var kasa = new GameObject("PC_Kasa").transform;
            kasa.SetParent(pc, false);
            kasa.localPosition = new Vector3(0.55f, 0, -0.02f);
            Kutu("Govde", kasa, new Vector3(0, 0.25f, 0), new Vector3(0.22f, 0.48f, 0.46f), m.PCKasa);
            var yanCam = Kutu("CamYanPanel", kasa, new Vector3(-0.112f, 0.25f, 0), new Vector3(0.004f, 0.44f, 0.42f), m.CamLit, false);
            GameObjectUtility.SetStaticEditorFlags(yanCam, GISiz);
            Kutu("IcLED", kasa, new Vector3(-0.09f, 0.46f, 0), new Vector3(0.01f, 0.01f, 0.4f), m.FanusIc, false);
            Kutu("AnaKart", kasa, new Vector3(0.09f, 0.27f, 0), new Vector3(0.01f, 0.36f, 0.32f), m.Kablo, false);
            Kutu("EkranKarti", kasa, new Vector3(0.02f, 0.2f, 0), new Vector3(0.14f, 0.04f, 0.28f), m.KoyuMetal, false);
            for (int i = 0; i < 3; i++)
            {
                var fan = Prim(PrimitiveType.Cylinder, "FanHalka", kasa, new Vector3(0, 0.12f + i * 0.13f, -0.232f), new Vector3(0.11f, 0.004f, 0.11f), m.FanusIc, false, GISiz);
                fan.transform.localRotation = Quaternion.Euler(90f, 0, 0);
            }
            Kutu("GucDugmesi", kasa, new Vector3(0, 0.495f, -0.18f), new Vector3(0.02f, 0.004f, 0.02f), m.FanusIc, false);

            // Ofis koltuğu (masanın önünde, hafif çevrilmiş)
            KoltukKur(pc, m, new Vector3(-0.05f, 0, -0.75f));

            // Ekran ışığı (realtime) — PC kapanınca söner
            var ekranIsigi = new GameObject("EkranIsigi").AddComponent<Light>();
            ekranIsigi.transform.SetParent(pc, false);
            ekranIsigi.transform.localPosition = new Vector3(0, 1.1f, -0.3f);
            ekranIsigi.type = LightType.Point;
            ekranIsigi.range = 2.0f;
            ekranIsigi.intensity = 0.6f;
            ekranIsigi.color = Hex("#7FC6D8");
            ekranIsigi.lightmapBakeType = LightmapBakeType.Realtime;

            var ts = pc.gameObject.AddComponent<TerminalScreen>();
            var so = new SerializedObject(ts);
            DiziAta(so.FindProperty("ekranlar"), ekranlar);
            DiziAta(so.FindProperty("ekranIcerikleri"), icerikler);
            var isikProp = so.FindProperty("ekranIsigi");
            if (isikProp != null) isikProp.objectReferenceValue = ekranIsigi;
            so.ApplyModifiedPropertiesWithoutUndo();

            var etk = EtkilesimKutusu("PC_Etkilesim", etkilesim, pc.localPosition + new Vector3(0, 1.05f, 0.05f), new Vector3(1.4f, 0.55f, 0.4f), layer, "Bilgisayari ac/kapat");
            UnityEventTools.AddVoidPersistentListener(etk.onInteract, ts.Toggle);
        }

        private static void KoltukKur(Transform p, Materyaller m, Vector3 poz)
        {
            // Koltuk yerel +Z'si oturan kişinin baktığı yön; masaya (+Z) dönük, hafif açılı
            var k = new GameObject("OfisKoltugu").transform;
            k.SetParent(p, false);
            k.localPosition = poz;
            k.localRotation = Quaternion.Euler(0, -20f, 0);

            // Yıldız ayak (5 kol) + tekerlekler
            for (int i = 0; i < 5; i++)
            {
                var kol = new GameObject("AyakKolu").transform;
                kol.SetParent(k, false);
                kol.localRotation = Quaternion.Euler(0, i * 72f, 0);
                Kutu("Kol", kol, new Vector3(0, 0.07f, 0.16f), new Vector3(0.04f, 0.03f, 0.32f), m.Aluminyum, false);
                Prim(PrimitiveType.Sphere, "Teker", kol, new Vector3(0, 0.03f, 0.31f), Vector3.one * 0.05f, m.Kablo, false, GISiz);
            }
            Prim(PrimitiveType.Cylinder, "GazPistonu", k, new Vector3(0, 0.27f, 0), new Vector3(0.05f, 0.18f, 0.05f), m.Aluminyum, false, GISiz);
            Kutu("Oturak", k, new Vector3(0, 0.48f, 0), new Vector3(0.5f, 0.08f, 0.48f), m.Kumas);
            var sirt = Kutu("Sirt", k, new Vector3(0, 0.85f, -0.24f), new Vector3(0.46f, 0.6f, 0.06f), m.Kumas);
            sirt.transform.localRotation = Quaternion.Euler(-8f, 0, 0);
            Kutu("SirtCerceve", k, new Vector3(0, 0.62f, -0.25f), new Vector3(0.06f, 0.22f, 0.03f), m.Aluminyum, false);
            foreach (float s in new[] { -1f, 1f })
            {
                Kutu("KolDayamaDik", k, new Vector3(s * 0.26f, 0.58f, -0.02f), new Vector3(0.03f, 0.16f, 0.03f), m.Aluminyum, false);
                Kutu("KolDayama", k, new Vector3(s * 0.26f, 0.665f, 0f), new Vector3(0.06f, 0.025f, 0.26f), m.Kumas, false);
            }
        }

        // ------------------------------------------------------------------ SOL DUVAR

        private static void SolDuvariKur(Transform mobilya, Transform fanusKoku, Transform etkilesim, Materyaller m, int layer, AtmosphereBlender atmosfer)
        {
            // Sol duvar mobilyaları en fazla 0.5 m derin → ön yüz x = -1.6, masaya tam 1.2 m
            float duvarX = -Genislik * 0.5f;
            float x = duvarX + 0.25f;

            // --- Cam kapılı numune dolabı (arka tarafta, z -2.4..-1.0)
            var dolap = new GameObject("NumuneDolabi").transform;
            dolap.SetParent(mobilya, false);
            const float dz = -1.7f;
            Kutu("ArkaIsik", dolap, new Vector3(duvarX + 0.01f, 1.2f, dz), new Vector3(0.02f, 2.1f, 1.36f), m.ArkaIsik, false);
            Kutu("Tavan", dolap, new Vector3(x, 2.28f, dz), new Vector3(0.5f, 0.04f, 1.4f), m.Aluminyum, false);
            Kutu("Taban", dolap, new Vector3(x, 0.1f, dz), new Vector3(0.5f, 0.2f, 1.4f), m.KoyuMetal, false);
            foreach (float s in new[] { -1f, 1f })
                Kutu("Yan", dolap, new Vector3(x, 1.15f, dz + s * 0.69f), new Vector3(0.5f, 2.3f, 0.02f), m.Aluminyum, false);
            for (int i = 0; i < 2; i++)
            {
                float kz = dz + (i == 0 ? -0.345f : 0.345f);
                var kapak = Kutu("CamKapak", dolap, new Vector3(x + 0.245f, 1.2f, kz), new Vector3(0.008f, 2.0f, 0.67f), m.CamLit, false);
                GameObjectUtility.SetStaticEditorFlags(kapak, GISiz);
                kapak.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                Kutu("KapakKulp", dolap, new Vector3(x + 0.26f, 1.2f, dz + (i == 0 ? -0.04f : 0.04f)), new Vector3(0.015f, 0.5f, 0.015f), m.Aluminyum, false);
            }
            var dolapCol = dolap.gameObject.AddComponent<BoxCollider>();
            dolapCol.center = new Vector3(x, 1.15f, dz);
            dolapCol.size = new Vector3(0.5f, 2.3f, 1.4f);

            float[] dolapKatlari = { 0.55f, 1.0f, 1.45f, 1.9f };
            for (int k = 0; k < dolapKatlari.Length; k++)
            {
                float ky = dolapKatlari[k];
                var raf = Kutu("CamRaf", dolap, new Vector3(x - 0.01f, ky - 0.006f, dz), new Vector3(0.44f, 0.012f, 1.34f), m.CamLit, false);
                GameObjectUtility.SetStaticEditorFlags(raf, GISiz);
                Kutu("RafLED", dolap, new Vector3(x + 0.2f, ky - 0.03f, dz), new Vector3(0.01f, 0.006f, 1.3f), m.RafLed, false);
                var tip = k % 2 == 0 ? kucuk : orta;
                int adet = k % 2 == 0 ? 4 : 3;
                for (int f = 0; f < adet; f++)
                {
                    float fz = dz - 0.55f + 1.1f * (f + 0.5f) / adet;
                    // 180° döndür: etiket +X'e (koridora) baksın
                    FanusKur(fanusKoku, new Vector3(x - 0.02f, ky, fz), m, tip, Quaternion.Euler(0, 180f, 0));
                }
            }

            // --- Duvar ekranı (orta bölüm) + altında ince konsol
            Kutu("DuvarEkrani_Kasa", mobilya, new Vector3(duvarX + 0.02f, 1.55f, -0.1f), new Vector3(0.03f, 0.7f, 1.2f), m.PCKasa, false);
            var de = Kutu("DuvarEkrani", mobilya, new Vector3(duvarX + 0.036f, 1.55f, -0.1f), new Vector3(0.004f, 0.64f, 1.14f), m.Ekran, false);
            GameObjectUtility.SetStaticEditorFlags(de, GISiz);
            var deYazi = new GameObject("DuvarEkrani_Yazi").AddComponent<TextMeshPro>();
            deYazi.transform.SetParent(mobilya, false);
            deYazi.transform.localPosition = new Vector3(duvarX + 0.04f, 1.55f, -0.1f);
            deYazi.transform.localRotation = Quaternion.Euler(0, -90f, 0); // +X'e (koridora) bakar
            deYazi.rectTransform.sizeDelta = new Vector2(1.05f, 0.56f);
            deYazi.text = "<b>ORNEK ANALIZI</b>\n<size=60%>TUR: BLATTODEA  //  NESIL 4\nHUCRE BOLUNMESI ....... ANORMAL\nDAVRANIS .............. ORGANIZE</size>\n\n<color=#C83A3A><size=80%>UYARI: KONTEYNER BASINCI DUSUYOR</size></color>";
            deYazi.fontSize = 0.6f;
            deYazi.alignment = TextAlignmentOptions.TopLeft;
            deYazi.color = Hex("#CFE6EE");
            Kutu("DuvarKonsolu", mobilya, new Vector3(duvarX + 0.12f, 0.95f, -0.1f), new Vector3(0.24f, 0.04f, 1.2f), m.Beyaz);
            Kutu("DuvarKonsoluLED", mobilya, new Vector3(duvarX + 0.12f, 0.928f, -0.1f), new Vector3(0.22f, 0.004f, 1.16f), m.RafLed, false);

            // --- Kontrol paneli (ön taraf, z 0.9..2.3)
            var panel = new GameObject("KontrolPaneli").transform;
            panel.SetParent(mobilya, false);
            const float pz = 1.6f;
            Kutu("Govde", panel, new Vector3(x, 0.45f, pz), new Vector3(0.5f, 0.9f, 1.4f), m.Beyaz);
            Kutu("SupurgelikLED", panel, new Vector3(x + 0.245f, 0.012f, pz), new Vector3(0.01f, 0.008f, 1.36f), m.RafLed, false);
            var egik = Kutu("EgikPanel", panel, new Vector3(x + 0.02f, 0.93f, pz), new Vector3(0.46f, 0.04f, 1.4f), m.PCKasa);
            egik.transform.localRotation = Quaternion.Euler(0, 0, 14f); // koridora doğru eğik
            for (int i = 0; i < 10; i++)
            {
                var b = Kutu("Buton", egik.transform, new Vector3(i % 2 == 0 ? -0.15f : 0.15f, 0.6f, -0.4f + (i / 2) * 0.2f),
                    new Vector3(0.1f, 0.4f, 0.04f), i % 3 == 0 ? m.FanusIc : m.Buton, false);
                GameObjectUtility.SetStaticEditorFlags(b, GISiz);
            }
            var panelEkran = Kutu("PanelEkrani", egik.transform, new Vector3(0, 0.6f, 0.3f), new Vector3(0.7f, 0.3f, 0.3f), m.Ekran, false);
            GameObjectUtility.SetStaticEditorFlags(panelEkran, GISiz);
            foreach (float z in new[] { 1.25f, 1.95f })
                Kutu("DuvarMonitor", panel, new Vector3(duvarX + 0.02f, 1.6f, z), new Vector3(0.03f, 0.4f, 0.62f), m.Ekran, false);

            var panelEtk = EtkilesimKutusu("KontrolPaneli_Etkilesim", etkilesim, new Vector3(x, 1.05f, pz), new Vector3(0.5f, 0.3f, 1.4f), layer, "Kontrol panelini kullan");
            UnityEventTools.AddVoidPersistentListener(panelEtk.onInteract, atmosfer.PulseScare);
        }

        private static Interactable EtkilesimKutusu(string ad, Transform p, Vector3 poz, Vector3 boyut, int layer, string prompt)
        {
            var go = new GameObject(ad) { layer = layer };
            go.transform.SetParent(p, false);
            go.transform.localPosition = poz;
            go.AddComponent<BoxCollider>().size = boyut;
            var etk = go.AddComponent<Interactable>();
            etk.SetPrompt(prompt);
            return etk;
        }

        // ------------------------------------------------------------------ IŞIK

        private static void AydinlatmayiKur(Transform p, Materyaller m)
        {
            // Tavan LED şeritleri: emissive gövde + ince Area (Rectangle) light. URP'de Area light yalnızca Baked çalışır.
            foreach (float x in new[] { -1.2f, 1.2f })
            {
                Kutu("TavanLED", p, new Vector3(x, Yukseklik - 0.055f, 0), new Vector3(0.06f, 0.02f, 5.6f), m.Led, false);

                var alan = new GameObject("TavanLED_AreaLight").AddComponent<Light>();
                alan.transform.SetParent(p, false);
                alan.transform.localPosition = new Vector3(x, Yukseklik - 0.07f, 0);
                alan.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                alan.type = LightType.Rectangle;
                alan.areaSize = new Vector2(0.06f, 5.6f);
                alan.color = Hex("#D9E8F0");
                alan.intensity = 2.0f;
                alan.range = 6f;
                alan.lightmapBakeType = LightmapBakeType.Baked;
            }

            // Masanın üstünde asılı lineer lamba (titreyen 1. lamba)
            var aski = new GameObject("AskiliLamba").transform;
            aski.SetParent(p, false);
            foreach (float z in new[] { -0.8f, 0.8f })
                Kutu("AskiTeli", aski, new Vector3(0, 2.5f, MasaZ + z), new Vector3(0.006f, 0.4f, 0.006f), m.Kablo, false);
            Kutu("Govde", aski, new Vector3(0, 2.29f, MasaZ), new Vector3(0.08f, 0.04f, 1.8f), m.Aluminyum, false);
            var askiLed = Kutu("Difuzor", aski, new Vector3(0, 2.268f, MasaZ), new Vector3(0.06f, 0.006f, 1.76f), m.Led, false);
            GameObjectUtility.SetStaticEditorFlags(askiLed, GISiz);
            TitreyenIsik(p, "TitreyenLamba_Masa", new Vector3(0, 2.2f, MasaZ), askiLed.GetComponent<Renderer>(), 1.3f, 0.12f, 4.5f);

            // Arka kapı üstü lamba (titreyen 2. lamba)
            var kapiGovde = Kutu("KapiLambasi_Govde", p, new Vector3(0, Yukseklik - 0.04f, -2.7f), new Vector3(0.3f, 0.03f, 0.3f), m.Led, false);
            GameObjectUtility.SetStaticEditorFlags(kapiGovde, GISiz);
            TitreyenIsik(p, "TitreyenLamba_Kapi", new Vector3(0, Yukseklik - 0.12f, -2.7f), kapiGovde.GetComponent<Renderer>(), 0.9f, 0.3f, 3.5f);
        }

        private static void TitreyenIsik(Transform p, string ad, Vector3 poz, Renderer govde, float maxYogunluk, float dropout, float menzil)
        {
            // Mixed, realtime gölgeli (oyuncu + etkileşilebilir nesneler gölge alır)
            var isik = new GameObject(ad).AddComponent<Light>();
            isik.transform.SetParent(p, false);
            isik.transform.localPosition = poz;
            isik.type = LightType.Point;
            isik.range = menzil;
            isik.intensity = maxYogunluk;
            isik.color = Hex("#D9E8F0");
            isik.shadows = LightShadows.Soft;
            isik.lightmapBakeType = LightmapBakeType.Mixed;

            var titreme = isik.gameObject.AddComponent<LightFlicker>();
            var so = new SerializedObject(titreme);
            so.FindProperty("hedefIsik").objectReferenceValue = isik;
            so.FindProperty("emissiveRenderer").objectReferenceValue = govde;
            so.FindProperty("minIntensity").floatValue = maxYogunluk * 0.55f;
            so.FindProperty("maxIntensity").floatValue = maxYogunluk;
            so.FindProperty("dropoutChance").floatValue = dropout;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void ReflectionProbeKur(Transform p)
        {
            var probe = new GameObject("ReflectionProbe_Ic").AddComponent<ReflectionProbe>();
            probe.transform.SetParent(p, false);
            probe.transform.localPosition = new Vector3(0, 1.4f, 0);
            probe.mode = ReflectionProbeMode.Baked;
            probe.boxProjection = true;
            probe.size = new Vector3(Genislik, Yukseklik, Uzunluk);
            probe.center = new Vector3(0, Yukseklik * 0.5f - 1.4f, 0);
            probe.resolution = 256;
            probe.importance = 1;
        }

        // ------------------------------------------------------------------ POST-PROCESS

        private static VolumeProfile TabanProfiliOlustur()
        {
            var profil = ProfilHazirla("VP_TruckLabModern_Taban");
            var ca = Ekle<ColorAdjustments>(profil);
            ca.postExposure.Override(-0.2f);
            ca.contrast.Override(10f);
            ca.saturation.Override(-15f);
            ca.colorFilter.Override(Hex("#B8D4E8"));
            Ekle<WhiteBalance>(profil).temperature.Override(-10f);
            var vig = Ekle<Vignette>(profil);
            vig.intensity.Override(0.3f);
            vig.smoothness.Override(0.4f);
            var bloom = Ekle<Bloom>(profil);
            bloom.threshold.Override(1f);
            bloom.intensity.Override(0.3f);
            var grain = Ekle<FilmGrain>(profil);
            grain.type.Override(FilmGrainLookup.Thin1);
            grain.intensity.Override(0.15f);
            Ekle<ChromaticAberration>(profil).intensity.Override(0.1f);
            AssetDatabase.SaveAssets();
            return profil;
        }

        private static VolumeProfile KorkuProfiliOlustur()
        {
            // Korku anı: taban profilin üstüne karışan sert değerler
            var profil = ProfilHazirla("VP_TruckLabModern_Korku");
            var ca = Ekle<ColorAdjustments>(profil);
            ca.postExposure.Override(-0.6f);
            ca.contrast.Override(25f);
            ca.saturation.Override(-45f);
            ca.colorFilter.Override(Hex("#C8D8D0"));
            var vig = Ekle<Vignette>(profil);
            vig.intensity.Override(0.5f);
            vig.smoothness.Override(0.5f);
            Ekle<FilmGrain>(profil).intensity.Override(0.45f);
            Ekle<ChromaticAberration>(profil).intensity.Override(0.6f);
            Ekle<LensDistortion>(profil).intensity.Override(-0.2f);
            AssetDatabase.SaveAssets();
            return profil;
        }

        private static VolumeProfile ProfilHazirla(string ad)
        {
            string yol = $"{Kok}/Profiles/{ad}.asset";
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(yol) != null) AssetDatabase.DeleteAsset(yol);
            var profil = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profil, yol);
            return profil;
        }

        private static T Ekle<T>(VolumeProfile profil) where T : VolumeComponent
        {
            var c = profil.Add<T>(false); // yalnızca .Override() edilen parametreler aktif olur
            c.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(c, profil);
            EditorUtility.SetDirty(profil);
            return c;
        }

        private static AtmosphereBlender PostProcessKur(VolumeProfile taban, VolumeProfile korku)
        {
            var global = new GameObject("Global Volume").AddComponent<Volume>();
            global.isGlobal = true;
            global.priority = 0;
            global.weight = 1f;
            global.sharedProfile = taban;

            var korkuVol = new GameObject("Korku Volume").AddComponent<Volume>();
            korkuVol.isGlobal = true;
            korkuVol.priority = 10;
            korkuVol.weight = 0f;
            korkuVol.sharedProfile = korku;

            var blender = korkuVol.gameObject.AddComponent<AtmosphereBlender>();
            var so = new SerializedObject(blender);
            so.FindProperty("korkuVolume").objectReferenceValue = korkuVol;
            so.ApplyModifiedPropertiesWithoutUndo();
            return blender;
        }

        // ------------------------------------------------------------------ OYUNCU

        private static void OyuncuyuKur(int interactableLayer)
        {
            var oyuncu = new GameObject("Player");
            oyuncu.transform.position = new Vector3(0f, 0.05f, -2.5f);
            var cc = oyuncu.AddComponent<CharacterController>();
            cc.radius = 0.35f;
            cc.height = 1.8f;
            cc.center = new Vector3(0, 0.9f, 0);

            var kameraGo = new GameObject("Main Camera") { tag = "MainCamera" };
            kameraGo.transform.SetParent(oyuncu.transform, false);
            kameraGo.transform.localPosition = new Vector3(0, 1.62f, 0);
            var kamera = kameraGo.AddComponent<Camera>();
            kamera.nearClipPlane = 0.05f;
            kamera.fieldOfView = 70f;
            kamera.clearFlags = CameraClearFlags.SolidColor;
            kamera.backgroundColor = Color.black;
            kamera.useOcclusionCulling = true;
            kameraGo.AddComponent<AudioListener>();
            kamera.GetUniversalAdditionalCameraData().renderPostProcessing = true;

            var walker = oyuncu.AddComponent<TruckTestWalker>();
            var wso = new SerializedObject(walker);
            wso.FindProperty("kameraPivot").objectReferenceValue = kameraGo.transform;
            wso.ApplyModifiedPropertiesWithoutUndo();

            var etk = kameraGo.AddComponent<PlayerInteractor>();
            var eso = new SerializedObject(etk);
            eso.FindProperty("kamera").objectReferenceValue = kamera;
            eso.FindProperty("interactableMask").intValue = 1 << interactableLayer;
            eso.FindProperty("engelMask").intValue = 1; // Default
            eso.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ YARDIMCILAR

        private static GameObject Kutu(string ad, Transform p, Vector3 poz, Vector3 boyut, Material mat, bool collider = true)
            => Prim(PrimitiveType.Cube, ad, p, poz, boyut, mat, collider, TamStatic);

        private static GameObject Prim(PrimitiveType tip, string ad, Transform p, Vector3 poz, Vector3 olcek, Material mat, bool collider, StaticEditorFlags bayraklar)
        {
            var go = GameObject.CreatePrimitive(tip);
            go.name = ad;
            go.transform.SetParent(p, false);
            go.transform.localPosition = poz;
            go.transform.localScale = olcek;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) Object.DestroyImmediate(go.GetComponent<Collider>());
            GameObjectUtility.SetStaticEditorFlags(go, bayraklar);
            return go;
        }

        private static void DiziAta(SerializedProperty dizi, List<Object> degerler)
        {
            if (dizi == null || !dizi.isArray) return; // TerminalScreen alan adı değişmişse sessizce geç
            dizi.arraySize = degerler.Count;
            for (int i = 0; i < degerler.Count; i++)
                dizi.GetArrayElementAtIndex(i).objectReferenceValue = degerler[i];
        }

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }
}
