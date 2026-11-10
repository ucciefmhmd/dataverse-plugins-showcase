# Custom API: `ysm_GetCustomerCreditSummary`

Create these records in a solution (in make.powerapps.com: **New → More → Other → Custom API**, then add the request parameter and response properties). `ysm` is the publisher prefix used in this sample; use your own.

## Custom API

| Field | Value |
|---|---|
| Unique name | `ysm_GetCustomerCreditSummary` |
| Display name | Get Customer Credit Summary |
| Binding type | Global |
| Is function | **Yes**, so it can be called with `GET` and has no side effects |
| Is private | No |
| Allowed custom processing step type | None (only the plugin below implements it) |
| Plugin type | `DataverseShowcase.Plugins.CustomApis.GetCustomerCreditSummaryApi` |

## Request parameter

| Unique name | Type | Optional | Description |
|---|---|---|---|
| `AccountId` | Guid | No | The account to summarise |

## Response properties

| Unique name | Type | Present when |
|---|---|---|
| `Status` | String | Always: `WithinLimit`, `NearLimit` (80% or more), `OverLimit`, `OnHold` or `NoLimit` |
| `OpenOrdersTotal` | Decimal | Always: sum of `totalamount` over active and submitted orders |
| `CreditLimit` | Decimal | The account has a credit limit |
| `AvailableCredit` | Decimal | The account has a limit (0 when on hold or over the limit) |
| `UtilisationPercent` | Decimal | The limit is greater than 0 |

## Behaviour

* Runs with the **caller's** security roles: a user who can't read the account gets "not found".
* Unknown account or missing `AccountId`: a clear `InvalidPluginExecutionException` message.
* The rule itself is `CreditPolicy.Summarize` in `DataverseShowcase.Core`.
