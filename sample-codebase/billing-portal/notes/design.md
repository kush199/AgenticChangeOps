# Billing Portal: design notes (invented)

## Flow

1. The portal reads the status queue named in `config/portal.settings`. When an order is reported as
   Shipped, an invoice is created.
2. `src/InvoiceList` shows a customer their invoices, newest first.
3. `src/PaymentReminder` decides each day which unpaid invoices are due a reminder.
4. `src/StatementExport` builds a summary of the last twelve months.

## Settings

All tunable values live in `config/portal.settings`: currency, page size, session length, reminder
thresholds and the statement period.

## Known limits

- One currency only.
- The third reminder is never sent.
- Statements carry totals only.

## Related system

Order status events come from the orders service (see `orders-service/notes/design.md` and
`orders-service/src/OrderStatusTracker`). The portal never writes back to it.
