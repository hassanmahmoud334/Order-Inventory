from django.db import models

class Product(models.Model):
    name = models.CharField(max_length=255)
    sku = models.CharField(max_length=100, unique=True)
    quantity = models.IntegerField(default=0)

    def decrease_stock(self, amount):
        if self.quantity >= amount:
            self.quantity -= amount
            self.save()
            return True
        return False
