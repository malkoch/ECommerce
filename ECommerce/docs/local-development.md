# Lokal Geliştirme Ortamı

Bu aşamada Docker yalnızca PostgreSQL ve RabbitMQ altyapısını
çalıştırır. .NET servisleri JetBrains Rider üzerinden lokal olarak
başlatılır.

## Ön koşullar

-   Docker Desktop veya Docker Engine + Compose plugin
-   JetBrains Rider
-   Desteklenen .NET SDK sürümü
-   Git

## Önerilen servis portları

  Bileşen                    Host port
  ------------------------ -----------
  PostgreSQL                      5432
  RabbitMQ AMQP                   5672
  RabbitMQ Management UI         15672
  Order API                       5101
  Stock API                       5102
  Notification API                5103

Servis API portları öneridir; launchSettings ve appsettings ile aynı
tutulmalıdır.

## Başlatma

1.  Depo kökünde `.env.example` dosyasını `.env` olarak kopyala.
2.  Lokal geliştirme parolalarını `.env` içinde değiştir.
3.  Altyapıyı başlat:

``` bash
docker compose up -d
docker compose ps
```

4.  RabbitMQ Management UI: `http://localhost:15672`
5.  PostgreSQL'e `localhost:5432` üzerinden bağlan.
6.  .NET servislerini Rider'dan ayrı run configuration'larla başlat.

## Servisleri Docker'da çalıştırma

`docker compose up -d` altyapıyla birlikte üç API servisini de
(`order-api`, `stock-api`, `notification-api`) çalıştırır. İmajlar kök
`Dockerfile` ile build edilir. Container içinde servisler host adı olarak
`postgres` ve `rabbitmq` kullanır; bağlantı bilgileri compose'daki
environment değişkenleriyle gelir.

Kod değiştikçe imajı yeniden build edip container'ı güncellemek için:

``` bash
docker compose up -d --build   # tek seferlik güncelleme
docker compose watch           # src/ değişince otomatik rebuild
```

Servis portları (5101-5103) Rider'dan çalıştırılan servislerle çakışır;
ikisini aynı anda çalıştırma (`docker compose stop order-api` gibi).

Kapatmak için:

``` bash
docker compose down
```

Veri volume'larını da silerek sıfırlamak için (kalıcı veriyi siler):

``` bash
docker compose down -v
```

## Connection string yaklaşımı

Her servis yalnızca kendi database'ine bağlanmalıdır. Örnek isimler:

-   `OrderDb`
-   `StockDb`
-   `NotificationDb`

Connection string'leri kaynak koda veya Git'e commit etme. Lokal
geliştirmede .NET User Secrets veya environment variable kullan.
Container içinden bağlanan bir servis olursa PostgreSQL host adı
`localhost` değil Compose service adı olur; bu case'in ilk aşamasında
servisler host üzerinde çalıştığı için `localhost` kullanılır.

## Rider çalışma düzeni

-   Her servis için ayrı run configuration oluştur.
-   Her servisin environment variable ve ASP.NET Core URL değerlerini
    ayrı tanımla.
-   İlk olarak PostgreSQL ve RabbitMQ'nun hazır olduğunu doğrula.
-   Servisleri Order, Stock, Notification sırasıyla başlat.
-   Consumer servisleri broker'a bağlanamazsa kontrollü reconnect
    uygulamalı; uygulama başlangıcını gereksiz yere sonsuza kadar
    bloklamamalıdır.

## Seed ürünleri ve test

Stock DB şu ürünlerle başlar (sipariş denemek için `productId` olarak kullan):

| Ürün | productId | Stok |
| ---- | --------- | ---- |
| Laptop | `11111111-1111-1111-1111-111111111111` | 50 |
| Phone | `22222222-2222-2222-2222-222222222222` | 100 |
| Headphones | `33333333-3333-3333-3333-333333333333` | 5 |

Stock integration testleri çalışan PostgreSQL'e karşı geçici bir database
oluşturur:

``` bash
TEST_POSTGRES_ADMIN="Host=localhost;Username=postgres;Password=<admin parolası>" \
  dotnet test tests/Stock.IntegrationTests
```

## Notification hata senaryosu

Mock sağlayıcıyı hata verecek şekilde başlat (3 deneme sonrası bildirim
`Failed` olur ve mesaj `notification.order-events.dlq`'ya düşer):

``` bash
NOTIFICATION_FAIL_SMS=true docker compose up -d notification-api   # veya NOTIFICATION_FAIL_EMAIL=true
```

Sağlayıcıyı düzeltip (`docker compose up -d notification-api`) DLQ
mesajını RabbitMQ UI'dan `ecommerce.events` exchange'ine tekrar
yayınlarsan yalnızca `Failed` kanal yeniden denenir; `Sent` kanal
tekrar gönderilmez. Alıcı adresi gerçek bir müşteri servisi olmadığı için
`customerId`'den türetilen sahte değerdir.

## Sık karşılaşılan sorunlar

-   **Port kullanımda:** `5432`, `5672` veya `15672` portlarını kullanan
    süreci kontrol et ya da host portunu değiştir.
-   **DB bağlantı hatası:** Container health durumunu, database adını,
    kullanıcıyı ve parolayı kontrol et.
-   **RabbitMQ bağlantı hatası:** AMQP portu `5672`; yönetim arayüzü
    portu `15672`'dir.
-   **Migration hatası:** Migration'ı ilgili servisin kendi DbContext'i
    ve startup projesiyle çalıştır.
-   **Mesaj kayboldu sanılıyor:** Queue binding, routing key, ACK/NACK
    ve consumer loglarını kontrol et.

## Güvenlik notları

Bu Compose ayarı yalnızca lokal geliştirme içindir. Varsayılan
kullanıcı/parolaları production'da kullanma; broker ve database
portlarını internete açma.

## Dayanıklılık ve gözlemlenebilirlik

- **Consumer retry:** Geçici hatalarda mesaj `RabbitMq:ConsumerMaxAttempts` (varsayılan 3) kez, `ConsumerRetryBaseDelayMs` (500 ms) tabanlı üstel backoff ile denenir. Hâlâ başarısızsa DLQ'ya gider. `PermanentMessageException` ve `JsonException` (geçersiz payload, bilinmeyen event tipi, tükenmiş bildirim denemeleri) tekrar denenmeden doğrudan DLQ'ya gider.
- **Retention:** `Messaging:Retention:RetentionDays` (7) ve `IntervalMinutes` (60). Order ve Stock'ta işlenmiş outbox ve inbox kayıtları süre dolunca silinir. Notification kendi `notifications` tablosunu kullandığı için temizlik uygulanmaz.
- **Health:** `/health/live` yalnızca process'i, `/health/ready` PostgreSQL ve RabbitMQ'yu kontrol eder (RabbitMQ kapalıyken 503).
- **Log:** JSON console log; `ServiceName`, `CorrelationId`, `MessageId`, `EventType` scope alanları içerir. HTTP isteğinde `X-Correlation-Id` başlığı okunur (yoksa üretilir), yanıta eklenir ve eventlerle servisler arası taşınır.
- **Testler:** `dotnet test tests/Messaging.UnitTests` (retry kuralları).

## Demo

`./scripts/demo.sh` Docker'daki servislerde şu senaryoları çalıştırır: yeterli stok (Confirmed), yetersiz stok (Rejected), RabbitMQ kapalıyken sipariş kabulü ve açılınca işlenmesi (outbox). Duplicate mesaj ve Notification sağlayıcı hatası → DLQ → replay senaryoları yukarıdaki bölümlerde, entegrasyon testleri (`tests/*IntegrationTests`) ile birlikte anlatılmıştır.

Temiz kurulum: `docker compose down -v && docker compose up -d --build` (servisler `/health/ready` 200 dönene kadar bekleyin).
