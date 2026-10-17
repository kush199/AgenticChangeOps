# Billing Portal: decisions (invented)

## D1: Invoice on shipment

An invoice is created when an order ships, not when it is placed, so customers are never billed for
cancelled orders.

## D2: Reminders are counted, not scheduled

Each invoice carries a count of reminders sent. The daily check compares age against thresholds.
Simple to reason about; it cannot send reminders on a custom schedule.

## D3: Read-only towards orders

The portal only reads order status events. It never calls the orders service.
