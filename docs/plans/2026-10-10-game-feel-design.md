# Oyun hissi ve büyük anlar — tasarım (Faz 1)

> Durum: **uygulandı** (bkz. `CLAUDE.md` "Oyun hissi"). Planlanandan sapmalar: havai fişek `FeelFx` içinde (ayrı `Fireworks` sınıfı yok); bina boyunca ışık süpürmesi yerine parıltı sütunu.

Sıra (kullanıcı onayı): **1) his + büyük anlar**, 2) sıradaki hedef çubuğu, 3) tekdüzeliği kıran çeşitlilik.
Bu belge yalnızca Faz 1'i kapsar. Genel ilke: oyun sade ve baskısız kalır (süre/ceza/görev listesi yok, bkz. `CLAUDE.md`).

## Sorun
Oyuncu ne yapacağını bilmiyor, büyük anlar sönük, ilerleme tekdüze, vuruş/inşaat anı yeterince tatmin etmiyor.
Bugün: kıvılcım parçacığı, binada hafif ezilme, kat "pop" animasyonu, `+N` yüzen yazı, tek tip vuruş sesi;
bina bitişi = konfeti + yazı + ses; seviye atlama = yazı plakası; mahalle/şehir bitişi = ipucu yazısı.

## 1. Vuruş ve inşaat hissi
1. **Uçan paralar.** `+N` yazısı yerine kazanılan para küçük altınlar olarak binadan yayla para paneline uçar; panel her varışta sıçrar,
   sayı akarak artar. İşçi vuruşları 0,4 sn'de bir toplu gönderilir. Aynı anda en çok 24 altın.
2. **Vuruş ağırlığı.** Yalnızca oyuncunun vuruşunda çok hafif kamera sarsıntısı (~0,08 sn) ve kıvılcımla birlikte küçük toz bulutu.
   İşçi vuruşları sarsmaz.
3. **Kat çıkışı.** Her yeni katta toz halkası, ilerleme çubuğunda kısa parlama, ve **her katta bir perde yukarı çıkan** nota
   (bitişe doğru "merdiven" duyulur).
4. **Çekiç ritmi.** Vuruş anında hafif ileri atılma (anticipation) ve geri gerilme; mevcut dönüş animasyonu korunur.

## 2. Büyük anlar (kademeli tören)
- **Bina bitişi (küçük, ~2 sn):** kısa kamera yaklaşması (%10), konfeti + bina boyunca ışık süpürmesi, ortada "**<Bina> hazır!**" şeridi
  (bonus + XP), sakin dışarı çıkıp el sallar.
- **Seviye atlama (orta):** yıldız patlaması, büyük "**SEVİYE N**", XP çubuğu dolup taşar; yeni mahalle açıldıysa "**Yeni mahalle: X!**" kartı.
- **Mahalle tamamlanma (büyük, ~4 sn):** havai fişek, damga gibi "**<MAHALLE> TAMAMLANDI**", haritada o ile **bayrak**, kart yeşil, kısa fanfar.
- **Şehir tamamlama (final):** haritaya dönüş, tüm illerde bayrak + havai fişek, "Çekiçköy yeniden doğdu", yavaşça açılan bitiş kartı.
- Tören sırasında oyun durmaz; oyuncu istediği an devam eder.

## Mimari
- `GameController`'a olaylar: `HitLanded(site, point, coins, byPlayer)`, `FloorBuilt(site, floor)`, `BuildingCompleted(site)`,
  `DistrictCompleted(index)`, `LevelUp(level)`. Oyun mantığı değişmez, olaylar sadece haber verir.
- Yeni `Game.Feel` katmanı, her bileşen tek iş:
  `CoinFlight` (UI havuzu, uçuş + sayaç), `CameraKick` (`IsoCameraFollow`'a sönümlü ofset: sarsıntı ve bina yaklaşması),
  `Ceremony` (küçük/orta/büyük törenleri sıraya sokar, üst üste bindirmez), `Fireworks` (3D parçacık, koddan üretilir),
  `MapFlags` (haritada tamamlanan illere bayrak).
- Ses: `AudioManager` kat perdesini yükseltir; yeni fanfar/damga sesleri `AudioSynth` ile sentezlenir (dış dosya yok).
- Çıktılar mevcut HUD kitiyle (Panel/Button/Star vb.) çizilir; yeni görsel varlık gerekirse `UiKitBuilder`'a eklenir.

## Test ve doğrulama
- Birim testli saf mantık: para toplu gönderimi (0,4 sn), tören önceliği/sırası, kat perdesi hesabı.
- Görsel: `-shot` + yeni `-ceremony level|building|district` bayrağı ile ekran görüntüsü.
- Performans: `-stats` ile renderer/üçgen sayısı değişmemeli (efektler havuzlu ve sınırlı).

## Hata ve sınırlar
- Efekt oyunu asla bozmamalı: olay dinleyicisi yoksa sessizce geçilir; mahalleden çıkınca uçan altınlar ve törenler temizlenir.
- Mobil/WebGL için havuzlar sınırlı (altın 24, parçacık patlamaları sınırlı), kamera sarsıntısı kısa ve ayarlanabilir şiddette.

## Sonraki fazlar (bu belgenin dışında)
- Faz 2: **sıradaki hedef çubuğu** — tek yumuşak hedef ("Sonraki: Okul'u bitir"), süre/ceza yok, bitişte küçük ödül.
- Faz 3: tekdüzeliği kıran yeni olaylar/mekanikler (ayrıca tasarlanacak).
