# Registering the plugins

These steps deploy the sample to a **trial or developer** Dataverse environment. Don't install a sample into production.

## 1. Build the signed assembly

```bash
dotnet build -c Release
# → src/DataverseShowcase.Plugins/bin/Release/net462/DataverseShowcase.Plugins.dll
```

The assembly is strong-named with `DataverseShowcase.snk`, a public sample key. Replace it with your own key for real use.

## 2. Register the assembly

With the **Plugin Registration Tool** (part of the Power Platform tools, also available through `pac tool prt`):

1. Connect to your environment.
2. **Register → Register New Assembly**, choose the DLL, isolation mode **Sandbox**, location **Database**.

## 3. Register the steps

| Plugin | Message | Primary table | Stage | Mode | Filtering attributes | Run as |
|---|---|---|---|---|---|---|
| `DataverseShowcase.Plugins.ContactDataQualityPlugin` | Create | contact | PreOperation | Synchronous | n/a | Calling user |
| `DataverseShowcase.Plugins.ContactDataQualityPlugin` | Update | contact | PreOperation | Synchronous | `emailaddress1, telephone1, mobilephone` | Calling user |
| `DataverseShowcase.Plugins.AccountCreditLimitGuardPlugin` | Update | account | PreOperation | Synchronous | `creditlimit` | Calling user |

No images are needed: both plugins only read the `Target` and query Dataverse.

The plugins check their own registration. A step on the wrong message, table or stage fails with a message that says what was expected.

## 4. Create the Custom API

Follow [custom-api.md](custom-api.md). Set **Plugin type** to `DataverseShowcase.Plugins.CustomApis.GetCustomerCreditSummaryApi`. A Custom API's plugin is linked through its definition, not through a step.

## 5. Try it

* Create a contact with `"  Someone@Example.COM "` and the phone `0020 100 123 4567`. They're saved as `someone@example.com` and `+201001234567`.
* Create a second active contact with the same email: blocked.
* Give an account open orders, then lower its credit limit below their total: blocked, with the amounts in the message.
* Call `GET /api/data/v9.2/ysm_GetCustomerCreditSummary(AccountId=@id)?@id=<account id>`.

If anything fails, enable the **plug-in trace log** (Settings → Administration → System Settings → Customization). Every execution logs its entry, exit and correlation id there.
