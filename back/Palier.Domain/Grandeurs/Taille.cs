namespace Palier.Domain.Grandeurs;

/// <summary>
/// Une taille corporelle, saisie en centimètres.
/// </summary>
/// <remarks>
/// <see cref="Metres" /> existe parce que les équations ne s'accordent pas sur
/// l'unité : Mifflin-St Jeor prend des centimètres, Ten-Haaf des mètres. Une
/// conversion laissée à l'appelant est une division par cent qu'on oublie une
/// fois, et l'erreur d'un facteur cent ne se voit pas dans un test qui n'existe
/// pas encore.
/// </remarks>
public readonly record struct Taille
{
    private Taille(decimal centimetres) => Centimetres = centimetres;

    public decimal Centimetres { get; }

    public decimal Metres => Centimetres / 100m;

    public static Taille DepuisCentimetres(decimal centimetres) =>
        centimetres is >= 30m and <= 300m
            ? new Taille(centimetres)
            : throw new ArgumentOutOfRangeException(nameof(centimetres), centimetres, null);
}
