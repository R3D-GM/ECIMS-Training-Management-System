using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace TCS.External;

// Call this from a controller right after you SaveChangesAsync() something
// that should be mirrored to the instructor's system - a Company, a User,
// a TrainingSession, a UatProject, etc. Every method here is "fire and
// forget safe": if BaseUrl isn't set yet, or the call fails, it logs a
// warning and returns - it never throws, so it can never break your own
// app's save.
public class ExternalSyncClient
{
    private readonly HttpClient _http;
    private readonly ExternalSystemOptions _options;
    private readonly ILogger<ExternalSyncClient> _logger;

    public ExternalSyncClient(HttpClient http, IOptions<ExternalSystemOptions> options, ILogger<ExternalSyncClient> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;

        if (!string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            _http.BaseAddress = new Uri(_options.BaseUrl);
            if (!string.IsNullOrWhiteSpace(_options.ApiKey))
                _http.DefaultRequestHeaders.Add("X-Api-Key", _options.ApiKey);
        }
    }

    public Task SyncConsigneeAsync(ConsigneeDTO dto) => PostAsync(_options.ConsigneeEndpoint, dto, "Consignee");
    public Task SyncUserAsync(UserDTO dto) => PostAsync(_options.UserEndpoint, dto, "User");
    public Task SyncUserRoleMapperAsync(UserRoleMapperDTO dto) => PostAsync(_options.UserRoleMapperEndpoint, dto, "UserRoleMapper");
    public Task SyncVoucherAsync(VoucherDTO dto) => PostAsync(_options.VoucherEndpoint, dto, "Voucher");
    public Task SyncActivityAsync(ActivityDTO dto) => PostAsync(_options.ActivityEndpoint, dto, "Activity");
    public Task SyncSystemConstantAsync(SystemConstantDTO dto) => PostAsync(_options.SystemConstantEndpoint, dto, "SystemConstant");

    // Same as above, but returns the Id his system assigned to the new
    // record (or null if syncing is off/failed) - you need this Id to
    // reference the record later (e.g. a Consignee's Id becomes the
    // "Person" field on a UserDTO).
    public Task<int?> SyncConsigneeAndGetIdAsync(ConsigneeDTO dto) => PostAndGetIdAsync(_options.ConsigneeEndpoint, dto, "Consignee");
    public Task<int?> SyncUserAndGetIdAsync(UserDTO dto) => PostAndGetIdAsync(_options.UserEndpoint, dto, "User");
    public Task<int?> SyncVoucherAndGetIdAsync(VoucherDTO dto) => PostAndGetIdAsync(_options.VoucherEndpoint, dto, "Voucher");

    // "Save and update" - once a record has already been created and has an
    // ExternalVoucherId (or similar), later changes go through PUT to the
    // same endpoint + /{id} instead of creating a duplicate.
    public Task UpdateVoucherAsync(int externalId, VoucherDTO dto) => PutAsync($"{_options.VoucherEndpoint}/{externalId}", dto, "Voucher");
    public Task UpdateConsigneeAsync(int externalId, ConsigneeDTO dto) => PutAsync($"{_options.ConsigneeEndpoint}/{externalId}", dto, "Consignee");

    private async Task PutAsync<T>(string endpointPath, T dto, string label)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            _logger.LogInformation("External sync (update) skipped ({Label}) - ExternalSystem:BaseUrl is not configured yet.", label);
            return;
        }

        try
        {
            var response = await _http.PutAsJsonAsync(endpointPath, dto);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("External sync (update) failed ({Label}): {Status} - {Body}", label, response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "External sync (update) errored ({Label}) while calling {Endpoint}", label, endpointPath);
        }
    }

    // Same as above, but returns the Id his system assigned to the new
    // record (or null if syncing is off/failed) - you need this Id to
    // reference the record later (e.g. a Consignee's Id becomes the
    // "Person" field on a UserDTO).
    private async Task<int?> PostAndGetIdAsync<T>(string endpointPath, T dto, string label)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            _logger.LogInformation("External sync skipped ({Label}) - ExternalSystem:BaseUrl is not configured yet in appsettings.json.", label);
            return null;
        }

        try
        {
            var response = await _http.PostAsJsonAsync(endpointPath, dto);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("External sync failed ({Label}): {Status} - {Body}", label, response.StatusCode, body);
                return null;
            }

            // Assumes his system echoes back the created object (with its
            // own assigned Id) in the response body, same shape as T.
            // If his API instead returns just a raw number or a wrapper
            // object, this line is exactly what needs adjusting.
            var created = await response.Content.ReadFromJsonAsync<T>();
            var idProperty = typeof(T).GetProperty("Id");
            return idProperty?.GetValue(created) as int?;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "External sync errored ({Label}) while calling {Endpoint}", label, endpointPath);
            return null;
        }
    }

    private async Task PostAsync<T>(string endpointPath, T dto, string label)
    {
        if (string.IsNullOrWhiteSpace(_options.BaseUrl))
        {
            _logger.LogInformation("External sync skipped ({Label}) - ExternalSystem:BaseUrl is not configured yet in appsettings.json.", label);
            return;
        }

        try
        {
            var response = await _http.PostAsJsonAsync(endpointPath, dto);
            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("External sync failed ({Label}): {Status} - {Body}", label, response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            // Never let a sync failure break the user's actual save.
            _logger.LogWarning(ex, "External sync errored ({Label}) while calling {Endpoint}", label, endpointPath);
        }
    }
}
