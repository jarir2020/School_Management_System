# Phase 5 — Fees and Finance

This phase adds a fee ledger for the single school: class fee schedules, student assignments, invoices, approved discounts, partial collections, receipts, refunds, overdue reminders, and financial summaries.

## 1. Finance roles

- **Accountant:** creates fees, assigns them to active students, issues invoices, records cash/bank collections, requests discounts and refunds, and reconciles payments.
- **Principal:** reads finance queues and approves or rejects discount and refund requests. A requester cannot review their own request.
- **Super Admin:** has the finance and reviewer actions.
- **Parent/Student:** sees invoices for students linked to the account by school staff, starts online checkout when configured, and opens receipts for successful payments.

Both the page and the finance service enforce roles. Parent invoice and receipt lookups also filter by the linked guardian account in the database.

## 2. Configure fees and issue invoices

1. Sign in as an Accountant or Super Admin and open **Fees and finance**.
2. Create a fee structure for an academic year and class. The due date must be inside that academic year.
3. Select **Assign current students**. The service creates one assignment for every active enrollment in that year and class. Running it again is safe; the unique fee/enrollment key prevents duplicates. Run it again later to include newly active enrollments.
4. An Accountant may request a discount for an assignment that has not been invoiced. The Principal or a different Super Admin approves or rejects it from **Discount approvals**.
5. Select **Issue invoices**. Assignments with a pending discount review are skipped. Each assignment can have only one invoice, and the invoice snapshots its gross amount, approved discount, and total.

Issued invoice amounts are not silently changed if a fee structure is later edited. The Phase 5 screen does not expose editing issued invoices or fee structures.

## 3. Collect payments and issue receipts

From the invoice table, an Accountant can record a cash payment or a bank transfer. Bank transfers require a reference. Collections can be partial, but a payment cannot exceed the current invoice balance. A successful collection and its allocation and receipt are stored in one database transaction. Duplicate bank references and duplicate allocations are prevented by database constraints.

Only `Succeeded` payments with an allocation count toward collection totals. The balance is the invoice total minus successful allocations plus completed refunds. Receipts are available to finance staff and linked parents at `/finance/receipts/{paymentId}` and can be printed from the browser.

## 4. Enable SSLCOMMERZ sandbox checkout

The adapter sends checkout sessions only to the SSLCOMMERZ sandbox. Put sandbox credentials and the publicly reachable HTTPS callback base URL in local User Secrets for the Web project:

```sh
dotnet user-secrets set "Payments:SslCommerz:StoreId" "<sandbox-store-id>" --project src/SchoolManagement.Web
dotnet user-secrets set "Payments:SslCommerz:StorePassword" "<sandbox-store-password>" --project src/SchoolManagement.Web
dotnet user-secrets set "Payments:SslCommerz:PublicBaseUrl" "https://<public-host>" --project src/SchoolManagement.Web
```

Do not put real credentials in `appsettings.json`, source control, or this guide. The return and IPN routes are `/payments/sslcommerz/success` and `/payments/sslcommerz/ipn`; the failure and cancellation routes return parents to their invoice page. A local server is not reachable by the payment provider unless a trusted HTTPS development tunnel or staging host is configured.

The parent account must be linked to the student's guardian record, and the linked guardian or parent account must have an email address and phone number. The parent selects **Pay online with SSLCOMMERZ** from **Children's fees**. The application creates a pending ledger row and sends the parent to the hosted sandbox checkout.

A browser return or IPN never settles an invoice by itself. The server validates the provider's validation ID, transaction ID, API status, exact amount, BDT currency, and risk level. Valid, matching, risk-level-zero payments are applied once. A confirmed payment with a mismatch or elevated risk is held for staff reconciliation and does not reduce the invoice balance. Duplicate callbacks do not create a second allocation or receipt. See the [SSLCOMMERZ v4 integration documentation](https://developer.sslcommerz.com/doc/v4/) for the hosted session, validation, and refund API contracts.

## 5. Review and process refunds

An Accountant can request a refund from a payment in the invoice history. A Principal or different Super Admin approves or rejects it with a reason required for rejection. Approval reserves the amount so multiple open requests cannot exceed the unrefunded payment.

- For an online payment with a provider bank transaction ID, finance staff sends the approved refund to SSLCOMMERZ and can refresh its status. Only a provider-confirmed `refunded` result completes the refund.
- For cash, bank transfers, or online payments without a provider bank transaction ID, staff records the completed external refund reference after it has been issued. The service prevents this path when an online provider refund is available.
- If the provider request times out after submission, the amount stays reserved and the request is marked for reconciliation. Finance staff must confirm the result with SSLCOMMERZ before recording a manual completion.

A completed refund increases the invoice balance again. A failed or pending refund does not.

## 6. Reports and overdue reminders

The finance dashboard shows invoiced, collected, refunded, outstanding, overdue, and current-month amounts. Due-date and monthly grouping use the school's configured time zone; invalid or missing settings fall back to UTC. Collection amounts are grouped using payment allocation records.

Overdue invoices appear in the reminder queue with the linked guardian contacts. **Draft email** opens the staff mail application with a suggested message, and **Call** opens the phone handler where available. The application does not claim that a reminder was sent. SMTP delivery and scheduled/background reminders remain in Phase 6.

## 7. Local schema and validation

The `Phase5FeesAndFinance` migration creates `FeeStructures`, `StudentFeeAssignments`, `FeeDiscountRequests`, `FeeInvoices`, `Payments`, `PaymentAllocations`, `PaymentReceipts`, and `RefundRequests`. Development startup applies checked-in migrations automatically. Production deployment must apply migrations explicitly after a database backup.

Build from the repository root:

```sh
dotnet build SchoolManagement.sln
```

The solution build verifies compilation and Razor page generation. The fee workflows still need a manual browser walkthrough. A real sandbox checkout and refund require merchant sandbox credentials plus a publicly reachable HTTPS callback host; those credentials and the end-to-end provider path are not included in the repository.
