using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace PestCo.EditorTools
{
    /// <summary>
    /// Mutfağı "retro bistro" tarzına çevirir: adaçayı yeşili alt dolaplar, krem üst dolaplar, meşe tezgâh, pirinç kulplar,
    /// tereyağı sarısı duvar, metro karo tezgâh arkası, siyah-krem dama zemin, domates kırmızısı buzdolabı/mikrodalga.
    /// Dolaplar silinmez (saklanma noktaları kapılara bağlı); yalnızca materyaller değişir. Polyfork süsleri (sarkıt lamba,
    /// aplik, kara tahta, saat, tablo, halı, bitkiler, tezgâh üstü yiyecek/içecek) duvar ve tezgâh yüzeyleri ışınla bulunarak
    /// yerleştirilir; sıcak dolap altı ve sarkıt ışıkları eklenir. Menü: PestCo/Mutfak/Bistro Uygula. Her şey Undo'ya kaydedilir.
    /// </summary>
    public static class MutfakBistro
    {
        private const string MatKlasor = "Assets/_Project/Materials/Bistro";
        private const string PfKlasor = "Assets/_Project/Models/Polyfork";
        private const string Onek = "BST_";

        private static readonly List<string> rapor = new List<string>();
        private static Transform mutfak, sus;
        private static Material sablon;
        private static readonly Dictionary<string, Material> matlar = new Dictionary<string, Material>();

        // Palet
        private static readonly Color Ada = Hex("6B9A80"), AdaAcik = Hex("7FAE93"), AdaKoyu = Hex("3E604F"), AdaDavlumbaz = Hex("557F69");
        private static readonly Color Krem = Hex("EFE0BE"), KremAcik = Hex("FBF2DC"), Govde = Hex("F2E6CC"), SicakBeyaz = Hex("FAF3E3");
        private static readonly Color Mese = Hex("C8935C"), Piring = Hex("C9A24B"), Domates = Hex("D9483B"), Hardal = Hex("E9B949");
        private static readonly Color Yaprak = Hex("5FA05A"), Terrakota = Hex("D27A57"), KaroKrem = Hex("F5EBD6"), TereyagSari = Hex("FCE2A0"), LambaIsik = Hex("FFE3A3");

        [MenuItem("PestCo/Mutfak/Bistro Uygula")]
        public static string Uygula()
        {
            rapor.Clear();
            matlar.Clear();
            mutfak = Object.FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .FirstOrDefault(t => t.name == "Kitchen" && t.parent != null && t.parent.name == "SpaciousFamilyHouse");
            if (mutfak == null) return "Kitchen bulunamadı";
            if (mutfak.Find(Onek + "Susler") != null) return "Bistro zaten uygulanmış (önce Geri Al).";
            Klasor(MatKlasor); Klasor(MatKlasor + "/Textures");
            sablon = AssetDatabase.FindAssets("K_Pastel_White t:Material").Select(g => AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault();

            Undo.SetCurrentGroupName("Mutfak bistro");
            int grup = Undo.GetCurrentGroup();

            Renklendir();
            Zemin();
            TezgahArkasi();
            Buzdolabi();

            sus = new GameObject(Onek + "Susler").transform;
            Undo.RegisterCreatedObjectUndo(sus.gameObject, "Bistro süsleri");
            sus.SetParent(mutfak, false);
            Susle();
            Isiklar();

            Undo.CollapseUndoOperations(grup);
            UnityEditor.SceneManagement.EditorSceneManager.MarkAllScenesDirty();
            AssetDatabase.SaveAssets();
            string s = string.Join("\n", rapor);
            Debug.Log("[MutfakBistro]\n" + s);
            return s;
        }

        // ------------------------------------------------------------------ RENKLER

        private static void Renklendir()
        {
            // Materyal adına göre (yalnız mutfağa ait materyaller)
            var harita = new Dictionary<string, (string ad, Color renk)>
            {
                { "K_Pastel_PinkFrame", ("B_AdaCerceve", Ada) },
                { "K_Pastel_PinkPanel", ("B_AdaPanel", AdaAcik) },
                { "K_Cabinet_Mint", ("B_AdaCerceve", Ada) },
                { "K_Cabinet_Mint_Out", ("B_AdaCerceve", Ada) },
                { "K_Pastel_LimeFrame", ("B_KremCerceve", Krem) },
                { "K_Pastel_LimePanel", ("B_KremPanel", KremAcik) },
                { "K_Pastel_White", ("B_Govde", Govde) },
                { "K_Mint_Dark", ("B_AdaKoyu", AdaKoyu) },
                { "K_Pastel_HoodPink", ("B_Davlumbaz", AdaDavlumbaz) },
                { "K_Red", ("B_Domates", Domates) },
                { "K_Red_Ink", ("B_Domates", Domates) },
                { "K_Red_Out", ("B_Domates", Domates) },
                { "K_Tile_Green", ("B_Yaprak", Yaprak) },
                { "K_Tile_Green_Ink", ("B_Yaprak", Yaprak) },
                { "K_Tile_Light", ("B_KaroKrem", KaroKrem) },
                { "K_Wood", ("B_Mese", Mese) },
                { "K_Wood_Out", ("B_Mese", Mese) },
                { "K_Lamp", ("B_LambaIsik", LambaIsik) },
                { "K_Lamp_Ink", ("B_LambaIsik", LambaIsik) },
                { "K_Cloth_Pink", ("B_Hardal", Hardal) },
                { "K_Cloth_Pink_Out", ("B_Hardal", Hardal) },
                { "K_White_Out", ("B_SicakBeyaz", SicakBeyaz) },
                { "K_White_Out_Ink", ("B_SicakBeyaz", SicakBeyaz) },
            };
            int n = 0;
            foreach (var r in mutfak.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name.StartsWith(Onek) || r.GetComponentInParent<Transform>().name.StartsWith(Onek)) continue;
                var mats = r.sharedMaterials;
                bool degisti = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    if (m == null) continue;
                    var yeni = ParcaKurali(r, m);
                    if (yeni == null && harita.TryGetValue(m.name, out var h)) yeni = Mat(h.ad, h.renk, m);
                    if (yeni == null && m.name.StartsWith("K_WP_")) yeni = Mat("B_Duvar", TereyagSari, m);
                    if (yeni != null && yeni != m) { mats[i] = yeni; degisti = true; }
                }
                if (degisti) { Undo.RecordObject(r, "Bistro renk"); r.sharedMaterials = mats; n++; }
            }
            rapor.Add($"Yeniden renklendirilen parça: {n}");
        }

        // Parçanın adına göre özel kurallar: tezgâh meşe, kulp/musluk pirinç, ocak krem, mikrodalga domates, lambri adaçayı
        private static Material ParcaKurali(Renderer r, Material m)
        {
            string ad = r.name, ebeveyn = r.transform.parent != null ? r.transform.parent.name : "";
            if ((ad == "Counter" || ad.StartsWith("Counter ") || ad.EndsWith("_Top") || ad == "Worktop") && ebeveyn != "KitchenTable") return Mat("B_Mese", Mese, m);
            if (ad.StartsWith("CounterBand")) return Mat("B_Piring", Piring, m);
            if (ad.Contains("Pull") || ad == "Handle" || ad == "HandleLeg" || ad == "OvenHandle" || ad.StartsWith("Tap")) return Mat("B_Piring", Piring, m);
            if (ebeveyn == "Stove" && (ad == "Body" || ad == "Backguard" || ad == "OvenDoor")) return Mat("B_KremPanel", KremAcik, m);
            if (ebeveyn == "Stove" && (ad == "HoodBand" || ad == "HoodTrim" || ad == "BackguardTrim")) return Mat("B_Piring", Piring, m);
            if (ebeveyn == "Microwave" && ad == "Body") return Mat("B_Domates", Domates, m);
            if (ebeveyn == "Wainscot" || ebeveyn.StartsWith("Lambri")) return Mat("B_AdaCerceve", Ada, m);
            return null;
        }

        // ------------------------------------------------------------------ ZEMİN / TEZGÂH ARKASI / BUZDOLABI

        private static void Zemin()
        {
            var zemin = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).FirstOrDefault(r => r.name == "Floor_Mutfak");
            if (zemin == null) { rapor.Add("Floor_Mutfak yok"); return; }
            var tex = Doku("B_DamaZemin", 256, 256, (x, y) =>
            {
                bool kare = ((x / 128) + (y / 128)) % 2 == 0;
                var c = kare ? Hex("1F1E24") : Hex("F3EBDD");
                // ince derz + hafif boya lekesi
                if (x % 128 < 2 || y % 128 < 2) c = Color.Lerp(c, Hex("8C8478"), 0.55f);
                float g = (Mathf.PerlinNoise(x * 0.05f, y * 0.05f) - 0.5f) * 0.05f;
                return new Color(c.r + g, c.g + g, c.b + g, 1f);
            });
            var m = Mat("B_Zemin", Color.white, zemin.sharedMaterial);
            m.SetTexture("_BaseMap", tex);
            var b = zemin.bounds.size;
            // 0.35 m dama kareleri (doku 2x2 kare)
            m.SetTextureScale("_BaseMap", new Vector2(b.x / 0.7f, b.z / 0.7f));
            m.SetFloat("_BrushStrength", 0.06f);
            EditorUtility.SetDirty(m);
            Undo.RecordObject(zemin, "Bistro zemin");
            zemin.sharedMaterial = m;
            rapor.Add($"Zemin: siyah-krem dama ({b.x:0.0}×{b.z:0.0} m)");
        }

        private static void TezgahArkasi()
        {
            var tex = Doku("B_MetroKaro", 256, 256, (x, y) =>
            {
                // 2:1 metro karo, yarım kaydırmalı sıralar; açık krem karo, sıcak gri derz, karo kenarında hafif gölge
                int sira = y / 64; int kx = (x + (sira % 2) * 64) % 128; int ky = y % 64;
                bool derz = kx < 4 || ky < 4;
                if (derz) return Hex("BFAE92");
                float kenar = Mathf.Min(Mathf.Min(kx - 4, 128 - kx), Mathf.Min(ky - 4, 64 - ky)) / 10f;
                var c = Color.Lerp(Hex("E9DCC2"), Hex("FBF5E8"), Mathf.Clamp01(kenar));
                float g = (Mathf.PerlinNoise(x * 0.08f, y * 0.08f) - 0.5f) * 0.03f;
                return new Color(c.r + g, c.g + g, c.b + g, 1f);
            });
            int n = 0;
            foreach (var r in mutfak.GetComponentsInChildren<Renderer>(true))
            {
                var m = r.sharedMaterial;
                if (m == null || !m.name.StartsWith("K_BS_")) continue;
                var yeni = Mat("B_" + m.name.Substring(2), Color.white, m);
                yeni.SetTexture("_BaseMap", tex);
                // Eski dama dokusunun döşeme sıklığını koru; metro karo 2:1 olduğundan dikeyi ikiye katla
                var st = m.GetTextureScale("_BaseMap");
                yeni.SetTextureScale("_BaseMap", new Vector2(st.x * 0.5f, st.y));
                yeni.SetFloat("_BrushStrength", 0.04f);
                EditorUtility.SetDirty(yeni);
                Undo.RecordObject(r, "Bistro karo");
                r.sharedMaterial = yeni;
                n++;
            }
            rapor.Add($"Tezgâh arkası metro karo: {n} yüzey");
        }

        private static void Buzdolabi()
        {
            var dolap = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None).FirstOrDefault(t => t.name == "Fridge_New");
            if (dolap == null) return;
            int n = 0;
            foreach (var r in dolap.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || !(mats[i].name.Contains("Body") || mats[i].name.Contains("Door"))) continue;
                    var yeni = Mat("B_BuzdolabiKirmizi", Domates, mats[i], sablonuKoru: true);
                    mats[i] = yeni; n++;
                }
                Undo.RecordObject(r, "Bistro buzdolabı");
                r.sharedMaterials = mats;
            }
            rapor.Add($"Buzdolabı domates kırmızısı ({n} yüzey)");
        }

        // ------------------------------------------------------------------ SÜSLER

        private static void Susle()
        {
            var kb = SinirMutfak();
            var merkez = kb.center;
            float tavan = Tavan(merkez);
            rapor.Add($"Mutfak sınırı {kb.min:0.0}..{kb.max:0.0}, tavan {tavan:0.00}");

            // Yemek masası: sarkıt lambalar + masa üstü, altına halı
            var masa = mutfak.Find("KitchenTable");
            if (masa != null)
            {
                var mb = Sinir(masa);
                var mm = new Vector3(mb.center.x, 0f, mb.center.z);
                // eski mavi tavan lambasını gizle (ışığı yeni lambalara taşınır)
                foreach (var r in mutfak.GetComponentsInChildren<Renderer>().Where(r => r.transform.parent != null && r.transform.parent.name == "Detail_Toon" && (r.name.StartsWith("Lamp_") || r.name.StartsWith("Rug"))))
                { Undo.RecordObject(r, "Bistro"); r.enabled = false; }
                var ux = (mb.size.x >= mb.size.z) ? Vector3.right : Vector3.forward;
                foreach (float k in new[] { -0.32f, 0.32f })
                {
                    var p = Koy("ceiling-pendant-d21307", mm + ux * k, 0f);
                    if (p != null) TavanaAs(p, tavan - 0.55f);
                }
                Koy("area-rug-5c56e2", mm + Vector3.up * 0.003f, mb.size.x >= mb.size.z ? 0f : 90f);
                float ust = Physics.Raycast(mm + Vector3.up * 1.6f, Vector3.down, out var mh, 2f, ~0, QueryTriggerInteraction.Ignore) ? mh.point.y : 0.76f;
                Koy("table-candle-lantern-a7b5af", mm + Vector3.up * ust + ux * 0.05f, 0f, 0.9f);
                Isik("Sarkit_Isik", mm + Vector3.up * (tavan - 0.75f), Hex("FFC77A"), 2.4f, 3.6f, LightShadows.Soft);
            }

            // Batı duvarı: kara tahta menü + iki yanında aplik, üstünde saat; masanın yanında tablo
            var bati = Duvar(new Vector3(merkez.x, 1.4f, merkez.z + 1.6f), Vector3.left);
            if (bati.HasValue)
            {
                var (nokta, normal) = bati.Value;
                var kt = Koy("chalkboard-menu-board-a24d9d", new Vector3(nokta.x, 0f, nokta.z) + normal * 0.08f, YonY(normal));
                if (kt != null) DuvaraAs(kt, nokta, normal, 0.82f);
                foreach (float k in new[] { -0.8f, 0.8f })
                {
                    var yan = Vector3.Cross(Vector3.up, normal).normalized;
                    var ap = Koy("cafe-wall-sconce-1ee40b", new Vector3(nokta.x, 0f, nokta.z) + yan * k, YonY(normal));
                    if (ap != null) DuvaraAs(ap, nokta + yan * k, normal, 1.72f);
                    Isik("Aplik_Isik", nokta + yan * k + normal * 0.25f + Vector3.up * (1.95f - nokta.y), Hex("FFC27A"), 0.9f, 2.2f, LightShadows.None);
                }
                var saat = Koy("wall-clock-0c555c", new Vector3(nokta.x, 0f, nokta.z), YonY(normal));
                if (saat != null) DuvaraAs(saat, nokta, normal, 2.25f);
            }
            var guney = Duvar(new Vector3(merkez.x - 0.6f, 1.5f, merkez.z), Vector3.back);
            if (guney.HasValue)
            {
                var (nokta, normal) = guney.Value;
                var tablo = Koy("framed-wall-art-d2c8a2", new Vector3(nokta.x, 0f, nokta.z), YonY(normal));
                if (tablo != null) DuvaraAs(tablo, nokta, normal, 1.35f);
            }

            // Bitkiler: zeminde boş köşeler
            BosZemineKoy("potted-olive-tree-7eee05", kb, new Vector3(0.55f, 1.0f, 0.5f), new[] { new Vector2(0.12f, 0.92f), new Vector2(0.12f, 0.08f), new Vector2(0.88f, 0.08f) }, 0.85f);
            BosZemineKoy("potted-shrub-43bd21", kb, new Vector3(0.45f, 0.7f, 0.45f), new[] { new Vector2(0.12f, 0.08f), new Vector2(0.12f, 0.5f), new Vector2(0.88f, 0.92f) }, 0.75f);
            BosZemineKoy("clay-storage-pot-626452", kb, new Vector3(0.3f, 0.35f, 0.3f), new[] { new Vector2(0.88f, 0.08f), new Vector2(0.12f, 0.3f), new Vector2(0.5f, 0.08f) }, 0.7f);

            // Tezgâh üstü: boş yerler bul ve gruplar halinde diz
            var bosluklar = TezgahBosluklari(kb);
            rapor.Add($"Tezgâhta boş yer: {bosluklar.Count}");
            var gruplar = new List<System.Action<Vector3, float>>
            {
                (p, y) => { Koy("serving-tray-a1cf27", p, y); Koy("apple-b15012", p + Yer(y, -0.12f, 0.05f) + Vector3.up * 0.02f, 10f); Koy("orange-ea3791", p + Yer(y, 0.0f, -0.04f) + Vector3.up * 0.02f, 60f); Koy("pear-6171b0", p + Yer(y, 0.12f, 0.05f) + Vector3.up * 0.02f, 0f); Koy("banana-09c206", p + Yer(y, 0.02f, 0.08f) + Vector3.up * 0.02f, y + 20f); Koy("grapes-e7ddb2", p + Yer(y, -0.15f, -0.07f) + Vector3.up * 0.02f, 0f, 0.8f); Koy("lemon-1c574f", p + Yer(y, 0.15f, -0.07f) + Vector3.up * 0.02f, 45f); },
                (p, y) => { Koy("bread-loaf-0e0ab5", p + Yer(y, -0.06f, 0f), y + 80f); Koy("croissant-758d69", p + Yer(y, 0.14f, 0.04f), 20f); Koy("croissant-758d69", p + Yer(y, 0.16f, -0.08f), 120f); },
                (p, y) => { Koy("water-carafe-6f1774", p + Yer(y, -0.1f, 0f), 0f); Koy("milk-steaming-pitcher-4757b5", p + Yer(y, 0.04f, 0.04f), y + 30f); Koy("office-mug-8afee0", p + Yer(y, 0.15f, -0.04f), 200f); },
                (p, y) => { Koy("condiment-caddy-500e44", p, y); Koy("wine-bottle-75a822", p + Yer(y, 0.2f, 0f), 0f); },
            };
            for (int i = 0; i < gruplar.Count && i < bosluklar.Count; i++) gruplar[i](bosluklar[i].p, bosluklar[i].yaw);
        }

        // Tezgâh yüzeyinde (y≈0.85–0.95) 0.5×0.3 m boş, birbirinden uzak noktalar
        private static List<(Vector3 p, float yaw)> TezgahBosluklari(Bounds kb)
        {
            var aday = new List<(Vector3 p, float yaw)>();
            for (float x = kb.min.x + 0.25f; x < kb.max.x - 0.25f; x += 0.1f)
                for (float z = kb.min.z + 0.25f; z < kb.max.z - 0.25f; z += 0.1f)
                {
                    var ust = new Vector3(x, 1.25f, z);
                    if (!Physics.Raycast(ust, Vector3.down, out var h, 0.6f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (h.point.y < 0.84f || h.point.y > 0.97f || h.normal.y < 0.95f) continue;
                    if (h.collider.GetComponentInParent<Rigidbody>() != null) continue; // masa vb. tutulabilir eşyalar değil
                    // Duvara bakan yön: en yakın duvar normali (tezgâh derinliği yönü)
                    float yaw = EnYakinDuvarYaw(h.point);
                    var kutu = Quaternion.Euler(0f, yaw, 0f);
                    if (Physics.CheckBox(h.point + Vector3.up * 0.16f, new Vector3(0.26f, 0.14f, 0.16f), kutu, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (Physics.Raycast(h.point + Vector3.up * 0.05f, Vector3.up, 0.45f, ~0, QueryTriggerInteraction.Ignore)) continue; // üst dolap çok alçaksa
                    aday.Add((h.point, yaw));
                }
            // Birbirinden en az 0.9 m uzak olanları seç
            var secilen = new List<(Vector3 p, float yaw)>();
            foreach (var a in aday.OrderBy(a => a.p.z).ThenBy(a => a.p.x))
                if (secilen.All(s => Vector3.Distance(s.p, a.p) > 0.9f)) secilen.Add(a);
            return secilen;
        }

        private static float EnYakinDuvarYaw(Vector3 p)
        {
            float enYakin = 9f; Vector3 n = Vector3.forward;
            foreach (var d in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
                if (Physics.Raycast(p + Vector3.up * 0.3f, d, out var h, 1.2f, ~0, QueryTriggerInteraction.Ignore) && h.distance < enYakin) { enYakin = h.distance; n = -d; }
            return YonY(n);
        }

        private static void BosZemineKoy(string id, Bounds kb, Vector3 yarim, Vector2[] oranlar, float olcek)
        {
            foreach (var o in oranlar)
            {
                var p = new Vector3(Mathf.Lerp(kb.min.x, kb.max.x, o.x), 0f, Mathf.Lerp(kb.min.z, kb.max.z, o.y));
                // köşeye doğru biraz kaydırarak boş yer ara
                for (int i = 0; i < 6; i++)
                {
                    var q = p + new Vector3((i % 3 - 1) * 0.25f, 0f, (i / 3) * 0.25f * Mathf.Sign(0.5f - o.y));
                    if (!Physics.Raycast(q + Vector3.up * 1.0f, Vector3.down, out var h, 1.2f, ~0, QueryTriggerInteraction.Ignore) || h.point.y > 0.05f) continue;
                    if (Physics.CheckBox(q + Vector3.up * (yarim.y + 0.03f), yarim, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore)) continue;
                    Koy(id, new Vector3(q.x, h.point.y, q.z), Random.Range(0f, 360f), olcek);
                    return;
                }
            }
            rapor.Add($"{id}: boş zemin bulunamadı");
        }

        // ------------------------------------------------------------------ IŞIK

        private static void Isiklar()
        {
            // Üst dolapların altına sıcak ışık havuzları (tezgâhta derinlik)
            int n = 0;
            foreach (var r in mutfak.GetComponentsInChildren<Renderer>().Where(r => r.name == "UnderLight" || r.name.StartsWith("LightRail")))
            {
                var b = r.bounds;
                float uzun = Mathf.Max(b.size.x, b.size.z);
                int adet = Mathf.Clamp(Mathf.RoundToInt(uzun / 1.3f), 1, 3);
                var eksen = b.size.x > b.size.z ? Vector3.right : Vector3.forward;
                for (int i = 0; i < adet; i++)
                {
                    float t = adet == 1 ? 0f : Mathf.Lerp(-0.5f, 0.5f, i / (float)(adet - 1)) * (uzun - 0.6f);
                    Isik("DolapAlti_Isik", b.center + eksen * t + Vector3.down * 0.08f, Hex("FFC27A"), 0.75f, 1.5f, LightShadows.None);
                    n++;
                }
            }
            rapor.Add($"Dolap altı ışık: {n}");
        }

        private static Light Isik(string ad, Vector3 p, Color renk, float guc, float menzil, LightShadows golge)
        {
            var go = new GameObject(Onek + ad);
            Undo.RegisterCreatedObjectUndo(go, "Bistro ışık");
            go.transform.SetParent(sus, true);
            go.transform.position = p;
            var l = go.AddComponent<Light>();
            l.type = LightType.Point; l.color = renk; l.intensity = guc; l.range = menzil; l.shadows = golge;
            l.lightmapBakeType = LightmapBakeType.Realtime;
            return l;
        }

        // ------------------------------------------------------------------ YARDIMCILAR

        private static Transform Koy(string id, Vector3 p, float yaw, float olcek = 1f)
        {
            var kaynak = AssetDatabase.LoadAssetAtPath<GameObject>($"{PfKlasor}/{id}.glb");
            if (kaynak == null) { rapor.Add("YOK: " + id); return null; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(kaynak, mutfak.gameObject.scene);
            Undo.RegisterCreatedObjectUndo(go, "Bistro süs");
            go.name = Onek + id;
            go.transform.SetParent(sus, true);
            go.transform.position = p;
            go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one * olcek;
            var mat = PfMat();
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
            {
                var ms = r.sharedMaterials;
                for (int i = 0; i < ms.Length; i++) ms[i] = mat;
                r.sharedMaterials = ms;
            }
            return go.transform;
        }

        // Polyfork modelleri için köşe renkli toon materyal (mutfak gölge tonuyla)
        private static Material PfMat()
        {
            var m = Mat("B_Polyfork", Color.white, sablon);
            m.SetFloat("_UseVertexColor", 1f);
            m.SetTexture("_BaseMap", null);
            m.SetColor("_ShadowColor", Hex("A99BB8"));
            EditorUtility.SetDirty(m);
            return m;
        }

        // Tavan lambasını üst ucu tavana değecek şekilde kaldır
        private static void TavanaAs(Transform t, float altUc)
        {
            var b = Sinir(t);
            t.position += Vector3.up * (altUc - b.min.y);
        }

        // Duvar süsünü duvara yasla (arka yüzü duvarda) ve verilen yüksekliğe ortala
        private static void DuvaraAs(Transform t, Vector3 duvarNoktasi, Vector3 normal, float merkezY)
        {
            var b = Sinir(t);
            var arka = Vector3.Dot(b.center - duvarNoktasi, normal) - Mathf.Abs(Vector3.Dot(b.extents, new Vector3(Mathf.Abs(normal.x), 0, Mathf.Abs(normal.z))));
            t.position += -normal * (arka - 0.01f) + Vector3.up * (merkezY - b.center.y);
        }

        private static (Vector3 nokta, Vector3 normal)? Duvar(Vector3 baslangic, Vector3 yon)
        {
            if (Physics.Raycast(baslangic, yon, out var h, 4f, ~0, QueryTriggerInteraction.Ignore) && Mathf.Abs(h.normal.y) < 0.2f)
                return (h.point, new Vector3(h.normal.x, 0f, h.normal.z).normalized);
            rapor.Add($"Duvar bulunamadı: {yon}");
            return null;
        }

        private static float Tavan(Vector3 p) =>
            Physics.Raycast(new Vector3(p.x, 1.5f, p.z), Vector3.up, out var h, 3f, ~0, QueryTriggerInteraction.Ignore) ? h.point.y : 2.8f;

        private static float YonY(Vector3 n) => Mathf.Atan2(n.x, n.z) * Mathf.Rad2Deg;
        private static Vector3 Dik(Vector3 v) => new Vector3(-v.z, 0f, v.x);
        private static Vector3 Yer(float yaw, float x, float z) => Quaternion.Euler(0f, yaw, 0f) * new Vector3(x, 0f, z);

        private static Bounds SinirMutfak()
        {
            var z = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).FirstOrDefault(r => r.name == "Floor_Mutfak");
            if (z != null) { var b = z.bounds; b.Expand(new Vector3(-0.1f, 0f, -0.1f)); return b; }
            return Sinir(mutfak);
        }

        private static Bounds Sinir(Transform t)
        {
            var rs = t.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToArray();
            if (rs.Length == 0) return new Bounds(t.position, Vector3.zero);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        private static Material Mat(string ad, Color renk, Material kaynak, bool sablonuKoru = false)
        {
            if (matlar.TryGetValue(ad, out var hazir)) return hazir;
            string yol = $"{MatKlasor}/{ad}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(yol);
            if (m == null)
            {
                m = new Material(kaynak != null ? kaynak : sablon) { name = ad };
                AssetDatabase.CreateAsset(m, yol);
            }
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", renk);
            else if (m.HasProperty("_Color")) m.SetColor("_Color", renk);
            if (!sablonuKoru && m.HasProperty("_ShadowColor") && kaynak != null && kaynak.HasProperty("_ShadowColor") && kaynak.HasProperty("_BaseColor"))
            {
                m.SetColor("_ShadowColor", Golge(renk));
            }
            EditorUtility.SetDirty(m);
            matlar[ad] = m;
            return m;
        }

        // Gölge: aynı ton, daha doygun ve koyu; çok az mor (ton kaydırmak adaçayını mavi gösteriyordu)
        public static Color Golge(Color renk)
        {
            Color.RGBToHSV(renk, out float h, out float s, out float v);
            var g = Color.HSVToRGB(h, Mathf.Clamp01(s * 1.2f + 0.06f), v * 0.66f);
            return Color.Lerp(g, Hex("5A4A78"), 0.08f);
        }

        private static Texture2D Doku(string ad, int w, int h, System.Func<int, int, Color> f)
        {
            string yol = $"{MatKlasor}/Textures/{ad}.png";
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++) tex.SetPixel(x, y, f(x, y));
            tex.Apply();
            System.IO.File.WriteAllBytes(yol, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(yol);
            var imp = (TextureImporter)AssetImporter.GetAtPath(yol);
            imp.wrapMode = TextureWrapMode.Repeat; imp.mipmapEnabled = true; imp.filterMode = FilterMode.Trilinear; imp.anisoLevel = 4;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(yol);
        }

        private static void Klasor(string yol)
        {
            if (AssetDatabase.IsValidFolder(yol)) return;
            var ust = System.IO.Path.GetDirectoryName(yol).Replace('\\', '/');
            Klasor(ust);
            AssetDatabase.CreateFolder(ust, System.IO.Path.GetFileName(yol));
        }

        private static Color Hex(string h) { ColorUtility.TryParseHtmlString("#" + h, out var c); return c; }
    }
}
