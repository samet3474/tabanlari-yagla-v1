# Tabanları Yağla — Geliştirme Notları

Son güncelleme: 12 Ekim 2026

## Hızlı bağlantılar

| Ne | Adres |
|---|---|
| Oyun (oyuncu sürümü) | https://samet3474.github.io/tabanlari-yagla-v1/ |
| iPhone test sürümü (zaman çizelgeli) | https://samet3474.github.io/tabanlari-yagla-v1/dev/ |
| Test sürümü, kayıt sıfırlanmış | https://samet3474.github.io/tabanlari-yagla-v1/dev/?kayitsil=1 |
| Kaynak kod | https://github.com/samet3474/tabanlari-yagla-v1 |

## Hedef

- **Birincil:** iPhone'da Safari/Chrome'dan, ana ekrana eklenerek oynanan, dikey, akıcı bir endless runner (WebGL).
- **İkincil:** Aynı altyapıdan Android APK.
- Kurallar: çalışan sistemi koru, küçük ve geri alınabilir adımlar, onaysız commit/push yok, gerçek cihazda test edilmeyeni doğrulanmış sayma, ölçmeden performans iddiası yok.

## Teknik temel

- Unity **6000.3.26f1** (Unity 6.3), URP 17.3, Addressables, yeni Input System.
- Temel proje: Unity'nin resmî "Endless Runner Sample Game" (Trash Dash), `6.3` dalı.
- Kayıt: `save.bin` (PlayerData, sürüm 12), web'de tarayıcının IndexedDB'sinde.

## İş yerinde kurulum

```bash
git lfs install
git clone https://github.com/samet3474/tabanlari-yagla-v1.git
cd tabanlari-yagla-v1
git checkout b-tutorial-test   # iPhone'da test edilen B çalışması (main'e henüz alınmadı)
```

Unity Hub → **Add project from disk** → klasörü seç → Unity **6000.3.26f1** ile aç.
Menü: **Tabanları Yağla → Windows V1 Derle / Web V1 Derle**.

> Not: Proje dosyaları Git LFS kullanıyor; `git lfs install` yapılmazsa modeller/dokular eksik gelir.

## Dallar ve geri dönüş noktaları

| Dal / etiket | İçerik |
|---|---|
| `main` | Yayındaki oyun sürümü (Faz 0.1 + 0.2 + A). B burada **yok**. |
| `b-tutorial-test` | `main` + B (tutorial düzeltmeleri) + test araçları. iPhone testi bekliyor. |
| `gh-pages` | Yayınlanan web derlemeleri (kök = oyuncu sürümü, `/dev/` = test sürümü). |
| etiket `v1.1-analiz-oncesi` | Analizden önceki ilk sürüm. |
| etiket `yayin-faz02` (gh-pages) | A'dan önceki web yayını. |

Yerel ek yedekler (yalnızca ev bilgisayarında): `Documents\Unity\_yedekler\` (analiz öncesi tam zip, B yama yedeği).

## Yapılanlar (sırasıyla)

### Kurulum ve ilk yayın
- Trash Dash 6.3 alındı, ad "Tabanları Yağla", sürüm 1.0 yapıldı.
- Unity 6.3 uyumu: URP Compatibility Mode kapatıldı (Render Graph).
- iPhone Safari ses hatası ("EncodingError: Decoding failed") düzeltildi: müzik katmanları streaming'e alındı, web şablonu ses çözme hatalarında oyunu durdurmuyor.
- 30 FPS sınırı 60'a çıkarıldı, mobilde çizim çözünürlüğü 1.5x ile sınırlandı, gölge haritası 1024.

### Faz 0.1 — Web kayıt sorunu (`7b5fe8b`)
- **Kök neden:** `config.autoSyncPersistentDataPath` kapalıydı; `save.bin` IndexedDB'ye yalnızca tesadüfen yazılıyordu, sayfa yenilenince ilerleme kaybolabiliyordu.
- **Çözüm:** Web şablonunda `autoSyncPersistentDataPath = true`. Kayıt formatı değişmedi.
- **Test (Chromium):** satın alma → yenile → korunuyor; elle hazırlanmış v12 ve v10 kayıtları doğru okunuyor/yükseltiliyor.

### Faz 0.2 — Altın/engel çakışması (`aec87e8`)
- **Kök neden:** Altınlar, engeller Addressables ile asenkron yüklenmeden aynı karede yerleştiriliyordu.
- **Çözüm:** `SpawnObstacle` engellerin bitmesini bekliyor (hata durumunda da `finally` ile), sonra altın yerleştiriyor.
- **Ölçüm (sabit tohum, 240 sn):** sabit engel içinde altın 111/4245 (%2,61) → 0/4209. Altın/güçlendirme sayısı ve kare süreleri değişmedi.

### A — Menüye dönüp yeniden koşma hatası (`0638f38`)
- **Kök nedenler:** (1) yüklenmekte olan parçalar menüye dönüşte sayaçta unutuluyor, sonraki tüm koşularda yol hiç oluşmuyordu (60 sn'de 3.780 hata); (2) yetim parça/engeller yeni yolun üstünde kalıyordu; (3) altın havuzu her koşu 256 nesne sızdırıyordu; (4) mıknatıs listesi yok edilmiş altınları tutuyordu (501 hata).
- **Çözüm:** `TrackManager.End()` hareketi durduruyor, yüklenen parçaları serbest bırakıyor, sayacı sıfırlıyor, havuzu ve mıknatıs listesini temizliyor; yetim engeller yeni koşu başında siliniyor. `Pooler.Clear()` eklendi.
- **Test:** 0–8. karede, geri sayımda, art arda çıkışlar: sayaç 0, yetim parça 0, hata 0.

### B — Tutorial akışı (dal: `b-tutorial-test`, iPhone testi bekliyor)
- Tutorial bitince **ana menü açılmıyor**; "HARIKA!" yazısından sonra aynı koşu kesintisiz normal oyuna dönüyor (`TrackManager.ContinueAsNormalRun`).
- Tutorial durumu her başlangıçta sıfırlanıyor (yarıda bırakıp yeniden başlatma bozulmuyor); bitiş tek seferlik.
- Tutorial geri çağrıları normal koşuda temizleniyor.
- Tutorial sırasında duraklatma/odak kaybı artık kendiliğinden devam etmiyor.
- Buton üzerinde başlayan dokunuş kaydırma sayılmıyor (pause/envanter).
- Zıplarken/kayarken menüye dönünce sonraki koşu yerde ve normal çarpışma kutusuyla başlıyor.
- **Test:** Windows otomatik senaryolar 41/41 (×2), 0 hata; Chromium'da dokunma ayrımı, duraklatma, isteyerek menüye dönüş, yarıda bırakma, tamamlama ve yenileme sonrası tutorial'ın açılmaması doğrulandı.
- **Gerçek iPhone'da henüz test edilmedi.**

## Test araçları (yalnızca geliştirme/test derlemelerinde, oyuncu sürümüne girmez)

| Araç | Nasıl çalışır |
|---|---|
| `Assets/_Dev/CoinOverlapDiagnostic.cs` | Windows geliştirme derlemesi: `-coinDiag -diagSeconds 240 -diagSeed 12345 -diagPhases measure/full/lifecycle` |
| `Assets/_Dev/TutorialAutoTest.cs` | Windows: `-tutorialTest` (tüm senaryolar). Web: `?autotest=tutorial` (bot tutorial'ı oynar) |
| `Assets/_Dev/DevTimeline.cs` | Web test sürümünde otomatik açılan zaman çizelgesi. `?kayitsil=1` kaydı sıfırlar, `?tl=0` gizli başlar, **iki parmakla dokunma** aç/kapa |
| `Assets/_Dev/Editor/DevBuild.cs` | `DevBuild.BuildWindowsDev`, `DevBuild.BuildWebDev` (TY_DEVTOOLS sembollü optimize web test derlemesi) |

Örnek (Windows, batch): `Unity.exe -batchmode -projectPath <proje> -buildTarget Win64 -executeMethod DevBuild.BuildWindowsDev`

## iPhone 13 Safari test listesi (/dev/)

1. `?kayitsil=1` ile aç → START → Run → tutorial'ı elle oyna. Sonda menü açılmamalı, koşu sürmeli.
2. Tutorial sırasında Duraklat → menü açık kalmalı → Devam.
3. Pause butonuna basıp parmağı kaydırarak çek → şerit değişmemeli.
4. Duraklat → Ana Menü → Run → tutorial 1. aşamadan başlamalı.
5. Parametresiz yeniden aç → Run → tutorial açılmamalı.
6. Safari sekmesinde ve "Ana Ekrana Ekle" ile ayrı ayrı dene; sol kenardan kaydırmayı da dene.
7. Sorun olursa zaman çizelgesinin ekran görüntüsünü al.

## Bilinen ortam sorunları (ev bilgisayarı, 16 GB RAM)

- WebGL derlemesinde ara sıra `wasm-opt` çökmesi veya IL2CPP `OutOfMemoryException`. Kod hatası değil; tarayıcı/oyun kapalıyken ve `BINARYEN_CORES=2` ortam değişkeniyle tekrar denenince geçiyor.
- WebGL **geliştirme** derlemesi sıkıştırmasız ~118 MB wasm üretiyor (GitHub sınırı 100 MB); bu yüzden test sürümü optimize derleme + `TY_DEVTOOLS` sembolüyle alınıyor.

## Bekleyen işler ve sıra

1. **Şimdi:** iPhone 13 Safari'de `/dev/` testi → sonuca göre düzeltme → B için ayrı commit onayı (oyun kodu ve test araçları ayrı).
2. Faz 0.3 — Addressables bellek yönetimi (yükleme tutamaçlarının serbest bırakılması, havuzlama önerisi).
3. Faz 0.4 — Her karede bellek ayırma (GameState.Tick listeleri, UI metinleri, bulutlar).
4. Kritik hatalar iPhone'da doğrulandıktan **sonra**:
   - Oyunun adı her yerde **"Tabanları Yağla"** (logo, menü, sekme, mağaza; "Trash Dash/Trash Cat" kalmayacak).
   - **Türkçe arayüz ve tutorial**, dağınık metin değişikliği yerine sürdürülebilir dil sistemiyle (anahtar→metin tablosu, Türkçe karakterli yazı tipi/TextMeshPro).
   - **Yeni ana menü:** Subway Surfers'ın profesyonel mobil menü düzeni ve kullanım hissi referans, tasarım ve görseller özgün. İstanbul sokakları, tarihî binalar, kırmızı tramvay teması; karakter, büyük OYNA butonu, altın göstergeleri, görevler, ödüller, karakter seçimi, mağaza ve ayarlar tek tutarlı arayüzde.
5. Her aşamada ayrı test maddeleri: **iPhone Safari**, **farklı ekran boyutları**, **performans (ölçülmüş FPS/bellek)**, **dokunmatik kontroller**.
6. Sonra: Android APK.

## Ayrı iş: iPhone'da tutorial sırasında takılma (ertelendi)

- **Durum:** Kullanıcı iPhone 13'te tutorial oynarken takılma yaşadı. Neden henüz araştırılmadı; ad/marka ve Türkçe arayüz aşamalarından bağımsız, ayrı bir iş olarak bekliyor.
- **İlgili dal:** `b-tutorial-test` (tutorial akışı düzeltmeleri). Bu iş bitene kadar B `main`'e alınmayacak.
- **B birleştirilirken yapılacak dil işi:** `GameState.cs` içindeki sabit `"Passed!"` ve B'nin eklediği `"HARIKA!"` metinleri `Loc`/`tr.txt` tablosuna taşınacak (doğru yazım: `HARİKA!`). Türkçe arayüz aşamasında B ile çakışmamak için `GameState.cs`'e bilerek dokunulmadı.
- **Başlarken:** `/dev/` sürümünde (`?kayitsil=1`) otomatik açılan zaman çizelgesiyle takılma anının ekran görüntüsü alınmalı; ölçmeden düzeltme yapılmamalı.
