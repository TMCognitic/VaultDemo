public class VaultSettings
{
    /// <summary>Adresse du serveur Vault.</summary>
    public string Address { get; init; } = "http://localhost:8200";

    /// <summary>Identifiants AppRole (TestRole ou ProdRole).</summary>
    public required string RoleId { get; init; }
    public required string SecretId { get; init; }

    /// <summary>Moteur KV v2 à lire : "Test" ou "Prod" (sensible à la casse).</summary>
    public required string MountPoint { get; init; }

    /// <summary>Chemin du secret dans le moteur.</summary>
    public string SecretPath { get; init; } = "config";
}