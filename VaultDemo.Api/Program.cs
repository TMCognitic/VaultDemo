using VaultSharp;
using VaultSharp.V1.AuthMethods.AppRole;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();


/* Contenu du fichier user-secret pour l'exemple en cours
    "Vault": {
        "Address": "http://localhost:8200",
        "RoleId": "test-role-id",
        "SecretId": "test-secret-id",
        "MountPoint": "Test",
        "SecretPath": "config"
    } 
*/

var vaultSettings = builder.Configuration.GetRequiredSection("Vault").Get<VaultSettings>()!;

builder.Services.AddSingleton<IVaultClient>(_ =>
{
    var auth = new AppRoleAuthMethodInfo(vaultSettings.RoleId, vaultSettings.SecretId);
    return new VaultClient(new VaultClientSettings(vaultSettings.Address, auth));
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapGet("/status", async (IVaultClient vault, ILogger<Program> logger) =>
{
    logger.LogInformation($"Address : {vaultSettings.Address}");
    logger.LogInformation($"MountPoint : {vaultSettings.MountPoint}");
    logger.LogInformation($"SecretPath : {vaultSettings.SecretPath}");
    logger.LogInformation($"RoleId : {vaultSettings.RoleId}");
    logger.LogInformation($"SecretId : {vaultSettings.SecretId}");

    var secret = await vault.V1.Secrets.KeyValue.V2.ReadSecretAsync(path: vaultSettings.SecretPath, mountPoint: vaultSettings.MountPoint);

    return secret.Data.Data.TryGetValue("data", out var value)
        ? Results.Ok(new { vaultSettings.MountPoint, data = value?.ToString() })
        : Results.NotFound($"Pas de clé 'data' dans {vaultSettings.MountPoint}/{vaultSettings.SecretPath}.");
})
.WithName("Status");

app.Run();
