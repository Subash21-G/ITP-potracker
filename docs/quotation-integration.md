# ITP Quotation → POTracker

The receiving endpoint is `POST /api/purchase-orders/from-quotation`.
Call it from the quotation backend after the customer PO is confirmed.
Send `Content-Type: application/json` and `X-Quotation-Api-Key`.
Use HTTPS when connecting separate machines. Keep the key in the backend, never browser code.

Configure POTracker through environment variables (or local user secrets):

```
QuotationIntegration__ApiKey=<shared random secret>
QuotationIntegration__CustomerMappings__CUST-100=1
```

Replace `1` with the existing POTracker purchaser ID for customer `CUST-100`.
Add one mapping per quotation customer. Unmapped customers are rejected.
The endpoint is disabled until a key is configured; it does not require session cookies or CSRF tokens.
Other APIs retain their existing session authorization.

Example payload:

```json
{
  "sourceSystem": "ITPQuotation",
  "quotationNumber": "QTN-2026-001",
  "revision": 1,
  "rfqNumber": "RFQ-2026-024",
  "customerPoNumber": "PO-4587",
  "customerCode": "CUST-100",
  "items": [
    {
      "materialNo": "11753667",
      "description": "INTERM BEAR CAP",
      "quantity": 25,
      "unitPrice": 1840,
      "totalPrice": 46000
    }
  ]
}
```

201 creates the purchase order and material rows together. The response contains
`purchaseOrderId`, `poNumber`, `quotationNumber`, `revision`, and `alreadyImported`.
An identical retry returns 200 with `alreadyImported: true`. Changed data for an
imported revision, or a new revision using an existing PO number, returns 409.
Revisions never replace material rows already used for production or invoices.
400 reports validation or customer mapping errors; 401 reports an invalid integration key.
Prices and quantities support two decimal places. Tax is not included in this contract.

The generated EF migration is applied by POTracker's existing startup migration logic.
Restart the updated POTracker application before sending requests.
The quotation backend must store the returned POTracker purchase order ID and handle
409 as requiring reconciliation. Retry transient transport/500 failures with backoff.

The sending application is a separate project. Its local implementation and
verification status are described below.

## Local sender implementation

The sender is now implemented in `D:\Project\ITP-Quotation`. Its quotations list
offers **Send to POTracker** for Accepted and POReceived quotations. The user enters
the confirmed customer PO number. The backend retains the returned purchase order
ID, PO number, and exported quotation revision. Admin and Estimator can send;
Viewer cannot. Two-way status synchronization is not implemented.

The current local POTracker HTTP launch profile uses `http://localhost:5001`;
ITP-Quotation uses `http://localhost:5263`. Both projects have a shared development
API key in user secrets. Do not copy that key into frontend code or source control.

Customer mappings are still required. In ITP-Quotation configure
`POTracker__CustomerCodes__<customer ID>=CUST-<customer ID>`; in POTracker configure
`QuotationIntegration__CustomerMappings__CUST-<customer ID>=<purchaser ID>`.
The two databases' customer IDs need not match. The local quotation database had
no customers during setup; POTracker purchaser 22 is WEG Industries (India) PVT LTD.
Only map that purchaser to its actual quotation customer.

The source application's initial revision is 0. Its sender transmits source revision
plus 1, preserving this endpoint's positive revision contract. Quantities with more
than two decimal places are rejected; unit prices are rounded to two places and
line totals are recalculated consistently. Tax is excluded.

Validation completed: quotation UI production build, 97 quotation backend tests,
and an isolated HTTP smoke test using the actual sender and receiver. The smoke
test verifies persisted references, duplicate retries, conflicts, authentication,
and material values without creating business orders. The running POTracker API
also accepts the configured key and rejects an invalid key with non-importable
payloads. A real business import still needs an actual customer mapping and PO.
