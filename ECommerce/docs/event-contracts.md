# RabbitMQ Event Contracts

Bu doküman servisler arasında taşınan mesajların ilk sürüm
sözleşmelerini tanımlar. Mesajlar JSON olarak taşınır. İsimler ve
alanlar örnek başlangıç sözleşmeleridir; kodlamaya başlamadan önce
sabitlenmelidir.

## Ortak mesaj zarfı

Her mesajda aşağıdaki metadata bulunmalıdır:

``` json
{
  "messageId": "uuid",
  "correlationId": "uuid",
  "occurredAtUtc": "2026-11-12T08:00:00Z",
  "eventType": "OrderCreated",
  "schemaVersion": 1,
  "payload": {}
}
```

-   `messageId`: Her mesaj için benzersiz kimlik; idempotency anahtarı.
-   `correlationId`: Aynı iş akışındaki mesajları ilişkilendirir.
-   `occurredAtUtc`: UTC ISO-8601 zaman damgası.
-   `eventType` ve `schemaVersion`: Sözleşme türü ve versiyonu.
-   `payload`: Olayın iş verisi.

## 1. OrderCreated

**Publisher:** Order Service\
**Consumer:** Stock Service\
**Exchange / routing key:** `ecommerce.events` / `order.created`

``` json
{
  "messageId": "uuid",
  "correlationId": "uuid",
  "occurredAtUtc": "2026-11-12T08:00:00Z",
  "eventType": "OrderCreated",
  "schemaVersion": 1,
  "payload": {
    "orderId": "uuid",
    "customerId": "uuid",
    "items": [
      {
        "productId": "uuid",
        "quantity": 2
      }
    ]
  }
}
```

## 2. StockReservationSucceeded

**Publisher:** Stock Service\
**Consumer:** Order Service\
**Routing key:** `stock.reservation.succeeded`

``` json
{
  "messageId": "uuid",
  "correlationId": "uuid",
  "occurredAtUtc": "2026-11-12T08:00:03Z",
  "eventType": "StockReservationSucceeded",
  "schemaVersion": 1,
  "payload": {
    "orderId": "uuid",
    "reservationId": "uuid"
  }
}
```

## 3. StockReservationFailed

**Publisher:** Stock Service\
**Consumer:** Order Service\
**Routing key:** `stock.reservation.failed`

``` json
{
  "messageId": "uuid",
  "correlationId": "uuid",
  "occurredAtUtc": "2026-11-12T08:00:03Z",
  "eventType": "StockReservationFailed",
  "schemaVersion": 1,
  "payload": {
    "orderId": "uuid",
    "reasonCode": "INSUFFICIENT_STOCK",
    "failedProductIds": ["uuid"]
  }
}
```

## 4. OrderConfirmed

**Publisher:** Order Service\
**Consumer:** Notification Service\
**Routing key:** `order.confirmed`

``` json
{
  "messageId": "uuid",
  "correlationId": "uuid",
  "occurredAtUtc": "2026-11-12T08:00:04Z",
  "eventType": "OrderConfirmed",
  "schemaVersion": 1,
  "payload": {
    "orderId": "uuid",
    "customerId": "uuid"
  }
}
```

## 5. OrderRejected

**Publisher:** Order Service\
**Consumer:** Notification Service\
**Routing key:** `order.rejected`

``` json
{
  "messageId": "uuid",
  "correlationId": "uuid",
  "occurredAtUtc": "2026-11-12T08:00:04Z",
  "eventType": "OrderRejected",
  "schemaVersion": 1,
  "payload": {
    "orderId": "uuid",
    "customerId": "uuid",
    "reasonCode": "INSUFFICIENT_STOCK"
  }
}
```

## Mesajlaşma kuralları

1.  Consumer mesajı başarıyla işleyip kendi DB transaction'ını commit
    etmeden ACK vermemelidir.
2.  Mesaj işlendikten sonra ACK kaybolursa broker mesajı tekrar teslim
    edebilir; consumer idempotent olmalıdır.
3.  Publisher confirm kullanılmalı; mesajlar persistent gönderilmeli ve
    exchange/queue durable olmalıdır.
4.  Retry için sınırlı deneme, gecikme ve DLQ politikası uygulanmalıdır.
5.  Mesaj sözleşmeleri geriye uyumlu genişletilmeli; kırıcı
    değişikliklerde yeni `schemaVersion` veya yeni event tipi
    kullanılmalıdır.
6.  Event payload'larına gereksiz kişisel veri eklenmemelidir.
7.  `OrderCreated` aynı sipariş için yeniden işlenirse Stock Service
    ikinci kez stok düşmemelidir. `orderId` bazlı rezervasyon
    benzersizliği de korunmalıdır.

## Komut mu olay mı?

Bu örnekte `OrderCreated` olaydır. Stok servisinden bir iş yapması
bekleniyor olsa da başlangıçta olay tabanlı akış kullanılır. Daha katı
komut/olay ayrımı istenirse `ReserveStock` komutu ve `StockReserved` /
`StockReservationRejected` sonuç olayları olarak adlandırılabilir. Takım
tek terminoloji seçip tutarlı kullanmalıdır.
