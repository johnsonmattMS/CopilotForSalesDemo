using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Identity.Client;
using Microsoft.Identity.Client.Extensibility;
using Microsoft.PowerPlatform.Dataverse.Client;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;

namespace C4STranscriptPlayer;

public sealed class DataverseAppointmentService
{
    private const string PublicClientId = "51f81489-12ee-4a9e-aaae-a2591f45987d";

    public async Task<AppointmentContext> LoadAppointmentAsync(string environmentUrl, string appointmentId, EdgeProfile? edgeProfile = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(environmentUrl)) throw new InvalidOperationException("Enter a Dataverse environment URL.");
        if (!Guid.TryParse(NormalizeGuid(appointmentId), out var id)) throw new InvalidOperationException("Enter a valid appointment id.");

        if (edgeProfile != null)
        {
            return await LoadAppointmentViaWebApiAsync(environmentUrl, id, edgeProfile, cancellationToken);
        }

        return await Task.Run(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var client = CreateClient(environmentUrl, edgeProfile);
            if (edgeProfile == null && !client.IsReady)
            {
                throw new InvalidOperationException(client.LastError ?? "Dataverse connection failed.");
            }

            Entity appointment;
            try
            {
                appointment = client.Retrieve("appointment", id, new ColumnSet("subject", "scheduledstart", "scheduledend", "regardingobjectid", "ownerid"));
            }
            catch (Exception ex)
            {
                var detail = string.IsNullOrWhiteSpace(client.LastError) ? ex.Message : $"{ex.Message} {client.LastError}";
                throw new InvalidOperationException("Dataverse authenticated, but the appointment could not be retrieved. " + detail, ex);
            }
            var context = new AppointmentContext
            {
                AppointmentId = id.ToString("D"),
                Subject = appointment.GetAttributeValue<string>("subject") ?? "Selected appointment",
                Start = appointment.GetAttributeValue<DateTime?>("scheduledstart")?.ToLocalTime() ?? DateTime.Now,
                DurationMinutes = CalculateDuration(appointment),
                Regarding = FromEntityReference(appointment.GetAttributeValue<EntityReference>("regardingobjectid"), "Selected CRM record", "appointment"),
                Seller = FromEntityReference(appointment.GetAttributeValue<EntityReference>("ownerid"), "Seller", "systemuser")
            };

            ApplyPartyDefaults(client, id, context);
            ApplyRegardingDefaults(client, context);
            return context;
        }, cancellationToken);
    }

    private static ServiceClient CreateClient(string environmentUrl, EdgeProfile? edgeProfile)
    {
        return edgeProfile == null
            ? CreateDefaultClient(environmentUrl)
            : CreateEdgeProfileClient(environmentUrl, edgeProfile);
    }

    private static ServiceClient CreateDefaultClient(string environmentUrl)
    {
        var cachePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "C4STranscriptPlayer");
        Directory.CreateDirectory(cachePath);
        var connectionString = $"AuthType=OAuth;Url={environmentUrl.TrimEnd('/')};AppId={PublicClientId};RedirectUri=http://localhost;LoginPrompt=Auto;TokenCacheStorePath={cachePath}";
        return new ServiceClient(connectionString);
    }

    private static ServiceClient CreateEdgeProfileClient(string environmentUrl, EdgeProfile edgeProfile)
    {
        var normalizedUrl = environmentUrl.TrimEnd('/');
        var redirectUri = "http://localhost:8400/";
        var publicClient = PublicClientApplicationBuilder
            .Create(PublicClientId)
            .WithAuthority(AzureCloudInstance.AzurePublic, AadAuthorityAudience.AzureAdMultipleOrgs)
            .WithRedirectUri(redirectUri)
            .Build();

        return new ServiceClient(new Uri(normalizedUrl), async resource =>
        {
            var scope = resource.EndsWith("/.default", StringComparison.OrdinalIgnoreCase)
                ? resource
                : $"{resource.TrimEnd('/')}/.default";
            var accounts = await publicClient.GetAccountsAsync();
            try
            {
                var silentResult = await publicClient.AcquireTokenSilent([scope], accounts.FirstOrDefault()).ExecuteAsync();
                return silentResult.AccessToken;
            }
            catch (MsalUiRequiredException)
            {
                var interactiveResult = await publicClient
                    .AcquireTokenInteractive([scope])
                    .WithCustomWebUi(new EdgeProfileWebUi(edgeProfile))
                    .ExecuteAsync();
                return interactiveResult.AccessToken;
            }
        }, useUniqueInstance: true, logger: null);
    }

    private static async Task<AppointmentContext> LoadAppointmentViaWebApiAsync(string environmentUrl, Guid appointmentId, EdgeProfile edgeProfile, CancellationToken cancellationToken)
    {
        var normalizedUrl = environmentUrl.TrimEnd('/');
        var token = await AcquireTokenForEdgeProfileAsync(normalizedUrl, edgeProfile, cancellationToken);
        using var client = new HttpClient { BaseAddress = new Uri(normalizedUrl + "/api/data/v9.2/") };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.DefaultRequestHeaders.Add("OData-MaxVersion", "4.0");
        client.DefaultRequestHeaders.Add("OData-Version", "4.0");
        client.DefaultRequestHeaders.Add("Prefer", "odata.include-annotations=\"OData.Community.Display.V1.FormattedValue,Microsoft.Dynamics.CRM.lookuplogicalname\"");

        var appointment = await GetJsonAsync(client, $"appointments({appointmentId:D})?$select=subject,scheduledstart,scheduledend,_regardingobjectid_value,_ownerid_value", cancellationToken);
        var context = new AppointmentContext
        {
            AppointmentId = appointmentId.ToString("D"),
            Subject = GetString(appointment, "subject") ?? "Selected appointment",
            Start = GetDateTime(appointment, "scheduledstart")?.ToLocalTime() ?? DateTime.Now,
            Regarding = LookupFromWebApi(appointment, "_regardingobjectid_value", "Selected CRM record", "appointment"),
            Seller = LookupFromWebApi(appointment, "_ownerid_value", "Seller", "systemuser")
        };
        context.DurationMinutes = CalculateDuration(context.Start, GetDateTime(appointment, "scheduledend")?.ToLocalTime());

        await ApplyPartyDefaultsViaWebApiAsync(client, appointmentId, context, cancellationToken);
        await ApplyRegardingDefaultsViaWebApiAsync(client, context, cancellationToken);
        return context;
    }

    private static async Task<string> AcquireTokenForEdgeProfileAsync(string environmentUrl, EdgeProfile edgeProfile, CancellationToken cancellationToken)
    {
        var redirectUri = "http://localhost:8400/";
        var publicClient = PublicClientApplicationBuilder
            .Create(PublicClientId)
            .WithAuthority(AzureCloudInstance.AzurePublic, AadAuthorityAudience.AzureAdMultipleOrgs)
            .WithRedirectUri(redirectUri)
            .Build();

        var scope = $"{environmentUrl.TrimEnd('/')}/.default";
        var accounts = await publicClient.GetAccountsAsync();
        try
        {
            var silentResult = await publicClient.AcquireTokenSilent([scope], accounts.FirstOrDefault()).ExecuteAsync(cancellationToken);
            return silentResult.AccessToken;
        }
        catch (MsalUiRequiredException)
        {
            var interactiveResult = await publicClient
                .AcquireTokenInteractive([scope])
                .WithCustomWebUi(new EdgeProfileWebUi(edgeProfile))
                .ExecuteAsync(cancellationToken);
            return interactiveResult.AccessToken;
        }
    }

    private static async Task<JsonElement> GetJsonAsync(HttpClient client, string requestUri, CancellationToken cancellationToken)
    {
        using var response = await client.GetAsync(requestUri, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Dataverse Web API returned {(int)response.StatusCode} {response.ReasonPhrase}. {body}");
        }

        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }

    private static async Task<JsonElement[]> GetJsonArrayAsync(HttpClient client, string requestUri, CancellationToken cancellationToken)
    {
        var json = await GetJsonAsync(client, requestUri, cancellationToken);
        if (!json.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.Array) return [];
        return value.EnumerateArray().Select(item => item.Clone()).ToArray();
    }

    private static async Task ApplyPartyDefaultsViaWebApiAsync(HttpClient client, Guid appointmentId, AppointmentContext context, CancellationToken cancellationToken)
    {
        var parties = await GetJsonArrayAsync(client, $"activityparties?$select=_partyid_value,participationtypemask&$filter=_activityid_value eq {appointmentId:D}", cancellationToken);
        foreach (var party in parties)
        {
            var partyLookup = LookupFromWebApi(party, "_partyid_value", "Customer", "contact");
            var participation = GetInt(party, "participationtypemask");
            if (participation == 7 && partyLookup.EntityName == "systemuser")
            {
                context.Seller = partyLookup;
            }

            if ((participation == 5 || participation == 6) && partyLookup.EntityName == "contact" && context.CustomerContact.Id == null)
            {
                context.CustomerContact = partyLookup;
            }
        }
    }

    private static async Task ApplyRegardingDefaultsViaWebApiAsync(HttpClient client, AppointmentContext context, CancellationToken cancellationToken)
    {
        if (context.Regarding.EntityName == "account")
        {
            context.CustomerAccount = new LookupValue(context.Regarding.Id, context.Regarding.Name, "account");
            return;
        }

        if (context.Regarding.EntityName != "opportunity" || !Guid.TryParse(context.Regarding.Id, out var opportunityId)) return;

        var opportunity = await GetJsonAsync(client, $"opportunities({opportunityId:D})?$select=_parentaccountid_value,_parentcontactid_value,_customerid_value", cancellationToken);
        var parentAccount = LookupFromWebApi(opportunity, "_parentaccountid_value", "Selected account", "account");
        var parentContact = LookupFromWebApi(opportunity, "_parentcontactid_value", "Customer", "contact");
        var customer = LookupFromWebApi(opportunity, "_customerid_value", "Selected customer", "account");

        if (parentAccount.Id != null) context.CustomerAccount = parentAccount;
        else if (customer.Id != null && customer.EntityName == "account") context.CustomerAccount = customer;

        if (parentContact.Id != null) context.CustomerContact = parentContact;
        else if (customer.Id != null && customer.EntityName == "contact") context.CustomerContact = customer;
    }

    private static LookupValue LookupFromWebApi(JsonElement element, string propertyName, string fallbackName, string fallbackEntityName)
    {
        var id = GetString(element, propertyName);
        var name = GetString(element, propertyName + "@OData.Community.Display.V1.FormattedValue") ?? fallbackName;
        var entityName = GetString(element, propertyName + "@Microsoft.Dynamics.CRM.lookuplogicalname") ?? fallbackEntityName;
        return new LookupValue(id, name, entityName);
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetString()
            : null;
    }

    private static int? GetInt(JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out var result) ? result : null;
    }

    private static DateTime? GetDateTime(JsonElement element, string propertyName)
    {
        var value = GetString(element, propertyName);
        return DateTime.TryParse(value, out var result) ? result : null;
    }

    private static int CalculateDuration(DateTime start, DateTime? end)
    {
        return end.HasValue ? Math.Max(15, (int)Math.Round((end.Value - start).TotalMinutes)) : 30;
    }

    private static int CalculateDuration(Entity appointment)
    {
        var start = appointment.GetAttributeValue<DateTime?>("scheduledstart");
        var end = appointment.GetAttributeValue<DateTime?>("scheduledend");
        if (start.HasValue && end.HasValue)
        {
            return Math.Max(15, (int)Math.Round((end.Value - start.Value).TotalMinutes));
        }
        return 30;
    }

    private static void ApplyPartyDefaults(ServiceClient client, Guid appointmentId, AppointmentContext context)
    {
        var query = new QueryExpression("activityparty")
        {
            ColumnSet = new ColumnSet("partyid", "participationtypemask")
        };
        query.Criteria.AddCondition("activityid", ConditionOperator.Equal, appointmentId);
        var parties = client.RetrieveMultiple(query).Entities;

        foreach (var party in parties)
        {
            var partyId = party.GetAttributeValue<EntityReference>("partyid");
            var participation = party.GetAttributeValue<OptionSetValue>("participationtypemask")?.Value;
            if (partyId == null) continue;

            if (participation == 7 && partyId.LogicalName == "systemuser")
            {
                context.Seller = FromEntityReference(partyId, context.Seller.Name, "systemuser");
            }

            if ((participation == 5 || participation == 6) && partyId.LogicalName == "contact" && context.CustomerContact.Id == null)
            {
                context.CustomerContact = FromEntityReference(partyId, partyId.Name ?? "Customer", "contact");
            }
        }
    }

    private static void ApplyRegardingDefaults(ServiceClient client, AppointmentContext context)
    {
        if (context.Regarding.EntityName == "account")
        {
            context.CustomerAccount = new LookupValue(context.Regarding.Id, context.Regarding.Name, "account");
            return;
        }

        if (context.Regarding.EntityName != "opportunity" || !Guid.TryParse(context.Regarding.Id, out var opportunityId)) return;

        var opportunity = client.Retrieve("opportunity", opportunityId, new ColumnSet("parentaccountid", "parentcontactid", "customerid"));
        var parentAccount = opportunity.GetAttributeValue<EntityReference>("parentaccountid");
        var parentContact = opportunity.GetAttributeValue<EntityReference>("parentcontactid");
        var customer = opportunity.GetAttributeValue<EntityReference>("customerid");

        if (parentAccount != null)
        {
            context.CustomerAccount = FromEntityReference(parentAccount, parentAccount.Name ?? "Selected account", "account");
        }
        else if (customer?.LogicalName == "account")
        {
            context.CustomerAccount = FromEntityReference(customer, customer.Name ?? "Selected account", "account");
        }

        if (parentContact != null)
        {
            context.CustomerContact = FromEntityReference(parentContact, parentContact.Name ?? "Customer", "contact");
        }
        else if (customer?.LogicalName == "contact")
        {
            context.CustomerContact = FromEntityReference(customer, customer.Name ?? "Customer", "contact");
        }
    }

    private static LookupValue FromEntityReference(EntityReference? reference, string fallbackName, string fallbackEntityName)
    {
        if (reference == null) return new LookupValue(null, fallbackName, fallbackEntityName);
        return new LookupValue(reference.Id.ToString("D"), reference.Name ?? fallbackName, reference.LogicalName);
    }

    private static string NormalizeGuid(string value)
    {
        return value.Trim().Trim('{', '}');
    }
}
