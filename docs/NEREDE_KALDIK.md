# Nerede kaldık? — Tabanları Yağla

> Her aşamanın sonunda güncellenir. Projeye ara verip döndüğünde **önce bu dosyayı oku.**
> Ayrıntılı geçmiş: [GELISTIRME_NOTLARI.md](GELISTIRME_NOTLARI.md) (bu dosya ondan daha güncel).

**Son güncelleme:** 2026-10-12 (akşam). **Yeni eğitim tasarımı** (`egitim-yeni` dalı) /test/ adresinde; iPhone testi bekleniyor.

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
git diff --stat canli-v1-yayin HEAD -- .nojekyll index.html Build StreamingAssets TemplateData   # boş çıkmalı
git diff --stat canli-v1-yayin HEAD -- dev test-ad test-dil                                     # boş çıkmalı
```

## 1. Kısaca durum

| Konu | Durum |
|---|---|
| Canlı oyun (kök adres) | Eski A sürümü. **Değiştirilmedi** (kanıt: canli-v1-yayin ile kök ağaçları aynı). |
| Test adayı | `egitim-yeni` dalı (`378c04a`). **/test/ adresinde.** İçerik: Aşama 1 + Türkçe arayüz + B (menüye dönmeden normal koşu) + **yeni eğitim**. |
| Yeni eğitim | SAĞA → SOLA → YUKARI → AŞAĞI → HAZIRSIN. Her hareket bir kez, engelsiz pist, beklemesiz. Windows otomatik testleri **78/78**, hata kaydı 0. **iPhone testi bekleniyor.** |
| Eski eğitimin sorunları (A, B, C, ~9 sn bekleme) | Yeni tasarımda bu akış yok (üçer engel, oyunu durduran yönerge, seviye geçişi kaldırıldı). Eski düzeltmeler `egitim-duzeltme` dalında duruyor. |
| Dokunma yönünün erken seçilmesi | Değiştirilmedi. Yeni eğitimde yanlış yön adımı ilerletmez ve karakteri oynatmaz; iPhone'da `/test/olcum/` ile "FARKLI EKSEN" sayısına bakılmalı. |
| Gerçek iPhone 13 testi | **Yapılmadı.** |

## 2. Adresler

| Adres | İçerik | Kayıt klasörü (IndexedDB) |
|---|---|---|
| https://samet3474.github.io/tabanlari-yagla-v1/ | **Canlı oyun** (A sürümü) | `/idbfs/4ebc64a6…` |
| https://samet3474.github.io/tabanlari-yagla-v1/test/ | **Test adayı** (`egitim-yeni`), normal derleme | `/idbfs/7e1905a8…` |
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
| `main` | (yalnızca not commit'leri) | Canlı oyunun kaynağı; oyun kodu `canli-v1-kaynak` ile aynı |
| `gelistirme` | `638289b` | Ana geliştirme dalı: main + Aşama 1 + Türkçe arayüz |
| `egitim-yeni` | `378c04a` | egitim-duzeltme + yeni eğitim tasarımı. **/test/'te.** |
| `egitim-duzeltme` | `502d3c7` | gelistirme + B + eski eğitimin düzeltmeleri (egitim-yeni'nin içinde) |
| `ad-marka` | `4c9244a` | Aşama 1 (gelistirme'nin içinde) |
| `dil-turkce` | `638289b` | Türkçe arayüz (= gelistirme) |
| `b-tutorial-test` | `d5f0f2f` | B (eğitim akışı) — egitim-duzeltme'nin içinde |
| `b-egitim-olcum` | `2188d73` | B + girdi teşhis araçları — egitim-duzeltme'nin içinde |
| `gh-pages` | `5d9bd72` | Yayın: kök (canlı), /dev/, /test-ad/, /test-dil/, /test/, /test/olcum/ |

Geri dönüş etiketleri (hepsi GitHub'da):

| Etiket | Gösterdiği | Anlamı |
|---|---|---|
| `canli-v1-kaynak` | `0638f38` | Canlı oyunun kaynağı |
| `canli-v1-yayin` | gh-pages `b2bf7c0` | Kökü canlı oyun olan yayın |
| `test-yedek-egitim-duzeltme` | gh-pages `8bbf337` | Yeni eğitimden önceki /test/ (eski eğitim + düzeltmeler) |
| `egitim-yeni-oncesi` | `502d3c7` | Yeni eğitimden önceki kaynak |
| `test-yedek-2026-10-12` | gh-pages `330d9ed` | Eğitim düzeltmesi öncesi /test/ (= /test-dil/) |
| `yayin-A` | gh-pages `c543b23` | Daha eski yayın |
| `asama1-oncesi`, `dil-oncesi`, `egitim-olcum-oncesi` | `97135aa`, `4c9244a`, `d5f0f2f` | Aşama öncesi noktalar |

## 4. Son yapılanlar (en yeni üstte)

1. **Yeni eğitim tasarımı** (`egitim-yeni`). Commit'ler (eskiden yeniye):
   - `ddb5a5f` — Eğitim ekranı (`TutorialOverlay`): yarı saydam yönerge şeridi, kayan altın ok, adım noktaları.
   - `882c260` — Eğitim pisti engelsiz; eğitim bitince sisin ötesindeki (50 m+) parçalara engel ve altın konur.
   - `378c04a` — Yeni akış ve girdi kapısı (`HandleMoveInput`), "HAZIRSIN!", test araçları.
   - /test/ ve /test/olcum/ yayında (gh-pages `5d9bd72`).
2. **Eski eğitimin düzeltmeleri** (`egitim-duzeltme`; yeni tasarımla akışı değişti). Commit'ler (eskiden yeniye):
   - `5759135` — B akışı ve teşhis araçları birleştirildi.
   - `f824961` — "GEÇTİN!" ve "HARİKA!" dil tablosuna taşındı.
   - `09a4f4b` — Adım ölçer eklendi (yalnızca test derlemesi).
   - `3e7c111` — Yönerge engele kalan gerçek mesafede açılıyor (Sorun B).
   - `e234205` — Adım, oyuncunun durumuyla tutarlı (Sorun A).
   - `8846946` — Şerit kararı engel yüklenmeden verilmiyor.
   - `cbb6cc1` — Çarpma bayrağı sıfırlanıyor; doğru ilk hareket artık sayılıyor.
   - `75e3ca3`, `20f14d6`, `502d3c7` — Test araçları.
   - /test/ ve /test/olcum/ yayında (gh-pages `8bbf337`).
3. **Geliştirme/test düzeni kuruldu.** Etiketler, `gelistirme` dalı, `/test/` adresi.
4. **Eğitim dokunma sorunu ölçüldü** (`b-egitim-olcum`).
5. **Türkçe arayüz** (`638289b`) ve **Aşama 1 — ad ve marka** (`4c9244a`).

## 5a. Yeni eğitim — nasıl çalışıyor (egitim-yeni)

- İlk koşu (kayıtta `tutorialDone=false`) eğitimdir. Geri sayımda kontrol yok, karakter ortada.
- Koşu başlayınca adımlar: **SAĞA KAYDIR → SOLA KAYDIR → YUKARI KAYDIR → AŞAĞI KAYDIR → HAZIRSIN!**
- Her adımda yalnızca o hareket kabul edilir (`CharacterInputController.tutorialAllowedMove`); yanlış kaydırma yok sayılır, karakter yerinde kalır.
- Adım, karakter hareketi gerçekten bitirince geçer: yeni şeride varınca / zıplamadan yere inince / kayma bitince.
- Pist eğitim boyunca engelsiz ve altınsız; koşu durmaz, hız sabit 10 m/sn.
- Son adımdan sonra 1,6 sn "HAZIRSIN!", kayıt yazılır, aynı koşu normal koşu olarak sürer (menü yok, sahne yüklenmez).
- Geçiş: 50 m'den uzaktaki parçalara engel/altın konur; ilk engel bitişten ~6–8 sn sonra gelir (ayar: `TrackManager.k_TutorialFillDistance`).
- Süre (Windows, oto-test): 0,5 sn tepkiyle dört adım 3,7 sn; 1,0 sn tepkiyle 5,7 sn. Geri sayım 3,3 sn + HAZIRSIN 1,6 sn ayrı.
- Performans (Windows): en uzun kare 20 ms, 50 ms'yi aşan kare yok, eğitim boyunca GC ~0.

## 5. Eski eğitim — doğrulanan nedenler ve sonuçlar (egitim-duzeltme; yeni tasarımda bu akış yok)

| Sorun | Doğrulanmış neden | Düzeltme | Önce → Sonra (Windows ölçümü) |
|---|---|---|---|
| A — oyuncu önceden hareket edince eğitim yine aynı hareketi istiyor | Yönerge oyuncunun şeridine hiç bakmıyordu. Yönergeden önce şerit değiştirilebiliyordu. Yönerge açılınca doğrulama bayrağı sıfırlanıyordu. | Yönerge anında oyuncunun durumu kontrol ediliyor: boş şeritteyse ya da engeli aşacak zıplama/kaymadaysa yönerge açılmıyor. Engeli aşamayacak erken zıplama/kayma yönergede bitiriliyor. | S8 testi: önceden yapılan doğru hareketle yönerge açılmadı ve engel geçildi (3/3 hareket) |
| B — talimat geç açılıyor | Yönerge segmentin %10'unda açılıyordu. | Hareket fiziğinden türetilen mesafe: şerit 8 m, zıplama 3,9 m, kayma 5,5 m. | Engele 1,7–2,7 m (0,2 sn) → şerit 7–8 m, zıplama 3,7–3,9 m, kayma 5,4–5,5 m |
| (Yeni bulunan) doğru ilk hareket sayılmıyor | Adımlar arasındaki sayılmayan engelde çarpma olursa bayrak açık kalıyor, sonraki adımın ilk engeli "çarpıldı" sayılıyordu (Unity örneğinden). | Sayım her yeni adımda temiz başlıyor. | Ölçüm kaydında 4/4 tekrarlanıyordu → testlerde artık yok |
| C — takılarak ilerleme | Windows'ta eğitim boyunca 50 ms'yi aşan kare yok, GC adım başına 0–3. Asıl gecikme mantık kaynaklı: 3. engelden sonra önceden oluşturulmuş 3–4 eski bölge segmenti bitene kadar ~9 sn yeni yönerge yok. Ayrıca yönerge oyunu sert durduruyor ve müziği kesiyor (tasarım). | Performans tarafında değişiklik yapılmadı (ölçüm sorun göstermedi). iPhone'da `/test/olcum/` ile ölçülmeli. Mantık gecikmesi karar bekliyor (6. bölüm). | — |

## 6. Bekleyen kararlar ve onaylar

- [ ] **Yeni eğitimi iPhone 13'te dene** (/test/; eğitimi görmek için ilk kez açılan tarayıcı ya da /test/olcum/?kayitsil=1). Kontrol et:
  - Ok ve yazı okunaklı mı, ekranı kapatıyor mu?
  - Kaydırmalar ilk seferde algılanıyor mu, yanlış yön karakteri oynatıyor mu?
  - HAZIRSIN sonrası ilk engele kadarki ~6–8 sn boş yol uzun mu? (kısaltmak için `k_TutorialFillDistance` 50 → 35 m; engeller sisin önünde belirir)
- [ ] **iPhone ölçümü:** `/test/olcum/?kayitsil=1`. Takılma anında ekran görüntüsü: ADIM/KARE/SICRAMA satırları adım zamanlarını ve kare sürelerini, KAYDIR satırları dokunmaların nasıl algılandığını gösterir.
- [ ] **Ayarlardan eğitimi yeniden başlatma** (isteğe bağlı, bu aşamada yapılmadı).
- [ ] **Dokunma yönünün erken seçilmesi:** iPhone ölçümünde "FARKLI EKSEN" sayısı görülürse eşik/karar düzeltmesi (ayrı iş).
- [ ] **Canlıya geçiş:** yalnızca /test/ onaylanınca, ayrıca açık onayla (0. bölüm).
- [ ] **Önbellek iyileştirmesi** (Name Files As Hashes) — ayrı test değişikliği olarak.

## 7. Sıradaki adım (döndüğünde buradan başla)

1. Yeni eğitim için /test/ ve /test/olcum/ iPhone sonuçlarını ilet.
2. Onaylanırsa canlıya geçiş (0. bölüm), ayrıca açık onayla.
3. Sonra: Aşama 2 (performans/bellek), İstanbul temalı yeni ana menü, Android APK.

## 8. Başka bilgisayarda devam etmek

```bash
git clone https://github.com/samet3474/tabanlari-yagla-v1.git
cd tabanlari-yagla-v1
git checkout egitim-yeni   # /test/'teki sürüm (en güncel)
```

- Git LFS kurulu olmalı. Unity 6000.3.26f1.
- Otomatik eğitim testleri: Windows geliştirme derlemesi (`DevBuild.BuildWindowsDev`), sonra `TabanlariYaglaDev.exe -tutorialTest -timeline -batchmode`.
  - Pencere odağını kaybedince oyun duraklar ve testler takılır; bu yüzden `-batchmode` önerilir.
  - Senaryolar S1–S10 (`Assets/_Dev/TutorialAutoTest.cs`); son çalıştırma 78/78.
- Yayın dalı için ayrı klasör: `git clone --branch gh-pages --single-branch https://github.com/samet3474/tabanlari-yagla-v1.git v1-pages`
- Unity her açılışta `AddressableAssetsData/link.xml` dosyasını siler; bu normal. `git restore` ile geri al.
- `addressables_content_state.bin` ve `TabanlariYagla-V1.slnx` derleme çıktısıdır, commit'e katılmaz.
