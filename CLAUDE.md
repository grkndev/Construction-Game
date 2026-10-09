# Construction — proje bağlamı

Casual, çizgi film tarzında basit bir inşaat oyunu (Unity 6, URP). Önceki "görev/süre/NPC" tasarımı iptal edildi.

## Ortam
- Unity editör: `/Volumes/ExternalSSD/Unity/6000.6.5f1/Unity.app/Contents/MacOS/Unity` (sistemdeki tek kurulum). Script'ler `UNITY=...` ile override edilebilir.
- Proje kökü: `/Volumes/ExternalSSD/Unity/Projects/Construction`. Geliştirme makinesi MacBook Air M4, Windows erişimi yok.

## Oyun
- Küçük bir adada 5 inşaat alanı (plot). İşçiyi WASD / sol çubukla gezdir, aktif alanda **Space / sol tık / gamepad A** ile çekiç vur.
- Her vuruş = ilerleme + o kadar para. Bina kat kat yükselir, bitince bonus ve konfeti. Sıradaki plot açılır.
- 5 plotun hepsi baştan açık; oyuncu menzildeki en yakın plota vurur, işçiler en az işçisi olan plota dağılır.
- **Enerji:** oyuncunun enerjisi vuruşla azalır, durunca dolar; bitince %40'a dolana kadar vuramaz. Her plotun (şantiye) kendi enerjisi var, oyuncu ve işçi vuruşları onu da harcar, bitince plot "dinleniyor". Bu yüzden plotlar arasında gezmek gerekir.
- **Sarı Kola:** adada rastgele belirir, üstünden geçince cebe girer (en çok 3), **E / gamepad X** ile içilir → oyuncu + tüm plotların enerjisi dolar. Parayla alınan kola hemen içilir ve ayrıca 15 sn **Coşku** verir (vuruşlar ×2, enerji harcanmaz).
- Parayla: **Çekiç** (vuruş gücü), **İşçi** (otomatik çalışan yardımcı, en çok 8) ve **Sarı Kola**.
- 5 bina bitince "Şehir tamamlandı" görünür.

## Görsel stil
- Primitive'lerden üretilmiş, düz parlak renkler; kendi yazdığımız **toon shader** (`Art/Shaders/Toon.shader`: bantlı ışık, mor gölge tonu, ters-hull outline).
- Her materyal `ArtBuilder` ile üretilir, hepsi `Data/ToonPalette.asset` içinde toplanır. Yeni renk = `ArtBuilder.BuildPalette`.
- Font: Paytone One (OFL, Türkçe karakterleri tam). UI: TextMeshPro + UGUI, 1920x1080 referans, match 0.5.
- Yüksek kalite ışık/texture kapsam dışı. Kamera: izometrik takip (`IsoCameraFollow`).

## Komutlar (hepsi batchmode, editör kapalı olmalı)
- `scripts/setup.sh [rebuild]` — TMP + ayarlar + sahne/prefab/materyal üretimi. `rebuild` sahneyi sıfırdan yazar.
- `scripts/compile.sh` — derle. `scripts/test.sh` — EditMode testleri.
- `scripts/build.sh mac|web` — build (`mac fast` = Mono, sadece hızlı görsel kontrol). `win` yazılı ama Windows modülü kurulu değil, denenmedi.
- Loglar `Logs/` altında; `error CS` ya da hata kodu varsa script ≠ 0 döner.
- Build'i görsel doğrulama: `Construction -shot dosya.png -shotDelay 5 [-demo]` (oyunun kendi karesini yazar, `-demo` otomatik oynar). Masaüstü ekran görüntüsü alma.

## Mimari
- `Core/` düz C# mantık (Economy, UpgradeTrack, BuildPlan, BuildProgress, EnergyMeter, BoostTimer, Balance) — **birim testli**, MonoBehaviour yok. Denge sayıları sadece `Balance.cs`'te; değiştirirken `python3 tools/balance_sim.py` ile (enerjiyi de modeller) toplam süreyi kontrol et: enerji oyunu ~2.4x yavaşlatır.
- `GameController` sahnenin kompozisyon kökü; singleton yok, referanslar sahnede `EditorWire` ile bağlanır.
- `Player/`, `World/`, `UI/` MonoBehaviour katmanı. Yeni içerik (bina) = `Balance.Plans`'e satır.
- Tween için DOTween yok (OpenUPM'den kalkmış); `Easing.cs` + Update tabanlı animasyonlar.

## Çalışma kuralları
- `.unity/.prefab/.asset` YAML'ı elle yazma; Editor script'iyle üret (`GameSceneBuilder`, `ArtBuilder`). `Ensure*` yalnızca eksik olanı üretir, `Rebuild*` zorlar (fileID'ler rastgele olduğu için gereksiz yeniden üretme git diff'ini kirletir).
- `NewScene`'den **sonra** asset yükleme gotcha'sı: sahneyi önce oluştur, asset'leri sonra yükle (yoksa referanslar `fileID: 0` kaydolur).
- `.meta` dosyalarını silme/elle üretme. Binary asset'ler Git LFS'te (`.gitattributes`).
- Dosya yolu: yalnızca `Application.persistentDataPath` + `Path.Combine`. Thread yok (WebGL).
- Hazır asset eklenince `CREDITS.md`'ye işle.
- Commit: conventional commits, küçük ve anlamlı.
- Mac IL2CPP + WebGL ana hedefler; Windows (Mono) sonra.
