using DataverseShowcase.Core.Contacts;
using DataverseShowcase.Plugins.Data;
using DataverseShowcase.Plugins.Infrastructure;
using Microsoft.Xrm.Sdk;

namespace DataverseShowcase.Plugins;

/// <summary>
/// Keeps contact data clean on the way in.
/// <list type="bullet">
/// <item>Normalises <c>emailaddress1</c> (trimmed, lower case) and the phone fields (digits with an optional leading '+').</item>
/// <item>Rejects malformed values with a clear message.</item>
/// <item>Blocks a second active contact with the same email address.</item>
/// </list>
/// <para><b>Register:</b> <c>Create</c> and <c>Update</c> of <c>contact</c>, stage <b>PreOperation (20)</b>, synchronous.
/// On Update, use filtering attributes <c>emailaddress1, telephone1, mobilephone</c>.</para>
/// <para>PreOperation runs inside the transaction and before the row is written, so changes made to
/// <c>Target</c> are saved without a second update.</para>
/// </summary>
public sealed class ContactDataQualityPlugin : PluginBase
{
    private static readonly string[] PhoneAttributes = ["telephone1", "mobilephone"];

    public ContactDataQualityPlugin()
        : this(null, null)
    {
    }

    public ContactDataQualityPlugin(string? unsecureConfiguration, string? secureConfiguration)
        : base(unsecureConfiguration, secureConfiguration)
    {
    }

    protected override void ExecuteDataversePlugin(ILocalPluginContext context)
    {
        context.EnsureRegistration("contact", PipelineStage.PreOperation, "Create", "Update");
        var target = context.GetTargetEntity();

        foreach (var attribute in PhoneAttributes)
        {
            if (target.Contains(attribute))
            {
                target[attribute] = Normalize(ContactNormalizer.NormalizePhone(target.GetAttributeValue<string>(attribute)));
            }
        }

        if (!target.Contains("emailaddress1"))
        {
            return;
        }

        var email = Normalize(ContactNormalizer.NormalizeEmail(target.GetAttributeValue<string>("emailaddress1")));
        target["emailaddress1"] = email;

        if (email is null)
        {
            return;
        }

        // SYSTEM context: the check must see contacts the current user is not allowed to read.
        var contacts = new ContactRepository(context.SystemService);
        var currentContactId = context.ExecutionContext.MessageName == "Update" ? target.Id : (Guid?)null;
        if (contacts.IsEmailUsedByAnotherContact(email, currentContactId))
        {
            throw new InvalidPluginExecutionException(
                $"Another active contact already uses the email address '{email}'. Open that contact instead of creating a duplicate.");
        }

        context.Trace($"Contact email normalised and unique: {email}");
    }

    private static string? Normalize(NormalizationResult result) =>
        result.IsValid ? result.Value : throw new InvalidPluginExecutionException(result.Error);
}
