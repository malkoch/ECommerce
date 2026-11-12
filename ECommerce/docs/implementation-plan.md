# Uygulama Planı ve Kabul Kriterleri

## Faz 0 --- Mimari kararlar

-   [ ] .NET hedef sürümünü seç (tercihen desteklenen LTS sürüm;
    ekip/Rider uyumluluğunu doğrula).
-   [ ] Servislerin ayrı PostgreSQL database ve kullanıcılarını belirle.
-   [ ] Exchange, queue, routing key ve event sözleşmelerini sabitle.
-   [ ] Sipariş durum makinesini tanımla.
-   [ ] Lokal portları ve environment variable adlarını belirle.

**Çıkış kriteri:** `architecture.md` ve `event-contracts.md` ekipçe
anlaşılır ve tutarlı.

## Faz 1 --- Lokal altyapı

-   [ ] Docker Compose ile PostgreSQL ve RabbitMQ başlat.
-   [ ] RabbitMQ Management UI erişimini doğrula.
-   [ ] Üç ayrı database ve kullanıcı oluştur.
-   [ ] Servislerin lokal `appsettings.Development.json` / User Secrets
    konfigürasyonunu hazırla.
-   [ ] Health check bağlantılarını doğrula.

**Çıkış kriteri:** Lokal çalışan .NET uygulamaları kendi DB'lerine ve
RabbitMQ'ya bağlanabiliyor.

## Faz 2 --- Order Service

-   [ ] Sipariş ve sipariş kalemi domain modellerini oluştur.
-   [ ] `POST /api/orders` ve `GET /api/orders/{id}` endpoint'lerini
    yaz.
-   [ ] EF Core PostgreSQL provider ve migration ekle.
-   [ ] Siparişi `PendingStock` durumunda kaydet.
-   [ ] Transactional Outbox tablosu ve publisher background service
    ekle.
-   [ ] API için unit ve integration test yaz.

**Kabul kriterleri:** - Geçersiz ürün/quantity girdileri reddedilir. -
Sipariş DB'ye kaydedilir. - `POST` çağrısı asenkron iş akışını başlatır
ve sipariş kimliği döndürür. - RabbitMQ kapalıyken sipariş ve outbox
kaydı aynı DB transaction'ında korunur.

## Faz 3 --- Stock Service

-   [ ] Ürün/stok modeli ve başlangıç seed verisi oluştur.
-   [ ] `OrderCreated` consumer'ı yaz.
-   [ ] Stok rezervasyonunu atomik DB transaction'ında yap.
-   [ ] Aynı `orderId` için ikinci rezervasyonu engelle.
-   [ ] Başarılı/başarısız sonuç için outbox event üret.
-   [ ] Inbox/idempotency mekanizmasını ekle.

**Kabul kriterleri:** - Yeterli stok varsa miktar yalnızca bir kez
düşer. - Yetersiz stokta stok negatif olmaz. - Bir siparişteki
ürünlerden biri yetersizse kısmi düşüm kalmaz. - Aynı mesaj tekrar
teslim edilirse ikinci kez stok azaltılmaz.

## Faz 4 --- Order sonuç consumer'ı

-   [ ] Stock sonucu event'lerini tüket.
-   [ ] Sipariş durumunu `Confirmed` veya `Rejected` yap.
-   [ ] `OrderConfirmed` / `OrderRejected` event'lerini outbox'a yaz.
-   [ ] Aynı sonuç event'i tekrar geldiğinde nihai durum tutarlı kalır.

**Kabul kriteri:** Sipariş akışı uçtan uca doğru durumla tamamlanır.

## Faz 5 --- Notification Service

-   [ ] Sipariş sonucu event'lerini tüket.
-   [ ] İlk aşamada `IEmailSender` ve `ISmsSender` arayüzlerinin
    mock/log implementasyonunu kullan.
-   [ ] Notification DB'ye gönderim kaydı yaz.
-   [ ] Sağlayıcı hatalarında sınırlı retry ve başarısız durum yönetimi
    ekle.
-   [ ] Aynı event için mükerrer bildirim gönderimini önle.

**Kabul kriteri:** Sipariş sonucu notification DB'de loglanır; mock
gönderim çağrısı izlenebilir.

## Faz 6 --- Dayanıklılık ve kalite

-   [ ] Retry/backoff ve DLQ.
-   [ ] Publisher confirms.
-   [ ] Inbox/Outbox temizleme ve saklama politikası.
-   [ ] Correlation ID propagation.
-   [ ] Yapılandırılmış log ve health checks.
-   [ ] API, consumer ve DB integration testleri.
-   [ ] Docker Compose README ve temiz kurulum testi.
-   [ ] Hata senaryolarını demo et.

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
