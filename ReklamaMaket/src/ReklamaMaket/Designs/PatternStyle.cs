namespace ReklamaMaket.Designs
{
    /// <summary>Gul (teshik) shakli.</summary>
    public enum HoleShape
    {
        /// <summary>Olti burchakli setka (asalari uyasi).</summary>
        Hexagon,

        /// <summary>Dumaloq teshik.</summary>
        Circle,

        /// <summary>Xochsimon (+) teshik.</summary>
        Cross,

        /// <summary>Kvadrat teshik / kvadrat setka.</summary>
        Square,

        /// <summary>Uzunchoq tirqish (oval uchli).</summary>
        Slot,

        /// <summary>Romb setka (prosechno-vytyajnoy list).</summary>
        Diamond
    }

    /// <summary>
    /// Temir list guli (perforatsiya / setka). Barcha gullar <see cref="PatternStyles.All"/> ro'yxatida.
    /// </summary>
    public sealed class PatternStyle
    {
        public PatternStyle(string name, string englishName, HoleShape shape, double pitch, double open, double aspect = 1)
        {
            Name = name;
            EnglishName = englishName;
            Shape = shape;
            Pitch = pitch;
            Open = open;
            Aspect = aspect;
        }

        public string Name { get; }

        /// <summary>Inglizcha nomi — ChatGPT so'rovi uchun.</summary>
        public string EnglishName { get; }

        public HoleShape Shape { get; }

        /// <summary>Teshiklar orasidagi qadam (markazdan markazga), mm.</summary>
        public double Pitch { get; }

        /// <summary>Teshik o'lchami qadamga nisbatan (0..1). Kattaroq — temir chiziqlar ingichkaroq.</summary>
        public double Open { get; }

        /// <summary>Teshik bo'yi / eni (romb va tirqish uchun).</summary>
        public double Aspect { get; }

        public override string ToString() => Name;
    }

    /// <summary>Yuborilgan rasmdagi barcha gullar (qatorma-qator).</summary>
    public static class PatternStyles
    {
        public static readonly PatternStyle[] All =
        {
            // 1-qator: olti burchakli setka — sim ingichkadan qalinga
            new PatternStyle("Olti burchakli setka — juda ingichka", "hexagonal honeycomb mesh, very thin bars", HoleShape.Hexagon, 30, 0.94),
            new PatternStyle("Olti burchakli setka — ingichka", "hexagonal honeycomb mesh, thin bars", HoleShape.Hexagon, 30, 0.90),
            new PatternStyle("Olti burchakli setka — o'rta", "hexagonal honeycomb mesh, medium bars", HoleShape.Hexagon, 30, 0.85),
            new PatternStyle("Olti burchakli setka — qalin", "hexagonal honeycomb mesh, thick bars", HoleShape.Hexagon, 30, 0.79),
            new PatternStyle("Olti burchakli setka — juda qalin", "hexagonal honeycomb mesh, very thick bars", HoleShape.Hexagon, 30, 0.72),

            // 2-qator: dumaloq teshiklar — kattadan kichikka
            new PatternStyle("Dumaloq teshik — zich", "round-hole perforated sheet, large tightly packed holes", HoleShape.Circle, 25, 0.92),
            new PatternStyle("Dumaloq teshik — katta", "round-hole perforated sheet, large holes", HoleShape.Circle, 25, 0.82),
            new PatternStyle("Dumaloq teshik — o'rta", "round-hole perforated sheet, medium holes", HoleShape.Circle, 25, 0.68),
            new PatternStyle("Dumaloq teshik — kichik", "round-hole perforated sheet, small holes", HoleShape.Circle, 25, 0.52),
            new PatternStyle("Dumaloq teshik — mayda", "round-hole perforated sheet, tiny sparse holes", HoleShape.Circle, 20, 0.40),

            // 3-qator: xochsimon teshiklar
            new PatternStyle("Xochsimon teshik — katta", "cross-shaped (plus) perforation, large crosses", HoleShape.Cross, 30, 0.70),
            new PatternStyle("Xochsimon teshik — o'rta", "cross-shaped (plus) perforation, medium crosses", HoleShape.Cross, 30, 0.60),
            new PatternStyle("Xochsimon teshik — kichik", "cross-shaped (plus) perforation, small crosses", HoleShape.Cross, 30, 0.50),
            new PatternStyle("Xochsimon teshik — mayda", "cross-shaped (plus) perforation, tiny crosses", HoleShape.Cross, 24, 0.42),

            // 4-qator: kvadrat
            new PatternStyle("Kvadrat teshik — kichik", "square-hole perforated sheet, small square holes", HoleShape.Square, 30, 0.48),
            new PatternStyle("Kvadrat teshik — katta", "square-hole perforated sheet, large square holes", HoleShape.Square, 30, 0.64),
            new PatternStyle("Kvadrat setka", "square welded mesh with thin bars", HoleShape.Square, 30, 0.86),

            // 5-qator: uzunchoq tirqishlar
            new PatternStyle("Tirqish — juda uzun", "slotted sheet, very long vertical slots", HoleShape.Slot, 24, 0.40, 6),
            new PatternStyle("Tirqish — uzun", "slotted sheet, long vertical slots", HoleShape.Slot, 24, 0.42, 4.5),
            new PatternStyle("Tirqish — o'rta", "slotted sheet, medium staggered slots", HoleShape.Slot, 24, 0.44, 3),
            new PatternStyle("Tirqish — qisqa", "slotted sheet, short staggered slots", HoleShape.Slot, 24, 0.45, 2.2),

            // 6–7-qatorlar: romb setka (prosechka)
            new PatternStyle("Romb setka — ingichka, cho'ziq", "expanded metal mesh, elongated diamonds, thin strands", HoleShape.Diamond, 40, 0.90, 0.45),
            new PatternStyle("Romb setka — o'rta, cho'ziq", "expanded metal mesh, elongated diamonds, medium strands", HoleShape.Diamond, 40, 0.82, 0.45),
            new PatternStyle("Romb setka — qalin, cho'ziq", "expanded metal mesh, elongated diamonds, thick strands", HoleShape.Diamond, 40, 0.72, 0.45),
            new PatternStyle("Romb setka — juda cho'ziq, ingichka", "expanded metal mesh, very flat diamonds, thin strands", HoleShape.Diamond, 40, 0.88, 0.33),
            new PatternStyle("Romb setka — juda cho'ziq, qalin", "expanded metal mesh, very flat diamonds, thick strands", HoleShape.Diamond, 40, 0.74, 0.33),
            new PatternStyle("Romb setka — keng, ingichka", "expanded metal mesh, wide diamonds, thin strands", HoleShape.Diamond, 40, 0.90, 0.6),
            new PatternStyle("Romb setka — keng, qalin", "expanded metal mesh, wide diamonds, thick strands", HoleShape.Diamond, 40, 0.75, 0.6),
            new PatternStyle("Romb setka — mayda", "fine expanded metal mesh, small diamonds", HoleShape.Diamond, 24, 0.84, 0.5),
            new PatternStyle("Romb setka — yirik", "large expanded metal mesh, big diamonds", HoleShape.Diamond, 60, 0.84, 0.5),
        };
    }
}
