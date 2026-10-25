# MUTANT EXPRESS — Proje Teknik Hafızası (CLAUDE.md)

> Bu dosya yeni oturumlar için teknik hafızadır. İçeriği gerçek proje koduna ve bu oturumlardaki çalışmalara
> dayanır; doğrulanmayan her şey "doğrulanmadı" diye işaretlidir. Kullanıcı Türkçe yazar, **her zaman Türkçe yanıt ver**.
> Kullanıcı Unity'de yeni; sahne kurulumlarını (GameObject/Inspector bağlantıları) mümkünse sen yap (unity-mcp ile).

## 1. Oyun konsepti

FPS bakışıyla evlere girilir; sevimli ama tehlikeli yaratıklar (şimdilik böcekler) vakumla yakalanır, kamyonetteki
fiziksel laboratuvara getirilir. Slime Rancher + R.E.P.O. + fizik tabanlı kaos tarzı. Steam için indie.
Laboratuvarda yaratıklar beslenir, büyütülür, risk alınır; sonunda **SAT / SAKLA / DENEY YAP**. Deneyler kontrolden
çıkabilir; yaratıklar fanusu kırıp kaçabilir. Kazanılan parayla laboratuvar geliştirilir (planlı).

## 2. Temel oynanış döngüsü

Yakala → Fanusa koy → Besle → Büyüt → Risk al → Sat/Sakla/Deney

## 3. Unity altyapısı

- Unity **6000.3.24f1**, **URP 17.3.0**, **New Input System 1.20.0** (eski Input yok), uGUI 2.0 + TextMeshPro, AI Navigation 2.0.14.
- Unity-MCP (`com.ivanmurzak.unity.mcp` 1.5.0) kuruludur; Editor'ü araç olarak sürmek için kullanılır.
- Ana sahne: `Assets/Scenes/SampleScene.unity` (kamyon lab, ev, mutfak vb. hepsi içinde). Git dalı: `pestos-cizgi-film`.
- Kod: `Assets/_Project/Scripts/` altında klasörler: `Pests`, `Player`, `Systems`, `Tools`, `Truck`, `UI`, `Data`, `Grab`, `Interactables`, `Shop`, `Visual`, `Editor`.
- Görsel stil: çizgi film (Cesur Korkak Köpek tarzı) kağıt/mürekkep arayüzü (`PestHudTema`, `PestOS`).
- Yedek sahne kopyaları kökte `SampleScene.backup_*.unity` ve `Assets/Scenes/*.bak` olarak durur; silme.

## 4. Aktif scriptler ve görevleri (yaratık/fanus hattı)

- `CageableBug` (Pests): ana yaratık. Rigidbody + BoxCollider; sprey (`IDamageable`) bayıltır, vakum çeker, kapsüle girer.
  Her yaratığın bağımsız `ValueMultiplier`, **mantıksal** `GrowthMultiplier`(=`LogicalGrowth`) ve **gösterilen** `DisplayGrowth` değeri vardır.
  `ApplyFeeding(valueFactor, targetGrowth, duration)`, `SnapDisplayFor(cage)`, `BaseBodySize`, olay `GrowthChanged`.
- `BugData` (ScriptableObject, Data): `bugName`, `startState`, `baseValue`, `dangerMultiplier`, renkler, `icon`. Asset: `Assets/_Project/Data/Hamamboecegi.asset`.
- `VacuumTool` (Tools): sağ tık çekim; baygın `CageableBug`'ı `VacuumCapsule`'a alır. Ayrıca `Pest`/`BugController` yolları ve balon/kule vardır. Herkese açık API: `StoredCount`, `MaxCapacity`, `IsFull`, `IsRunning`, `ReleaseOne`, `Empty`.
- `VacuumCapsule` (Tools): vakum üstü kapsül; `TryStore`, `Take`, `CountOf`, `FirstType`.
- `BugCage` (Systems): kamyondaki cam fanus bölmesi, `IInteractable`. Olay `ContentsChanged`. `TotalValue()`,
  `CreatureValue` (private, tek fiyat formülü), `GetCreatureInfos`, `GetDisplayGrowthLimit`, `SellContents()`, `IsSelling`, `Contains`.
  E: aktarılacak uygun yaratık varsa transfer; yoksa ve fanusta yaratık varsa inceleme paneli. Broken iken E onarımdır (bkz. Aşama 4D).
- `CageInspectPanel` (UI): fanus inceleme paneli; sahnedeki Canvas nesnelerine Inspector referanslarıyla bağlı (runtime UI üretmez; referans eksikse `Debug.LogError`). Panel açıkken oyuncu girdisi kilitlenir (aşağıya bak).
- `CreatureFeeder` (Systems): besleme kuralları ve yem tablosu (Soda 1.10/1.25, Candy 1.20/1.50, Cookie 1.30/1.75 = büyüme/değer; mantıksal büyüme üst sınırı 3.0). `TryFeed(cage, bug, food)`.
- `CagePressureController` (Systems): fanus başına baskı = Σ Max(0, LogicalGrowth−1)×70. Durumlar `CagePressureState` Safe/Warning/Danger/Critical (40/70/100). Olaylar `OnPressureChanged`, `OnPressureStateChanged`. Durum ışığı rengi (`MaterialPropertyBlock`).
- `CageCriticalController` (Systems): Critical olunca 10 sn geri sayım (`Time.unscaledDeltaTime`); `CageCriticalState` Inactive/CountingDown/BreachPending; olaylar `OnCountdownStarted/Updated/Cancelled`, `OnBreachRequested` (bir kez). Besleme engeli, alarm (AudioSource), RadioManager uyarısı, fanus üstü World Space sayaç.
- `CageSaleService` (Systems, statik): fanus satışının tek güvenilir yolu (bkz. §7).
- `FanusVurma` (Truck, namespace `PestCo.Truck`): uyanık yaratığı cama atar (ses/titreme). Baskı durumuna göre bekleme süresi kısalır (yalnızca zamanlama).
- `BugShelf` (Systems): 16 fanusu toplayan raf; B tuşuyla toplu satış (`SellAll`).
- `KamyonLab` (Truck): lab ışık/ortam yönetimi. `PestPC` (Truck, ~1900 satır): kamyon bilgisayarı arayüzü.
- `GameManager` (Systems): `PlayerData` sahibi; `AddMoney(int)`, `SpendMoney(int)`, `RegisterJobComplete`. `PestCoSaveSystem`: yalnızca `PlayerData`'yı `persistentDataPath/pestco_save.json`a yazar.
- `RadioManager`: `PlayRadioSubtitle(string)`. `PlayerInteraction`: E ile `IInteractable`, kendi HUD'unu kurar. `PlayerController`: hareket/bakış imleç kilidine bağlıdır.
- `FoodInventory` (Player): `Dictionary<FoodType,int>`; `FoodType` = NuclearSoda, ExplosiveCandy, CursedCookie. Kaydedilmez.
- Eski/paralel hatlar (silinmedi): `Pest`+`PestPool` (ev düşmanları, hâlâ aktif), `BugController` (sahnede yalnızca pasif `TestBug`), `BugInventory`, `TruckZone`+`FedBugVisual` (eski kamyon beslemesi; `FoodType.ValueMultiplier()` 2/2/3 buna aittir), `UIManager` (yalnızca vakum sayacı), `PlayerWeapon` (sahnede yok).

## 5. Tamamlanan aşamalar

- **Aşama 1 — ValueMultiplier:** `CageableBug.ValueMultiplier` (≥1, NaN/Inf→1); `BugCage.TotalValue` per-yaratık çarpanla; fanus başına üst sınır 1e8.
- **Aşama 2 — CageInspectPanel:** E ile inceleme paneli; imleçsiz değil → **imleç açık, girdi kilitli, `timeScale` değişmez**; ESC/CloseButton kapatır; PestPC/JobBoard ile çakışmaz. 4.5 m uzaklaşınca kapanır.
- **Aşama 3 — CreatureFeeder:** BESLE → yem seçimi → `TryFeed`; stok `FoodInventory`'den tek yem düşer; animasyon sırasında tekrar besleme yok.
- **Aşama 4A — LogicalGrowth/DisplayGrowth:** besleme mantıksal büyümeyi artırır; fanus içinde gösterilen boyut fanusun **fiziksel** iç ölçülerine (cam halkası yarıçapı 0.235, iç yükseklik 0.46) göre sınırlanır (şu an ≈ ×1.02); fanus dışında gerçek boyuta döner.
- **Aşama 4B — CagePressureController:** baskı hesabı, panel göstergesi (yüzde/durum/çubuk), fanus kapağındaki `DurumIsigi` küresi, FanusVurma sıklığı.
- **Aşama 4C-1 — CageCriticalController:** 10 sn geri sayım, panel banner'ı, fanus üstü sayaç, ışık yanıp sönme, alarm, telsiz, besleme engeli, `BreachPending` + `OnBreachRequested`. **Kırılma yok.**
- **Aşama 4C-2 — Tek fanus acil satışı (CageSaleService, SAT butonu):** tamamlandı, kullanıcı onayladı.
- **Aşama 4D — Fanus kırılması ve kaçış (onaylandı):**
  - `BugCage`: `BugCageState` Intact/Broken (yalnızca runtime), `IsBroken`, `RepairCost` (Inspector `repairCost`, varsayılan 50). `CageCriticalController.OnBreachRequested` dinlenir; kırılma bir kez çalışır.
  - Kırılınca: `Cam` renderer'ları kapanır; `Duvar` (x12) ve `Kapak` collider'ları devre dışı kalır (`Zemin` ve `Etkilesim` trigger açık). Parça listeleri (`glassRenderers`, `breachColliders`) boşsa çocuk adlarından ("Cam", "Duvar", "Kapak") bulunur. İsteğe bağlı `breachVfxPrefab` (atanmamış).
  - Fanustaki yaratıklar `caged`'den çıkar, `Cage=null` (boyut mantıksal büyümeye yumuşakça açılır), `CageableBug.Escape(velocity)` ile dışarı/yukarı fırlar (`breachPushOut` 1.8, `breachPushUp` 2.2); `IsEscaped=true`.
  - Broken iken: yeni yaratık alınmaz, `SellContents` 0 döner, `CageSaleService` atlar, `CagePressureController` baskıyı 0 sayar, panel "Fanus KIRIK / FANUS KIRILDI! ONARMAK İÇİN KAPAT, E — $50" gösterir ve açık kalır.
  - Onarım: Broken fanusa bakıp E → `GameManager.SpendMoney(repairCost)`; yetmezse `RadioManager` mesajı. Başarılıysa cam/collider geri gelir, Intact, `ContentsChanged` (baskı 0/Safe), `CageCriticalController.ResetAfterBreach()`.
  - Kaçan yaratık (`IsEscaped`) hiçbir fanusun trigger'ı tarafından alınmaz; yalnızca kapsülden geçip E ile bırakılınca fanusa girer (bayrak `Cage` atanınca ve `OnStoredInCapsule`'da temizlenir). Test sırasında yandaki sağlam fanusun kaçanı yakalaması hatası bulunup düzeltildi.
  - Play'de doğrulandı: gerçek sayaçla kırılma, panel durumu, büyümüş yaratığın ×3'e açılması, itme, onarım (para düşüşü, cam/collider, Intact). Yedek: `Assets/Scenes/SampleScene_4D.bak`.
- **Aşama 4D düzeltmesi — Işınlamalı çıkış (BugCage):** Fanusların `forward`'ı (0,0,−1) komşu fanuslara bakar; gerçek açık yüz dünya +X (yerel −X). Kırılınca her yaratık `TeleportAndPush` ile (bir fizik adımı `isKinematic` + `Physics.SyncTransforms`) `breachEscapeOffset` (yerel, varsayılan (−0.3, 0.2, 0)) + büyümüş gövdenin yarı genişliği kadar ileriye ışınlanır, sonra dışarı fırlar. Birden çok yaratık yanlara 0.4 m ayrılır. Kırılma sonrası açık kalan Duvar/Kapak için `LogWarning`. Fanusun önünde raf yüzeyi yok: yaratık ~1.15 m aşağıdaki lab zeminine (y≈1.55) düşer.
- **Kaçak yaratık saldırısı ve korku (CageableBug, yalnızca bu dosya):** `Escape()` ile `rampaging` aşaması başlar. Yol bulma/NavMesh YOK, fizik zıplaması:
  - Kovalama: `chaseRange` 12 m içinde oyuncuya zıplar (`chaseSpeed` 3.5, `chaseInterval` 0.35–0.7 sn, 2 m içinde `lungeSpeed` 5). Havadayken zıplamaz (`|vy|>0.3`).
  - Saldırı: çarpınca/temas sürerken (`OnCollisionEnter` + `OnCollisionStay`, `TryAttack`) `PlayerStatus.TakeDamage(attackDamage=5)` + `Juice.PlayerHurt()` + `CharacterController.Move` ile ~0.6 m itme + `Juice.Shake`; 1 sn bekleme. Yalnızca `rampaging` ve bayılmamışken.
  - Bırakma: `rampageSeconds` (12 sn), oyuncu `chaseRange`'den uzaklaşınca ya da oyuncu ölünce biter. Bitince bir kez `SpreadFear()`: telsiz (`fearMessage`), kodla üretilmiş alçak hırıltı (`AnomalySounds.PlayAt`), `Juice.Shake(fearShake)`.
  - Sakinleşme: oyuncu `calmFleeRange` (6 m) içindeyse ±`zigzagAngle` zigzaglı kaçar, değilse rastgele zıplar; saldırmaz. Bayılma/kapsüle alınma/fanusa girme kaçak bayrağını temizler. Kaçak yaratık hiçbir fanus trigger'ına otomatik girmez.
  - Pest.cs (eski NavMesh kaçış/saklanma AI'ı, `Pest_Hamambocegi` prefab'ı) ayrı hat; fanus böceğine taşınmadı (lab'da NavMesh yok, model animatörsüz statik).
- **PestWaddle uyarlaması:** `PestWaddle` (sekme/yalpa/öne eğilme/squash) NavMeshAgent yoksa `Rigidbody` yatay hızını okur; görseli `CageableBug.Visual`'dan bulur; `Suppressed` bayrağı eklendi (`OnEnable`'da yalnızca ajanlı haşerede `ApplyRest`). Sahnedeki 3 `KafesBocek_Hamamboecegi`'ye bileşen eklendi; `CageableBug` bayılınca `Suppressed=true`, uyanınca false yapar (yatış duruşu bozulmaz). Play'de ölçüldü (sekme 0.025 m, squash %11, yatış 90° korunur, uyanınca eski poz); gözle kontrol edilmedi. Yedek: `Assets/Scenes/SampleScene_waddle.bak`.
- **Mutfak hamamböcekleri yeniden aktif (eski `Pest` hattı):** `PestSpawner` (GameObject + bileşen) kapalıydı, `SpawnSwitch.spawnsEnabled` false'tu ve spawner merkezi (0,0,0)'daydı (evde NavMesh yok). Şimdi: spawner açık, konum (42.3, 0.1, 5.4) (`KitchenHideSpots` ortası), `spawnCount` 12, `spawnAreaRadius` 4, `startDelay` 5 sn; `SpawnSwitch.spawnsEnabled` true (F10 aç/kapat, F8 `HouseRoaches` aç/kapat). Havuz `PestPool` (Pest_Hamambocegi, 50); prefab agent tipi ev NavMesh'iyle uyumlu (kitchen 40/40 nokta). Play'de doğrulandı: 12 böcek NavMesh üstünde, oyuncu ~3 m'ye yaklaşınca Flee, birkaç sn sonra 4–6'sı Hide (15 `HideSpot_*` dolap/çekmece). Saklanmanın görünümü gözle kontrol edilmedi. Sahnede `PestNest` yok; `JobManager` "hiç yuva bulunamadı" uyarısı sürüyor (iş bitmeyebilir). Yedek: `Assets/Scenes/SampleScene_hamam.bak`.
- **Mutfak böceğini bayıltıp fanusa koyma:** `Pest.stunnedBugPrefab` (yeni alan; `Pest_Hamambocegi.prefab`'a `Assets/_Project/Prefabs/KafesBocek_Hamamboecegi.prefab` atandı). Öldürücü sprey vuruşunda `Pest.CaptureAsStunned()`: aynı yerde baygın `CageableBug` (`BecomeStunned()`, ~12 sn) belirir, Pest havuza döner, `TotalKills` artmaz (görev sayımı etkilenmez), `onDeath` tetiklenir. Alan boşsa eski ölüm. Sonra mevcut hat: vakum (yalnızca baygın CageableBug'ı çeker) → kapsül → fanusa E. İki böcek aynı Meshy modelini kullanır. Spreysiz vakumlanan Pest eskisi gibi yutulur (öldürme sayılır). Uyanan CageableBug Pest AI'ına dönmez, rastgele zıplar. Eski 3 el yerleştirilmiş böcek (`KafesBocekleri`) PASİF (silinmedi). Play'de doğrulandı: sprey → baygın CageableBug (0.15 m), kills 0→0, kapsül → Fanus_B-01 count=1. Yedekler: `Assets/Scenes/SampleScene_yakala.bak`, Pest prefab yedeği `/tmp/Pest_Hamambocegi.prefab.bak` (geçici).
- **Mutfak düzeni (açılır dolaplar):**
  - Görünmez engel: `SM_Kitchen_Shelf`'in LOD0'ı 0.7 m kaymıştı, collider + LOD1-3 oda içinde (43.12, 3.36) kalmıştı. Kök LOD0'ın konumuna taşındı (artık hepsi (43.78, 0.50, 3.09), tezgah ucunda).
  - Modeldeki GERÇEK çekmece/kapak ön yüzleri tezgah mesh'inden ayrıldı (üstüne panel eklenmedi; ilk denenen panel yöntemi kullanıcı isteğiyle geri alındı). Orijinal FBX'e dokunulmadı: `Assets/_Project/Meshes/Mutfak/SM_Kitchen_Countertop_{A,B}_LOD0_Acilir.asset` (ön yüz üçgenleri çıkarılmış gövde, LOD0'a atandı) + her ön yüz için `Mutfak_<id>.asset` (çift yüzlü, ad sonu `_2Y`). Seçim: ön yüz dikdörtgeni ±2 cm ve derinlik (kuzey z 7.84–7.935, doğu x 43.31–43.40); her ön yüz ayrı geometri olduğu için temiz ayrıldı.
  - `Mutfak_Dolaplar` altında 12 birim (kuzey: N_Cekmece_1/2/3/5/6 + N_Kapak; doğu: E_Cekmece_1–5 + E_Kapak). Birim: `Cekmece` (`Drawer`, openOffset 0.30 m odaya; `Model` + koyu iç kutu Taban/Yan/Arka) ya da `Mentese` (`CabinetDoor`, −100°, menteşe kapağın kulpsuz kenarında) ve sabit `Bosluk` (ön yüzün hemen arkasında koyu levha). Collider hareketli kökte, layer `Interactable`. Materyaller `Mutfak_DolapIci.mat`, `Mutfak_CekmeceIci.mat`; ön yüzler tezgahın kendi materyalini kullanır.
  - Tezgahların `LODGroup`'u kapatıldı ve LOD1–3 renderer'ları kapalı (uzak LOD'lar eski sabit ön yüzleri gösterirdi); her zaman LOD0 çizilir.
  - Köşedeki üst çekmece (doğu tezgahın arkasında) ayrılmadı, modelde sabit kaldı.
  - `KitchenHideSpots`: 12 `CabinetHideSpot` (ön yüzün 0.30 m önü, NavMesh'te) yeni parçalara bağlı; 3'ü pasif.
  - Play'de doğrulandı: 12/12 parçaya göz hizasından ışın ulaşıyor, E ile çekmece 0.30 m kayıyor / kapak 100° dönüyor ve geri kapanıyor; kapalı görünüm eskisiyle aynı (render ile karşılaştırıldı); zorlanmış testte böcek E_Kapak'a girdi, oyuncu açınca Flee'ye geçti. Böceğin dolaba girme şansı varsayılan %30 ve 25 sn bekleme (Pest ayarı, değiştirilmedi).
  - Gizli detaylar: renderer'ı kapalı `Detail2/Pot, Pot_Knob, Mug, Mug_Handle` ve `Walls_Toon/E_Tile_Stove` collider'ları kapatıldı. `cantina-table-ef03e3` masasına BoxCollider eklendi (sandalyeler tek mesh, collider yok).
  - Yedekler: `Assets/Scenes/SampleScene_mutfak.bak` (hiçbir mutfak değişikliği yokken), `SampleScene_mutfak2.bak` (raf düzeltmesinden sonra, çekmece ayırmadan önce).
- **Yem test nesneleri:** Sahnede `FoodTest` altında 9 `CollectibleFood` (3 NuclearSoda yeşil silindir, 3 ExplosiveCandy pembe küre, 3 CursedCookie kahve küp), konum (43.5, 0.25, −8.6) = oyuncu başlangıcının arkası; materyaller `Assets/_Project/Materials/Yem_*.mat`. Yalnızca test için, toplanınca yok olur. Soda sayacı bir testte 3 yerine 6 arttı (silindir `CapsuleCollider` çift tetik şüphesi, araştırılmadı). Yedek: `Assets/Scenes/SampleScene_yem.bak`.

## 6. Mevcut veri akışı

`CageableBug` (sprey bayıltır) → `VacuumTool` (çeker) → `VacuumCapsule.TryStore` (yaratık `SetActive(false)`, kapsülde mini kopyası) →
fanusa bakıp E → `BugCage.Interact` (`SnapDisplayFor` + `ReleaseAt`) → fanus tetikleyicisi yaratığı `caged` listesine alır (`FixedUpdate`) →
`BugCage.ContentsChanged` → `CagePressureController.Recalculate` → `OnPressureStateChanged` → `CageCriticalController` (Critical ise sayaç).
Besleme: panel → `CreatureFeeder.TryFeed` → `CageableBug.ApplyFeeding` → `GrowthChanged` → `ContentsChanged` → baskı yeniden hesaplanır.
Satış: panel SAT ya da `BugShelf.SellAll` (B / PestPC) → `CageSaleService.SellCages` → `BugCage.SellContents` → `GameManager.AddMoney(int)`.

## 7. Satış sistemleri

- **Tek fiyat formülü:** `BugCage.CreatureValue` = `baseValue` × (Horror ise `dangerMultiplier`) × `ValueMultiplier`; `TotalValue` toplar (NaN/Inf atlanır, fanus başına ≤1e8).
- **`CageSaleService.SellCage/SellCages`:** uygun fanusları seçer (dolu, `IsSelling` değil, `BreachPending` değil), `GameManager` yoksa hiçbir şey silmez, içeriği siler, toplam değeri tek `AddMoney(int)` ile öder (float toplanıp bir kez yuvarlanır; int sınırlı). Sonuç: Success/NothingToSell/TooLate/Failed. `PreviewValue/PreviewPayout` gösterim içindir.
- **`BugShelf.SellAll()`** imzası korunur; artık servisi kullanır (kırılma bekleyen fanus satılmaz). **`PestPC`** "Fanus Takip → HEPSİNİ SAT" `BugShelf.SellAll()` çağırır ve kazancı bakiye farkından hesaplar.
- **`GameManager.AddMoney(int)`:** `Data.money` (float) artar ve her çağrıda diske kaydeder. Test sırasında gerçek kaydı bozmamak için `pestco_save.json`u yedekleyip geri yükle.
- Eski kamyon satışı (`TruckZone`, `ShopZone`) ayrı ve eski hattadır; fanus sistemiyle ilişkisi yok.

## 8. Korunması gereken API'ler ve Inspector referansları

- Serialized alan adlarını değiştirme (özellikle `BugCage` `bugType/capacity/label`, `CageableBug` `data/state/valueMultiplier/growthMultiplier/visual`, `TruckZone` `_cagePoint` vb. `FormerlySerializedAs`).
- Korunacak API: `GameManager.AddMoney(int)`, `BugShelf.SellAll()`, `BugCage.TotalValue()/SellContents()/Count/Capacity/Caged`, `CageableBug.ValueMultiplier/GrowthMultiplier/DisplayGrowth`, `VacuumTool` herkese açık üyeleri, `BugInventory.Changed/BugFed`, `IInteractable`.
- Sahne: `CageInspectManager` (kökte, aktif) üzerinde `CageInspectPanel` + `CreatureFeeder`; görsel panel `Canvas/PanelBackground` (başlangıçta kapalı). `PanelBackground` altında: Header, CloseButton, NavRow, CreatureInfoContainer (ad/tür/durum/değerler/doluluk/baskı/mutasyon/stres + CriticalBanner), ButtonStrip (Feed/Experiment/Sell/Keep), FoodPanel, MessageText. Bütün alanlar Inspector'a bağlıdır; eksikse panel açılmaz ve `LogError` verir.
- `CageInspectPanel.Disable While Open` listesi: VacuumTool, SprayTool, SprayVisual, ToolHolder, Flashlight, InventoryHotbar, BugShelf, TruckZone, FoodShopZone, SpawnSwitch (kendi girdisini okuyan bileşenler panel açıkken geçici kapatılır).
- 16 fanus (`Fanuslar` altında `Fanus_B-01…16`) her birinde: `BugCage` (capacity 2), `FanusVurma`, `CagePressureController` (+`DurumIsigi` küre çocuğu), `CageCriticalController` (+`KritikSayac` World Space Canvas, AudioSource). Fanus capacity değerlerini kodla değiştirme.
- Ortak materyal: `Assets/_Project/Materials/CageStatusLight.mat` (URP Unlit, renk `MaterialPropertyBlock` ile).
- `PlayerController` imleç kilidi mantığı, `PlayerInteraction`'ın `CageInspectPanel.IsOpen` kontrolü ve `PestCo.Truck.PestPC.IsOpen` deseni korunmalıdır.
- Sahnede `KafesBocekleri` (3 el yerleştirilmiş `KafesBocek_Hamamboecegi`) artık PASİF; fanus yaratıkları mutfak `Pest`'lerinin spreyle bayıltılmasından gelir (`PestSpawner` 12 böcek). `KafesBocek_Hamamboecegi.prefab` CageableBug prefab'ıdır.

## 9. Bilinen teknik problemler

- Yaratık tedariki yok: 3 örnek vardır, satılınca `Destroy` edilir; yeniden üretim yok.
- Fanus içindeki güvenli gösterim boyutu fiziksel olarak çok küçük (≈ ×1.02): yatan yaratığın köşeleri cam yarıçapına dayanır. Modelin görseli collider'dan geniştir; gözle kontrol edilmedi.
- Zıplayan yaratık ×1.0'da bile kapağa çarpabilir (zıplama tepesi 0.13 m, boşluk 0.06 m).
- `FoodInventory`, yaratıklar, fanus içeriği, baskı ve büyüme **kaydedilmez** (yalnızca `PlayerData`).
- Gerçek bir pause menüsü / `PauseManager` yok; `PestPC`, `JobBoardUI`, `ResultScreen` `timeScale=0` yapar. Kritik sayaç unscaled çalıştığı için bunlarda da akar.
- Kırık fanus onarılana kadar `BreachPending`'de kalır (ışık sabit kırmızı, alarm durur); onarım `ResetAfterBreach()` çağırır.
- 4D'de gözle doğrulanmayanlar: kamera sarsıntısı, para yetersizken telsiz mesajı, `breachVfxPrefab`; kırılma sesi ve "kırık cam" görseli yok (cam yalnızca kapanır).
- Kaçan yaratık kamyon içinde zıplar; oyuncu `CharacterController` olduğu için itme `Move` ile yapılır (AddForce değil). Kaçan yaratıkları geri toplamak için sprey + vakum + kapsül gerekir. Büyük (×3) kaçak yaratığın sprey/kapsülle yakalanması Play'de denenmedi.
- Kaçak AI'ında yol bulma yok: engel/basamağın arkasındaki oyuncuya düz zıplar, takılabilir (sundurma basamağında tırmanamadı). Kamyon lab'ında (oyuncu ışınlama testi başarısız oldu) kovalama, 12 sn'lik gerçek süre, 12 m uzaklaşma bırakması, ses/sarsıntı ve çoklu kaçak denenmedi. Oyuncu ölünce ölüm ekranı yok (yalnızca uyarı).
- Test için oyuncuyu `CharacterController` kapatıp ışınlamak geometriye gömebilir; Play'i yeniden başlatmak gerekir.
- `FoodTest` yemleri bir kez toplanır; sürekli yem kaynağı yok. Soda çift sayma şüphesi açık.
- Kırıkken fanustaki yaratıklar kaçtığı için fanus içeriği/satış değeri kaybolur; kaçanlar kaydedilmez.
- `PestPC` "Fanus Takip" toplamı `BugCage.TotalValue()`'yu doğrudan toplar; kırılma bekleyen fanus gösterilen toplamda sayılır ama satılmaz.
- Alarm ses dosyası atanmamış (`Alarm Clip` boş; sistem hatasız sessiz çalışır). Fanus üstü sayacın konumu/boyutu gözle doğrulanmadı.
- `Time.timeScale=0` iken Play'de script-execute tabanlı testler güvenilir olmayabilir.
- `Assets/Plugins/NuGet` ve `StylArts` materyalleri git'te değişmiş görünür (proje dışı, dokunma).

## 10. Henüz yapılmamış sistemler

- Kaçan büyük yaratığın yeniden yakalanması (kapsüle sığma/sprey sağlığı kararı bekliyor), kırılma sesi/kırık cam görseli, kaçak yaratık saldırıları (mutasyon bazlı).
- Mutasyon sistemi (panelde `Mutasyon: Yok`, `Stres: —` sabit), SAKLA ve DENEY YAP, Experiment/Keep butonları (pasif).
- Save/Load genişletmesi (yem, yaratıklar, fanus, baskı).
- Yaratık spawner/tedarik, laboratuvar yükseltme (fanus seviyeleri), multiplayer (İlk aşamada kodlanmayacak; oyun mantığı ile görsel sunum ayrı tutulur).

## 11. Kodlama kuralları

- Mevcut çalışan sistemleri bozma. Önce ilgili kodları ve sahne/prefab referanslarını incele; varsayım yapma.
- Gereksiz refactor yapma. Yeni singleton/manager oluşturma; her fanus kendi durumunu yönetir.
- Inspector referanslarını ve serialized alan adlarını koru. Runtime'da gizlice UI üretme.
- Her aşamada **yalnızca belirtilen dosyalara** dokun; başka dosya gerekirse önce gerekçeyi açıkla.
- Sahne/prefab değişikliğinden önce yedek al (`Assets/Scenes/SampleScene_<ad>.bak`). Sahneyi MCP ile değiştirirsen kaydet; gerçek kayıt dosyasını (`pestco_save.json`) test sonrası geri yükle.
- Değişiklikten sonra Unity derlemesini kontrol et (Editor.log'da `error CS` yok, assembly yeniden yüklendi). Mümkünse Play Mode'da test et; doğrulanan ve doğrulanamayan testleri ayrı ayrı raporla.
- Test harness'ı: `script-execute` ile girdiler Input System'e kuyruğa alınabilir; `isPlaying` `rql-mutate editor | set isPlaying = ...` ile değişir.
- Yorumlar ve kullanıcıya dönük metinler Türkçe; mevcut dosyanın yorum yoğunluğu ve adlandırmasına uy.
- **Onay almadan sonraki aşamaya geçme.**

## 12. Sıradaki görev

- **Aşama 4C-2 ve 4D tamamlandı ve onaylandı** (kaçak saldırısı/korku, ışınlama, PestWaddle ve yem test nesneleri dahil).
- Bekleyen: **Aşama 5 — GIANT mutasyon.** Kullanıcı iki farklı taslak gönderdi, ikisi de yarım kesildi: (a) `MutationType {None, Giant}` + eşik 1.8 + panelde "Mutasyon: Dev (Giant)" + kaçak Giant daha güçlü zıplar/iter; (b) "5A": `BugMutationFlags` bayrak enum'u, yalnızca veri + UI, davranış değişmez. `Assets/_Project/Scripts/Pests/BugMutationFlags.cs` (None, Giant) oluşturuldu, başka hiçbir şey yazılmadı. Kullanıcı kalan adımları/dosya limitini göndermeden ve planı onaylamadan başlama.
