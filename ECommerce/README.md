# E-Ticaret Microservices Case Study

.NET, RabbitMQ ve PostgreSQL kullanılarak geliştirilecek örnek e-ticaret
altyapısı.

## Amaç

Bu depo; sipariş oluşturma, stok rezervasyonu ve kullanıcı bildirimi
süreçlerini bağımsız servisler olarak ele alır. Servisler kendi
PostgreSQL veritabanlarına sahiptir ve iş akışları RabbitMQ üzerinden
asenkron olaylarla ilerler.

> **Önemli mimari kararı:** Servisler arası stok ve bildirim işlemleri
> HTTP ile doğrudan çağrılmayacaktır. Bu işlemler RabbitMQ üzerinden
> mesajlaşma ile yürütülecektir. HTTP, istemcinin API Gateway/Order API
> gibi giriş noktalarına erişmesi ve servislerin kendi yönetim/health
> endpoint'leri için kullanılabilir.

## Başlangıç kapsamı

-   **Order Service:** Sipariş oluşturma ve sipariş durumunu yönetme.
-   **Stock Service:** Ürün stoklarını tutma ve siparişler için stok
    rezervasyonu yapma.
-   **Notification Service:** Sipariş sonucu hakkında e-posta/SMS
    bildirimi üretme ve gönderim kayıtlarını tutma.
-   **RabbitMQ:** Servisler arası asenkron mesajlaşma.
-   **PostgreSQL:** Servis başına ayrı veritabanı.
-   **Docker Compose:** PostgreSQL, RabbitMQ ve üç API servisini lokal
    ortamda çalıştırma (`docker compose watch` ile kod değişince
    otomatik rebuild).
-   **.NET:** Servisler istenirse Rider'dan lokalde de çalıştırılabilir.

## Önerilen depo yapısı

``` text
src/
  BuildingBlocks/
    Messaging/
    Observability/
  Services/
    Order/
      Order.Api/
      Order.Application/
      Order.Domain/
      Order.Infrastructure/
    Stock/
      Stock.Api/
      Stock.Application/
      Stock.Domain/
      Stock.Infrastructure/
    Notification/
      Notification.Api/
      Notification.Application/
      Notification.Domain/
      Notification.Infrastructure/
tests/
  Order.UnitTests/
  Order.IntegrationTests/
  Stock.UnitTests/
  Stock.IntegrationTests/
  Notification.UnitTests/
docs/
  architecture.md
  event-contracts.md
  implementation-plan.md
  local-development.md
.github/
  copilot-instructions.md
docker-compose.yml
.env.example
```

Bu yapı başlangıç önerisidir; case kapsamı büyümezse her servis için tek
Web API projesiyle başlanıp katmanlar daha sonra ayrıştırılabilir. Ancak
Domain/Application/Infrastructure sınırlarını korumak faydalıdır.

## Sipariş akışı (özet)

1.  İstemci `POST /api/orders` ile sipariş talebi gönderir.
2.  Order Service siparişi `PendingStock` durumunda kendi veritabanına
    kaydeder.
3.  Order Service, `OrderCreated` olayını RabbitMQ'ya yayınlar.
4.  Stock Service olayı tüketir, stokları tek transaction içinde kontrol
    edip rezerve eder.
5.  Stock Service `StockReservationSucceeded` veya
    `StockReservationFailed` olayını yayınlar.
6.  Order Service sonucu tüketir ve siparişi `Confirmed` veya `Rejected`
    durumuna geçirir.
7.  Order Service, `OrderConfirmed` veya `OrderRejected` olayını
    yayınlar.
8.  Notification Service ilgili olayı tüketir, bildirimi gönderir (ilk
    aşamada mock provider kullanılabilir) ve sonucu loglar.

Bu süreç dağıtık bir transaction değildir. Servisler arası tutarlılık
**Saga / eventual consistency** yaklaşımıyla sağlanır.

## Geliştirme sırası

1.  `docs/architecture.md` ve `docs/event-contracts.md` kararlarını
    netleştir.
2.  Docker Compose ile PostgreSQL ve RabbitMQ'yu ayağa kaldır.
3.  Servislerin solution/proje iskeletlerini oluştur.
4.  Önce Order API ve kendi veritabanını çalıştır.
5.  RabbitMQ publisher/consumer altyapısını ekle.
6.  Stock rezervasyon akışını tamamla.
7.  Notification consumer ve mock göndericiyi ekle.
8.  Retry, dead-letter queue, idempotency, outbox ve testleri tamamla.

## Lokal altyapı

Ayrıntılar: [`docs/local-development.md`](docs/local-development.md).

Önerilen lokal adresler:

-   RabbitMQ AMQP: `localhost:5672`
-   RabbitMQ Management UI: `http://localhost:15672`
-   PostgreSQL: `localhost:5432`

Geliştirme parolalarını yalnızca lokal kullanım için `.env` dosyasında
tut; `.env` dosyasını Git'e ekleme.

## Kalite hedefleri

-   Her servis yalnızca kendi veritabanına erişir.
-   Bir servis başka servisin tablolarına doğrudan erişmez.
-   Mesajlar en az bir kez teslim edilebilir varsayımıyla tasarlanır;
    consumer işlemleri idempotent olmalıdır.
-   Mesaj yayınlama ve veritabanı yazımı arasındaki çift yazma problemi
    için Transactional Outbox değerlendirilir.
-   Geçici hatalar sınırlı retry ve backoff ile ele alınır; kalıcı
    hatalar DLQ'ya yönlendirilir.
-   Loglarda `CorrelationId`, `OrderId` ve mesaj kimliği bulunur; hassas
    kişisel veri loglanmaz.
