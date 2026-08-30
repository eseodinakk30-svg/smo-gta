using UnityEngine;

namespace SanMonica.Characters
{
    /// <summary>One buyable set of clothes.</summary>
    public struct OutfitStyle
    {
        public string Id;
        public string Name;
        public string Detail;
        public int Price;
        public Color Shirt;
        public Color Trousers;
        public Color Shoes;
        public Color Accent;
        public bool Hat;
        public bool Vest;
        public bool Backpack;
    }

    /// <summary>One buyable cut and colour.</summary>
    public struct HairStyle
    {
        public string Id;
        public string Name;
        public string Detail;
        public int Price;
        public Color Colour;
        public bool Cropped;
    }

    /// <summary>
    /// Everything the player can wear. The shops read this table, and buying an
    /// entry rebuilds the body mesh - clothes and haircuts used to be a
    /// notification and a saved integer that nothing ever looked at again.
    /// </summary>
    public static class Wardrobe
    {
        public static readonly OutfitStyle[] Outfits =
        {
            new OutfitStyle { Id = "street", Name = "Street", Detail = "Grey tee, dark jeans", Price = 180,
                Shirt = new Color(0.62f, 0.63f, 0.66f), Trousers = new Color(0.20f, 0.22f, 0.30f),
                Shoes = new Color(0.12f, 0.12f, 0.14f), Accent = new Color(0.45f, 0.47f, 0.50f) },

            new OutfitStyle { Id = "workwear", Name = "Workwear", Detail = "Canvas jacket and boots", Price = 420,
                Shirt = new Color(0.55f, 0.40f, 0.20f), Trousers = new Color(0.28f, 0.30f, 0.34f),
                Shoes = new Color(0.24f, 0.17f, 0.11f), Accent = new Color(0.72f, 0.55f, 0.18f), Hat = true },

            new OutfitStyle { Id = "suit", Name = "Sharp Suit", Detail = "Charcoal two-piece", Price = 2600,
                Shirt = new Color(0.16f, 0.17f, 0.20f), Trousers = new Color(0.14f, 0.15f, 0.18f),
                Shoes = new Color(0.09f, 0.08f, 0.08f), Accent = new Color(0.80f, 0.80f, 0.84f) },

            new OutfitStyle { Id = "track", Name = "Track Set", Detail = "Two stripes, no explanation", Price = 340,
                Shirt = new Color(0.16f, 0.42f, 0.62f), Trousers = new Color(0.15f, 0.16f, 0.20f),
                Shoes = new Color(0.90f, 0.90f, 0.92f), Accent = new Color(0.90f, 0.90f, 0.92f) },

            new OutfitStyle { Id = "linen", Name = "Coastal Linen", Detail = "For the marina crowd", Price = 900,
                Shirt = new Color(0.93f, 0.91f, 0.84f), Trousers = new Color(0.80f, 0.76f, 0.66f),
                Shoes = new Color(0.55f, 0.42f, 0.28f), Accent = new Color(0.35f, 0.55f, 0.62f) },

            new OutfitStyle { Id = "night", Name = "Night Out", Detail = "Black on black", Price = 1400,
                Shirt = new Color(0.10f, 0.10f, 0.12f), Trousers = new Color(0.09f, 0.09f, 0.11f),
                Shoes = new Color(0.07f, 0.07f, 0.09f), Accent = new Color(0.70f, 0.18f, 0.30f) },

            new OutfitStyle { Id = "utility", Name = "Utility Rig", Detail = "Jacket over a plate carrier", Price = 3200,
                Shirt = new Color(0.24f, 0.28f, 0.24f), Trousers = new Color(0.20f, 0.22f, 0.20f),
                Shoes = new Color(0.14f, 0.13f, 0.12f), Accent = new Color(0.18f, 0.20f, 0.18f),
                Vest = true, Backpack = true },

            new OutfitStyle { Id = "formal", Name = "Formal Black", Detail = "Funerals and board rooms", Price = 4800,
                Shirt = new Color(0.07f, 0.07f, 0.09f), Trousers = new Color(0.07f, 0.07f, 0.09f),
                Shoes = new Color(0.05f, 0.05f, 0.06f), Accent = new Color(0.62f, 0.10f, 0.12f) },

            new OutfitStyle { Id = "hivis", Name = "Dock High-Vis", Detail = "Nobody questions the vest", Price = 260,
                Shirt = new Color(0.92f, 0.72f, 0.10f), Trousers = new Color(0.22f, 0.24f, 0.28f),
                Shoes = new Color(0.18f, 0.16f, 0.14f), Accent = new Color(0.95f, 0.55f, 0.08f),
                Hat = true, Vest = true },

            new OutfitStyle { Id = "beach", Name = "Beach Day", Detail = "Shorts and a loud shirt", Price = 220,
                Shirt = new Color(0.20f, 0.68f, 0.62f), Trousers = new Color(0.88f, 0.84f, 0.72f),
                Shoes = new Color(0.72f, 0.66f, 0.56f), Accent = new Color(0.95f, 0.45f, 0.30f) },
        };

        public static readonly HairStyle[] Hairstyles =
        {
            new HairStyle { Id = "buzz",    Name = "Buzz Cut",     Detail = "Short, black",     Price = 90,  Colour = new Color(0.08f, 0.07f, 0.07f), Cropped = true },
            new HairStyle { Id = "crop",    Name = "Crop",         Detail = "Short, dark brown",Price = 120, Colour = new Color(0.22f, 0.14f, 0.09f), Cropped = true },
            new HairStyle { Id = "fade",    Name = "Ash Fade",     Detail = "Short, ash grey",  Price = 180, Colour = new Color(0.55f, 0.54f, 0.52f), Cropped = true },
            new HairStyle { Id = "grown",   Name = "Grown Out",    Detail = "Long, black",      Price = 140, Colour = new Color(0.10f, 0.09f, 0.09f), Cropped = false },
            new HairStyle { Id = "sand",    Name = "Sandblond",    Detail = "Long, sand",       Price = 260, Colour = new Color(0.76f, 0.66f, 0.40f), Cropped = false },
            new HairStyle { Id = "auburn",  Name = "Auburn",       Detail = "Long, red brown",  Price = 260, Colour = new Color(0.44f, 0.20f, 0.12f), Cropped = false },
            new HairStyle { Id = "bleach",  Name = "Bleached",     Detail = "Long, near white", Price = 420, Colour = new Color(0.90f, 0.88f, 0.82f), Cropped = false },
            new HairStyle { Id = "marine",  Name = "Marine Blue",  Detail = "Short, dyed blue", Price = 520, Colour = new Color(0.18f, 0.32f, 0.62f), Cropped = true },
        };

        public static OutfitStyle Outfit(int index)
            => Outfits[Mathf.Clamp(index, 0, Outfits.Length - 1)];

        public static HairStyle Hair(int index)
            => Hairstyles[Mathf.Clamp(index, 0, Hairstyles.Length - 1)];

        public static CharacterAppearance With(CharacterAppearance app, in OutfitStyle outfit)
        {
            app.Shirt = outfit.Shirt;
            app.Trousers = outfit.Trousers;
            app.Shoes = outfit.Shoes;
            app.Accent = outfit.Accent;
            app.Hat = outfit.Hat;
            app.Vest = outfit.Vest;
            app.Backpack = outfit.Backpack;
            return app;
        }

        public static CharacterAppearance With(CharacterAppearance app, in HairStyle hair)
        {
            app.Hair = hair.Colour;
            app.ShortHair = hair.Cropped;
            return app;
        }
    }
}
