# Stripe Billing Integration Guide

## Overview

This guide covers the complete Stripe billing integration for Planilla SaaS, including subscription management, feature gating, and webhook handling.

## Architecture

```
User Request → BillingController → StripeBillingService → Stripe API
                                 ↓
                          Local Subscription Update

Stripe Webhook → StripeWebhookController → Event Handler → Database Update
```

## Features Implemented

### ✅ 1. Subscription Plans

The system supports four subscription plans with automatic feature gating:

| Plan | Max Employees | Max Users | Export Excel | Export PDF | API Access | Price/Month |
|------|--------------|-----------|--------------|------------|------------|-------------|
| **Free** | 5 | 1 | ❌ | ❌ | ❌ | $0 |
| **Starter** | 25 | 3 | ✅ | ❌ | ❌ | $29.99 |
| **Professional** | 100 | 10 | ✅ | ✅ | ✅ | $79.99 |
| **Enterprise** | Unlimited | Unlimited | ✅ | ✅ | ✅ | $199.99 |

### ✅ 2. Stripe Checkout

Users can upgrade/downgrade plans via Stripe Checkout:

**Endpoint:** `POST /api/billing/checkout`

**Request:**
```json
{
  "plan": 2  // 1=Starter, 2=Professional, 3=Enterprise
}
```

**Response:**
```json
{
  "sessionId": "cs_test_...",
  "checkoutUrl": "https://checkout.stripe.com/c/pay/cs_test_..."
}
```

### ✅ 3. Customer Portal

Tenants can manage their subscription via Stripe Customer Portal:

**Endpoint:** `POST /api/billing/portal`

**Request:**
```json
{
  "returnUrl": "https://yourapp.com/dashboard"
}
```

**Response:**
```json
{
  "url": "https://billing.stripe.com/p/session/..."
}
```

### ✅ 4. Feature Gating

Automatic enforcement of plan limits:

#### Employee Creation
```csharp
[HttpPost]
public async Task<IActionResult> Create(EmpleadoCrearDto dto)
{
    // Check plan limit
    var (allowed, reason) = await _planLimitService.CanCreateEmployeeAsync(tenantId);
    if (!allowed)
    {
        return StatusCode(403, new { error = reason });
    }
    // ... create employee
}
```

#### Report Export
```csharp
[HttpGet("css/{planillaId}/excel")]
public async Task<IActionResult> ExportarCssExcel(int planillaId)
{
    // Check if plan allows export
    var canExport = await _planLimitService.CanExportReportsAsync(tenantId);
    if (!canExport)
    {
        return StatusCode(403, new { error = "Upgrade to export reports" });
    }
    // ... export report
}
```

### ✅ 5. Webhook Handling

Secure webhook processing with signature verification and idempotency:

**Endpoint:** `POST /api/stripe/webhook`

**Supported Events:**
- `checkout.session.completed` - User completed checkout
- `customer.subscription.created` - New subscription
- `customer.subscription.updated` - Plan change
- `customer.subscription.deleted` - Cancelation
- `invoice.payment_succeeded` - Successful payment
- `invoice.payment_failed` - Failed payment (sets Status=PastDue)
- `customer.subscription.trial_will_end` - Trial ending notification

**Idempotency:**
All webhook events are stored in `StripeWebhookEvents` table with unique constraint on `StripeEventId` to prevent duplicate processing.

## Setup Instructions

### 1. Stripe Account Setup

1. Create a Stripe account at https://stripe.com
2. Navigate to **Dashboard → Developers → API Keys**
3. Copy your **Secret Key** (starts with `sk_test_` for test mode)
4. Navigate to **Dashboard → Developers → Webhooks**
5. Click **Add Endpoint**
6. URL: `https://yourapp.com/api/stripe/webhook`
7. Select these events:
   - `checkout.session.completed`
   - `customer.subscription.created`
   - `customer.subscription.updated`
   - `customer.subscription.deleted`
   - `invoice.payment_succeeded`
   - `invoice.payment_failed`
   - `customer.subscription.trial_will_end`
8. Copy the **Signing Secret** (starts with `whsec_`)

### 2. Create Stripe Products & Prices

1. Navigate to **Dashboard → Products → Add Product**
2. Create three products:

**Starter Plan:**
- Name: Planilla Starter
- Price: $29.99/month (recurring)
- Copy the Price ID (starts with `price_`)

**Professional Plan:**
- Name: Planilla Professional
- Price: $79.99/month (recurring)
- Copy the Price ID

**Enterprise Plan:**
- Name: Planilla Enterprise
- Price: $199.99/month (recurring)
- Copy the Price ID

### 3. Configure Environment Variables

**IMPORTANT:** Never commit Stripe keys to source control!

**For Development (appsettings.json):**
```json
{
  "Stripe": {
    "SecretKey": "sk_test_YOUR_TEST_KEY",
    "WebhookSecret": "whsec_YOUR_TEST_WEBHOOK_SECRET",
    "PriceIdStarter": "price_YOUR_STARTER_PRICE_ID",
    "PriceIdProfessional": "price_YOUR_PROFESSIONAL_PRICE_ID",
    "PriceIdEnterprise": "price_YOUR_ENTERPRISE_PRICE_ID",
    "SuccessUrl": "https://localhost:5001/dashboard?checkout=success",
    "CancelUrl": "https://localhost:5001/pricing?checkout=cancel"
  }
}
```

**For Production (Environment Variables):**
```bash
Stripe__SecretKey=sk_live_YOUR_LIVE_KEY
Stripe__WebhookSecret=whsec_YOUR_LIVE_WEBHOOK_SECRET
Stripe__PriceIdStarter=price_LIVE_STARTER_PRICE_ID
Stripe__PriceIdProfessional=price_LIVE_PROFESSIONAL_PRICE_ID
Stripe__PriceIdEnterprise=price_LIVE_ENTERPRISE_PRICE_ID
Stripe__SuccessUrl=https://yourapp.com/dashboard?checkout=success
Stripe__CancelUrl=https://yourapp.com/pricing?checkout=cancel
```

### 4. Apply Database Migration

```bash
dotnet ef database update --project src/Infrastructure/Planilla.Infrastructure --startup-project src/UI/Planilla.Web
```

This creates the `StripeWebhookEvents` table for idempotency.

## Local Testing with Stripe CLI

### 1. Install Stripe CLI

**Windows:**
```bash
choco install stripe-cli
```

**Mac:**
```bash
brew install stripe/stripe-cli/stripe
```

**Linux:**
```bash
curl -s https://packages.stripe.dev/api/security/keypair/stripe-cli-gpg/public | gpg --dearmor | sudo tee /usr/share/keyrings/stripe.gpg
echo "deb [signed-by=/usr/share/keyrings/stripe.gpg] https://packages.stripe.dev/stripe-cli-debian-local stable main" | sudo tee -a /etc/apt/sources.list.d/stripe.list
sudo apt update
sudo apt install stripe
```

### 2. Login to Stripe

```bash
stripe login
```

### 3. Forward Webhooks to Local Server

```bash
stripe listen --forward-to https://localhost:5001/api/stripe/webhook
```

This will output a webhook signing secret like `whsec_...` - use this in your `appsettings.json`.

### 4. Trigger Test Events

```bash
# Test successful checkout
stripe trigger checkout.session.completed

# Test subscription creation
stripe trigger customer.subscription.created

# Test failed payment
stripe trigger invoice.payment_failed
```

## API Endpoints Reference

### GET /api/billing/subscription
**Auth:** Required (JWT)
**Roles:** All authenticated users
**Description:** Get current subscription status and usage

**Response:**
```json
{
  "plan": 2,
  "planName": "Professional",
  "status": 1,
  "statusName": "Activa",
  "trialEndsAt": null,
  "nextBillingDate": "2026-02-07T00:00:00Z",
  "monthlyPrice": 79.99,
  "maxEmployees": 100,
  "maxUsers": 10,
  "currentEmployees": 45,
  "currentUsers": 5,
  "canExportExcel": true,
  "canExportPdf": true,
  "canUseApi": true,
  "hasAuditLog": true
}
```

### POST /api/billing/checkout
**Auth:** Required (JWT)
**Roles:** Owner, Admin
**Description:** Create Stripe Checkout session for plan upgrade

**Request:**
```json
{
  "plan": 2
}
```

**Response:**
```json
{
  "sessionId": "cs_test_...",
  "checkoutUrl": "https://checkout.stripe.com/..."
}
```

### POST /api/billing/portal
**Auth:** Required (JWT)
**Roles:** Owner, Admin
**Description:** Create Stripe Customer Portal session

**Request:**
```json
{
  "returnUrl": "https://yourapp.com/dashboard"
}
```

**Response:**
```json
{
  "url": "https://billing.stripe.com/..."
}
```

### POST /api/billing/cancel
**Auth:** Required (JWT)
**Roles:** Owner only
**Description:** Cancel subscription at period end

**Response:**
```json
{
  "message": "Suscripción cancelada al final del periodo actual"
}
```

### GET /api/billing/limits
**Auth:** Required (JWT)
**Roles:** All authenticated users
**Description:** Get plan limits and current usage

**Response:**
```json
{
  "limits": {
    "maxEmployees": 100,
    "maxUsers": 10,
    "canExportExcel": true,
    "canExportPdf": true,
    "canUseApi": true,
    "pricePerMonth": 79.99
  },
  "usage": {
    "currentEmployees": 45,
    "currentUsers": 5,
    "employeePercentage": 45.0,
    "userPercentage": 50.0
  }
}
```

## Security Considerations

### ✅ Webhook Signature Verification

All webhooks are verified using Stripe's signature:

```csharp
var signatureHeader = Request.Headers["Stripe-Signature"].ToString();
stripeEvent = EventUtility.ConstructEvent(json, signatureHeader, _stripeOptions.WebhookSecret);
```

**NEVER** skip signature verification - this prevents attackers from forging webhook events.

### ✅ Tenant Isolation

All billing operations filter by `TenantId`:

```csharp
var tenantId = _tenantContext.TenantId; // From JWT claims
var subscription = await _context.Subscriptions
    .FirstOrDefaultAsync(s => s.TenantId == tenantId);
```

### ✅ Role-Based Access

- **Owner** - Can cancel subscriptions
- **Owner/Admin** - Can create checkout sessions and access customer portal
- **All Users** - Can view subscription status

## Feature Gating Flow

```
┌─────────────────────────────────────────────────────────┐
│  User attempts to create employee                        │
└─────────────────┬───────────────────────────────────────┘
                  │
                  ▼
┌─────────────────────────────────────────────────────────┐
│  IPlanLimitService.CanCreateEmployeeAsync(tenantId)     │
└─────────────────┬───────────────────────────────────────┘
                  │
                  ├─► Get Subscription for tenantId
                  ├─► Check Status (PastDue blocks creates)
                  ├─► Get PlanFeatures.GetLimits(plan)
                  ├─► Count current active employees
                  │
                  ▼
┌─────────────────────────────────────────────────────────┐
│  Current < Limit?                                        │
└───┬─────────────────────────────────────────────────┬───┘
    │ YES                                             │ NO
    ▼                                                 ▼
┌───────────────────┐                   ┌──────────────────────┐
│  Allow creation   │                   │  Return 403 Forbidden│
│  (200 OK)         │                   │  with upgrade message│
└───────────────────┘                   └──────────────────────┘
```

## Troubleshooting

### Webhook Not Receiving Events

1. Check webhook is configured in Stripe Dashboard
2. Verify webhook secret matches `appsettings.json`
3. Check endpoint is publicly accessible (use ngrok for local testing)
4. Review `StripeWebhookEvents` table for processing status

### Signature Verification Failing

1. Ensure `WebhookSecret` is correct (starts with `whsec_`)
2. Check raw request body is not modified by middleware
3. Verify endpoint is `[AllowAnonymous]` (no authentication middleware)

### Plan Limits Not Enforcing

1. Verify `IPlanLimitService` is registered in `Program.cs`
2. Check `_tenantContext.TenantId` is set correctly
3. Review `Subscription.Plan` matches expected plan
4. Confirm `PlanFeatures.GetLimits()` returns correct limits

## Production Deployment Checklist

- [ ] Replace test Stripe keys with live keys
- [ ] Update webhook URL to production domain
- [ ] Configure webhook secret for production
- [ ] Test all subscription flows end-to-end
- [ ] Verify webhook signature validation
- [ ] Monitor `StripeWebhookEvents` for failed events
- [ ] Set up alerts for payment failures
- [ ] Configure backup payment method collection
- [ ] Test plan limit enforcement
- [ ] Verify tenant isolation
- [ ] Enable audit logging for subscription changes

## Support

For issues or questions:
- Stripe Documentation: https://stripe.com/docs
- Stripe Support: https://support.stripe.com

---

**Last Updated:** January 7, 2026
**Version:** 1.0.0
