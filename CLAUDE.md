# İnşaat İşçisi Oyunu — Proje Bağlamı

## Ortam
- Unity editör: `UNITY="/Volumes/ExternalSSD/Unity/6000.6.5f1/Unity.app/Contents/MacOS/Unity"`
- Proje kökü: `/Volumes/ExternalSSD/Unity/Projects/Construction` (harici SSD, APFS)
- Geliştirme makinesi: MacBook Air M4. Windows makinesine erişim yok.

## Oyun
Casual 3D oyun. Oyuncu bir inşaat işçisi ve süre dolmadan inşaat görevlerini bitirmeye çalışıyor.

Döngü:
1. HUD aktif görevleri gösterir. Aynı anda 2–3 görev açıktır, sırayı oyuncu seçer.
2. Oyuncu araç deposundan (`ToolRack`) gerekli aracı alır. Tek elde tek araç taşınır ve taşırken hız düşer (araca göre değişir).
3. Aracı görev alanına (`TaskSite`) götürür. Among Us tarzı kısa (5–15 sn) bir 2D mini görev açılır.
4. Başarılı olursa bina bir aşama yükselir (tween ile) ve süreye bonus eklenir.
5. Sahada:
   - **Enerji içecekleri:** Havada dönüp zıplayan pickup'lar, süreli hız buff'ı verir.
   - **Yavaşlatan NPC'ler:** Yakınlarındayken oyuncunun hızı düşer, çarparlarsa taşınan araç düşer, etkileşimle sahadan kovulurlar.

Görsel stil:
- Blok/tuğla stili low-poly, düz renk paleti (6–8 renk), hafif parlak plastik materyal.
- Tek `DirectionalLight` ve basit gölge.
- Yüksek kaliteli ışık ve texture kapsam dışı.
- "LEGO" markası ve logolu tuğla tasarımı hiçbir yerde kullanılmaz.

## Platform ve teknoloji
- **Motor:** Unity 6 LTS, URP.
- **Hedefler:**
  - macOS: IL2CPP, Universal
  - Windows x64: Mono (Mac'ten IL2CPP ile Windows build'i alınamıyor)
  - WebGL: ikincil hedef, test ve paylaşım için
  - Mobil hedef yok.
- **Input:** Input System, `Player` ve `UI` olmak üzere iki action map. Klavye/mouse ve gamepad aynı action'lara bağlı.
- **UI:** UGUI + TextMeshPro. Canvas Scaler: Scale With Screen Size, match 0.5. Hem 16:10 hem 16:9 oranında doğru görünmeli.
- **Tween:** DOTween (OpenUPM üzerinden). Kurulamazsa dur ve bana sor.
- **Kamera:** Cinemachine. Varsayılan izometrik/yukarıdan takip kamerası. Third-person'a geçiş kolay olmalı, çünkü bu karar henüz kesin değil.
- **NPC yol bulma:** AI Navigation (NavMesh).

## Platform kısıtları (her kodda geçerli)
- **Thread kullanılmaz** (WebGL desteklemiyor): `Task.Run`, `Thread` yok. Coroutine ve ana thread'de async/await serbest.
- **Dosya yolları:** Kayıt için yalnızca `Application.persistentDataPath`. Yollar `Path.Combine` ile birleştirilir, hardcoded yol yok.
- **İsimlendirme:** Dosya ve klasör adlarında büyük/küçük harf tutarlı olmalı.
- **Platforma özel kod:** `#if UNITY_...` blokları ile ve tek bir yerde toplanır.

## Mimari

### Veri (ScriptableObject)
- `ToolData`: id, prefab, icon, carrySpeedMultiplier
- `TaskData`: requiredTool, miniGamePrefab, timeReward, buildStage referansı

Yeni içerik eklemek kod değil veri gerektirmeli.

### Bileşenler
- **`PlayerController`:** `CharacterController` tabanlı hareket.
- **`PlayerCarry`:** El soketinde tek item tutar. `Pickup`/`Drop` işlemlerini yapar.
- **`SpeedModifiers`:** Tüm hız buff ve debuff'larının (taşıma, içecek, NPC) toplandığı **tek** yer.
- **`ToolRack`:** Aracı verir, gecikmeli olarak yeniden spawn eder.
- **`TaskSite`:** Durum makinesi `Locked → WaitingForTool → InProgress → Done`.
- **`MiniGame`:** Soyut sınıf. `Begin(TaskData)`, `event Action<bool> Completed`, `protected void Finish(bool success)`. Host, hangi somut görevin çalıştığını bilmez.
- **`MiniGameHost`:** Mini görevi ayrı bir Canvas'ta oluşturur, input map'ini değiştirir, dünyayı duraklatır.
- **`TaskDirector`:** Açık görevleri, bina aşamalarını, süreyi ve kazan/kaybet durumunu yönetir.
- **Oyun durumu:** `Menu`, `Playing`, `MiniGame`, `Paused`, `GameOver`.
- **`ObjectiveMarker` + ekran dışı HUD göstergesi:** `WorldToScreenPoint`, ekran kenarına clamp, yöne göre döndürme.

### Kurallar
- **Duraklatma `Time.timeScale` ile YAPILMAZ.** Kendi pause servisimiz (`IPausable`) kullanılır.
  - Mini görev açıkken oyuncu ve NPC'ler durur, ama oyun süresi akmaya devam eder.
  - UI tween'leri duraklatmadan etkilenmez.
- **Sistemler arası iletişim:** C# event'leri ya da ScriptableObject event kanalları. Singleton kullanımı minimum tutulur, servisler tek bir bootstrap noktasında kurulur.
- **Saf oyun mantığı** (süre, görev durumları, hız hesabı) MonoBehaviour'lardan ayrı, düz C# sınıflarında yazılır ve EditMode testleriyle test edilir.

## Klasör ve assembly yapısı
```
Assets/_Project/
  Scripts/Runtime/      (Game.Runtime.asmdef)
    Core/ Player/ Tasks/ MiniGames/ NPC/ Pickups/ UI/
  Scripts/Editor/       (Game.Editor.asmdef)
  Tests/EditMode/       (Game.Tests.EditMode.asmdef)
  Data/  Prefabs/  Scenes/  Art/  Audio/  UI/
Assets/ThirdParty/      (üçüncü parti asset'ler)
```

## Claude Code çalışma kuralları
- **`.unity`, `.prefab` ve `.asset` YAML dosyalarını elle oluşturma veya düzenleme.**
  - Sahne, prefab ve ScriptableObject asset'leri Editor script'leriyle üretilir.
  - Bu script'ler `[MenuItem]` olarak tanımlanır, `-batchmode -executeMethod` ile de çalıştırılabilir ve idempotent olur (tekrar çalıştırılınca aynı sonucu üretir).
- **Derleme doğrulaması:** Her değişiklikten sonra batchmode ile derle. Logda `error CS` kalmamalı. Mantık değişikliklerinde EditMode testlerini de çalıştır.
- **Editör açıkken** batchmode aynı projeyi açamaz. Bu durumda zorlamadan bana söyle.
- **`.meta` dosyaları:** Asla silme veya elle üretme. Bir dosyayı taşırken `.meta` dosyasıyla birlikte taşı.
- **Placeholder görseller:** Gerçek asset gelene kadar Unity primitive'leri ve düz renk materyaller kullanılır.
- **Hazır asset kaydı:** Eklenen her hazır asset `CREDITS.md`'ye işlenir: ad, kaynak, link, lisans, kullanım yeri.
- **Açıklama:** Önemli mimari kararlarda kısaca "neden"i yaz. Amacım motorun iç işleyişini de öğrenmek.
- **Commit:** Küçük ve anlamlı commit'ler, conventional commits formatında.
- **Milestone sonu:** Her milestone bitince dur, yapılanları özetle ve onay bekle.

## Yol haritası
- **M0 – Kurulum:**
  - Paketlerin kurulumu.
  - Force Text serialization ve Visible Meta Files ayarları.
  - `.gitignore` ve `.gitattributes` (LFS).
  - asmdef'ler ve klasör yapısı.
  - Bootstrap ve Playground sahneleri.
  - `scripts/compile.sh`, `scripts/test.sh`, `scripts/build.sh`.
- **M1 – Dikey kesit:**
  - Gri kutularla saha, oyuncu hareketi ve kamera.
  - Tuğla `ToolRack` → duvar `TaskSite` → "tuğla diz" mini görevi (drag & snap) → duvar bir aşama yükselir.
- **M2 – Görev sistemi:**
  - Süre sayacı.
  - 2–3 eşzamanlı görev.
  - İşaretçi ve ekran dışı gösterge.
  - Kazan/kaybet ekranı.
- **M3 – Pickup'lar:** Enerji içeceği ve `SpeedModifiers` entegrasyonu.
- **M4 – NPC'ler:**
  - NavMesh ile hareket.
  - "Dolaş → yapış → kaç" durum makinesi.
  - Çarpınca aracı düşürme.
  - Kovma etkileşimi.
- **M5 – İçerik ve his:**
  - İkinci mini görev: vida sık ya da su terazisi.
  - Juice: screen shake, hit-stop, ses varyasyonu.
- **M6 – Bitiş:**
  - Menü, ayarlar, kayıt, duraklatma ekranı.
  - macOS, Windows ve WebGL build'leri.
  - GameCI ile GitHub Actions.
