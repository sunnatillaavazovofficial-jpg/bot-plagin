using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;
using ReklamaMaket.Model;

namespace ReklamaMaket.Rendering
{
    /// <summary>3D sahna: model markazlashtirilgan va eng katta tomoni 2 birlikka keltirilgan.</summary>
    public sealed class Scene3D
    {
        public Scene3D(Model3DGroup model, double halfWidth, double halfHeight, double halfDepth)
        {
            Model = model;
            HalfWidth = halfWidth;
            HalfHeight = halfHeight;
            HalfDepth = halfDepth;
        }

        public Model3DGroup Model { get; }
        public double HalfWidth { get; }
        public double HalfHeight { get; }
        public double HalfDepth { get; }
    }

    /// <summary>SignModel dan WPF 3D modelini quradi (har bir qism — quti).</summary>
    public static class Scene3DBuilder
    {
        private static readonly Color LampGlow = Color.FromRgb(0xFF, 0xF1, 0xC4);

        public static Scene3D Build(SignModel model, BitmapSource faceTexture, bool night)
        {
            var group = new Model3DGroup();
            double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
            double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;

            foreach (Part part in model.Parts)
            {
                switch (part.Kind)
                {
                    case PartKind.Shadow:
                        continue;
                    case PartKind.Person:
                        AddPerson(group, part);
                        break;
                    case PartKind.Face:
                        AddBox(group, part, Solid(part.Color), FaceMaterial(faceTexture, night && part.Glows));
                        break;
                    case PartKind.Lamp:
                        AddBox(group, part, night ? Glowing(part.Color, LampGlow) : Solid(part.Color), null);
                        break;
                    default:
                        AddBox(group, part, Solid(part.Color), null);
                        break;
                }

                // Kamera faqat konstruksiya va odamga qaraydi; devor va yer kadrdan chiqib ketaveradi
                if (part.IsEnvironment && part.Kind != PartKind.Person)
                    continue;

                minX = Math.Min(minX, part.X);
                minY = Math.Min(minY, part.Y);
                minZ = Math.Min(minZ, part.Z);
                maxX = Math.Max(maxX, part.Right);
                maxY = Math.Max(maxY, part.Top);
                maxZ = Math.Max(maxZ, part.Front);
            }

            double size = Math.Max(maxX - minX, maxY - minY);
            double scale = 2.0 / Math.Max(size, 1);

            var transform = new Transform3DGroup();
            transform.Children.Add(new TranslateTransform3D(-(minX + maxX) / 2, -(minY + maxY) / 2, -(minZ + maxZ) / 2));
            transform.Children.Add(new ScaleTransform3D(scale, scale, scale));
            group.Transform = transform;
            group.Freeze();

            return new Scene3D(group,
                (maxX - minX) / 2 * scale,
                (maxY - minY) / 2 * scale,
                (maxZ - minZ) / 2 * scale);
        }

        public static Model3DGroup BuildLights(bool night)
        {
            var lights = new Model3DGroup();
            if (night)
            {
                lights.Children.Add(new AmbientLight(Color.FromRgb(38, 40, 52)));
                lights.Children.Add(new DirectionalLight(Color.FromRgb(55, 58, 78), new Vector3D(-0.4, -0.6, -1)));
            }
            else
            {
                lights.Children.Add(new AmbientLight(Color.FromRgb(105, 105, 105)));
                lights.Children.Add(new DirectionalLight(Color.FromRgb(200, 200, 190), new Vector3D(-0.5, -0.7, -1)));
                lights.Children.Add(new DirectionalLight(Color.FromRgb(55, 55, 65), new Vector3D(0.6, -0.2, -0.5)));
            }
            lights.Freeze();
            return lights;
        }

        private static Material Solid(Color color)
        {
            var material = new DiffuseMaterial(new SolidColorBrush(color));
            material.Freeze();
            return material;
        }

        private static Material Glowing(Color color, Color glow)
        {
            var material = new MaterialGroup();
            material.Children.Add(new DiffuseMaterial(new SolidColorBrush(color)));
            material.Children.Add(new EmissiveMaterial(new SolidColorBrush(glow)));
            material.Freeze();
            return material;
        }

        private static Material FaceMaterial(BitmapSource texture, bool glow)
        {
            var brush = new ImageBrush(texture) { Stretch = Stretch.Fill };
            brush.Freeze();

            var material = new MaterialGroup();
            material.Children.Add(new DiffuseMaterial(brush));
            if (glow)
                material.Children.Add(new EmissiveMaterial(brush));
            material.Freeze();
            return material;
        }

        /// <summary>Quti: old yuzga alohida material (tekstura) berish mumkin.</summary>
        private static void AddBox(Model3DGroup group, Part p, Material sides, Material front)
        {
            double x0 = p.X, x1 = p.Right;
            double y0 = p.Y, y1 = p.Top;
            double z0 = p.Z, z1 = p.Front;

            // Har bir tomon tashqaridan qaralganda: chap-yuqori, chap-past, o'ng-past, o'ng-yuqori
            AddQuad(group, new Point3D(x0, y1, z1), new Point3D(x0, y0, z1), new Point3D(x1, y0, z1), new Point3D(x1, y1, z1), front ?? sides); // old
            AddQuad(group, new Point3D(x1, y1, z0), new Point3D(x1, y0, z0), new Point3D(x0, y0, z0), new Point3D(x0, y1, z0), sides);          // orqa
            AddQuad(group, new Point3D(x1, y1, z1), new Point3D(x1, y0, z1), new Point3D(x1, y0, z0), new Point3D(x1, y1, z0), sides);          // o'ng
            AddQuad(group, new Point3D(x0, y1, z0), new Point3D(x0, y0, z0), new Point3D(x0, y0, z1), new Point3D(x0, y1, z1), sides);          // chap
            AddQuad(group, new Point3D(x0, y1, z0), new Point3D(x0, y1, z1), new Point3D(x1, y1, z1), new Point3D(x1, y1, z0), sides);          // tepa
            AddQuad(group, new Point3D(x0, y0, z1), new Point3D(x0, y0, z0), new Point3D(x1, y0, z0), new Point3D(x1, y0, z1), sides);          // past
        }

        private static void AddQuad(Model3DGroup group, Point3D topLeft, Point3D bottomLeft, Point3D bottomRight, Point3D topRight, Material material)
        {
            var mesh = new MeshGeometry3D
            {
                Positions = new Point3DCollection { topLeft, bottomLeft, bottomRight, topRight },
                TextureCoordinates = new PointCollection { new Point(0, 0), new Point(0, 1), new Point(1, 1), new Point(1, 0) },
                TriangleIndices = new Int32Collection { 0, 1, 2, 0, 2, 3 }
            };
            mesh.Freeze();
            group.Children.Add(new GeometryModel3D(mesh, material) { BackMaterial = material });
        }

        /// <summary>Oddiy odam siluyeti: oyoqlar, tana, bosh.</summary>
        private static void AddPerson(Model3DGroup group, Part p)
        {
            Material material = Solid(p.Color);
            double x = p.X, y = p.Y, z = p.Z, w = p.W, h = p.H, d = p.D;
            double head = h * 0.13;

            AddBox(group, new Part("", PartKind.Person, x + w * 0.2, y, z + d * 0.2, w * 0.26, h * 0.46, d * 0.6, p.Color), material, null);
            AddBox(group, new Part("", PartKind.Person, x + w * 0.54, y, z + d * 0.2, w * 0.26, h * 0.46, d * 0.6, p.Color), material, null);
            AddBox(group, new Part("", PartKind.Person, x + w * 0.1, y + h * 0.45, z, w * 0.8, h * 0.4, d, p.Color), material, null);
            AddBox(group, new Part("", PartKind.Person, x + (w - head * 0.8) / 2, y + h - head, z + (d - head * 0.8) / 2, head * 0.8, head, head * 0.8, p.Color), material, null);
        }
    }
}
