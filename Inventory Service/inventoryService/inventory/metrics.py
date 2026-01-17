from prometheus_client import Counter, Gauge

orders_consumed = Counter(
    "inventory_orders_consumed_total",
    "Total orders consumed"
)

stock_failures = Counter(
    "inventory_stock_failures_total",
    "Stock update failures"
)

stock_retry_count = Counter(
    "inventory_retries_total",
    "Total retries"
)
