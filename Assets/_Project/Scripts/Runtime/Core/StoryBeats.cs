namespace Game.Core
{
    /// <summary>
    /// Every line of story text in one table: the opening, one line per district on its first visit, the names of the
    /// building problems and what residents say. Short on purpose; the story is told by the world, not by reading.
    /// </summary>
    public static class StoryBeats
    {
        public const string Opening = "Fırtına Çekiçköy'ü yerle bir etti. Çekicini al, köyü yeniden kur!";

        /// <summary>Shown once, the first time a district is entered (same order as <see cref="Balance.Districts"/>).</summary>
        public static readonly string[] DistrictIntro =
        {
            "Sahil: dalgalar her şeyi alıp götürdü. Bir inşaata yaklaş, çekicin kendiliğinden vurur!",
            "Merkez: meydan bomboş. Dükkanlar açılınca köy canlanacak!",
            "Liman: balıkçılar tekneleriyle bekliyor, iskeleyi hazırlayalım!",
            "Tepe: değirmen durmuş. Rüzgar yine esiyor, haydi!",
            "Sanayi: son mahalle. Bacalar tüttüğünde köy tamam!",
        };

        /// <summary>Index 0 is "no problem"; 1..<see cref="Balance.RepairKinds"/> match <see cref="DistrictState.Issues"/>.</summary>
        public static readonly string[] IssueNames = { "", "Çatı sızıntısı", "Kırık boru", "Bozuk elektrik", "Çatlak duvar" };

        /// <summary>The six decorations of a district, in the order they are built (see <see cref="Balance.DecorationCount"/>).</summary>
        public static readonly string[] DecorationNames = { "Çiçeklik", "Bank ve Lamba", "Küçük Park", "Fıskiye", "Çardak", "Heykel" };

        static readonly string[] Thanks = { "Teşekkürler!", "Eline sağlık!", "Harika iş!", "Çok yaşa!" };

        public static string ThanksLine(int seed) => Thanks[((seed % Thanks.Length) + Thanks.Length) % Thanks.Length];

        public static string MovedIn(string building) => $"Yeni sakin taşındı: {building}";

        public static string Problem(string building, int kind) => $"{building}: {IssueNames[kind]}!";
    }
}
