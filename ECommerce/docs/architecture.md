# Mimari Tasarım

## 1. Servis sorumlulukları

  -----------------------------------------------------------------------
  Servis                  Sorumluluk              Kendi verisi
  ----------------------- ----------------------- -----------------------
  Order Service           Sipariş oluşturma,      Orders, OrderItems,
                          durum geçişleri,        Outbox, Inbox
                          sipariş API'si          

  Stock Service           Ürün stok miktarı ve    Products/StockItems,
                          rezervasyon             StockReservations,
                                                  Outbox, Inbox

  Notification Service    E-posta/SMS gönderimi   Notifications (message_id+channel unique)
                          ve gönderim geçmişi     
  -----------------------------------------------------------------------

Her servis kendi PostgreSQL veritabanını (veya en azından ayrı
database/schema ve ayrı erişim yetkisini) yönetir. Gerçek mikroservis
izolasyonu için ayrı database ve kullanıcı tercih edilir.

## 2. İletişim modeli

-   **Senkron HTTP:** İstemci -\> Order API. Gerekirse servislerin kendi
    health/metrics endpoint'leri.
-   **Asenkron RabbitMQ:** Order -\> Stock ve Order -\> Notification iş
    akışları.
-   **Event-driven:** Servisler komut/olay sözleşmeleri üzerinden
    haberleşir; başka servisin repository veya DB'sini çağırmaz.

Önerilen exchange:

-   `ecommerce.events` --- durable `topic` exchange.
-   Routing key örnekleri:
    -   `order.created`
    -   `stock.reservation.succeeded`
    -   `stock.reservation.failed`
    -   `order.confirmed`
    -   `order.rejected`

Önerilen kuyruklar:

-   `stock.order-created` --- `order.created` binding.
-   `order.stock-reservation-result` --- `stock.reservation.*` binding.
-   `notification.order-events` --- `order.confirmed` ve
    `order.rejected` binding.

Kuyruklar durable olmalı; mesajlar persistent gönderilmelidir.
Queue/exchange/binding tanımları uygulama başlangıcında idempotent
biçimde oluşturulabilir veya deployment tanımında yönetilebilir.

## 3. Sipariş Saga akışı

``` mermaid
sequenceDiagram
    participant Client
    participant Order
    participant Rabbit as RabbitMQ
    participant Stock
    participant Notification

    Client->>Order: POST /api/orders
    Order->>Order: PendingStock olarak kaydet
    Order->>Rabbit: order.created
    Rabbit->>Stock: order.created
    Stock->>Stock: Stok kontrolü ve rezervasyon
    Stock->>Rabbit: stock.reservation.succeeded / failed
    Rabbit->>Order: Rezervasyon sonucu
    Order->>Order: Confirmed / Rejected
    Order->>Rabbit: order.confirmed / order.rejected
    Rabbit->>Notification: Sipariş sonucu
    Notification->>Notification: Gönder ve logla
```

Order API ilk istekte `202 Accepted` ve sipariş kimliği/durumunu
döndürebilir. İstemci durum için `GET /api/orders/{id}` çağırır. İşlem
asenkron tamamlanır.

### Durumlar

-   `PendingStock`: Sipariş kaydedildi, stok sonucu bekleniyor.
-   `Confirmed`: Stok rezervasyonu başarılı.
-   `Rejected`: Stok yetersiz veya iş kuralı nedeniyle sipariş
    reddedildi.

Durum geçişleri kontrollü olmalıdır. Aynı olay tekrar gelirse siparişin
nihai durumu bozulmamalıdır.

## 4. Güvenilirlik desenleri

### Transactional Outbox

Order Service aynı PostgreSQL transaction'ında siparişi ve yayınlanacak
olayı `OutboxMessages` tablosuna yazar. Ayrı bir background publisher
outbox kayıtlarını RabbitMQ'ya gönderir ve başarılı yayın sonrası kaydı
işaretler. Böylece DB commit olup mesajın yayınlanmaması riski
azaltılır.

Aynı desen Stock Service'in sonuç olayları için de uygulanabilir.
Notification Service genellikle dış sağlayıcı çağrısı öncesi/sonrası
kendi gönderim durumunu kaydeder.

### Inbox / Idempotent Consumer

Her consumer, mesajın `MessageId` değerini Inbox tablosunda benzersiz
tutar. Mesaj daha önce işlendi ise iş mantığı tekrar çalıştırılmaz.
Inbox kaydı ve iş verisi aynı yerel DB transaction'ında yazılmalıdır.

### Retry ve Dead Letter Queue

-   Geçici hatalarda sınırlı sayıda retry ve exponential backoff.
-   Poison message / kalıcı iş hataları DLQ'ya yönlendirilir.
-   Sonsuz requeue döngüsünden kaçınılır.
-   DLQ için alarm, inceleme ve kontrollü replay prosedürü tanımlanır.
-   İş kuralı hataları (ör. yetersiz stok) teknik retry sebebi değildir;
    başarısız sonuç olayıdır.

### Stok tutarlılığı

Stok azaltma işlemi atomik olmalıdır. Örneğin koşullu SQL güncellemesi:

``` sql
UPDATE stock_items
SET available_quantity = available_quantity - @quantity
WHERE product_id = @productId
  AND available_quantity >= @quantity;
```

Etkilenen satır sayısı `1` ise rezervasyon başarılıdır; `0` ise stok
yetersizdir. Bir siparişte birden fazla ürün varsa tüm satırlar tek
Stock DB transaction'ında işlenmelidir. Kısmi rezervasyon oluşursa
transaction rollback edilmelidir.

## 5. Hata senaryoları

  -----------------------------------------------------------------------
  Senaryo                             Beklenen davranış
  ----------------------------------- -----------------------------------
  RabbitMQ geçici olarak kapalı       Outbox mesajı DB'de bekler;
                                      publisher tekrar dener

  Consumer mesajı aldıktan sonra      ACK verilmediyse broker yeniden
  çöktü                               teslim edebilir; idempotency
                                      gerekir

  Aynı mesaj iki kez geldi            Inbox/unique MessageId ile ikinci
                                      işlem etkisiz kalır

  Stok yetersiz                       `StockReservationFailed`
                                      yayınlanır; Order `Rejected` olur

  E-posta sağlayıcısı hata verdi      Notification retry politikası
                                      uygular; başarısızsa DLQ/Failed
                                      durumu

  Mesaj şeması değişti                Sözleşme versiyonlama ve geriye
                                      uyumluluk uygulanır
  -----------------------------------------------------------------------

## 6. Gözlemlenebilirlik

Yapılandırılmış log kullan. En azından şu alanları taşı:

-   `ServiceName`
-   `CorrelationId`
-   `MessageId`
-   `OrderId`
-   `EventType`
-   `Attempt`
-   `ElapsedMilliseconds`

Şifre, erişim token'ı, kart verisi ve gereksiz kişisel veriler
loglanmamalıdır. Başlangıçta `ILogger` + JSON console logging
yeterlidir; daha sonra OpenTelemetry, Prometheus/Grafana veya merkezi
log sistemi eklenebilir.

## 7. API taslağı

-   `POST /api/orders` --- sipariş oluşturur, `202 Accepted` döner.
-   `GET /api/orders/{orderId}` --- siparişin güncel durumunu döner.
-   `GET /health/live` --- process canlılık kontrolü.
-   `GET /health/ready` --- DB ve gerekli bağımlılıkların hazır olma
    kontrolü.

Stok yönetim endpoint'leri case kapsamına göre eklenebilir; stok
rezervasyonu servisler arasında HTTP ile değil RabbitMQ mesajıyla
yapılır.
