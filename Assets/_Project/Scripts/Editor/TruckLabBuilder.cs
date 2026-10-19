using System.Collections.Generic;
using PestCo.Truck;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace PestCo.Truck.EditorTools
{
    /// <summary>
    /// "Mobil böcek laboratuvarı" kamyon içi sahnesini (URP) sıfırdan kurar.
    /// Menü: PestCo > Truck Lab
    /// Koordinatlar: oyuncu +Z'ye bakar; sağ duvar +X (fanus rafları), sol duvar -X (panel + terminal).
    /// </summary>
    public static class TruckLabBuilder
    {
        private const string Kok = "Assets/_Project/TruckLab";
        private const string SahneYolu = "Assets/Scenes/TruckLab.unity";

        // İç ölçüler (metre)
        private const float Genislik = 4.2f;  // X
        private const float Uzunluk = 6.4f;   // Z
        private const float Yukseklik = 2.7f; // Y
        private const float Kalinlik = 0.1f;

        private const StaticEditorFlags TamStatic =
            StaticEditorFlags.ContributeGI | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic |
            StaticEditorFlags.BatchingStatic | StaticEditorFlags.ReflectionProbeStatic;

        private const StaticEditorFlags GISiz =
            StaticEditorFlags.OccludeeStatic | StaticEditorFlags.BatchingStatic;

        private class Materyaller
        {
            public Material Duvar, Tavan, Zemin, Kablo, KoyuMetal, Led, FanusIc, RafLed, Ekran, Buton, Cam;
        }

        // ------------------------------------------------------------------ MENÜ

        [MenuItem("PestCo/Truck Lab/1 - Sahneyi Kur")]
        public static void SahneyiKur()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            KlasorleriHazirla();
            AssetDatabase.Refresh();

            var m = MateryalleriOlustur();
            Mesh fanusMesh = FanusMeshiOlustur();
            VolumeProfile tabanProfil = TabanProfiliOlustur();
            VolumeProfile korkuProfil = KorkuProfiliOlustur();

            var sahne = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            OrtamAyarlari();

            int interactableLayer = LayerMask.NameToLayer("Interactable");
            if (interactableLayer < 0)
            {
                Debug.LogWarning("[TruckLab] 'Interactable' layer bulunamadı, Default kullanılıyor.");
                interactableLayer = 0;
            }

            var ortam = new GameObject("Ortam").transform;
            var mobilya = new GameObject("Mobilya").transform;
            var fanuslar = new GameObject("Fanuslar").transform;
            var isiklar = new GameObject("Aydinlatma").transform;
            var etkilesim = new GameObject("Etkilesim").transform;

            KabuguKur(ortam, m);
            MasayiKur(mobilya, m);
            RaflariKur(mobilya, fanuslar, m, fanusMesh);
            var atmosfer = PostProcessKur(tabanProfil, korkuProfil);
            SolPaneliKur(mobilya, etkilesim, m, interactableLayer, atmosfer);
            AydinlatmayiKur(isiklar, m);
            ReflectionProbeKur(isiklar);
            OyuncuyuKur(interactableLayer);
            GolgeMesafesiniAyarla(25f);
            LightingAyarlariniOlustur();

            EditorSceneManager.SaveScene(sahne, SahneYolu);
            AssetDatabase.SaveAssets();
            Debug.Log("[TruckLab] Sahne kuruldu: " + SahneYolu + "  → Sonra 'PestCo/Truck Lab/2 - Bake' çalıştırın.");
        }

        [MenuItem("PestCo/Truck Lab/2 - Occlusion + Lightmap Bake")]
        public static void Bake()
        {
            StaticOcclusionCulling.smallestOccluder = 0.5f;
            StaticOcclusionCulling.smallestHole = 0.1f;
            StaticOcclusionCulling.Compute();
            Lightmapping.BakeAsync(); // Reflection Probe'lar da bu bake ile üretilir
            Debug.Log("[TruckLab] Occlusion culling hesaplandı, lightmap bake başladı.");
        }

        // ------------------------------------------------------------------ KLASÖR / AYAR

        private static void KlasorleriHazirla()
        {
            foreach (var alt in new[] { "Materials", "Textures", "Meshes", "Profiles", "Shaders" })
            {
                if (!AssetDatabase.IsValidFolder($"{Kok}/{alt}"))
                {
                    if (!AssetDatabase.IsValidFolder(Kok)) AssetDatabase.CreateFolder("Assets/_Project", "TruckLab");
                    AssetDatabase.CreateFolder(Kok, alt);
                }
            }
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

        /// <summary>
        /// Global URP asset'ine dokunmaz (ana sahne 12 m kullanıyor); sahneye geçici override bileşeni ekler.
        /// </summary>
        private static void GolgeMesafesiniAyarla(float mesafe)
        {
            var go = new GameObject("GolgeMesafesi");
            var bilesen = go.AddComponent<ShadowDistanceOverride>();
            var so = new SerializedObject(bilesen);
            so.FindProperty("golgeMesafesi").floatValue = mesafe;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp && !urp.supportsAdditionalLightShadows)
                Debug.LogWarning("[TruckLab] URP asset'te ek ışık gölgeleri kapalı; titreyen lambalar gölge vermez.");
        }

        private static void LightingAyarlariniOlustur()
        {
            string yol = $"{Kok}/TruckLab.lighting";
            var ls = AssetDatabase.LoadAssetAtPath<LightingSettings>(yol);
            if (ls == null)
            {
                ls = new LightingSettings { name = "TruckLab" };
                AssetDatabase.CreateAsset(ls, yol);
            }
            ls.bakedGI = true;
            ls.realtimeGI = false;
            ls.mixedBakeMode = MixedLightingMode.IndirectOnly; // Mixed ışıklar: dolaylı baked, doğrudan + gölge realtime
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
                Duvar = Lit("M_FircalanmisCelik", "#8A9196", 1f, 0.6f, normal),
                Tavan = Lit("M_TavanPaneli", "#1E2326", 0f, 0.25f),
                Zemin = Lit("M_EpoksiZemin", "#3A4248", 0f, 0.85f),
                Kablo = Lit("M_KabloKanali", "#0B0D0E", 0.2f, 0.4f),
                KoyuMetal = Lit("M_KoyuMetal", "#202528", 1f, 0.45f),
                Led = Emissive("M_TavanLED", "#D9E8F0", 2.0f),
                FanusIc = Emissive("M_FanusIc", "#6FD6C8", 1.5f),
                RafLed = Emissive("M_RafAltiLED", "#6FD6C8", 0.8f),
                Ekran = Emissive("M_TerminalEkran", "#4FB3C8", 1.2f),
                Buton = Emissive("M_PanelButon", "#C83A3A", 1.0f),
            };
            CamLitOlustur(); // Tablodaki URP Lit karşılığı (yedek)
            m.Cam = CamFresnelOlustur();
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
                mat.SetFloat("_BumpScale", 0.35f);
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

        private static void CamLitOlustur()
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
        }

        private static Material CamFresnelOlustur()
        {
            var shader = Shader.Find("PestCo/FanusGlass");
            if (shader == null)
            {
                Debug.LogWarning("[TruckLab] PestCo/FanusGlass shader bulunamadı, Lit cam kullanılıyor.");
                return AssetDatabase.LoadAssetAtPath<Material>($"{Kok}/Materials/M_FanusCam_Lit.mat");
            }
            var mat = MateryalYukle("M_FanusCam_Fresnel", "PestCo/FanusGlass");
            var c = Hex("#BFE8E2"); c.a = 0.12f;
            mat.SetColor("_BaseColor", c);
            mat.SetColor("_EdgeColor", Hex("#9FEDE0"));
            mat.SetFloat("_Smoothness", 0.95f);
            mat.SetFloat("_FresnelPower", 4.5f);
            mat.SetFloat("_FresnelIntensity", 0.7f);
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
        /// oluşan bir normal map üretilir (fırçalanmış çelik görünümü).
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

        /// <summary>24 segmentli silindir gövde + kubbeli kapak (tek parça, döndürülmüş profil).</summary>
        private static Mesh FanusMeshiOlustur()
        {
            string yol = $"{Kok}/Meshes/FanusCam.asset";
            var mevcut = AssetDatabase.LoadAssetAtPath<Mesh>(yol);
            if (mevcut != null) return mevcut;

            const int segment = 24;
            const float r = 0.12f, govde = 0.20f, kubbe = 0.08f;
            var profil = new List<Vector2> { new Vector2(r, 0f), new Vector2(r, govde * 0.5f), new Vector2(r, govde) };
            const int kubbeAdim = 8;
            for (int i = 1; i <= kubbeAdim; i++)
            {
                float a = i / (float)kubbeAdim * Mathf.PI * 0.5f;
                profil.Add(new Vector2(r * Mathf.Cos(a), govde + kubbe * Mathf.Sin(a)));
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

            var mesh = new Mesh { name = "FanusCam" };
            mesh.SetVertices(v);
            mesh.SetNormals(nrm);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(tri, 0);
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, yol);
            return mesh;
        }

        // ------------------------------------------------------------------ YAPI

        private static void KabuguKur(Transform p, Materyaller m)
        {
            float hx = Genislik * 0.5f, hz = Uzunluk * 0.5f, k = Kalinlik;
            Kutu("Zemin", p, new Vector3(0, -k / 2, 0), new Vector3(Genislik + 2 * k, k, Uzunluk + 2 * k), m.Zemin);
            Kutu("Tavan", p, new Vector3(0, Yukseklik + k / 2, 0), new Vector3(Genislik + 2 * k, k, Uzunluk + 2 * k), m.Tavan);
            Kutu("Duvar_Sag", p, new Vector3(hx + k / 2, Yukseklik / 2, 0), new Vector3(k, Yukseklik, Uzunluk), m.Duvar);
            Kutu("Duvar_Sol", p, new Vector3(-hx - k / 2, Yukseklik / 2, 0), new Vector3(k, Yukseklik, Uzunluk), m.Duvar);
            Kutu("Duvar_On", p, new Vector3(0, Yukseklik / 2, hz + k / 2), new Vector3(Genislik, Yukseklik, k), m.Duvar);
            Kutu("Duvar_Arka", p, new Vector3(0, Yukseklik / 2, -hz - k / 2), new Vector3(Genislik, Yukseklik, k), m.Duvar);

            // Tavan panel bölmeleri (koyu, hafif derinlik)
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
            Kutu("KabloKanali_OnDikey", p, new Vector3(-0.9f, Yukseklik / 2, hz - 0.05f), new Vector3(0.1f, Yukseklik, 0.1f), m.Kablo, false);
        }

        private static void MasayiKur(Transform p, Materyaller m)
        {
            // 2.0 (Z) x 0.8 (X), üst yüzey 0.9 m. Etrafında: sol 1.2 m, sağ 1.3 m, ön/arka 2.2 m boşluk.
            var masa = new GameObject("CalismaMasasi").transform;
            masa.SetParent(p, false);
            Kutu("Ust", masa, new Vector3(0, 0.875f, 0), new Vector3(0.8f, 0.05f, 2.0f), m.Zemin);
            foreach (float x in new[] { -0.34f, 0.34f })
                foreach (float z in new[] { -0.94f, 0.94f })
                    Kutu("Ayak", masa, new Vector3(x, 0.425f, z), new Vector3(0.06f, 0.85f, 0.06f), m.KoyuMetal);
            Kutu("AltRaf", masa, new Vector3(0, 0.25f, 0), new Vector3(0.72f, 0.03f, 1.9f), m.KoyuMetal, false);
            Kutu("MasaLED", masa, new Vector3(0, 0.845f, 0), new Vector3(0.7f, 0.01f, 1.9f), m.RafLed, false);
        }

        private static void RaflariKur(Transform mobilya, Transform fanusKoku, Materyaller m, Mesh fanusMesh)
        {
            const float derinlik = 0.4f;
            float x = Genislik * 0.5f - derinlik * 0.5f;         // raf merkezi
            float onKenar = Genislik * 0.5f - derinlik;          // 1.7 m → masaya 1.3 m
            float[] katlar = { 1.2f, 1.65f, 2.10f };             // zemin üstü 1.2 m, katlar arası 0.45 m
            float[] gruplar = { -1.7f, 0f, 1.7f };               // 3 grup, aralarında 0.5 m boşluk
            const float grupGenislik = 1.2f;

            int sayac = 1;
            for (int g = 0; g < gruplar.Length; g++)
            {
                var grup = new GameObject($"RafGrubu_{g + 1}").transform;
                grup.SetParent(mobilya, false);
                float gz = gruplar[g];

                Kutu("ArkaPanel", grup, new Vector3(Genislik * 0.5f - 0.01f, 1.8f, gz), new Vector3(0.02f, 1.3f, grupGenislik), m.KoyuMetal);
                foreach (float s in new[] { -1f, 1f })
                    Kutu("Dikme", grup, new Vector3(x, 1.8f, gz + s * (grupGenislik * 0.5f - 0.015f)), new Vector3(derinlik, 1.3f, 0.03f), m.KoyuMetal);
                Kutu("AltDolap", grup, new Vector3(x, 0.55f, gz), new Vector3(derinlik, 1.1f, grupGenislik), m.Duvar);

                for (int k = 0; k < katlar.Length; k++)
                {
                    float ky = katlar[k];
                    Kutu($"Kat_{k + 1}", grup, new Vector3(x, ky - 0.03f, gz), new Vector3(derinlik, 0.06f, grupGenislik - 0.04f), m.KoyuMetal);
                    // Raf altı LED şerit (ön kenar)
                    Kutu("RafAltiLED", grup, new Vector3(onKenar + 0.03f, ky - 0.065f, gz), new Vector3(0.03f, 0.01f, grupGenislik - 0.1f), m.RafLed, false);

                    for (int f = -1; f <= 1; f++)
                    {
                        var poz = new Vector3(x, ky, gz + f * 0.38f);
                        FanusKur(fanusKoku, poz, m, fanusMesh, $"B-{sayac:00}");
                        sayac++;
                    }
                }
            }
        }

        private static void FanusKur(Transform p, Vector3 poz, Materyaller m, Mesh mesh, string etiket)
        {
            var kok = new GameObject("Fanus_" + etiket).transform;
            kok.SetParent(p, false);
            kok.localPosition = poz;

            // Koyu metal taban (Unity silindiri 24 segment)
            var taban = Prim(PrimitiveType.Cylinder, "Taban", kok, new Vector3(0, 0.03f, 0), new Vector3(0.27f, 0.03f, 0.27f), m.KoyuMetal, false, TamStatic);

            // İç emissive yüzey (numune / yem tabağı)
            Prim(PrimitiveType.Cylinder, "IcIsikDiski", kok, new Vector3(0, 0.065f, 0), new Vector3(0.18f, 0.005f, 0.18f), m.FanusIc, false, TamStatic);
            Prim(PrimitiveType.Sphere, "Numune", kok, new Vector3(0, 0.1f, 0), new Vector3(0.05f, 0.03f, 0.07f), m.KoyuMetal, false, GISiz);

            // Cam gövde + kubbe
            var cam = new GameObject("Cam");
            cam.transform.SetParent(kok, false);
            cam.transform.localPosition = new Vector3(0, 0.06f, 0);
            cam.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = cam.AddComponent<MeshRenderer>();
            mr.sharedMaterial = m.Cam;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            GameObjectUtility.SetStaticEditorFlags(cam, GISiz);

            // İç ışık: Mixed + gölgesiz. Baked point ışığın fiziksel azalması 0.8 m menzilde parıltıyı yok ediyordu;
            // doğrudan ışık realtime (ucuz, gölge yok), dolaylı ışık baked.
            var isik = new GameObject("IcIsik").AddComponent<Light>();
            isik.transform.SetParent(kok, false);
            isik.transform.localPosition = new Vector3(0, 0.16f, 0);
            isik.type = LightType.Point;
            isik.range = 0.8f;
            isik.intensity = 0.6f;
            isik.color = Hex("#6FD6C8");
            isik.shadows = LightShadows.None;
            isik.lightmapBakeType = LightmapBakeType.Mixed;

            // Taban etiketi (TextMeshPro, oyuncuya -X yönünde bakar)
            var yazi = new GameObject("Etiket").AddComponent<TextMeshPro>();
            yazi.transform.SetParent(taban.transform.parent, false);
            yazi.transform.localPosition = new Vector3(-0.137f, 0.03f, 0);
            yazi.transform.localRotation = Quaternion.Euler(0, 90f, 0);
            yazi.rectTransform.sizeDelta = new Vector2(0.2f, 0.05f);
            yazi.text = etiket;
            yazi.fontSize = 0.35f;
            yazi.alignment = TextAlignmentOptions.Center;
            yazi.color = Hex("#9FEDE0");
        }

        private static void SolPaneliKur(Transform mobilya, Transform etkilesim, Materyaller m, int layer, AtmosphereBlender atmosfer)
        {
            // Sol duvar: derinlik 0.5 m → ön yüz x = -1.6, masaya tam 1.2 m
            float x = -Genislik * 0.5f + 0.25f;

            // --- Kontrol paneli
            var panel = new GameObject("KontrolPaneli").transform;
            panel.SetParent(mobilya, false);
            Kutu("Govde", panel, new Vector3(x, 0.475f, 1.6f), new Vector3(0.5f, 0.95f, 1.6f), m.Duvar);
            Kutu("UstPlaka", panel, new Vector3(x, 0.96f, 1.6f), new Vector3(0.5f, 0.02f, 1.6f), m.KoyuMetal);
            for (int i = 0; i < 8; i++)
            {
                var b = Kutu("Buton", panel, new Vector3(x + (i % 2 == 0 ? -0.08f : 0.08f), 0.98f, 1.0f + i * 0.16f),
                    new Vector3(0.05f, 0.02f, 0.05f), i % 3 == 0 ? m.FanusIc : m.Buton, false);
                GameObjectUtility.SetStaticEditorFlags(b, GISiz);
            }
            Kutu("DuvarMonitor_1", panel, new Vector3(-Genislik * 0.5f + 0.02f, 1.55f, 1.2f), new Vector3(0.03f, 0.45f, 0.7f), m.Ekran, false);
            Kutu("DuvarMonitor_2", panel, new Vector3(-Genislik * 0.5f + 0.02f, 1.55f, 2.0f), new Vector3(0.03f, 0.45f, 0.7f), m.Ekran, false);

            var panelEtk = EtkilesimKutusu("KontrolPaneli_Etkilesim", etkilesim, new Vector3(x, 1.05f, 1.6f), new Vector3(0.5f, 0.25f, 1.6f), layer, "Kontrol panelini kullan");
            UnityEventTools.AddVoidPersistentListener(panelEtk.onInteract, atmosfer.PulseScare);

            // --- Terminal
            var term = new GameObject("Terminal").transform;
            term.SetParent(mobilya, false);
            Kutu("Masa", term, new Vector3(x, 0.45f, -0.6f), new Vector3(0.5f, 0.9f, 1.0f), m.Duvar);
            Kutu("MonitorAyak", term, new Vector3(x - 0.1f, 1.0f, -0.6f), new Vector3(0.05f, 0.2f, 0.05f), m.KoyuMetal, false);
            var kasa = Kutu("MonitorKasa", term, new Vector3(x - 0.1f, 1.32f, -0.6f), new Vector3(0.05f, 0.45f, 0.7f), m.KoyuMetal, false);
            kasa.transform.localRotation = Quaternion.Euler(0, 0, -8f);
            var ekran = Kutu("Ekran", term, new Vector3(x - 0.07f, 1.32f, -0.6f), new Vector3(0.01f, 0.4f, 0.64f), m.Ekran, false);
            ekran.transform.localRotation = Quaternion.Euler(0, 0, -8f);
            GameObjectUtility.SetStaticEditorFlags(ekran, GISiz); // açılıp kapandığı için GI'ya katılmaz
            Kutu("Klavye", term, new Vector3(x + 0.1f, 0.915f, -0.6f), new Vector3(0.18f, 0.02f, 0.45f), m.Kablo, false);

            var ekranIsigi = new GameObject("EkranIsigi").AddComponent<Light>();
            ekranIsigi.transform.SetParent(term, false);
            ekranIsigi.transform.localPosition = new Vector3(x + 0.3f, 1.3f, -0.6f);
            ekranIsigi.type = LightType.Point;
            ekranIsigi.range = 1.6f;
            ekranIsigi.intensity = 0.5f;
            ekranIsigi.color = Hex("#4FB3C8");
            ekranIsigi.lightmapBakeType = LightmapBakeType.Realtime;

            var ts = term.gameObject.AddComponent<TerminalScreen>();
            var so = new SerializedObject(ts);
            var ekranlarProp = so.FindProperty("ekranlar");
            ekranlarProp.arraySize = 1;
            ekranlarProp.GetArrayElementAtIndex(0).objectReferenceValue = ekran.GetComponent<Renderer>();
            so.FindProperty("ekranIsigi").objectReferenceValue = ekranIsigi;
            so.ApplyModifiedPropertiesWithoutUndo();

            var termEtk = EtkilesimKutusu("Terminal_Etkilesim", etkilesim, new Vector3(x - 0.05f, 1.15f, -0.6f), new Vector3(0.45f, 0.65f, 1.0f), layer, "Terminali aç/kapat");
            UnityEventTools.AddVoidPersistentListener(termEtk.onInteract, ts.Toggle);
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
            foreach (float x in new[] { -0.9f, 0.9f })
            {
                Kutu("TavanLED", p, new Vector3(x, Yukseklik - 0.055f, 0), new Vector3(0.1f, 0.02f, 5.4f), m.Led, false);

                var alan = new GameObject("TavanLED_AreaLight").AddComponent<Light>();
                alan.transform.SetParent(p, false);
                alan.transform.localPosition = new Vector3(x, Yukseklik - 0.07f, 0);
                alan.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                alan.type = LightType.Rectangle;
                alan.areaSize = new Vector2(0.1f, 5.4f);
                alan.color = Hex("#D9E8F0");
                alan.intensity = 2.2f;
                alan.range = 6f;
                alan.lightmapBakeType = LightmapBakeType.Baked;
            }

            // Titreyen 2 lamba: Mixed, realtime gölgeli (oyuncu + etkileşilebilir nesneler gölge alır)
            TitreyenLamba(p, m, "TitreyenLamba_Orta", new Vector3(0f, Yukseklik - 0.1f, -1.4f), 1.2f, 0.12f);
            TitreyenLamba(p, m, "TitreyenLamba_Terminal", new Vector3(-1.2f, Yukseklik - 0.1f, -0.6f), 0.9f, 0.3f);
        }

        private static void TitreyenLamba(Transform p, Materyaller m, string ad, Vector3 poz, float maxYogunluk, float dropout)
        {
            var govde = Kutu(ad + "_Govde", p, poz + Vector3.up * 0.06f, new Vector3(0.3f, 0.03f, 0.3f), m.Led, false);
            GameObjectUtility.SetStaticEditorFlags(govde, GISiz);

            var isik = new GameObject(ad).AddComponent<Light>();
            isik.transform.SetParent(p, false);
            isik.transform.localPosition = poz;
            isik.type = LightType.Point;
            isik.range = 4.5f;
            isik.intensity = maxYogunluk;
            isik.color = Hex("#D9E8F0");
            isik.shadows = LightShadows.Soft;
            isik.lightmapBakeType = LightmapBakeType.Mixed;

            var titreme = isik.gameObject.AddComponent<LightFlicker>();
            var so = new SerializedObject(titreme);
            so.FindProperty("hedefIsik").objectReferenceValue = isik;
            so.FindProperty("emissiveRenderer").objectReferenceValue = govde.GetComponent<Renderer>();
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
            var profil = ProfilHazirla("VP_TruckLab_Taban");
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
            var profil = ProfilHazirla("VP_TruckLab_Korku");
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
            var ld = Ekle<LensDistortion>(profil);
            ld.intensity.Override(-0.2f);
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

        private static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString(hex, out var c);
            return c;
        }
    }
}
