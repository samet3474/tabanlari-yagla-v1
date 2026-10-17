# Nerede kaldık? — Tabanları Yağla

> Her aşamanın sonunda güncellenir. Projeye ara verip döndüğünde **önce bu dosyayı oku.**
> Ayrıntılı geçmiş: [GELISTIRME_NOTLARI.md](GELISTIRME_NOTLARI.md) (bu dosya ondan daha güncel).

**Son güncelleme:** 2026-10-17. Canlıda gerçek engelli eğitim (`main` `ac4cb60`, gh-pages kökü `e8777e3`). **Ana menü prototipi `/test/`'te** (`ana-menu-prototip` `1d713c6`), kullanıcının iPhone değerlendirmesini bekliyor.

---

## ▶ ANA MENÜ PROTOTİPİ — /test/'te, kullanıcının iPhone değerlendirmesini bekliyor (2026-10-17)

> Bu bölüm yalnızca `ana-menu-prototip` dalında. `main`'deki not, kullanıcının "main'e dokunma" talimatı nedeniyle güncellenmedi.
> Canlı oyun ve `main` değişmedi. Canlıya geçiş için ayrıca açık onay gerekir.

**Dal:** `ana-menu-prototip`, `main` `31565f6`'dan açıldı. Commit'ler (eskiden yeniye):
- `f1aad35` — yarım prototip.
- `e0533fd` — ara not.
- `1d713c6` — düzeltmeler ve son hâl.

**Yayın:** gh-pages `e82111e`; yalnızca `test/Build` (4 dosya) ve `test/olcum/Build` (4 dosya) değişti.

**Yedek:** `test-yedek-ana-menu-oncesi` → gh-pages `e8777e3` (prototip öncesi `/test/` = canlı derleme).

**Test adresi:** https://samet3474.github.io/tabanlari-yagla-v1/test/ — ölçüm sürümü `/test/olcum/`.

### Menü (Loadout) yeni düzeni
- **Üst:** solda logo, sağda altın sayacı (`PlayerData.coins`), altında sıralama düğmesi.
- **Orta:** karakter adı ve karakter okları. 3D karakter dönmüyor; hafif açılı ve bekleme animasyonuyla duruyor.
- **Arka plan:** Start sahnesinin sokak ortamının kopyası (`MenuBackdrop.prefab`, pistten uzakta), kendi menü kamerasıyla. Start'ın geçen kedi/rakun oyuncuları (`MenuActors`) çıkarıldı; ikinci karakter gibi görünüyordu.
- **Alt:** büyük OYNA (`LoadoutState.StartGame()` aynen).
- **En alt:** Karakterler (mağaza karakter sekmesi), Görevler, Mağaza, Ayarlar.
- **Yan seçiciler** (yalnızca sahip olunanlar varsa görünür): tema ve güçlendirme sol altta küçültülmüş, aksesuar sağda.
- **Eğitim perdesi:** OYNA dışını karartır; ok ve yazı OYNA'nın altında.
- **Güvenli alan** (`SafeAreaFitter`): yerelde `Screen.safeArea`, web'de CSS `env(safe-area-inset-*)` (`SafeArea.jslib`; şablonda `viewport-fit=cover` açık).
- **Pencereler:** 720 genişliğe göre çizilmiş ayarlar pencereleri dar ekranda `FitInsideParent` ile küçülür.
- **Kamera:** menüdeyken oyun kamerası kapalı, koşu başlarken geri açılır. `Camera.main` (TrackManager) değişmedi.
- **Sis:** Start'ın sis ayarı menüde kullanılır, koşuda eskisine döner.

### Değişen dosyalar
- **Oyun kodu (küçük):** `LoadoutState.cs` (menü sahnesi alanları, `ShowMenuScene`, `GoToCharacterStore`; alanlar boşsa eski davranış), `ShopUI.cs` (`openCharacterListOnStart`).
- **Yeni:** `_TabanlariYagla/Menu/` (`SafeAreaFitter`, `SafeArea.jslib`, `MenuCoinCounter`, `FitInsideParent`, `MenuBackdrop.prefab`, asmref).
- **Sahne:** `Scenes/Main.unity` — `MenuUygula` aracıyla kaydedildi; Start sahnesi değişmedi.
- **Araçlar:** `_TabanlariYagla/Editor/MenuUygula.cs` (tekrar çalıştırılabilir editör aracı), `_Dev/MenuShots.cs` (yalnızca test derlemesi).
- **Metin:** `tr.txt` → `ui.characters KARAKTERLER`.

### Test sonuçları
- **Eğitim ve oyun akışı:** son derlemede otomatik testler **96/96**, hata kaydı 0. Kapsam: OYNA, eğitim, normal koşu, duraklatma, odak kaybı, oyun sonu, ikinci şans, menüye dönüş, kayıt.
- **Ekran uyumu (Windows test derlemesi, pencereli, `MenuShots`):** 8 profilin hepsinde **39/39**.
  - Profiller: 360x640 (16:9), 375x667 (iPhone SE), 390x844 (iPhone 13), 390x844 + iPhone uygulama güvenli payı (üst 47 / alt 34), 360x800 (20:9) + Android payı, 412x915 (Pixel) + pay, 430x932 (Pro Max) + pay, 360x840 (21:9) + pay.
  - Denetimler: logo, altın, sıralama, karakter adı, OYNA, 4 alt düğme ve okların güvenli alan içinde olması ve üst üste binmemesi; yazıların kesilmemesi ve düğmeye sığması; alt düğmelerin dokunma genişliği; karakterin tamamen ekranda olması (yüksekliğin %41'i) ve arayüzün onu en çok %15 kapatması; ayarlar penceresinin sığması; "Karakterler" düğmesinin gerçek tıklama yoluyla mağazayı karakter sekmesiyle açması ve kapanınca menüye dönülmesi.
  - Görüntüler: `_araclar/menu/ekran/`.
- **Web (tarayıcı paneli, 390x844):** `/test/` açılıyor, menü doğru çiziliyor, eğitim perdesi doğru.
- **Gerçek cihaz:** iPhone ya da Android'de **hiç denenmedi.** Web'deki güvenli alan (`env()`) yalnızca tarayıcı panelinde yüklendi, gerçek çentikli cihazda doğrulanmadı.

### Bilinen sorunlar ve açık noktalar
- **RenderPass hatası:** Tarayıcı panelinin konsolunda "RenderPass: dimensions ... (16384 x 16384)" ve "EndRenderPass" hataları görüldü. Menü açıkken artmıyor (5 sn boyunca sayı sabit), açılış/başlık ekranında oluşuyor. Panelin grafik sınırına özgü olabilir; canlı sürümde de olup olmadığını kesin ayıramadım. iPhone'da `/test/olcum/` ile bakılmalı.
- **BestFit uyarısı:** "BestFit yalnızca dinamik yazı tiplerinde" uyarısı çıkıyor; LuckiestGuy dinamik değil. Alt şerit yazıları bu yüzden sabit boyuta alındı. Karakter adı ve altın sayısı en büyük boyutta çiziliyor (şimdiki değerlere sığıyor).
- **Işık:** Start'ın ışık haritası ve yansıma probu taşınmadı (gerçek zamanlı ışık); görüntüler iyi ama Start'tan biraz farklı olabilir.
- **Kapsam dışı:** Görevler ve Sıralama pencereleri ekran görüntüsünde sığıyor ama sayısal sığma denetimi yalnızca Ayarlar için yapıldı.

### Devam
1. Kullanıcı iPhone 13'te `/test/`'i değerlendirir. Eğitimi görmek için `/test/olcum/?kayitsil=1`.
2. Geri bildirime göre düzeltme. Değişiklikler `MenuUygula.Uygula` ile yeniden uygulanır, sonra `MenuShots` ve eğitim testleri tekrar çalıştırılır.
3. Onaylanırsa canlıya geçiş (0. bölümdeki adımlar), **yalnızca açık onayla**.
4. **Yeniden çalıştırma:**
   - `Unity.exe -batchmode -projectPath <proje> -executeMethod MenuUygula.Uygula`
   - Ekran denetimi: `_araclar/menu/ekranlar.ps1` (Windows test derlemesi gerekir).

---

## 0. Çalışma düzeni (kalıcı kural)

**Çalışan sürümü koru, geliştirmeleri test et, onaydan sonra güncelle.**

1. **Çalışan sürüm**
   - Adres sabit: https://samet3474.github.io/tabanlari-yagla-v1/ (gh-pages kökü).
   - Kaynağı `main`. Onaysız değiştirilmez.
2. **Geliştirme**
   - `gelistirme` dalında; iş dalları bundan açılır ve ona birleşir.
3. **Test**
   - Adres: https://samet3474.github.io/tabanlari-yagla-v1/test/. Kaydı canlı oyundan ayrı.
   - Eski test klasörleri silinmez, onlara dokunulmaz.
4. **Her geliştirmenin sonunda test raporu**
   - Neler değişti, testler ve sonuçları, riskler, bağlantı, dal ve commit.
   - "Çalışan sürüm değişmedi" kanıtı: aşağıdaki git karşılaştırması.
5. **Onay sonrası ana yayın** (yalnızca açık onayla)
   1. Son kontrol.
   2. Yeni geri dönüş etiketleri: `canli-<tarih>-kaynak` ve `canli-<tarih>-yayin`.
   3. Dal → `main` (`--no-ff` birleştirme).
   4. `/test/`'teki onaylı dosyalar yeniden derlenmeden köke kopyalanır. Yalnızca `Build/`, `StreamingAssets/`, `TemplateData/`, `index.html`, `manifest.webmanifest`.
   5. Doğrulama ve rapor.
   - Geri alma: kök dosyalar etiketten yeni bir commit'le geri yüklenir (zorla push yok).

"Çalışan sürüm değişmedi" kanıtı (gh-pages kopyasında):
```bash
git diff --stat canli-2026-10-12-yayin HEAD -- .nojekyll index.html manifest.webmanifest Build StreamingAssets TemplateData   # boş çıkmalı
git diff --stat canli-v1-yayin HEAD -- dev test-ad test-dil                                                                  # boş çıkmalı
```

## 1. Kısaca durum

| Konu | Durum |
|---|---|
| Canlı oyun (kök adres) | **2026-10-12 sürümü**: Aşama 1 + Türkçe arayüz + menüye dönmeden normal koşu + gerçek engelli eğitim. Etiketler `canli-2026-10-12-kaynak` / `canli-2026-10-12-yayin`. Önceki A sürümü `canli-v1-*` etiketlerinde. |
| Test adresi | Canlıyla aynı derleme (`egitim-engelli` `53103e0`); bekleyen yeni aday yok. |
| Eğitim | SAĞA → SOLA → YUKARI → AŞAĞI → HAZIRSIN, her adımda normal oyunun gerçek bir engeli (çöp kutusu ×2, alçak bariyer, yüksek bariyer). Windows otomatik testleri **96/96**, hata kaydı 0. **Kullanıcı iPhone 13'te onayladı, canlıda.** (Önceki engelsiz sürüm `egitim-yeni` reddedilmişti.) |
| Eski eğitimin sorunları (A, B, C, ~9 sn bekleme) | Yeni tasarımda bu akış yok (üçer engel, oyunu durduran yönerge, seviye geçişi kaldırıldı). Eski düzeltmeler `egitim-duzeltme` dalında duruyor. |
| Dokunma yönünün erken seçilmesi | Değiştirilmedi. Yeni eğitimde yanlış yön adımı ilerletmez ve karakteri oynatmaz; iPhone'da `/test/olcum/` ile "FARKLI EKSEN" sayısına bakılmalı. |
| Gerçek iPhone 13 testi | Gerçek engelli eğitim kullanıcı tarafından denendi ve onaylandı (2026-10-12). |

## 2. Adresler

| Adres | İçerik | Kayıt klasörü (IndexedDB) |
|---|---|---|
| https://samet3474.github.io/tabanlari-yagla-v1/ | **Canlı oyun** (2026-10-12: gerçek engelli eğitim) | `/idbfs/4ebc64a6…` |
| https://samet3474.github.io/tabanlari-yagla-v1/test/ | Test adresi (şu an canlıyla aynı derleme) | `/idbfs/7e1905a8…` |
| https://samet3474.github.io/tabanlari-yagla-v1/test/olcum/ | Aynı aday, **ölçüm derlemesi** (ekranda adım/kare kayıtları) | `/idbfs/f90ba6cc…` |
| https://samet3474.github.io/tabanlari-yagla-v1/test-dil/ | Eski test: Aşama 1 + Türkçe (= önceki /test/) | `/idbfs/5e701c6c…` |
| https://samet3474.github.io/tabanlari-yagla-v1/test-ad/ | Eski test: yalnızca Aşama 1 | `/idbfs/811705a2…` |
| https://samet3474.github.io/tabanlari-yagla-v1/dev/ | Eski B test sürümü | `/idbfs/80587b4b…` |

Kayıt klasörü = sondaki `/` olmadan klasör adresinin MD5 özeti; her adresin kaydı ayrı.

Ölçüm adresinde:
- `?kayitsil=1` → eğitimi baştan başlatır.
- İki parmakla dokunma → ölçüm katmanını açıp kapatır.
- `?kaydirtest=dogru|yanlis|egik|hizli` → yapay kaydırmalarla eğitimi oynatır (tarayıcı paneli görünür olmalı; gizliyken 1 kare/sn).

Not: /test/ eğitimini bir kez bitiren tarayıcıda eğitim tekrar açılmaz. Yeniden görmek için /test/olcum/?kayitsil=1 kullan.

## 3. Dallar, commit'ler ve geri dönüş noktaları

| Dal | Commit | Ne var |
|---|---|---|
| `main` | `ac4cb60` | Canlı oyunun kaynağı (`egitim-engelli` `--no-ff` birleştirmesi) |
| `gelistirme` | `638289b` | Ana geliştirme dalı: main + Aşama 1 + Türkçe arayüz |
| `egitim-engelli` | `53103e0` | egitim-yeni + gerçek engelli eğitim. **Canlıda** (main'e birleşti). |
| `egitim-yeni` | `378c04a` | egitim-duzeltme + engelsiz ok eğitimi (egitim-engelli'nin içinde) |
| `egitim-duzeltme` | `502d3c7` | gelistirme + B + eski eğitimin düzeltmeleri (egitim-yeni'nin içinde) |
| `ad-marka` | `4c9244a` | Aşama 1 (gelistirme'nin içinde) |
| `dil-turkce` | `638289b` | Türkçe arayüz (= gelistirme) |
| `b-tutorial-test` | `d5f0f2f` | B (eğitim akışı) — egitim-duzeltme'nin içinde |
| `b-egitim-olcum` | `2188d73` | B + girdi teşhis araçları — egitim-duzeltme'nin içinde |
| `gh-pages` | `e8777e3` | Yayın: kök (canlı), /dev/, /test-ad/, /test-dil/, /test/, /test/olcum/ |

Geri dönüş etiketleri (hepsi GitHub'da):

| Etiket | Gösterdiği | Anlamı |
|---|---|---|
| `canli-2026-10-12-kaynak` | main `ac4cb60` | **Şu anki canlı oyunun kaynağı** |
| `canli-2026-10-12-yayin` | gh-pages `e8777e3` | **Şu anki canlı yayın** |
| `canli-v1-kaynak` | `0638f38` | Önceki canlı (A) sürümünün kaynağı — geri dönüş |
| `canli-v1-yayin` | gh-pages `b2bf7c0` | Önceki canlı (A) yayını — geri dönüş |
| `test-yedek-egitim-yeni` | gh-pages `5d9bd72` | Gerçek engelli eğitimden önceki /test/ (engelsiz ok eğitimi) |
| `test-yedek-egitim-duzeltme` | gh-pages `8bbf337` | Yeni eğitimden önceki /test/ (eski eğitim + düzeltmeler) |
| `egitim-yeni-oncesi` | `502d3c7` | Yeni eğitimden önceki kaynak |
| `test-yedek-2026-10-12` | gh-pages `330d9ed` | Eğitim düzeltmesi öncesi /test/ (= /test-dil/) |
| `yayin-A` | gh-pages `c543b23` | Daha eski yayın |
| `asama1-oncesi`, `dil-oncesi`, `egitim-olcum-oncesi` | `97135aa`, `4c9244a`, `d5f0f2f` | Aşama öncesi noktalar |

## 4. Son yapılanlar (en yeni üstte)

0. **Canlıya geçiş — 2026-10-12** (kullanıcının açık onayıyla; iPhone 13'te onaylanan `/test/` derlemesi).
   - Hazırlık kontrolleri:
     - Otomatik testler 96/96, hata kaydı 0, 50 ms'yi aşan kare yok.
     - Üretim paketinde test araçlarına ait iz yok; aynı arama `/test/olcum/`'da hepsini buluyor.
     - Üretim derlemesi `BuildOptions.None` ile alınıyor; WebGL tanımlarında `TY_DEVTOOLS` yok.
     - Türkçe eğitim metinleri pakette var.
     - Kayıt biçimi aynı (sürüm 12).
     - `main`'e birleştirme denemesi çakışmasız çıktı.
   - Kaynak: `egitim-engelli` → `main` `--no-ff` birleştirme `ac4cb60` (oyun kodu = `53103e0`). Etiket: `canli-2026-10-12-kaynak`.
   - Yayın: gh-pages `e8777e3`. Etiket: `canli-2026-10-12-yayin`.
     - `test/`'teki dosyalar yeniden derlenmeden köke kopyalandı: `Build/` 4 dosya, `StreamingAssets/` 5 dosya, `TemplateData/` 7 yeni dosya, `index.html`, yeni `manifest.webmanifest`.
     - Eski 2 Addressables paketi (`monoscripts_9d48…`, `characters_assets_all_6913…`) bilerek silinmedi: geçiş anında açık eski oturumlar 404 almasın diye.
   - Doğrulama:
     - Canlıda sunulan wasm, data, framework, loader, index, manifest ve catalog.hash, onaylı `/test/` ile SHA1 olarak aynı.
     - `/dev/`, `/test-ad/`, `/test-dil/`, `/test/` değişmedi.
     - Canlı adres hatasız açılıyor.
     - Kayıt klasörü değişmedi (adres aynı): mevcut oyuncuların kayıtları korunuyor. Eğitimi bitirmiş oyuncular eğitimi tekrar görmez.
   - Geri alma (zorla push yok):
     - gh-pages kökü: `git checkout canli-v1-yayin -- Build StreamingAssets TemplateData index.html`, `git rm manifest.webmanifest`, ardından yeni bir commit ve push.
     - `main`: `git revert -m 1 ac4cb60`.
   - Bilinen risk: GitHub Pages önbelleği 10 dakika (`max-age=600`) ve dosya adları sabit. Geçişten sonraki ilk dakikalarda bir tarayıcı eski ve yeni dosyaları karışık alırsa sayfayı yenilemek gerekebilir. Kalıcı çözüm: Name Files As Hashes (ayrı iş).

1. **Gerçek engelli eğitim** (`egitim-engelli`). Commit'ler (eskiden yeniye):
   - `39af35f` — Parkur: belirli mesafe ve şeride tek engel yerleştirme (`TrackManager.SpawnObstacleAt`).
   - `7b89d7c` — Girdi: erken zıplama/kaymayı tutma, yeniden denemede şeride dönüş.
   - `fe39adc` — Eğitim ekranı: ok ufkun üstünde, "GÜZEL!" ve "TEKRAR DENE!".
   - `53103e0` — Gerçek engelli akış, metinler, test araçları (S1–S11).
   - /test/ ve /test/olcum/ yayında (gh-pages `b2c7e5e`).
2. **Engelsiz ok eğitimi** (`egitim-yeni`; iPhone'da denendi, engel olmadığı için reddedildi). Commit'ler (eskiden yeniye):
   - `ddb5a5f` — Eğitim ekranı (`TutorialOverlay`): yarı saydam yönerge şeridi, kayan altın ok, adım noktaları.
   - `882c260` — Eğitim pisti engelsiz; eğitim bitince sisin ötesindeki (50 m+) parçalara engel ve altın konur.
   - `378c04a` — Yeni akış ve girdi kapısı (`HandleMoveInput`), "HAZIRSIN!", test araçları.
   - /test/ ve /test/olcum/ yayında (gh-pages `5d9bd72`).
3. **Eski eğitimin düzeltmeleri** (`egitim-duzeltme`; yeni tasarımla akışı değişti). Commit'ler (eskiden yeniye):
   - `5759135` — B akışı ve teşhis araçları birleştirildi.
   - `f824961` — "GEÇTİN!" ve "HARİKA!" dil tablosuna taşındı.
   - `09a4f4b` — Adım ölçer eklendi (yalnızca test derlemesi).
   - `3e7c111` — Yönerge engele kalan gerçek mesafede açılıyor (Sorun B).
   - `e234205` — Adım, oyuncunun durumuyla tutarlı (Sorun A).
   - `8846946` — Şerit kararı engel yüklenmeden verilmiyor.
   - `cbb6cc1` — Çarpma bayrağı sıfırlanıyor; doğru ilk hareket artık sayılıyor.
   - `75e3ca3`, `20f14d6`, `502d3c7` — Test araçları.
   - /test/ ve /test/olcum/ yayında (gh-pages `8bbf337`).
4. **Geliştirme/test düzeni kuruldu.** Etiketler, `gelistirme` dalı, `/test/` adresi.
5. **Eğitim dokunma sorunu ölçüldü** (`b-egitim-olcum`).
6. **Türkçe arayüz** (`638289b`) ve **Aşama 1 — ad ve marka** (`4c9244a`).

## 5a. Gerçek engelli eğitim — nasıl çalışıyor (egitim-engelli)

| Adım | Engel (normal oyunun Day teması prefabı) | Engel şeridi | Oyuncu | Doğru hareket |
|---|---|---|---|---|
| 1 SAĞA | `ObstacleBin` (SimpleBarricade) | orta | orta | sağ şeride geç |
| 2 SOLA | `ObstacleBin` | sağ | sağ | orta şeride dön |
| 3 YUKARI | `ObstacleLowBarrier` (AllLaneObstacle, 0,61 m) | üç şerit | orta | üstünden zıpla |
| 4 AŞAĞI | `ObstacleHighBarrier` (AllLaneObstacle, alt kenar ~1,0 m) | üç şerit | orta | altından kay |

- Yerleşim (`GameState`, `TrackManager.SpawnObstacleAt`): 1. engel geri sayımda 28 m önde; sonraki engel, önceki adımın hareketi başlayınca sis sınırında (~45 m, önceki engelden en az 30 m sonra). Rastgele engel/altın eğitimde kapalı.
- Yönerge engele 14 m kala (1,4 sn) açılır; ok ufkun üstünde, engeli ve karakteri kapatmaz.
- Erken yukarı/aşağı kaydırma tutulur, engeli aşacağı anda yapılır (zıplama 3,6 m, kayma 5 m). Hareket gelmezse son metrelerde koşu yavaşlar (zaman ölçeği 0,35; durmaz).
- Adım ancak üçü birden olunca geçer: doğru hareket başladı + hareket bitti (şeride varış / yere iniş / kaymanın sonu) + engel çarpmadan 1,2 m geride.
- Yanlış hareket yok sayılır (karakter oynamaz). Çarpma normal oyundaki gibi (sendeleme), eğitimde can gitmez → "TEKRAR DENE!", karakter adımın şeridine döner, 40 m önde yeni engel. Sonsuz kilit yok (hareketsiz oyuncu testi).
- Son engelden sonra "HAZIRSIN!" (1,6 sn), kayıt, aynı koşu normal koşu (50 m ötesindeki parçalar engel/altın alır; ilk normal engel ~6 sn sonra).
- Süre (Windows oto-test): OYNA'dan bitişe 0,5 sn tepkiyle ~19,4 sn, 1,0 sn tepkiyle ~20,6 sn (geri sayım 3,3 + dört engel 14,4–15,6 + HAZIRSIN 1,6).
- Performans (Windows): en uzun kare 20 ms, 50 ms'yi aşan kare yok, GC adım başına 0–1.

## 5. Eski eğitim — doğrulanan nedenler ve sonuçlar (egitim-duzeltme; yeni tasarımda bu akış yok)

| Sorun | Doğrulanmış neden | Düzeltme | Önce → Sonra (Windows ölçümü) |
|---|---|---|---|
| A — oyuncu önceden hareket edince eğitim yine aynı hareketi istiyor | Yönerge oyuncunun şeridine hiç bakmıyordu. Yönergeden önce şerit değiştirilebiliyordu. Yönerge açılınca doğrulama bayrağı sıfırlanıyordu. | Yönerge anında oyuncunun durumu kontrol ediliyor: boş şeritteyse ya da engeli aşacak zıplama/kaymadaysa yönerge açılmıyor. Engeli aşamayacak erken zıplama/kayma yönergede bitiriliyor. | S8 testi: önceden yapılan doğru hareketle yönerge açılmadı ve engel geçildi (3/3 hareket) |
| B — talimat geç açılıyor | Yönerge segmentin %10'unda açılıyordu. | Hareket fiziğinden türetilen mesafe: şerit 8 m, zıplama 3,9 m, kayma 5,5 m. | Engele 1,7–2,7 m (0,2 sn) → şerit 7–8 m, zıplama 3,7–3,9 m, kayma 5,4–5,5 m |
| (Yeni bulunan) doğru ilk hareket sayılmıyor | Adımlar arasındaki sayılmayan engelde çarpma olursa bayrak açık kalıyor, sonraki adımın ilk engeli "çarpıldı" sayılıyordu (Unity örneğinden). | Sayım her yeni adımda temiz başlıyor. | Ölçüm kaydında 4/4 tekrarlanıyordu → testlerde artık yok |
| C — takılarak ilerleme | Windows'ta eğitim boyunca 50 ms'yi aşan kare yok, GC adım başına 0–3. Asıl gecikme mantık kaynaklı: 3. engelden sonra önceden oluşturulmuş 3–4 eski bölge segmenti bitene kadar ~9 sn yeni yönerge yok. Ayrıca yönerge oyunu sert durduruyor ve müziği kesiyor (tasarım). | Performans tarafında değişiklik yapılmadı (ölçüm sorun göstermedi). iPhone'da `/test/olcum/` ile ölçülmeli. Mantık gecikmesi karar bekliyor (6. bölüm). | — |

## 6. Bekleyen kararlar ve onaylar

- [ ] **Gerçek engelli eğitimi iPhone 13'te dene** (/test/; eğitimi görmek için ilk kez açılan tarayıcı ya da /test/olcum/?kayitsil=1). Kontrol et:
  - Engeller zamanında görünüyor mu, yönerge (14 m) yeterince erken mi?
  - Kaydırmalar ilk seferde algılanıyor mu; erken zıplama/kayma doğru anda yapılıyor mu?
  - Son metrelerdeki yavaşlama rahatsız ediyor mu? Çarpınca "TEKRAR DENE!" anlaşılır mı?
  - Toplam ~20 sn uzun mu? (kısaltma: sonraki engeli daha yakına koymak = görünür belirme riski)
- [ ] **iPhone ölçümü:** `/test/olcum/?kayitsil=1`. Takılma anında ekran görüntüsü: ADIM/KARE/SICRAMA satırları adım zamanlarını ve kare sürelerini, KAYDIR satırları dokunmaların nasıl algılandığını gösterir.
- [ ] **Ayarlardan eğitimi yeniden başlatma** (isteğe bağlı, bu aşamada yapılmadı).
- [ ] **Dokunma yönünün erken seçilmesi:** iPhone ölçümünde "FARKLI EKSEN" sayısı görülürse eşik/karar düzeltmesi (ayrı iş).
- [x] **Canlıya geçiş:** 2026-10-12'de kullanıcının açık onayıyla yapıldı (4. bölüm, madde 0).
- [ ] **Önbellek iyileştirmesi** (Name Files As Hashes) — ayrı test değişikliği olarak.

## 7. Sıradaki adım (döndüğünde buradan başla)

1. **Ana menü incelemesi** (Subway Surfers düzenine yakın, özgün görseller). Önce yalnızca inceleme ve rapor; kod değişikliği, commit ve yayın yok. Uygulama için ayrıca onay beklenir.
2. Sonra: Aşama 2 (performans/bellek), önbellek iyileştirmesi (Name Files As Hashes), Android APK.

## 8. Başka bilgisayarda devam etmek

```bash
git clone https://github.com/samet3474/tabanlari-yagla-v1.git
cd tabanlari-yagla-v1
git checkout egitim-engelli   # /test/'teki sürüm (en güncel)
```

- Git LFS kurulu olmalı. Unity 6000.3.26f1.
- Otomatik eğitim testleri: Windows geliştirme derlemesi (`DevBuild.BuildWindowsDev`), sonra `TabanlariYaglaDev.exe -tutorialTest -timeline -batchmode`.
  - Pencere odağını kaybedince oyun duraklar ve testler takılır; bu yüzden `-batchmode` önerilir.
  - Senaryolar S1–S11 (`Assets/_Dev/TutorialAutoTest.cs`); son çalıştırma 96/96.
  - `-tutorialShots <klasör>` (pencereli, `-batchmode` olmadan): yalnızca S1, her adımda ekran görüntüsü.
- Yayın dalı için ayrı klasör: `git clone --branch gh-pages --single-branch https://github.com/samet3474/tabanlari-yagla-v1.git v1-pages`
- Unity her açılışta `AddressableAssetsData/link.xml` dosyasını siler; bu normal. `git restore` ile geri al.
- `addressables_content_state.bin` ve `TabanlariYagla-V1.slnx` derleme çıktısıdır, commit'e katılmaz.
