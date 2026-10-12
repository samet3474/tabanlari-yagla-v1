# Nerede kaldık? — Tabanları Yağla

> Her aşamanın sonunda güncellenir. Projeye ara verip döndüğünde **önce bu dosyayı oku.**
> Ayrıntılı geçmiş: [GELISTIRME_NOTLARI.md](GELISTIRME_NOTLARI.md) (bu dosya ondan daha güncel).

**Son güncelleme:** 2026-10-12

---

## 1. Kısaca durum

| Konu | Durum |
|---|---|
| Canlı oyun (kök adres) | Eski A sürümü. **Değiştirilmedi.** |
| Aşama 1 — ad ve marka | Bitti. Test adresinde. main'e **alınmadı** (onay bekliyor). |
| Türkçe arayüz | Bitti, commit edildi. Test adresinde. main'e **alınmadı** (onay bekliyor). |
| Eğitim (tutorial) dokunma sorunu | **Neden ölçülerek bulundu.** Düzeltme yapılmadı, onay bekliyor. |
| B — eğitim akışı düzeltmeleri | `b-tutorial-test` dalında, main'e alınmadı. |
| Gerçek iPhone 13 testi | **Yapılmadı.** Ad/marka ve Türkçe test adreslerini senin cihazında denemen gerekiyor. |

## 2. Test adresleri (telefonda aç)

| Adres | İçerik |
|---|---|
| https://samet3474.github.io/tabanlari-yagla-v1/ | Canlı oyun (eski A sürümü) |
| https://samet3474.github.io/tabanlari-yagla-v1/dev/ | B (eğitim akışı) test sürümü, zaman çizelgesi katmanlı |
| https://samet3474.github.io/tabanlari-yagla-v1/test-ad/ | Aşama 1: yeni ad, logo, simgeler |
| https://samet3474.github.io/tabanlari-yagla-v1/test-dil/ | Aşama 1 + Türkçe arayüz |

Her klasörün kaydı ayrıdır (kayıt, adresin klasörüne bağlı). Test adresleri asıl oyundaki kaydına dokunmaz.

## 3. Dallar, commit'ler ve geri dönüş noktaları

| Dal | Son commit | Ne var |
|---|---|---|
| `main` | `97135aa` | Canlı A sürümünün kaynağı + notlar |
| `ad-marka` | `4c9244a` | main + Aşama 1 (ad, logo, simgeler, Türkçe görünen adlar) |
| `dil-turkce` | `638289b` | ad-marka + Türkçe arayüz (LocText, tr.txt, mobil yerleşim düzeltmeleri) |
| `b-tutorial-test` | `d5f0f2f` | main + B (eğitim akışı) + test araçları |
| `b-egitim-olcum` | `2188d73` | b-tutorial-test + eğitim girdi teşhis araçları (yalnızca test derlemesi) |
| `gh-pages` | `b2bf7c0` | Yayın: kök, /dev/, /test-ad/, /test-dil/ |

Geri dönüş etiketleri:
- `yayin-A` → gh-pages'in test-ad öncesi hâli (`c543b23`).
- `yayin-faz02`, `v1.1-analiz-oncesi` → daha eski.
- Yerel (yalnızca ev bilgisayarında): `asama1-oncesi`, `dil-oncesi`, `egitim-olcum-oncesi`.

Yedekler (ev bilgisayarı): `Documents/Unity/_yedekler/` → `asama1-gorseller-2026-10-12`, `dil-turkce-2026-10-12`.

## 4. Son yapılanlar (en yeni üstte)

1. **Eğitim dokunma sorunu ölçüldü** (`b-egitim-olcum`). Oyun koduna dokunulmadı. Sonuçlar 5. bölümde.
2. **Türkçe arayüz** (`dil-turkce` 638289b), **/test-dil/** adresinde yayında.
   - Bütün düğmeler ve metinler `tr.txt` dil tablosundan geliyor.
   - Tablo silinirse oyun çökmüyor, İngilizceye düşüyor (test edildi).
   - 360×800, 375×667, 390×844 ve 430×932 boyutlarında taşmalar düzeltildi.
   - Kayıt v12 ve kimlikler aynı kaldı (test edildi).
3. **Aşama 1 — ad ve marka** (`ad-marka` 4c9244a), **/test-ad/** adresinde yayında.

## 5. Eğitim sorunu — bulgular (ölçüldü, henüz düzeltilmedi)

Şikâyet: "İlk hareket bazen sayılmıyor, ikincisinde geçiyor; bütün hareketlerde."

Eğitim adımı tek bir bayrağa bağlı: `CharacterInputController.tutorialWaitingForValidation`. Gerçek dokunma olaylarıyla, üç seviyede de iki ayrı neden gösterildi:

1. **Yön çok erken seçiliyor.**
   - Parmak ekran genişliğinin yalnızca %1'i kadar ilerleyince (iPhone 13'te ~4 nokta) yön seçiliyor ve kaydırma bitiyor.
   - Doğru yöne yapılan ama ilk birkaç pikseli öbür eksene kayan kaydırma yanlış hareket sayılıyor (ya da hiçbir şey yapmıyor); adım tamamlanmıyor.
   - Ölçüm: eğik kaydırma 3/3 seviyede sayılmadı, ikinci temiz kaydırma 3/3 geçti.
2. **Yönergeden hemen önce yapılan doğru hareket siliniyor.**
   - Hareket karaktere uygulanıyor; ama yönerge açılınca `DisplayTutorial(true)` bayrağı yeniden kaldırıyor ve oyun aynı hareketi tekrar istiyor.
   - Ölçüm: ~1 saniye önce yapılan doğru hareket 3/3 seviyede sayılmadı.

Ek bulgu: B'nin 41/41 otomatik testi bu yolu hiç sınamıyordu (bot bayrağı kendisi düşürüyor). Ölçüm aracı açıkken de 41/41 geçti; araç akışı bozmuyor.

**Henüz bilinmeyen:** Gerçek parmakla iPhone'da bu iki durumun ne sıklıkla olduğu. Bunun için ölçüm sürümü iPhone'da denenmeli (aşağıda).

## 6. Senin onayını bekleyenler

- [ ] Türkçe arayüzü ve Aşama 1'i iPhone 13'te dene: /test-ad/ ve /test-dil/ (ana ekrana ekle, simge, ad, yazılar).
- [ ] Eğitim ölçüm sürümünü iPhone'da denemek için ayrı bir test adresine (örn. /dev-olcum/) yayın onayı.
- [ ] Eğitim düzeltmesine başlama onayı. Öneri:
  - Yönü ilk piksellere göre değil, biraz daha uzun harekete göre seçmek.
  - Yönergeden kısa süre önce yapılan doğru hareketi saymak.
  - Ayrı konu: sert duraklama yerine kısa yavaş çekim ve müziği kesmeden kısmak.
- [ ] Sonra: `ad-marka` → `dil-turkce` → main birleştirme ve kök yayının güncellenmesi (onaysız yapılmaz).

## 7. Sıradaki adım (döndüğünde buradan başla)

1. Bu dosyayı ve 6. bölümdeki onay listesini oku.
2. iPhone sonuçlarını (test-ad, test-dil) ilet.
3. Eğitim için: ölçüm sürümünün yayın onayı → iPhone'da ölçüm → düzeltme önerisi → onay → düzeltme (`b-egitim-olcum` üzerinden ayrı dalda).
4. Bekleyen büyük işler: Aşama 2 (performans/bellek ölçümü), İstanbul temalı yeni ana menü, Android APK.

## 8. Başka bilgisayarda devam etmek

```bash
git clone https://github.com/samet3474/tabanlari-yagla-v1.git
cd tabanlari-yagla-v1
git checkout dil-turkce        # en güncel oyun (Aşama 1 + Türkçe)
```

- Git LFS kurulu olmalı. Unity 6000.3.26f1.
- Eğitim ölçümü için: `git checkout b-egitim-olcum`.
- Unity her açılışta `AddressableAssetsData/link.xml` dosyasını siler; bu normal (Addressables'ın geçici dosyası). `git restore` ile geri al, commit'e katma.
- `addressables_content_state.bin` dosyaları derleme çıktısıdır, commit'e katılmaz.
