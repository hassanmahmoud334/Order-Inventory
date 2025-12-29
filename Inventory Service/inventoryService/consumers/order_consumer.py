import json
import pika
import django
import os
import sys
from django.db import transaction

# --- Django Setup ---
BASE_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.append(BASE_DIR)

os.environ.setdefault("DJANGO_SETTINGS_MODULE", "inventoryService.settings")
django.setup()


def callback(ch, method, properties, body):
    try:
        from inventory.models import Product, ProcessedEvent

        data = json.loads(body)

        event_id = data["EventId"]
        product_sku = data["ProductId"]
        quantity = data["Quantity"]

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

    except Product.DoesNotExist:
        print("[❌] Product not found  → sending to DLQ")
        ch.basic_nack(delivery_tag=method.delivery_tag, requeue=False)

    except Exception as e:
        print("[🔥 ERROR]", e)
        # ❗ Do NOT ACK → message will retry
        ch.basic_nack(delivery_tag=method.delivery_tag, requeue=True)


def start_consumer():
    connection = pika.BlockingConnection(
        pika.ConnectionParameters(
            host="localhost",
            port=5672,
            credentials=pika.PlainCredentials("guest", "guest")
        )
    )

    channel = connection.channel()

    # channel.queue_declare(queue="orderQueue", durable=True)
    channel.basic_qos(prefetch_count=1)

    print("[*] Inventory service waiting for messages...")
    channel.basic_consume(
        queue="orderQueue",
        on_message_callback=callback
    )

    channel.start_consuming()


if __name__ == "__main__":
    start_consumer()
