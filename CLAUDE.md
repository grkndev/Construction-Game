# Construction — proje bağlamı

Casual, çizgi film tarzında basit bir inşaat oyunu (Unity 6, URP). Önceki "görev/süre/NPC" tasarımı iptal edildi.

## Ortam
- Unity editör: `/Volumes/ExternalSSD/Unity/6000.6.5f1/Unity.app/Contents/MacOS/Unity` (sistemdeki tek kurulum). Script'ler `UNITY=...` ile override edilebilir.
- Proje kökü: `/Volumes/ExternalSSD/Unity/Projects/Construction`. Geliştirme makinesi MacBook Air M4, Windows erişimi yok.

## Oyun
- Mobil (yatay) + web hedefli casual inşaat oyunu. Hikaye: fırtına Çekiçköy'ü yıktı, işçi köyü yeniden kuruyor. Metinler tek tabloda: `Core/StoryBeats.cs`.
- **Dashboard** (ana ekran): ada yukarıdan görünür, 5 mahalle kartı (Sahil, Merkez, Liman, Tepe, Sanayi; ilerleme %, kilit + gereken seviye, "N sorun"), sol üstte para + seviye, ortada dünya adı + oklar (şimdilik tek dünya, sağ ok "yakında"). Karta dokununca o mahalleye girilir; **HARİTA** butonu geri döner. Aynı anda tek mahalle yüklü/aktif; mahalleler arası yürüme yok.
- Her mahallede 3 bina. **Vuruş tamamen otomatik** (PC/mobil/web): oyuncu menzildeki en yakın binaya kendiliğinden vurur (`PlayerHammer`), işçiler de. Hareket: WASD / gamepad / mobilde ekran çubuğu (`OnScreenStick`, dokunmatikte `TouchControls` açar).
- Her vuruş = ilerleme + o kadar para. Biten bina bonus + konfeti + **XP** verir; XP **seviye** yapar, seviye mahalle kilidini açar (`Balance.LevelThresholds`, `DistrictDef.RequiredLevel`: 1/3/5/7/9). 5 mahalle bitince "Şehir tamamlandı".
- **Enerji:** her karakterin (oyuncu ve her işçi) kendi enerjisi var, kafasının üstündeki barda görünür (oyuncununki ayrıca HUD'da). Vuruş enerji harcar, durunca dolar; bitince %40'a dolana kadar o karakter bekler. Bunun kodu `World/Character.cs` (yeni karakter türü = bu bileşeni eklemek).
- Dayanıklılık etkisi **görünür** olmalı: HUD enerji çubuğu kapasiteyle uzar, sayıyla yazar, mağaza kartı "25 → 30 vuruş" gösterir (`Balance.HitsPerBar`), karakter çubukları da uzar.
- **Mağaza** (sadece mahalle içinde, **oyunu durdurmaz**): sağdan kayan panel (`UI/ShopPanel.cs`, kısayol B / gamepad Y). **Çekiç** (vuruş gücü), **İşçi** (her mahallede 1 ücretsiz işçiyle başlanır, en çok 8), **Dayanıklılık** (enerji kapasitesi +%20, yenilenme +%15 / seviye, en çok 10), **Hız** (en çok 5, seviye başı +%15: tüm karakterlerin yürüme, vuruş ve enerji toparlama hızı; `Balance.SpeedMultiplier`, Coşku ile çarpılır), **Sarı Kola**. Çekiç/İşçi/Dayanıklılık/Hız **mahalleye özeldir** (her mahalle sıfırdan başlar, `DistrictState`). **Para da mahalleye özeldir** (`DistrictState.Economy`, dashboard'da para görünmez); sadece seviye/XP ve kola envanteri ortaktır. Mağaza kartları "seviye/en çok" gösterir ve her şey 1'den başlar (`UpgradeTrack.Tier/MaxTier`: son seviyeye MaxTier-1 alımla varılır). İşçiler yalnızca açık mahallede çalışır.
- **Sarı Kola:** mahallede rastgele belirir, üstünden geçince cebe girer (en çok 3), HUD'daki butonla (E / gamepad X) içilir → açık mahalledeki **tüm karakterlerin** enerjisi dolar ve 10 sn boyunca vuruş gücü "seviye +5" gibi hesaplanır. Parayla alınan kola hemen içilir ve 15 sn **Coşku** verir: aynı +5 seviye, ayrıca herkes ×1.5 hızlı ve kimse enerji harcamaz.
- **Sakinler ve onarım:** biten binaya bir sakin taşınır (`World/Resident.cs`). Yaklaşık 25 dk'da bir biten bir binada sorun çıkar (bina başı 1, mahalle başı en çok 3; uzakta geçen süre de sayılır ama birikmez). Sorunlu bina otomatik vuruşla onarılır → para + XP + teşekkür. **Ceza yok**: bozuk bina gelir kaybettirmez (`Core/RepairSchedule.cs`).
- **Ses:** müzik + efektler **kodla sentezlenir** (`Editor/AudioSynth.cs` → `AudioBuilder` → `Assets/_Project/Audio/*.wav`, `setup.sh` eksikleri üretir; tarifi değiştirince menüden `Tools/Project/Rebuild Audio`). `Audio/AudioManager` yalnızca `GameController.Sfx` olaylarını dinler (yeni efekt = `Core/SfxKind` + sentez tarifi + olayı fırlat). İki müzik döngüsü (dashboard sakin, mahalle tempolu) çapraz geçişle çalar. Sağ üstte Müzik/Ses anahtarları (PlayerPrefs). `-demo`/`-shot` çalıştırmaları sessizdir (`-sound` ile açılır).
- **Kayıt:** `save.json` (`Core/SaveStore.cs`, `Application.persistentDataPath`); bina bitince, her 30 sn, mahalleden çıkınca, uygulama arka plana gidince yazılır. Çevrimdışı kazanç yok.

## Görsel stil
- Primitive'lerden üretilmiş, düz parlak renkler; kendi yazdığımız **toon shader** (`Art/Shaders/Toon.shader`: bantlı ışık, mor gölge tonu, ters-hull outline).
- Her materyal `ArtBuilder` ile üretilir, hepsi `Data/ToonPalette.asset` içinde toplanır. Yeni renk = `ArtBuilder.BuildPalette`.
- Font: Paytone One (OFL, Türkçe karakterleri tam). UI: TextMeshPro + UGUI, 1920x1080 referans, match 0.5. Ekran yazıları `UiPlate` (yarı saydam siyah zemin, yazıyla büyür); paneller/butonlar `UI/Panel.png` (hafif köşeli dikdörtgen, 9-slice).
- Sprite gotcha: `spriteImportMode` **Single** olmalı, Multiple'da importer border'ı yok sayılır ve 9-slice görsel elips gibi gerilir (`ArtBuilder.EnsureRoundedSprite` her çalıştırmada doğrular).
- Yüksek kalite ışık/texture kapsam dışı. Kamera: izometrik takip (`IsoCameraFollow`).

## Komutlar (hepsi batchmode, editör kapalı olmalı)
- `scripts/setup.sh [rebuild]` — TMP + ayarlar + sahne/prefab/materyal üretimi. `rebuild` sahneyi sıfırdan yazar.
- `scripts/compile.sh` — derle. `scripts/test.sh` — EditMode testleri.
- `scripts/build.sh mac|web` — build (`mac fast` = Mono, sadece hızlı görsel kontrol). `win` yazılı ama Windows modülü kurulu değil, denenmedi.
- Loglar `Logs/` altında; `error CS` ya da hata kodu varsa script ≠ 0 döner.
- Build'i görsel doğrulama: `Construction -shot dosya.png -shotDelay 5 [bayraklar]` (oyunun kendi karesini yazar; **`-nographics` kullanma**, kare bozuk çıkar; log için `-logFile`). Masaüstü ekran görüntüsü alma. Bayraklar (`Core/DevCommands.cs`): `-demo` otomatik oynar (mahalleleri sırayla gezer), `-enter N` mahalle aç, `-xp N`/`-coins N` ver, `-finish N` mahalleyi bitir, `-break N` iki binayı boz, `-shop` mağazayı aç, `-speed N` hızlandır (yüksek hızda kare süresi sınırı yüzünden vuruş sayısı bozulur, süre ölçmek için kullanma), `-reset` kaydı sil. `-demo` ve `-shot` gerçek kayda dokunmaz (test için `-persist`).
- Dengeyi `python3 tools/balance_sim.py` ile kontrol et (5 mahalle, her birinde yükseltmeler sıfırdan). Model şu an ~34 dk (tüm yükseltmelerle), gerçek oyun yürüme dahil ~42+ dk. Oyuncu vuruşu yavaş (0.35 sn), işçiler hızlı (0.8 sn): işçiler asıl iş gücü.

## Mimari
- `Core/` düz C# mantık (Economy, UpgradeTrack, BuildPlan/BuildProgress, DistrictDef/DistrictState, LevelTrack, RepairSchedule, SaveData/SaveStore, EnergyMeter, BoostTimer, StoryBeats, Balance) — **birim testli**, oyun mantığı MonoBehaviour'da değil. Denge sayıları sadece `Balance.cs`'te; değiştirirken `tools/balance_sim.py` ile toplam süreyi kontrol et (enerji oyunu ~2.4x yavaşlatır).
- `DistrictDef.Scale`: her mahalle birincisinin aynısı ama para/iş/fiyat/vuruş gücü `Scale` kat büyük (x4/mahalle); sonraki mahallelerde binalar ayrıca daha büyük (`Balance.Districts`'teki sayılar).
- `GameController` sahnenin kompozisyon kökü (modlar: Dashboard / InDistrict, kayıt, seviye, mağaza işlemleri); singleton yok, referanslar sahnede `EditorWire` ile bağlanır. Tek sahne (`Main.unity`): her mahalle ayrı kök GameObject (`District`), dashboard hepsini açar, mahalle ziyareti sadece birini.
- `Player/`, `World/`, `UI/` MonoBehaviour katmanı (`GameHud` mahalle içi, `DashboardHud` harita, `ShopPanel`). Yeni içerik: yeni bina = `Balance.Districts[..]`'e `BuildPlan`; yeni mahalle = `Balance.Districts` + `GameSceneBuilder.BuildDistrict`/`BuildLandmark`; yeni dünya = ileride `MapDefinition` (kayıtta `mapId` hazır).
- Tween için DOTween yok (OpenUPM'den kalkmış); `Easing.cs` + Update tabanlı animasyonlar.

## Çalışma kuralları
- `.unity/.prefab/.asset` YAML'ı elle yazma; Editor script'iyle üret (`GameSceneBuilder`, `ArtBuilder`). `Ensure*` yalnızca eksik olanı üretir, `Rebuild*` zorlar (fileID'ler rastgele olduğu için gereksiz yeniden üretme git diff'ini kirletir).
- `NewScene`'den **sonra** asset yükleme gotcha'sı: sahneyi önce oluştur, asset'leri sonra yükle (yoksa referanslar `fileID: 0` kaydolur).
- `.meta` dosyalarını silme/elle üretme. Binary asset'ler Git LFS'te (`.gitattributes`).
- Dosya yolu: yalnızca `Application.persistentDataPath` + `Path.Combine`. Thread yok (WebGL).
- Hazır asset eklenince `CREDITS.md`'ye işle.
- Commit: conventional commits, küçük ve anlamlı.
- Mac IL2CPP + WebGL ana hedefler; Windows (Mono) sonra. iOS/Android modülleri kurulu değil; dokunmatik kontrol WebGL build'i telefon tarayıcısında açarak denenir.
