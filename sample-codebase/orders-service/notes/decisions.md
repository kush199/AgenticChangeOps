# Orders Service: decisions (invented)

## D1: Reserve stock before accepting

Reserve first, accept second. An order is never accepted that cannot be fulfilled. Cost: a rejected
order briefly holds stock until it is released.

## D2: Status moves only forward

`allowedNext` in `src/OrderStatusTracker` lists the only permitted moves. Cancellation is allowed only
before packing and inside the cancel window. This keeps the status history easy to audit.

## D3: Publish after save

Status events are published after the change is saved. Chosen for simplicity; a transactional outbox
was considered and postponed.
