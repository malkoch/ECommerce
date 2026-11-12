#!/bin/bash
# Servis başına ayrı kullanıcı ve database oluşturur (yalnızca ilk volume init'inde çalışır).
set -euo pipefail

create_db() {
  local db="$1" user="$2" password="$3"
  psql -v ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname postgres <<-EOSQL
    CREATE USER ${user} WITH PASSWORD '${password}';
    CREATE DATABASE "${db}" OWNER ${user};
    REVOKE ALL ON DATABASE "${db}" FROM PUBLIC;
EOSQL
}

create_db "OrderDb" "order_user" "$ORDER_DB_PASSWORD"
create_db "StockDb" "stock_user" "$STOCK_DB_PASSWORD"
create_db "NotificationDb" "notification_user" "$NOTIFICATION_DB_PASSWORD"
