# Harita, hikaye ve mağaza — tasarım

Durum: onaylandı (brainstorming), uygulama planı henüz yok.
Hedef platformlar: mobil (yatay) + web. PC/Mac de aynı kontrol şemasını kullanır.

## 1. Hikaye

Fırtına Çekiçköy'ü yerle bir etti. Sahile tek başına bir işçi çıkıyor, elinde çekicinden başka bir şey yok. Binalar yükseldikçe köye sakinler dönüyor.

Hikaye uzun metinle değil dünyayla anlatılır:

- **Açılış:** ~5 sn kamera akışı + tek satır altyazı.
- **Mahalle tanıtımı:** İlk girişte 1-2 satırlık konuşma balonu ("Liman çalışmıyor, balıkçılar bekliyor!").
- **Sakinler:** Biten binaya tek bir sakin taşınır, el sallar, küçük tepki verir.
- Metinler tek tabloda (`StoryBeats`), Paytone One Türkçe karakterleri destekliyor.

## 2. Dünya ve harita yapısı

Tek büyük ada **ayrı ayrı mahalle sahnelerinden** oluşur, oyuncuya tek ada gibi sunulur.

| Mahalle | Level | Plot | Dolgu |
|---|---|---|---|
| Sahil | 1 | 3 | Kulübe, ev |
| Merkez | 3 | 3 | Meydan, çeşme |
| Liman | 5 | 3 | İskele, vinç |
| Tepe | 7 | 3 | Değirmen, fener |
| Sanayi | 9 | 3 | Baca, fabrika |

- Toplam 15 plot. Mevcut 5 bina türü (kulübe → gökdelen) mahallelerde mahalleye özgü renk/dekorla yeniden kullanılır; yeni modelleme gerekmez.
- Aynı anda yalnızca **bir mahalle** yüklü (additive scene). Mahalleler arası yürüme yok; sınırı adanın kıyısı + su + görünmez duvar kapatır.
- İşçiler **yalnızca açık mahallede** çalışır. Çevrimdışı/uzaktayken kazanç yok.

### Dashboard (ana ekran)
- Sol üst: level, XP, para.
- Orta üst: dünya adı + sol/sağ oklar. İlk sürümde tek dünya; ikinci dünya "Yakında" olarak kilitli. Dünya bitince bir sonrakinin kilidi açılır. Yeni dünyalar sonraki güncellemelerle gelir, veri olarak eklenir.
- Gövde: adanın izometrik özeti, mahalle kartları (tamamlanma %, kilit + gereken level, sakin sayısı, "N sorun" rozeti). Kart seçilince o mahalle yüklenir.
- Kilit açılışı kamera sahnesi değil, dashboard'da kilit animasyonudur.
- Biten mahallelerde hareket eden sakinler, gece/gündüz gibi küçük canlılıklar eklenir (düz bir menü olmasın).

### Çoklu dünya altyapısı
- `MapDefinition`: bölgeler, plotlar, hikaye satırları, palet. Kayıt dosyasında harita kimliği tutulur.
- Başka dünya eklemek = yeni veri + sahne üretimi. Kod değişmez.

## 3. İlerleme: level ve XP

- Her biten bina XP verir (kulübe 1 birim, gökdelen ~12). Vuruş XP vermez.
- Level eşikleri `Balance.cs` tablosunda; mahalle kilidi level'a bağlı.
- Para yalnızca yükseltmelere gider; bölge açmak için ayrıca bedel yok.
- Level atlayınca: ortada "LEVEL N" `UiPlate`, yeni mahalle varsa dashboard'da kilit açılır. Atlanabilir, ~3 sn.

## 4. Kontrol

- **Tamamen otomatik vuruş** (mobil + PC + web): menzildeki en yakın plota kendiliğinden vurur. Space / sol tık / gamepad A kaldırılır.
- Mobil: sol altta sanal joystick (Input System on-screen stick), sağ altta küçük Kola butonu. PC: WASD / gamepad, E / X kola.
- Oyuncunun işçilerden ayrışması için: çekiç yükseltmesi oyuncuya özel etki versin (vuruş alanı ya da çift vuruş şansı); kola yalnızca oyuncunun elinde.
- Ekran yönelimi: yalnızca yatay.

## 5. Bakım / onarım (cezasız ödül)

- Sakinler yerleştikten sonra binalarda zamanla "sorun" çıkar. Hiçbir şey kötüleşmez, gelir kesilmez.
- Onarım mevcut vuruşla (kısa `BuildProgress`) yapılır; bitince para + küçük XP (ya da Sarı Kola şansı) ve sakinden teşekkür.
- Sınır: bina başına en fazla 1, mahalle başına en fazla 3 sorun. Birikme yok.
- Sorunlar gerçek zamanla (~20-30 dk) oluşur; saat uzaktayken işler ama üst sınır korunur. Dashboard kartında "N sorun" rozeti görünür.
- 3-4 tür (çatı sızıntısı, kırık boru, bozuk elektrik, çatlak duvar): farklı ikon + görsel efekt (çatıdan duman, eğik tabela, şikayet balonu), mekanik aynı.
- Sertleştirilmiş sürüm (onarılmayan binada gelir kaybı) bilinçli olarak kapsam dışı.

## 6. Mağaza ve mahalleye özel yükseltmeler

- Ekranın altındaki yükseltme butonları kaldırılır. Yerine **mahalle içi HUD'da** bir Mağaza butonu gelir. Dashboard'da mağaza yok (orada yalnızca para, level, mahalle seçimi).
- Buton dialog/yan panel açar: Çekiç, İşçi, Dayanıklılık, Sarı Kola. `UpgradeTrack` ve `Balance.HitsPerBar` bilgisi ("25 → 30 vuruş", enerji çubuğu etkisi) kartlarda görünür kalır.
- Mağaza açıkken **oyun durmaz**: enerji dolumu, işçiler, otomatik vuruş akar. Panel ekranın bir kısmını kaplar, oyun arkada görünür; joystick panel açıkken de çalışmalıdır (uygulamada test edilecek).
- Oyun ekranında yalnızca eylemler kalır: enerji barı, **Kola iç** butonu, Mağaza butonu.
- **Çekiç, Dayanıklılık ve İşçi mahalleye özeldir.** Her mahalle kendi `UpgradeTrack` setini ve işçi sayısını tutar, hepsi sıfırdan başlar. Başka mahallede alınan yükseltme geçmez.
- **Para** ve **Sarı Kola envanteri** (en çok 3) tüm mahallelerde ortak. Kola içilince yalnızca açık mahalledeki karakterler etkilenir. Parayla alınan kola hemen içilir ve 15 sn Coşku verir (mevcut davranış).
- Denge etkisi: her mahalle kendi küçük ilerleme eğrisi olur. Yükseltme baz maliyetleri mahalleye göre ölçeklenir ki sıfırdan başlamak tekrar gibi hissettirmesin. `tools/balance_sim.py` mahalle başına çalışacak şekilde güncellenir.

## 7. Kayıt

- JSON, yalnızca `Application.persistentDataPath` + `Path.Combine`; WebGL'de IndexedDB'ye gider, thread yok.
- İçerik: para, level, XP, kola sayısı, harita kimliği; mahalle başına biten plotlar, yükseltme seviyeleri, işçi sayısı ve aktif sorunlar.
- Yazma zamanı: her bina bitişinde, her 30 sn, mahalleden çıkışta, `OnApplicationPause`.
- Çevrimdışı kazanç **yok** (sorun üretimi hariç).

## 8. Mimari ve test

- Core (düz C#, birim testli): `LevelTrack`, `ZoneUnlocks`, `SaveData`, mahalle başına yükseltme durumu, onarım zamanlayıcısı. `Balance.Zones` tablosu.
- MonoBehaviour: dashboard, mahalle yükleyici, `StoryBeats`, mağaza dialog'u, on-screen stick.
- `GameSceneBuilder` mahalle başına `Ensure*` ile bölünür.
- Testler: LevelTrack, ZoneUnlocks, SaveData gidiş-dönüş, onarım üst sınırı. `tools/balance_sim.py` 15 plotu ve otomatik vuruşu kapsayacak şekilde güncellenir.
- Performans: kapalı mahalleler hiç yüklenmez; mobil/WebGL'de outline maliyeti tek mahalleyle sınırlı.

## Ek fikirler (kapsam dışı, sonra)

- Günlük giriş ödülü (Sarı Kola) ve basit görevler/başarımlar.
- Sakin kişilikleri (her biri bir tepki/şapka); şehir fotoğrafı çek/paylaş.
- Mevsim/hava olayları (fırtına bir sonraki onarım dalgasını tetikler).
- Mobil push bildirimi ("Çatı sızdırıyor!"), çevrimdışı kazanç, ses ve müzik.

## Açık kararlar

- Mahalle başına plot sayısı, level eşikleri ve mahalle başına yükseltme baz maliyetleri: `balance_sim.py` ile oturtulacak.
