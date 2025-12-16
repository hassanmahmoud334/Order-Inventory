import json
import pika
import django
import os
import sys

# --- Django Setup ---
BASE_DIR = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
sys.path.append(BASE_DIR)

os.environ.setdefault("DJANGO_SETTINGS_MODULE", "inventoryService.settings")
django.setup()



# --- Handle incoming orders ---
def callback(ch, method, properties, body):
    try:
        from inventory.models import Product    
        data = json.loads(body)
        product_sku = data["ProductId"]
        quantity = data["Quantity"]

        print(f"[x] Received order for {product_sku} - qty {quantity}")

        product = Product.objects.get(sku=product_sku)
        ok = product.decrease_stock(quantity)

        if ok:
            print("[✔] Stock updated successfully")
        else:
            print("[❌] Not enough stock!")

    except Product.DoesNotExist:
        print("[❌] Product not found")
    except Exception as e:
        print("[ERROR]", e)

    ch.basic_ack(delivery_tag=method.delivery_tag)


# --- Start RabbitMQ Consumer ---
def start_consumer():

    connection = pika.BlockingConnection(
        pika.ConnectionParameters(
            host="localhost",            # change if using container host
            port=5672,
            credentials=pika.PlainCredentials("guest", "guest")
        )
    )

    channel = connection.channel()

    # Ensure queue exists
    channel.queue_declare(queue="orderQueue", durable=True)

    print("[*] Inventory service waiting for messages...")
    channel.basic_qos(prefetch_count=1)

    channel.basic_consume(
        queue="orderQueue",
        on_message_callback=callback
    )

    channel.start_consuming()


if __name__ == "__main__":
    start_consumer()
