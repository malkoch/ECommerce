#!/usr/bin/env bash
# Docker'da çalışan servisler üzerinde temel senaryoları gösterir.
# Kullanım: ./scripts/demo.sh   (önce: docker compose up -d --build)
set -euo pipefail

ORDER=${ORDER_URL:-http://localhost:5101}
LAPTOP=11111111-1111-1111-1111-111111111111
HEADPHONES=33333333-3333-3333-3333-333333333333

place_order() { # $1=productId $2=quantity
  curl -s -X POST "$ORDER/api/orders" -H 'Content-Type: application/json' \
    -d "{\"customerId\":\"$(uuidgen | tr A-Z a-z)\",\"items\":[{\"productId\":\"$1\",\"quantity\":$2}]}" |
    python3 -c 'import sys,json; print(json.load(sys.stdin)["orderId"])'
}

order_status() { curl -s "$ORDER/api/orders/$1" | python3 -c 'import sys,json; d=json.load(sys.stdin); print(d.get("status"), d.get("rejectionReason") or "")'; }

wait_final() {
  for _ in $(seq 1 20); do
    s=$(order_status "$1"); [[ "$s" != PendingStock* ]] && { echo "$s"; return; }
    sleep 1
  done
  echo "$s (zaman aşımı)"
}

echo "1) Yeterli stok -> Confirmed bekleniyor"
id=$(place_order $LAPTOP 1); echo "   $id: $(wait_final "$id")"

echo "2) Yetersiz stok -> Rejected bekleniyor"
id=$(place_order $HEADPHONES 1000); echo "   $id: $(wait_final "$id")"

echo "3) RabbitMQ kapalıyken sipariş kabul edilir, açılınca işlenir (outbox)"
docker compose stop rabbitmq >/dev/null
id=$(place_order $LAPTOP 1); echo "   kabul edildi: $id: $(order_status "$id")"
docker compose start rabbitmq >/dev/null
echo "   broker açıldı: $(wait_final "$id")"

echo "Bitti. Duplicate mesaj ve Notification/DLQ senaryoları için docs/local-development.md'ye bakın."
