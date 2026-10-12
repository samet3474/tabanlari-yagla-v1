# Nerede kaldık? — Tabanları Yağla

> Her aşamanın sonunda güncellenir. Projeye ara verip döndüğünde **önce bu dosyayı oku.**
> Ayrıntılı geçmiş: [GELISTIRME_NOTLARI.md](GELISTIRME_NOTLARI.md) (bu dosya ondan daha güncel).

**Son güncelleme:** 2026-10-12. Eğitim düzeltmeleri (`egitim-duzeltme` dalı) /test/ adresinde; iPhone testi bekleniyor.

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
| Test adayı | `egitim-duzeltme` dalı. **/test/ adresinde.** İçerik: Aşama 1 + Türkçe arayüz + B eğitim akışı + eğitim düzeltmeleri. |
| Eğitim düzeltmeleri | Sorun A ve B düzeltildi. Ayrıca doğru yapılan ilk hareketin sayılmaması hatası düzeltildi. Windows otomatik testleri 61/61. **iPhone testi bekleniyor.** |
| Seviyeler arası bekleme (~9 sn) | Ölçüldü; mantık kaynaklı (performans değil). **Düzeltilmedi**, karar bekliyor (6. bölüm). |
| Dokunma yönünün erken seçilmesi | Mekanizma kanıtlı (yapay dokunma), gerçek cihaz sıklığı bilinmiyor. **Değiştirilmedi**; iPhone ölçümü bekliyor. |
| Gerçek iPhone 13 testi | **Yapılmadı.** |

## 2. Adresler

| Adres | İçerik | Kayıt klasörü (IndexedDB) |
|---|---|---|
| https://samet3474.github.io/tabanlari-yagla-v1/ | **Canlı oyun** (A sürümü) | `/idbfs/4ebc64a6…` |
| https://samet3474.github.io/tabanlari-yagla-v1/test/ | **Test adayı** (`egitim-duzeltme`), normal derleme | `/idbfs/7e1905a8…` |
| https://samet3474.github.io/tabanlari-yagla-v1/test/olcum/ | Aynı aday, **ölçüm derlemesi** (ekranda adım/kare kayıtları) | `/idbfs/f90ba6cc…` |
| https://samet3474.github.io/tabanlari-yagla-v1/test-dil/ | Eski test: Aşama 1 + Türkçe (= önceki /test/) | `/idbfs/5e701c6c…` |
| https://samet3474.github.io/tabanlari-yagla-v1/test-ad/ | Eski test: yalnızca Aşama 1 | `/idbfs/811705a2…` |
| https://samet3474.github.io/tabanlari-yagla-v1/dev/ | Eski B test sürümü | `/idbfs/80587b4b…` |

Kayıt klasörü = sondaki `/` olmadan klasör adresinin MD5 özeti; her adresin kaydı ayrı.

Ölçüm adresinde:
- `?kayitsil=1` → eğitimi baştan başlatır.
- İki parmakla dokunma → ölçüm katmanını açıp kapatır.

## 3. Dallar, commit'ler ve geri dönüş noktaları

| Dal | Commit | Ne var |
|---|---|---|
| `main` | (yalnızca not commit'leri) | Canlı oyunun kaynağı; oyun kodu `canli-v1-kaynak` ile aynı |
| `gelistirme` | `638289b` | Ana geliştirme dalı: main + Aşama 1 + Türkçe arayüz |
| `egitim-duzeltme` | `502d3c7` | gelistirme + B + eğitim düzeltmeleri. **/test/'te.** |
| `ad-marka` | `4c9244a` | Aşama 1 (gelistirme'nin içinde) |
| `dil-turkce` | `638289b` | Türkçe arayüz (= gelistirme) |
| `b-tutorial-test` | `d5f0f2f` | B (eğitim akışı) — egitim-duzeltme'nin içinde |
| `b-egitim-olcum` | `2188d73` | B + girdi teşhis araçları — egitim-duzeltme'nin içinde |
| `gh-pages` | `8bbf337` | Yayın: kök (canlı), /dev/, /test-ad/, /test-dil/, /test/, /test/olcum/ |

Geri dönüş etiketleri (hepsi GitHub'da):

| Etiket | Gösterdiği | Anlamı |
|---|---|---|
| `canli-v1-kaynak` | `0638f38` | Canlı oyunun kaynağı |
| `canli-v1-yayin` | gh-pages `b2bf7c0` | Kökü canlı oyun olan yayın |
| `test-yedek-2026-10-12` | gh-pages `330d9ed` | Eğitim düzeltmesi öncesi /test/ (= /test-dil/) |
| `yayin-A` | gh-pages `c543b23` | Daha eski yayın |
| `asama1-oncesi`, `dil-oncesi`, `egitim-olcum-oncesi` | `97135aa`, `4c9244a`, `d5f0f2f` | Aşama öncesi noktalar |

## 4. Son yapılanlar (en yeni üstte)

1. **Eğitim düzeltmeleri** (`egitim-duzeltme`). Commit'ler (eskiden yeniye):
   - `5759135` — B akışı ve teşhis araçları birleştirildi.
   - `f824961` — "GEÇTİN!" ve "HARİKA!" dil tablosuna taşındı.
   - `09a4f4b` — Adım ölçer eklendi (yalnızca test derlemesi).
   - `3e7c111` — Yönerge engele kalan gerçek mesafede açılıyor (Sorun B).
   - `e234205` — Adım, oyuncunun durumuyla tutarlı (Sorun A).
   - `8846946` — Şerit kararı engel yüklenmeden verilmiyor.
   - `cbb6cc1` — Çarpma bayrağı sıfırlanıyor; doğru ilk hareket artık sayılıyor.
   - `75e3ca3`, `20f14d6`, `502d3c7` — Test araçları.
   - /test/ ve /test/olcum/ yayında (gh-pages `8bbf337`).
2. **Geliştirme/test düzeni kuruldu.** Etiketler, `gelistirme` dalı, `/test/` adresi.
3. **Eğitim dokunma sorunu ölçüldü** (`b-egitim-olcum`).
4. **Türkçe arayüz** (`638289b`) ve **Aşama 1 — ad ve marka** (`4c9244a`).

## 5. Eğitim — doğrulanan nedenler ve sonuçlar

| Sorun | Doğrulanmış neden | Düzeltme | Önce → Sonra (Windows ölçümü) |
|---|---|---|---|
| A — oyuncu önceden hareket edince eğitim yine aynı hareketi istiyor | Yönerge oyuncunun şeridine hiç bakmıyordu. Yönergeden önce şerit değiştirilebiliyordu. Yönerge açılınca doğrulama bayrağı sıfırlanıyordu. | Yönerge anında oyuncunun durumu kontrol ediliyor: boş şeritteyse ya da engeli aşacak zıplama/kaymadaysa yönerge açılmıyor. Engeli aşamayacak erken zıplama/kayma yönergede bitiriliyor. | S8 testi: önceden yapılan doğru hareketle yönerge açılmadı ve engel geçildi (3/3 hareket) |
| B — talimat geç açılıyor | Yönerge segmentin %10'unda açılıyordu. | Hareket fiziğinden türetilen mesafe: şerit 8 m, zıplama 3,9 m, kayma 5,5 m. | Engele 1,7–2,7 m (0,2 sn) → şerit 7–8 m, zıplama 3,7–3,9 m, kayma 5,4–5,5 m |
| (Yeni bulunan) doğru ilk hareket sayılmıyor | Adımlar arasındaki sayılmayan engelde çarpma olursa bayrak açık kalıyor, sonraki adımın ilk engeli "çarpıldı" sayılıyordu (Unity örneğinden). | Sayım her yeni adımda temiz başlıyor. | Ölçüm kaydında 4/4 tekrarlanıyordu → testlerde artık yok |
| C — takılarak ilerleme | Windows'ta eğitim boyunca 50 ms'yi aşan kare yok, GC adım başına 0–3. Asıl gecikme mantık kaynaklı: 3. engelden sonra önceden oluşturulmuş 3–4 eski bölge segmenti bitene kadar ~9 sn yeni yönerge yok. Ayrıca yönerge oyunu sert durduruyor ve müziği kesiyor (tasarım). | Performans tarafında değişiklik yapılmadı (ölçüm sorun göstermedi). iPhone'da `/test/olcum/` ile ölçülmeli. Mantık gecikmesi karar bekliyor (6. bölüm). | — |

## 6. Bekleyen kararlar ve onaylar

- [ ] **/test/ adresini iPhone 13'te dene.** Özellikle şunları kontrol et:
  - Yönergeden önce sağa/sola geçince eğitim ne yapıyor?
  - Yönerge yeterince erken açılıyor mu?
  - Doğru hareket ilk seferde sayılıyor mu?
- [ ] **iPhone ölçümü:** `/test/olcum/?kayitsil=1` ile eğitimi oyna. Takılma anında ekran görüntüsü al: KARE/SICRAMA satırları kare sürelerini, KAYDIR satırları dokunmaların nasıl algılandığını gösterir.
- [ ] **Seviyeler arası ~9 sn bekleme** için seçim:
  - (a) Her adımda 3 yerine 2 engel: ~2,7 sn kısalır, tasarım değişikliği.
  - (b) Geçişte görünmeyen uzak segmentleri yenilemek: ~2,7 sn; sis sınırında görsel risk.
  - (c) Olduğu gibi bırakmak.
- [ ] **Sert duraklama ve müziğin kesilmesi:** kısa yavaş çekim veya müziği kısma seçenekleri (ayrı iş, ölçümden sonra).
- [ ] **Dokunma yönünün erken seçilmesi:** iPhone ölçümünde "FARKLI EKSEN" sayısı görülürse eşik/karar düzeltmesi (ayrı iş).
- [ ] **Canlıya geçiş:** yalnızca /test/ onaylanınca, ayrıca açık onayla (0. bölüm).
- [ ] **Önbellek iyileştirmesi** (Name Files As Hashes) — ayrı test değişikliği olarak.

## 7. Sıradaki adım (döndüğünde buradan başla)

1. /test/ ve /test/olcum/ için iPhone sonuçlarını ilet.
2. 6. bölümdeki kararlardan hangisiyle devam edileceğini seç.
3. Sonra: Aşama 2 (performans/bellek), İstanbul temalı yeni ana menü, Android APK.

## 8. Başka bilgisayarda devam etmek

```bash
git clone https://github.com/samet3474/tabanlari-yagla-v1.git
cd tabanlari-yagla-v1
git checkout egitim-duzeltme   # /test/'teki sürüm (en güncel)
```

- Git LFS kurulu olmalı. Unity 6000.3.26f1.
- Otomatik eğitim testleri: Windows geliştirme derlemesi (`DevBuild.BuildWindowsDev`), sonra `TabanlariYaglaDev.exe -tutorialTest -timeline -batchmode`.
  - Pencere odağını kaybedince oyun duraklar ve testler takılır; bu yüzden `-batchmode` önerilir.
  - S3 duraklatma testini ayrıca pencereli çalıştır.
- Yayın dalı için ayrı klasör: `git clone --branch gh-pages --single-branch https://github.com/samet3474/tabanlari-yagla-v1.git v1-pages`
- Unity her açılışta `AddressableAssetsData/link.xml` dosyasını siler; bu normal. `git restore` ile geri al.
- `addressables_content_state.bin` ve `TabanlariYagla-V1.slnx` derleme çıktısıdır, commit'e katılmaz.
