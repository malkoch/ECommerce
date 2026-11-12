# Uygulama Planı ve Kabul Kriterleri

## Faz 0 --- Mimari kararlar

-   [x] .NET hedef sürümünü seç (tercihen desteklenen LTS sürüm;
    ekip/Rider uyumluluğunu doğrula).
-   [x] Servislerin ayrı PostgreSQL database ve kullanıcılarını belirle.
-   [x] Exchange, queue, routing key ve event sözleşmelerini sabitle.
-   [x] Sipariş durum makinesini tanımla.
-   [x] Lokal portları ve environment variable adlarını belirle.

**Çıkış kriteri:** `architecture.md` ve `event-contracts.md` ekipçe
anlaşılır ve tutarlı.

## Faz 1 --- Lokal altyapı

-   [x] Docker Compose ile PostgreSQL ve RabbitMQ başlat.
-   [x] RabbitMQ Management UI erişimini doğrula.
-   [x] Üç ayrı database ve kullanıcı oluştur.
-   [x] Servislerin lokal `appsettings.Development.json` / User Secrets
    konfigürasyonunu hazırla.
-   [x] Health check bağlantılarını doğrula.

**Çıkış kriteri:** Lokal çalışan .NET uygulamaları kendi DB'lerine ve
RabbitMQ'ya bağlanabiliyor.

## Faz 2 --- Order Service

-   [x] Sipariş ve sipariş kalemi domain modellerini oluştur.
-   [x] `POST /api/orders` ve `GET /api/orders/{id}` endpoint'lerini
    yaz.
-   [x] EF Core PostgreSQL provider ve migration ekle.
-   [x] Siparişi `PendingStock` durumunda kaydet.
-   [x] Transactional Outbox tablosu ve publisher background service
    ekle.
-   [x] API için unit ve integration test yaz.

**Kabul kriterleri:** - Geçersiz ürün/quantity girdileri reddedilir. -
Sipariş DB'ye kaydedilir. - `POST` çağrısı asenkron iş akışını başlatır
ve sipariş kimliği döndürür. - RabbitMQ kapalıyken sipariş ve outbox
kaydı aynı DB transaction'ında korunur.

## Faz 3 --- Stock Service

-   [x] Ürün/stok modeli ve başlangıç seed verisi oluştur.
-   [x] `OrderCreated` consumer'ı yaz.
-   [x] Stok rezervasyonunu atomik DB transaction'ında yap.
-   [x] Aynı `orderId` için ikinci rezervasyonu engelle.
-   [x] Başarılı/başarısız sonuç için outbox event üret.
-   [x] Inbox/idempotency mekanizmasını ekle.

**Kabul kriterleri:** - Yeterli stok varsa miktar yalnızca bir kez
düşer. - Yetersiz stokta stok negatif olmaz. - Bir siparişteki
ürünlerden biri yetersizse kısmi düşüm kalmaz. - Aynı mesaj tekrar
teslim edilirse ikinci kez stok azaltılmaz.

## Faz 4 --- Order sonuç consumer'ı

-   [x] Stock sonucu event'lerini tüket.
-   [x] Sipariş durumunu `Confirmed` veya `Rejected` yap.
-   [x] `OrderConfirmed` / `OrderRejected` event'lerini outbox'a yaz.
-   [x] Aynı sonuç event'i tekrar geldiğinde nihai durum tutarlı kalır.

**Kabul kriteri:** Sipariş akışı uçtan uca doğru durumla tamamlanır.

## Faz 5 --- Notification Service

-   [x] Sipariş sonucu event'lerini tüket.
-   [x] İlk aşamada `IEmailSender` ve `ISmsSender` arayüzlerinin
    mock/log implementasyonunu kullan.
-   [x] Notification DB'ye gönderim kaydı yaz.
-   [x] Sağlayıcı hatalarında sınırlı retry ve başarısız durum yönetimi
    ekle.
-   [x] Aynı event için mükerrer bildirim gönderimini önle.

**Kabul kriteri:** Sipariş sonucu notification DB'de loglanır; mock
gönderim çağrısı izlenebilir.

## Faz 6 --- Dayanıklılık ve kalite

-   [x] Retry/backoff ve DLQ.
-   [x] Publisher confirms.
-   [x] Inbox/Outbox temizleme ve saklama politikası.
-   [x] Correlation ID propagation.
-   [x] Yapılandırılmış log ve health checks.
-   [x] API, consumer ve DB integration testleri.
-   [x] Docker Compose README ve temiz kurulum testi.
-   [x] Hata senaryolarını demo et.

## Demo senaryoları

1.  Stok yeterli: sipariş `PendingStock` -\> `Confirmed`, bildirim logu
    oluşur.
2.  Stok yetersiz: sipariş `PendingStock` -\> `Rejected`, ret bildirimi
    logu oluşur.
3.  RabbitMQ kapalı: sipariş/outbox kaydı kalır; broker açılınca event
    yayınlanır.
4.  Aynı mesajı tekrar teslim et: stok ikinci kez düşmez, bildirim
    mükerrer gönderilmez.
5.  Notification sağlayıcısını hata verecek şekilde mock et: retry ve
    DLQ davranışını göster.

## Case study sunumunda anlatılacak teknik kararlar

-   Neden HTTP yerine RabbitMQ: servisleri gevşek bağlı ve iş akışını
    asenkron tutmak.
-   Neden servis başına DB: bounded context ve veri sahipliği.
-   Outbox neden gerekli: DB commit + broker publish çift yazma
    problemi.
-   Idempotency neden gerekli: RabbitMQ teslimatının tekrar edebilmesi.
-   Saga neden gerekli: servisler arası tek ACID transaction
    bulunmaması.
-   DLQ neden gerekli: sürekli hata veren mesajların ana kuyruğu bloke
    etmesini önlemek.
