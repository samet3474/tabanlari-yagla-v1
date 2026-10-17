# Nerede kaldık? — Tabanları Yağla

> Her aşamanın sonunda güncellenir. Projeye ara verip döndüğünde **önce bu dosyayı oku.**
> Ayrıntılı geçmiş: [GELISTIRME_NOTLARI.md](GELISTIRME_NOTLARI.md) (bu dosya ondan daha güncel).

**Son güncelleme:** 2026-10-18. **Canlıda ana menü + tek yükleme ekranı** (`main` `ea229d9`, gh-pages kökü `4924636`). Önceki canlı: gerçek engelli eğitim (`ac4cb60` / `e8777e3`).
**Geliştirmede:** `istanbul-tema` dalı (`main` `e16c54d`'den). İstanbul teması: simit tezgâhı + ilk İstiklal test parçası. Yayın yok, `/test/` değişmedi.

---

## ▶ İSTANBUL TEMASI — AŞAMA 1: İLK İSTİKLAL TEST PARÇASI (2026-10-18, `istanbul-tema`)

> Kullanıcı onayıyla commit edildi ve yalnız `istanbul-tema` dalına push edildi. Yayın yok; `main`, `gh-pages`, `/test/` değişmedi.
> Sonraki adım (Galata, Eminönü, yeni parçalar) için kullanıcı kararı bekleniyor.

**Commit'ler:**
- `f8a9cf1`: İstiklal test parçası (prefab, `IstiklalSokak.asset`, Addressables kaydı, `IstanbulUygula.cs`).
- `341f1ce`: geliştirme testi `IstiklalTest.cs`.
- Bu not: ayrı commit.
- Commit dışı: `addressables_content_state.bin`, kullanıcı kaydı, `Build/`, depo dışındaki `_araclar/istanbul/*`.
- Yedekler: `_araclar/istanbul/yedek-asamaA-20261018-002238/`.

**Yapılan:** tek bir test parçası, `Assets/_TabanlariYagla/Istanbul/Test/Istiklal/IstiklalTest01.prefab`.
- Day temasındaki `UrbanBuilding01`'in kopyası. Korunanlar: yol (Road01/02), Entry z=-4,49 → Exit z=13,50 (18 m), şerit aralığı 1,5, sokak lambası, uzak arka plan, engel noktası `[0,5]`.
- Sanayi binaları ve borular yerine tek bir mesh (`IstiklalSokak.asset`, 1.388 üçgen). Malzeme projede zaten olan `VCOL` (CurvedUnlit, dokusuz, renk köşe renginden).
  - Cepheler: özgün, yazısız ve markasız; 3–4 katlı pastel cepheler, kat kornişleri, pencereler, balkonlar, cumba, dükkân camları ve renkli tenteler.
  - Ortada tek hatlı tramvay rayları ve açık renk taş döşeme. Çarpışma kutusu yok, yalnız görsel.
- Olası engeller: mevcut alçak bariyer, koni ve çöp kutusu + simit tezgâhı. Tek engel noktası olduğundan simit en fazla 2 kez çıkar.
- Addressables: `Istanbul-Test` grubuna `IstiklalTest01` kaydı (grup dosyasına 5 satır). `AddressableAssetSettings.asset` **değişmedi**.
- Tema, Day/Tutorial, kayıt formatı, `TrackManager`, oyuncu kodu, `Road01/02.fbx`, `WorldCurver` ve ortak shader'lar değişmedi. Yeni Meshy üretimi ve indirme yok.
- Araçlar:
  - `IstanbulUygula.IstiklalParca`: parçayı yeniden üretir.
  - `IstanbulUygula.ParcaDok`: salt okunur döküm.
  - `Assets/_Dev/IstiklalTest.cs` (`-istiklalTest`): yalnız geliştirme derlemesinde çalışır; Day bölgelerini yalnız bellekte bu parçaya çevirir.
  - Çalıştırma betiği: `_araclar/istanbul/istiklal_test.ps1`.

**Görsel düzeltme — eğrilik (2026-10-18):**
- Belirti: uzakta taş döşemenin yerine asfalt ve asfalt çatlakları görünüyordu.
  - İlk raporda "çatlaklar döşemenin üstünde" denmişti; bu **yanlıştı**. Gerçekte döşeme uzakta yolun altında kalıyordu.
- Kaynak: `Road01/02.fbx` içinde, orta şeritte (x ±0,4 m) yüzeyden 0,4 cm yüksekte duran koyu köşe renkli poligonlar (çatlaklar).
- Neden: `CurvedUnlit` köşe başına `y -= 0,005 × uzaklık²` uygular (Main: `curveStrength` 0,005).
  - 18 m'lik tek döşeme dörtgeni ortasında ~40 cm düz kalıyordu. Yolun köşeleri en fazla ~1 m arayla dizili, eğriyi izliyordu.
- Çözüm: yalnız `BuildIstiklalMesh` değişti. Döşeme, oluklar ve raylar 18 adet 1 m'lik parçaya bölündü; sapma ~1 mm, pay 1,2 cm.
  - Mesh dosya kimliği korundu (`14452df9…`), prefab bayt bayt aynı.
  - Üçgen 1.090 → 1.388.
- Ölçüm: yol ortasında döşeme oranı, düzeltmeden önce çoğu satırda %0 (asfalt %80–100); sonra her mesafede %71–88 (kalan kısım raylar, derzler ve karakter).
  - Karşılaştırma görüntüleri: `_araclar/istanbul/test/karsilastirma-sokak.png`, `karsilastirma-simit.png`.
- Bina gövdelerinde de benzer, küçük bir eğrilik farkı var (5–6 m'lik kutular, tabanda birkaç cm). Bu aşamada düzeltilmedi.

**Test (düzeltmeden sonra, yeni Windows test derlemesi, 390x844; gerçek iPhone'da denenmedi):**
- Eğitim 96/96, hata logu 0. Menü: 11 çalıştırmanın her biri 45/45. Kullanıcı kaydı her seferinde yedekle aynı (SHA-256 `F19B7495…`).
- İstiklal testi: 10/10 geçti, hata logu 0.
  - 64 parça, hepsi İstiklal.
  - 63 birleşim; boşluk 0,0000 m, açı 0,00°.
  - 4 engel türü yerleşti: simit 20, çöp kutusu 27, alçak bariyer 14, koni 22.
  - Parça başına en çok 2 simit; komşu şeride değen 0/40.
  - 724 altının 0'ı engelin içinde.
- Üçgen: engelsiz parça 3.353 (5 çizici). Engellerle ortalama 4.626, en çok 9.277 (hedef 10–15 bin). Çizici en çok 13.
- Doku: yeni doku yok. Parçanın bütün malzemeleri VCOL, dokusuz. Simit dokusu 1024.
- Addressables `Istanbul-Test` paketi (Windows): 633 KB (önce 628 KB; yalnız simitle 598 KB). WebGL ölçülmedi.
- Otomatik sürüşler — **bilinen başarısızlıklar, başarılı sayılmaz** (karışık engellerde otomatik pilotun sınırları):
  - zıplama: 5 çarpma, hepsi çöp kutusu (zıplanamaz yükseklikte).
  - şerit değiştirme: 6 çarpma (5 alçak bariyer — bütün şeritleri kapatır, 1 koni). Önceki çalıştırmalarda 6 ve 10.
  - dokunmadan: 9 çarpma; çarpışma çalışıyor.
  - Simit, zıplama ve şerit değiştirme sürüşlerinde çarpılmadı.
- Görüntüler: `_araclar/istanbul/test/istiklal-*.png` (yeni); `test/onceki/` (düzeltme öncesi).

**Geri alma:**
- Yalnız görsel düzeltme: `IstanbulUygula.cs` içinde eski döşeme/ray kodu geri alınır ve `IstanbulUygula.IstiklalParca` yeniden çalıştırılır. Alternatif: `f8a9cf1` öncesi duruma dönülür.
- Bütün aşama: `git revert 341f1ce f8a9cf1` (not commit'i de gerekirse). Ya da `git switch main`.
- `main`, `gh-pages` değişmedi.

---

## İstanbul teması — faz 1: simit tezgâhı boru hattı testi (2026-10-17, `istanbul-tema`)

> `main`, `gh-pages`, canlı oyun ve `/test/` değişmedi. Yayın yok. Sonraki her adım (yeni model, tema, parça, yayın) ayrı onay ister.

**Kararlar (kullanıcı):**
- İstanbul **ayrı bir tema** olacak, varsayılan değil. Day teması değişmeyecek.
- İlk bölge İstiklal Caddesi. Galata–Karaköy ve Eminönü için altyapı genişletilebilir kalacak; modeller henüz üretilmeyecek.

**Meshy (Pro plan; kayıt ve lisans notu: `_araclar/istanbul/URETIM_VE_LISANS.md`):**
- Konsept görseli: 3 kredi.
- Görselden 3B model: meshy-7.1, 2K doku, üçgen hedefi 3.000; 30 kredi.
- Toplam 33 kredi; bakiye 731 → 698 (2026-11-11'de yenilenir).
- Orijinaller `_araclar/istanbul/simit_3d_01a14b68/` altında, SHA-256 özetleriyle.
- §2.4 üst veri maddesi **belirsiz**: çıktılarda Meshy işareti bulunmadı, ama bu hukuki teyit değil. Yayından önce Meshy desteğinden yazılı teyit önerilir.

**Projeye eklenenler (bu dalda; kullanıcı onayıyla commit edildi):**
- `Assets/_TabanlariYagla/Istanbul/Test/SimitTezgahi/`:
  - `SimitTezgahi.fbx`: Meshy FBX'inin birebir kopyası.
  - `Textures/texture_0.png`: FBX'ten çıkarılan doku; oyunda en fazla 1024.
  - `SimitTezgahi.mat`: `Unlit/CurvedUnlit`.
  - `ObstacleSimitTezgahi.prefab`: `ObstacleRoadworksCone` kopyası; `SimpleBarricade`, katman 9, tetikleyici kutu 1,40 x 1,00 x 0,72 m; görsel 1,40 x 1,24 x 0,72 m; 2.962 üçgen.
- Addressables: yeni `Istanbul-Test` grubu (Day-Theme-Obstacles ayarları), adres `ObstacleSimitTezgahi`.
  - `AddressableAssetSettings.asset`: yalnız 1 satır eklendi (`m_GroupAssets` listesine `Istanbul-Test`, guid `2d270d11…`).
  - Editör aracı ayarı kaydedince Addressables 2.10 dosyayı baştan yazmıştı. Yazılan şeyler: eksik alanlar, kaldırılan `BuildScriptVirtualMode` kaydı (etkin derleyici numarası 3→2, yine PackedMode), `m_HostingServicesManager` ve grup sırası.
  - Bu dönüşüm commit edilmedi: dosya geri alındı, tek satır elle eklendi. Unity'de salt okunur denetimden geçti (`IstanbulUygula.Dogrula`: 17 grup, çözülemeyen 0) ve dosya yeniden yazılmadı.
  - Dönüşüm, Addressables ayarları Unity'den ilk kaydedildiğinde yine olacak; o zaman ayrı bir commit olarak ele alınmalı.
- `Assets/_TabanlariYagla/Editor/IstanbulUygula.cs`: aktarımı tekrarlayan editör aracı (`-executeMethod IstanbulUygula.SimitTezgahi`).
- `Assets/_Dev/SimitTest.cs`: yalnız geliştirme derlemesinde çalışan oyun içi test (`-simitTest`; `-simitKoni` aynı sürüşü mevcut koniyle yapar).
  - Day parçalarını **yalnız test sürecinin belleğinde** simit kullanacak şekilde ayarlar; dosyalar değişmez.
  - Çalıştırma betiği: `_araclar/istanbul/simit_test.ps1`.
- **Değişmeyenler:** tema, parça, mevcut engeller, oyun kodu.

**Commit'ler (`istanbul-tema`):**
- `7efc215`: entegrasyon (model, doku, malzeme, prefab, Addressables grubu ve 1 satırlık ayar, `IstanbulUygula.cs`).
- `97a936b`: geliştirme testi `SimitTest.cs`.
- Bu not: ayrı commit.
- Commit dışı bırakılanlar: `Windows/addressables_content_state.bin` (derleme durumu), kullanıcı kaydı, `Build/`, depo dışındaki `_araclar/istanbul/*`.
- Commit öncesi yedek: `_araclar/istanbul/yedek-commit-oncesi-20261017-234234/` (diff, durum ve dosya kopyaları).

**Test sonuçları (Windows test derlemesi, 390x844; gerçek iPhone'da denenmedi):**
- Commit öncesi yeniden çalıştırma (tek satırlık ayar dosyasıyla): eğitim **96/96**, hata logu 0. Menü: 3 açılış senaryosu + 8 ekran profili, **11 çalıştırmanın her biri 45/45**.
- Kullanıcı kaydı her çalıştırmadan sonra yedekle aynı (SHA-256).
- Normal yayın Windows derlemesi (`V1Build.BuildWindows`): `Assembly-CSharp.dll`'de `SimitTest`, `-simitTest`, `MenuShots` yok; `StartButton` ve `TrackManager` var. Geliştirme derlemesinde `SimitTest` var.
- Simit testi (entegrasyon sırasındaki son çalıştırma; commit öncesi yeniden çalıştırılmadı): 9 kontrol geçti, **2 kaldı**.
  - Addressables'tan yükleniyor; 2.962 üçgen; doku 1024; CurvedUnlit.
  - 70 tezgâh: komşu şeritteki karaktere değen 0/140.
  - Altınlar: 495 altının 0'ı tezgâhın içinde.
  - Dokunmadan sürüşte 13/13 çarpma (çarpışma çalışıyor).
- **Kalanlar:** "zıplama" (25 geçiş, 1 çarpma) ve "şerit değiştirme" (7 çarpma). Aynı sürüş mevcut **trafik konisiyle de aynı sonucu verdi** (1 ve 9 çarpma). Sorun simitte değil, test aracının otomatik pilotunda (hız 22'de zıplama zamanlaması; boş şerit bulamadığında bekleme).
- Parça başına üçgen (simitlerin yoğun olduğu test koşusu): ortalama 12.627, en çok 21.770. Aynı koşu koniyle: 7.662 / 10.706. Her tezgâh +2.962; bir sırada 2 tezgâh olabiliyor.
- Addressables paketi `Istanbul-Test` (Windows): 598 KB. WebGL paketi ölçülmedi.
- Görüntüler: `_araclar/istanbul/test/simit-kosu-uzak.png`, `simit-kosu-yakin.png`.

**Geri alma:**
- Dalın tamamı: `git switch main` (gerekirse `git branch -D istanbul-tema`; uzak dal için `git push origin --delete istanbul-tema`).
- Yalnız test aracı: `git revert 97a936b`. Entegrasyon: `git revert 7efc215`.
- `main` ve `gh-pages` hiç değişmedi; Meshy arşivi depo dışında ayrı duruyor.

---

## ▶ CANLIDA: ANA MENÜ + TEK YÜKLEME EKRANI (2026-10-17)

Kullanıcı iPhone 13'te `/test/` sürümünü (gh-pages `e774264`, kaynak `1110f81`) denedi ve canlıya geçişi açıkça onayladı.

**Kaynak:** `ana-menu-prototip` `main`'e çakışmasız birleştirildi (`--no-ff`). Birleştirme commit'i: `ea229d9`. Ağacı `1110f81` ile birebir aynı.

**Yayın:** gh-pages `4924636`. `/test/` derlemesi **yeniden derlenmeden** köke kopyalandı.
- Canlıdan indirilen `index.html`, `Build/*` (4 dosya), `catalog.bin`, `settings.json` ve logo, `/test/` dosyalarıyla SHA-256 olarak aynı.
- Eski Addressables paketleri (2 adet) açık eski oturumlar için kökte bırakıldı.
- Yollar göreli (`Build/…`, `{RuntimePath}` = sayfanın klasörü, manifest `./`); `/test/`'e bağlı yol yok.
- Kayıt biçimi değişmedi (`PlayerData` dokunulmadı). Kökün kayıt klasörü aynı kaldığından mevcut oyuncuların kaydı korunur.
- `/test/`, `/dev/`, `/test-ad/`, `/test-dil/`: `e774264`'e göre değişiklik 0 dosya; canlıdaki `index.html`'leri de aynı.

**Etiketler (GitHub'da):**
| Etiket | Gösterdiği | Amaç |
|---|---|---|
| `canli-2026-10-17-oncesi-kaynak` | main `31565f6` | geçiş öncesi kaynak |
| `canli-2026-10-17-oncesi-yayin` | gh-pages `e774264` (kök = eski canlı `e8777e3`) | geçiş öncesi yayın |
| `canli-2026-10-17-kaynak` | main `ea229d9` | canlı kaynak |
| `canli-2026-10-17-yayin` | gh-pages `4924636` | canlı yayın |

Önceki tüm etiketler ve yedekler duruyor.

**Testler:**
- Windows test derlemesi (aynı kaynak): eğitim 96/96, 3 açılış senaryosu 45/45, 8 ekran profili 45/45.
- Canlı (tarayıcı paneli, 390x844): logo → çubuk 81→87→92→100 → menü dokunmadan açıldı. OYNA oyunu başlattı, eğitim ilk adımı (SAĞA KAYDIR / TEKRAR DENE) göründü. Duraklat → ANA MENÜ → menüye dönüldü. Konsolda hata yok.
- Panelde klavye ve kaydırma oyuna ulaşmadığı için eğitimin tamamı, normal koşu, oyun sonu, karakter, mağaza, görevler ve ayarlar canlıda panelde denenemedi. Bunlar kullanıcının iPhone 13 testinde (aynı derleme) ve Windows testlerinde doğrulandı.

**Geri dönüş (gerekirse):**
- Yayın: v1-pages'te `git checkout canli-2026-10-17-oncesi-yayin -- Build StreamingAssets TemplateData index.html manifest.webmanifest`, commit, push gh-pages.
- Kaynak: `git revert -m 1 ea229d9` (main).

**Bilinen risk:** dosya adlarında sürüm eki yok; GitHub Pages ~10 dk önbellek tutar. Geçişten hemen sonra açanlarda nadiren eski/yeni karışımı olabilir; sayfayı yenilemek düzeltir.

---

## Ana menü prototipi + tek OYNA açılış — geliştirme kaydı (2026-10-17'de canlıya alındı, yukarıya bak)

> Bu bölüm `ana-menu-prototip` dalında yazıldı; dal 2026-10-17'de `main`'e birleşti (`ea229d9`).

**Dal:** `ana-menu-prototip`, `main` `31565f6`'dan açıldı. Commit'ler (eskiden yeniye):
- `f1aad35` — yarım prototip.
- `e0533fd` — ara not.
- `1d713c6` — düzeltmeler ve son hâl.
- `974a660` — tek OYNA açılış akışı.
- `91bcc8a` — logo → dolum çubuklu yükleme ekranı → ana menü.
- (bu commit) — tek yükleme ekranı, web ve Windows aynı.

**Yayın:** gh-pages `e774264`; yalnızca `test/` değişti.

**Yedekler:**
- `test-yedek-tekyukleme-oncesi` → gh-pages `06ff858` (tek yükleme ekranından önceki `/test/`).
- `test-yedek-yukleme-oncesi` → gh-pages `ef5f897` (logo/yükleme ekranından önceki `/test/`).
- `test-yedek-acilis-oncesi` → gh-pages `e82111e` (açılış değişikliğinden önceki `/test/`).
- `test-yedek-ana-menu-oncesi` → gh-pages `e8777e3` (prototip öncesi `/test/` = canlı derleme).

**Test adresi:** https://samet3474.github.io/tabanlari-yagla-v1/test/ — ölçüm sürümü `/test/olcum/`.

### Tek yükleme ekranı, her yerde aynı (2026-10-17, kullanıcının "tek yükleme ekranı olsun… hepsinde aynı olsun" isteği)
- **Akış:** lacivert zeminde ortada yalnız logo (0,8 sn) → logo üste kayar, altta İpucu + tek turuncu çubuk %0'dan %100'e dolar → ana menü dokunmadan açılır → OYNA → oyun.
- **Web:** yalnız sayfanın ekranı (`index.html` `#ty-loader`) görünür ve ana menü açılana kadar kalır. İndirme %0–80'i, oyunun kendi yüklemesi (`StartButton` → `Loading.jslib` → `window.tyLoadingProgress`) %80–100'ü doldurur. Main sahnesi açılınca `tyLoadingDone` ekranı kapatır; güvenlik için 60 sn sonra kendiliğinden kapanır. Çubuk hiç geri gitmez.
- **Windows / yerel:** Start sahnesi aynı görünür: tam ekran lacivert `LoadingBackground`, ortada `LogoImage` (560x315); 0,8 sn sonra logo yukarı (y=358, %90 boy) kayar, `LoadingSafeArea` (CanvasGroup) görünür olur, çubuk 0'dan dolar. Arkadaki 3B sokak çizilmez (kamera yalnız lacivert temizler). Logo süresi çizilen karelerle sayılır; ilk açılıştaki uzun kareler logo aşamasını atlatmaz.
- **Testler (Windows test derlemesi):**
  - Eğitim 96/96.
  - Açılış senaryoları 45/45 × 3. Menü ~3,3 sn'de hazır (0,8 sn logo + 0,45 sn geçiş dahil).
  - 8 ekran profili 45/45. Yeni denetimler: önce yalnız logo (ortada, çubuk gizli); sonra logo yukarıda ve çubuk görünür.
- **Web (tarayıcı paneli, 390x844, /test/ e774264):** çubuk 81→87→92→100 ilerledi, geri gitmedi. Ekran %100'de ~2 sn kaldıktan sonra kapandı (Main sahnesinin açılışı), menü dokunmadan geldi.
- **Gerçek cihazda (iPhone) henüz denenmedi.**

### Önceki adım: logo → yükleme ekranı → ana menü (2026-10-17, `91bcc8a`; yukarıdaki tek ekranla değişti, kullanıcının örnek görsellerine göre)
- **1. Logo:** web sayfasının kendi açılış ekranı (`WebGLTemplates/TabanlariYagla/index.html`, `#ty-loader`). Lacivert zeminde ortada yalnız logo, 0,8 sn; oyun dosyalarının indirmesi bu sırada sürer.
- **2. Yükleme ekranı:**
  - Önce sayfada: logo üste kayar; altta İpucu + dolan turuncu çubuk + yüzde. İndirme çubuğu %0–80 doldurur.
  - Oyun açılınca Unity'deki Start sahnesinde aynı düzen (logo, hareketli sokak, ipucu, çubuk, yüzde, güvenli alanda) %80'den %100'e gider.
  - Gerçek adımlar: kayıt %10, karakter ve tema veritabanları %25 + %25, Main sahnesinin arka planda yüklenmesi %60–100.
  - Main sahnesi ancak çubuk %100'ü gösterince açılır. Çubuk gerçek değere hızla yetişir (5/sn); yapay bekleme yok.
  - Unity'nin kendi ince çubuğu gizli.
- **3. Ana menü** dokunmadan açılır → OYNA → oyun.
- **Varlıklar:** sprite sayfası (`UIBorderFilled01`, `UISliderFill`), LuckiestGuy, proje logosu (`TemplateData/brand-logo.png`). Yeni görsel yok; Subway Surfers'tan hiçbir görsel kullanılmadı, yalnızca düzen örnek alındı.
- **İpuçları:** `tr.txt` → `tip.prefix`, `tip.1–5`; sayfadakiler `index.html` içinde.
- **Testler (Windows test derlemesi, son hâl):**
  - Açılış senaryoları (yeni / tekrar / kayıtlı kullanıcı): 43/43 × 3. Menü ~2 sn'de hazır (görüntü alma dahil).
  - 8 ekran profili: 43/43.
  - Eğitim testleri 96/96 (logo/yükleme değişikliğinden hemen önceki derlemede; bu değişiklik oyun akışına dokunmuyor).
- **Web (tarayıcı paneli, 390x844):** logo → yükleme ekranı (sayfa) → yükleme ekranı (Unity, sokak) → menü sırası görüldü, dokunma gerekmedi.

### Önceki adım: tek OYNA (974a660)
- **Akış:** Oyun açılır → başlık ekranı (logo + hareketli sokak + "YÜKLENİYOR...") → yükleme bitince **dokunmadan** ana menü → OYNA → oyun.
- **Start sahnesi silinmedi**, düğmesiz yükleme ekranına dönüştü (`MenuUygula.ApplyStartScene`): OYNA düğmesi gizli ve tıklanamaz, yazısı "YÜKLENİYOR...". Kayıt (`PlayerData`, `MusicPlayer` üzerinden) ve karakter/tema veritabanlarının yüklenmesi eskisi gibi burada başlıyor.
- **`StartButton.autoStart`:** kayıt ve iki veritabanı hazır olunca Main sahnesi arka planda (`LoadSceneAsync`) yüklenir. Yapay bekleme yok. Çift yükleme koruması var. 20 sn güvenlik sınırı: bir veritabanı hiç bitmezse yine geçilir, menü kendisi bekler. `ftueLevel` güncellemesi korunuyor.
- **Testler (Windows test derlemesi):**
  - Açılış senaryoları (iPhone 13 oranı): yeni kullanıcı, tekrar açılış (eğitim bitmemiş), eğitimi bitmiş kayıtlı kullanıcı. Üçünde de dokunmadan menüye geçildi, ~1,4 sn'de menü hazırdı (OYNA etkin, karakter yüklü). Main bir kez yüklendi, menüde tek karakter var, eğitim perdesi yalnızca eğitim bitmemişse göründü.
  - Eğitim testleri 96/96.
  - 8 ekran profili 42–43 denetimde hepsi geçti.
- **Web (tarayıcı paneli):** dokunmadan menüye geçti. Süre 11–25 sn, ama bu süre büyük ölçüde dosya ve paket indirmesi; panel yavaş bir ortam, iPhone süresini temsil etmiyor.
- **Gerçek iPhone ya da Android'de denenmedi.**

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

1. ~~Ana menü~~ — 2026-10-17'de canlıya alındı. Yeni iş için kullanıcıdan talimat beklenir.
2. Öneriler (onaysız başlanmaz): Aşama 2 (performans/bellek), önbellek iyileştirmesi (Name Files As Hashes), Android APK.

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
