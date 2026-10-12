# Nerede kaldık? — Tabanları Yağla

> Her aşamanın sonunda güncellenir. Projeye ara verip döndüğünde **önce bu dosyayı oku.**
> Ayrıntılı geçmiş: [GELISTIRME_NOTLARI.md](GELISTIRME_NOTLARI.md) (bu dosya ondan daha güncel).

**Son güncelleme:** 2026-10-12 — geliştirme/test düzeni kuruldu (`gelistirme` dalı, `/test/` adresi, `canli-v1-*` etiketleri).

---

## 0. Çalışma düzeni (kalıcı kural)

**Çalışan sürümü koru, geliştirmeleri test et, onaydan sonra güncelle.**

1. **Çalışan sürüm**
   - Adres sabit: https://samet3474.github.io/tabanlari-yagla-v1/ (gh-pages kökü).
   - Kaynağı `main`. Onaysız değiştirilmez.
2. **Geliştirme**
   - `gelistirme` dalında yapılır; yeni işler bundan dal alıp buna birleşir.
3. **Test**
   - Adres: https://samet3474.github.io/tabanlari-yagla-v1/test/ (`gelistirme`'nin son test sürümü). Kaydı canlı oyundan ayrı.
   - Eski test klasörleri (`/dev/`, `/test-ad/`, `/test-dil/`) silinmez, onlara dokunulmaz.
4. **Her geliştirmenin sonunda test raporu**
   - Neler değişti, testler ve sonuçları, riskler, bağlantı, dal ve commit.
   - "Çalışan sürüm değişmedi" kanıtı: aşağıdaki git karşılaştırması.
5. **Onay sonrası ana yayın** (yalnızca açık onayla)
   1. Son kontrol.
   2. Yeni geri dönüş etiketleri: `canli-<tarih>-kaynak` ve `canli-<tarih>-yayin`.
   3. `gelistirme` → `main` (`--no-ff` birleştirme).
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
| Test adayı (`gelistirme` = Aşama 1 + Türkçe arayüz) | **/test/ adresinde.** Senin iPhone incelemen bekleniyor. |
| Eğitim (tutorial) dokunma sorunu | Neden ölçülerek bulundu (5. bölüm). Düzeltme yapılmadı; onay bekliyor. |
| Önbellek iyileştirmesi (Name Files As Hashes) | Henüz yapılmadı. Ayrı bir test değişikliği olarak ele alınacak (6. bölüm). |
| Gerçek iPhone 13 testi | **Yapılmadı.** /test/ adresini senin cihazında denemen gerekiyor. |

## 2. Adresler

| Adres | İçerik | Kayıt klasörü (IndexedDB) |
|---|---|---|
| https://samet3474.github.io/tabanlari-yagla-v1/ | **Canlı oyun** (A sürümü) | `/idbfs/4ebc64a6…` |
| https://samet3474.github.io/tabanlari-yagla-v1/test/ | **Test adayı** (`gelistirme`) | `/idbfs/7e1905a8…` |
| https://samet3474.github.io/tabanlari-yagla-v1/test-dil/ | Eski test: Aşama 1 + Türkçe (şu an /test/ ile aynı dosyalar) | `/idbfs/5e701c6c…` |
| https://samet3474.github.io/tabanlari-yagla-v1/test-ad/ | Eski test: yalnızca Aşama 1 | `/idbfs/811705a2…` |
| https://samet3474.github.io/tabanlari-yagla-v1/dev/ | B (eğitim akışı) test sürümü | `/idbfs/80587b4b…` |

Kayıt klasörü = sondaki `/` olmadan klasör adresinin MD5 özeti. Her adresin kaydı ayrıdır; test adresleri canlı oyunun kaydına dokunmaz (tarayıcıda doğrulandı).

## 3. Dallar, commit'ler ve geri dönüş noktaları

| Dal | Commit | Ne var |
|---|---|---|
| `main` | (yalnızca not commit'leri) | Canlı oyunun kaynağı. Oyun kodu `canli-v1-kaynak` ile aynı. |
| `gelistirme` | `638289b` | **Ana geliştirme dalı.** main + Aşama 1 + Türkçe arayüz. /test/'te yayında. |
| `ad-marka` | `4c9244a` | Aşama 1 (artık gelistirme'nin içinde) |
| `dil-turkce` | `638289b` | Türkçe arayüz (gelistirme ile aynı commit) |
| `b-tutorial-test` | `d5f0f2f` | B (eğitim akışı) + test araçları |
| `b-egitim-olcum` | `2188d73` | B + eğitim girdi teşhis araçları (yalnızca test derlemesi) |
| `gh-pages` | `330d9ed` | Yayın: kök (canlı), /dev/, /test-ad/, /test-dil/, /test/ |

Geri dönüş etiketleri (hepsi GitHub'da):

| Etiket | Gösterdiği | Anlamı |
|---|---|---|
| `canli-v1-kaynak` | `0638f38` | Canlı oyunun derlendiği kaynak |
| `canli-v1-yayin` | gh-pages `b2bf7c0` | Kökü canlı oyun olan yayın hâli (/test/ eklenmeden önce) |
| `yayin-A` | gh-pages `c543b23` | Kökü aynı, test-ad öncesi |
| `asama1-oncesi` | `97135aa` | Aşama 1 öncesi main |
| `dil-oncesi` | `4c9244a` | Türkçe arayüz öncesi |
| `egitim-olcum-oncesi` | `d5f0f2f` | Eğitim ölçümü öncesi B |
| `yayin-faz02`, `v1.1-analiz-oncesi` | — | Daha eski |

Yedekler (ev bilgisayarı): `Documents/Unity/_yedekler/` → `asama1-gorseller-2026-10-12`, `dil-turkce-2026-10-12`.

## 4. Son yapılanlar (en yeni üstte)

1. **Geliştirme/test düzeni kuruldu.**
   - `canli-v1-kaynak` ve `canli-v1-yayin` etiketleri oluşturuldu.
   - Yerel geri dönüş etiketleri GitHub'a gönderildi.
   - `gelistirme` dalı açıldı (`638289b`).
   - `/test/` adresi açıldı (gh-pages `330d9ed`); içeriği `/test-dil/` ile birebir aynı (ağaç özeti `e53eeb3`).
   - Kök ve diğer test klasörleri değişmedi.
2. **Eğitim dokunma sorunu ölçüldü** (`b-egitim-olcum` `2188d73`). Oyun koduna dokunulmadı.
3. **Türkçe arayüz** (`638289b`).
   - Metinler `tr.txt` dil tablosundan geliyor; tablo yoksa oyun çökmüyor.
   - 360–430 genişlikte mobil yerleşim düzeltildi.
   - Kayıt v12 ve kimlikler aynı.
4. **Aşama 1 — ad ve marka** (`4c9244a`).

## 5. Eğitim sorunu — bulgular (ölçüldü, henüz düzeltilmedi)

Şikâyet: "İlk hareket bazen sayılmıyor, ikincisinde geçiyor; bütün hareketlerde."

Eğitim adımı tek bir bayrağa bağlı: `tutorialWaitingForValidation`. Gerçek dokunma olaylarıyla, 3 seviyede de iki neden gösterildi:

1. **Yön çok erken seçiliyor.**
   - Parmak ekran genişliğinin %1'i kadar (~4 nokta) ilerleyince yön seçiliyor ve kaydırmanın geri kalanı yok sayılıyor.
   - İlk pikselleri öbür eksene kayan doğru kaydırma sayılmadı (3/3).
2. **Yönergeden hemen önce yapılan doğru hareket siliniyor.**
   - `DisplayTutorial(true)` bayrağı yeniden kaldırıyor.
   - Yaklaşık 1 saniye önceki doğru hareket sayılmadı (3/3).

Ölçüm aracı açıkken B'nin otomatik testleri 41/41 geçti. Gerçek parmakla iPhone'daki sıklık henüz ölçülmedi.

## 6. Bekleyen işler ve onaylar

- [ ] **/test/ adresini iPhone 13'te incele.** Ana ekrana ekle; simge, ad ve Türkçe yazıları kontrol et.
- [ ] **Önbellek iyileştirmesi** — ayrı test değişikliği olarak, onayınla:
  - `gelistirme`'den bir dalda yalnızca web derlemesinde "Name Files As Hashes" açılır.
  - `/test/`'te denenir.
  - Amaç: canlı güncellemede eski/yeni dosyaların karışmasını önlemek (GitHub Pages önbelleği 10 dk, dosya adları şu an sabit).
- [ ] **Eğitim ölçüm sürümünü iPhone'da denemek** için ayrı bir test adresi (örn. `/dev-olcum/`), onayınla.
- [ ] **Eğitim düzeltmesi** (onayınla). Öneri:
  - Yönü daha uzun harekete göre seçmek.
  - `TutorialMoveCheck`'i yan etkisiz yapmak.
  - Yönergeden kısa süre önceki doğru hareketi saymak.
  - Ayrıca: sert duraklama yerine kısa yavaş çekim, müziği kesmeden kısmak.
- [ ] **Canlı oyunun güncellenmesi:** yalnızca /test/ onaylanınca, ayrıca açık onayla (0. bölümdeki adımlar).

## 7. Sıradaki adım (döndüğünde buradan başla)

1. /test/ için iPhone sonuçlarını ilet.
2. 6. bölümdeki onaylardan hangisiyle devam edileceğini seç.
3. Büyük işler (sırayla): Aşama 2 (performans/bellek ölçümü), İstanbul temalı yeni ana menü, Android APK.

## 8. Başka bilgisayarda devam etmek

```bash
git clone https://github.com/samet3474/tabanlari-yagla-v1.git
cd tabanlari-yagla-v1
git checkout gelistirme        # ana geliştirme dalı (/test/'teki sürüm)
```

- Git LFS kurulu olmalı. Unity 6000.3.26f1.
- Eğitim ölçümü için: `git checkout b-egitim-olcum`.
- Yayın dalı için ayrı klasör: `git clone --branch gh-pages --single-branch https://github.com/samet3474/tabanlari-yagla-v1.git v1-pages`
- Unity her açılışta `AddressableAssetsData/link.xml` dosyasını siler; bu normal. `git restore` ile geri al, commit'e katma.
- `addressables_content_state.bin` dosyaları derleme çıktısıdır, commit'e katılmaz.
