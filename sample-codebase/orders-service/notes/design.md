# Orders Service: design notes (invented)

## Flow

1. An order arrives on the intake queue and is handled by `src/OrderIntake`.
2. `src/StockCheck` reserves stock for every line, or releases everything and rejects the order.
3. `src/OrderStatusTracker` records each status change and publishes it to the status queue.

## Settings

All tunable values live in `config/orders.settings`: queue names, page size, stock check retries,
allowed statuses and the cancel window.

## Known limits

- Orders with more than 50 lines are rejected without a reason code.
- The stock table is refreshed every 10 minutes, so the last unit can be double-booked.
- Status changes are published after they are saved; a crash in between loses the notification.

## Related system

Invoices for shipped orders are produced by the billing portal (see `billing-portal/notes/design.md`
and `billing-portal/src/InvoiceList`). The orders service does not call it; the billing portal reads
the status queue.
