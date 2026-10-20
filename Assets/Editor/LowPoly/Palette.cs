using UnityEngine;

namespace Nuclear.EditorTools.LowPoly
{
    /// <summary>Цвета палитры. У каждого — свой материал с процедурной текстурой поверхности.</summary>
    public enum Swatch
    {
        // Земля и пепел
        AshDark, Ash, AshLight, Dirt, DeadGrass, Scorched, ScorchedLight,
        // Дорога и бетон
        Asphalt, AsphaltDark, RoadLine, Concrete, ConcreteDark, ConcreteStain,
        // Металл
        Metal, MetalDark, Rust, RustDark, Tin,
        // Дерево и кирпич
        Wood, WoodDark, WoodPale, Bark, BarkDark, Brick,
        // Машины
        PaintRed, PaintBlue, PaintGreen, PaintBeige, Glass, Tire,
        // Разметка и пометки
        HazardYellow, HazardBlack, SignalRed, White,
        // Выживший
        Jacket, JacketDark, Trousers, Canvas, Rubber, Lens, Boots,
        // Предметы
        FilterOlive, FilterOliveDark, Brass, Copper, Lead, BatteryCase, LabelPaper, CanLabel,
        // Слепыш: облезлая шерсть морской свинки, голая кожа, звёздчатый нос, резцы
        Skin, SkinDark, SkinPale, Mouth, Claw,
        Fur, FurDark, FurPale, NosePink, NoseDark, Teeth,
    }

    /// <summary>
    /// Палитра low-poly стиля: выгоревшие под пеплом, но тёплые цвета (ржавчина, сухая трава, старое дерево).
    /// Каждый цвет — основа своей процедурной текстуры (см. Surfaces).
    /// </summary>
    public static class Palette
    {
        private static readonly Color[] Colors =
        {
            // Земля и пепел
            Hex(0x5a5550), Hex(0x6f6a62), Hex(0x857e72), Hex(0x66574a), Hex(0x7a7048), Hex(0x2b2724), Hex(0x463d35),
            // Дорога и бетон
            Hex(0x3d3c3c), Hex(0x2c2b2b), Hex(0xb09a55), Hex(0x8f8a82), Hex(0x6d6862), Hex(0x5e574b),
            // Металл
            Hex(0x7a7d7f), Hex(0x45484b), Hex(0x8a4f2a), Hex(0x5a3320), Hex(0xa3a6a6),
            // Дерево и кирпич
            Hex(0x7a5a3c), Hex(0x523d2a), Hex(0x9c8160), Hex(0x433830), Hex(0x2d2620), Hex(0x84492f),
            // Машины
            Hex(0x8c3a2e), Hex(0x3f5a70), Hex(0x55663f), Hex(0xa8977a), Hex(0x2c3438), Hex(0x1c1c1d),
            // Разметка и пометки
            Hex(0xc9a227), Hex(0x232220), Hex(0xa8352a), Hex(0xd9d6cf),
            // Выживший
            Hex(0x5b6040), Hex(0x41452f), Hex(0x3f3f40), Hex(0x7a6c52), Hex(0x262626), Hex(0x5f7a6e), Hex(0x2a2420),
            // Предметы
            Hex(0x5b6340), Hex(0x434a2f), Hex(0xa88a46), Hex(0xa0603a), Hex(0x55575a), Hex(0x2d3238), Hex(0xc8bfa4), Hex(0x9a4a35),
            // Слепыш
            Hex(0xb8a89c), Hex(0x8c7a70), Hex(0xd6c9bd), Hex(0x3a1f1e), Hex(0x2e2a26),
            Hex(0x7a6450), Hex(0x47382c), Hex(0xb3a185), Hex(0xc98a86), Hex(0x8c5556), Hex(0xd9c58f),
        };

        public static Color ColorOf(Swatch swatch) => Colors[(int)swatch];

        private static Color Hex(int rgb) =>
            new Color(((rgb >> 16) & 0xff) / 255f, ((rgb >> 8) & 0xff) / 255f, (rgb & 0xff) / 255f);
    }
}
