import json
import pika
import django
import os
import sys
from django.db import transaction
from prometheus_client import start_http_server, Histogram

from datetime import datetime, timezone

# --- Django Setup ---
BASE_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.append(BASE_DIR)

os.environ.setdefault("DJANGO_SETTINGS_MODULE", "inventoryService.settings")
django.setup()
MAX_RETRIES = 3


QUEUE_LATENCY = Histogram(
    "inventory_queue_latency_seconds",
    "Time from publish to consume",
    buckets=(0.1, 0.3, 0.5, 1, 2, 5, 10)
)

def callback(ch, method, properties, body):
    try:
        from inventory.models import Product, ProcessedEvent
        from inventory.metrics import orders_consumed

        data = json.loads(body)

        event_id = data["EventId"]
        product_sku = data["ProductId"]
        quantity = data["Quantity"]

        published_at = datetime.fromisoformat(
            data["OccurredAt"].replace("Z", "+00:00")
        )

        now = datetime.now(timezone.utc)
        latency = (now - published_at).total_seconds()

        QUEUE_LATENCY.observe(latency)

        # 1️⃣ Idempotency check
        if ProcessedEvent.objects.filter(event_id=event_id).exists():
            print(f"[↩] Duplicate event {event_id}, skipping")
            ch.basic_ack(delivery_tag=method.delivery_tag)
            return

        print(f"[x] Processing event {event_id}")
        print(f"    Product {product_sku} - qty {quantity}")

        # 2️⃣ Atomic operation
        with transaction.atomic():
            product = Product.objects.select_for_update().get(sku=product_sku)

            if product.quantity < quantity:
                print("[❌] Not enough stock!")
                ch.basic_ack(delivery_tag=method.delivery_tag)
                return

            product.quantity -= quantity
            product.save()

            ProcessedEvent.objects.create(event_id=event_id)

        print("[✔] Stock updated & event recorded")

        # 3️⃣ ACK only after success
        ch.basic_ack(delivery_tag=method.delivery_tag)
        orders_consumed.inc()
    except Product.DoesNotExist:
        print("[!] Product not found - retrying...")
        _handle_retry(ch, method, properties, body)

    except Exception as e:
        print(f"[!] Error: {str(e)} - retrying...")
        _handle_retry(ch, method, properties, body)


def _handle_retry(ch, method, properties, body):
    """Handle message retry with TTL"""
    from inventory.metrics import stock_retry_count, stock_failures
    headers = properties.headers or {}
    retry_count = int(headers.get("x-retry-count", 0))

    if retry_count >= MAX_RETRIES:
        print(f"[☠] Max retries ({MAX_RETRIES}) reached → DLQ")
        ch.basic_nack(delivery_tag=method.delivery_tag, requeue=False)
        stock_failures.inc()
        return

    retry_count += 1
    print(f"[🔁] Retry {retry_count}/{MAX_RETRIES}")

    headers["x-retry-count"] = retry_count

    # Publish to retry queue using default exchange
    ch.basic_publish(
        exchange="",  # Default exchange
        routing_key="orderQueue.retry",
        body=body,
        properties=pika.BasicProperties(
            headers=headers,
            delivery_mode=2  # Persistent
        )
    )

    # ACK the original message after successfully publishing to retry queue
    ch.basic_ack(delivery_tag=method.delivery_tag)
    stock_retry_count.inc()


def start_consumer():
    start_http_server(8001)
    print("[📊] Prometheus metrics running on :8001")
    connection = pika.BlockingConnection(
        pika.ConnectionParameters(
            host="localhost",
            port=5672,
            credentials=pika.PlainCredentials("guest", "guest")
        )
    )

    channel = connection.channel()

    # 🔴 Declare DLX Exchange
    channel.exchange_declare(exchange="order.dlx", exchange_type="direct", durable=True)

    # 🟢 Main Queue with DLX fallback
    main_queue_args = {
        "x-dead-letter-exchange": "order.dlx",  # Send to DLX exchange
        "x-dead-letter-routing-key": "order.dead"
    }
    channel.queue_declare(
        queue="orderQueue",
        durable=True,
        arguments=main_queue_args
    )

    # 🟡 Retry Queue with TTL (5 seconds) → back to main queue via default exchange
    retry_queue_args = {
        "x-message-ttl": 5000,  # 5 seconds TTL
        "x-dead-letter-exchange": "",  # Send back to default exchange
        "x-dead-letter-routing-key": "orderQueue"  # Back to main queue
    }
    channel.queue_declare(
        queue="orderQueue.retry",
        durable=True,
        arguments=retry_queue_args
    )

    # 🔴 DLQ - Bind to DLX
    channel.queue_declare(
        queue="orderQueue.dlq",
        durable=True
    )
    channel.queue_bind(
        exchange="order.dlx",
        queue="orderQueue.dlq",
        routing_key="order.dead"
    )

    channel.basic_qos(prefetch_count=1)

    print("[*] Inventory service waiting for messages...")
    print("[*] Main Queue: orderQueue")
    print("[*] Retry Queue: orderQueue.retry (TTL: 5s)")
    print("[*] DLQ: orderQueue.dlq")
    
    channel.basic_consume(
        queue="orderQueue",
        on_message_callback=callback
    )

    channel.start_consuming()


if __name__ == "__main__":
    start_consumer()
