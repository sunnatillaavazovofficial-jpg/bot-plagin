using System;
using System.Collections.Generic;
using System.Windows.Media;
using ReklamaMaket.Core;
using ReklamaMaket.Designs;

namespace ReklamaMaket.Model
{
    /// <summary>Konstruksiya qayerda turadi.</summary>
    public enum SceneKind
    {
        /// <summary>Bino fasadi (devor) ustida: laytboks, banner, vyveska.</summary>
        Facade,

        /// <summary>Ochiq joyda, yerda turadi: bilbord, pilon.</summary>
        Street
    }

    public enum PartKind
    {
        // Atrof-muhit
        Wall,
        Plinth,
        Ground,
        Shadow,
        Person,

        // Konstruksiya
        Base,
        Pole,
        Frame,
        Face,
        Lamp,
        Eyelet
    }

    /// <summary>
    /// Maketning bitta qismi — to'g'ri burchakli quti. Koordinatalar millimetrda:
    /// X — o'ngga, Y — yuqoriga (yer sathi Y=0), Z — tomoshabin tomonga (devor yuzasi Z=0).
    /// (X, Y, Z) — qutining chap-past-orqa burchagi.
    /// </summary>
    public sealed class Part
    {
        public Part(string name, PartKind kind, double x, double y, double z, double width, double height, double depth, Color color)
        {
            Name = name;
            Kind = kind;
            X = x;
            Y = y;
            Z = z;
            W = Math.Max(width, 0.1);
            H = Math.Max(height, 0.1);
            D = Math.Max(depth, 0.1);
            Color = color;
        }

        public string Name { get; }
        public PartKind Kind { get; }
        public double X { get; }
        public double Y { get; }
        public double Z { get; }
        public double W { get; }
        public double H { get; }
        public double D { get; }
        public Color Color { get; set; }

        /// <summary>Old tomondan ellips bo'lib ko'rinadi (luvers, soya).</summary>
        public bool Round { get; set; }

        /// <summary>Devorga soya tushiradi (faqat fasad sahnasida).</summary>
        public bool CastsShadow { get; set; }

        /// <summary>Kechasi o'zi yorug'lik chiqaradi (projektor, yoritilgan yuz).</summary>
        public bool Glows { get; set; }

        public double Right => X + W;
        public double Top => Y + H;
        public double Front => Z + D;

        public bool IsEnvironment =>
            Kind == PartKind.Wall || Kind == PartKind.Plinth || Kind == PartKind.Ground ||
            Kind == PartKind.Shadow || Kind == PartKind.Person;
    }

    /// <summary>Old tomondan ko'rinadigan chegaralar (mm).</summary>
    public struct Extents
    {
        public double MinX;
        public double MinY;
        public double MaxX;
        public double MaxY;
        public bool IsEmpty;

        public static Extents Empty => new Extents { IsEmpty = true };

        public double Width => MaxX - MinX;
        public double Height => MaxY - MinY;
        public double CenterX => (MinX + MaxX) / 2;
        public double CenterY => (MinY + MaxY) / 2;

        public static Extents FromRect(double x, double y, double width, double height) =>
            new Extents { MinX = x, MinY = y, MaxX = x + width, MaxY = y + height };

        public Extents Union(Extents other)
        {
            if (other.IsEmpty) return this;
            if (IsEmpty) return other;
            return new Extents
            {
                MinX = Math.Min(MinX, other.MinX),
                MinY = Math.Min(MinY, other.MinY),
                MaxX = Math.Max(MaxX, other.MaxX),
                MaxY = Math.Max(MaxY, other.MaxY)
            };
        }
    }

    /// <summary>Tayyor maket: konstruksiya + atrof-muhit. Corel ham, 3D ham shundan chiziladi.</summary>
    public sealed class SignModel
    {
        public SignModel(DesignType design, MockupSettings settings)
        {
            Design = design;
            Settings = settings;
        }

        public DesignType Design { get; }
        public MockupSettings Settings { get; }
        public List<Part> Parts { get; } = new List<Part>();

        /// <summary>Logo bosiladigan yuz. Har bir dizayn turi buni albatta belgilashi kerak.</summary>
        public Part Face { get; set; }

        /// <summary>Osmon (faqat ko'cha sahnasida, atrof-muhit yoqilganda).</summary>
        public Extents Sky { get; set; } = Extents.Empty;

        public Part Add(Part part)
        {
            Parts.Add(part);
            return part;
        }

        /// <summary>Faqat konstruksiya qismlarining chegarasi.</summary>
        public Extents ConstructionExtents()
        {
            Extents result = Extents.Empty;
            foreach (Part part in Parts)
            {
                if (!part.IsEnvironment)
                    result = result.Union(Extents.FromRect(part.X, part.Y, part.W, part.H));
            }
            return result;
        }

        /// <summary>Butun sahna chegarasi (osmon bilan).</summary>
        public Extents SceneExtents()
        {
            Extents result = Sky;
            foreach (Part part in Parts)
                result = result.Union(Extents.FromRect(part.X, part.Y, part.W, part.H));
            return result;
        }
    }
}
