namespace Fixtures;

// Violation délibérée : déréférencement d'une référence possiblement nulle.
// Ce projet est hors de Palier.sln et n'est compilé que par son épreuve.
public static class Nullable
{
    public static int Longueur(string? valeur)
    {
        return valeur.Length;
    }
}
