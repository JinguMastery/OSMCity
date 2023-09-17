using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Numerics;


namespace HeightPrediction
{

    public delegate (double, double)? RotateObjFunc(double w, double h, double angle);

    public enum ColorComponent
    {
        Red, Green, Blue
    }

    class ObjectPicture
    {
        public Bitmap ObjImage { get; }

        //couleurs minimale et maximale, délimitant une plage de couleurs à considérer pour la segmentation des contours de l'objet sur l'image
        public Color MinColor { get; }
        public Color MaxColor { get; }

        public int ObjHeight
        {
            get
            {
                if (ObjImage == null)
                    return 0;
                int maxHeight = 0;
                for (int i = 0; i < ObjImage.Width; i++)
                {
                    int pixelCount = 0;
                    for (int j = 0; j < ObjImage.Height; j++)
                    {
                        Color c = ObjImage.GetPixel(i, j);
                        //Console.WriteLine($"R = {c.R}, G = {c.G}, B = {c.B}, A = {c.A}");
                        // teste si la couleur du pixel se trouve dans la plage de couleurs de l'objet
                        if (c.R >= MinColor.R && c.R <= MaxColor.R && c.G >= MinColor.G && c.G <= MaxColor.G && c.B >= MinColor.B && c.B <= MaxColor.B && c.A >= MinColor.A && c.A <= MaxColor.A)
                        {
                            pixelCount++;
                        }
                    }
                    if (pixelCount > maxHeight)
                        maxHeight = pixelCount;
                }
                return maxHeight ;
            }
        }

        public int ObjWidth
        {
            get
            {
                if (ObjImage == null)
                    return 0;
                int maxwidth = 0;
                for (int j = 0; j < ObjImage.Height; j++)
                {
                    int pixelCount = 0;
                    for (int i = 0; i < ObjImage.Width; i++)
                    {
                        Color c = ObjImage.GetPixel(i, j);
                        //Console.WriteLine($"R = {c.R}, G = {c.G}, B = {c.B}, A = {c.A}");
                        // teste si la couleur du pixel se trouve dans la plage de couleurs de l'objet
                        if (c.R >= MinColor.R && c.R <= MaxColor.R && c.G >= MinColor.G && c.G <= MaxColor.G && c.B >= MinColor.B && c.B <= MaxColor.B && c.A >= MinColor.A && c.A <= MaxColor.A)
                        {
                            pixelCount++;
                        }
                    }
                    if (pixelCount > maxwidth)
                        maxwidth = pixelCount;
                }
                return maxwidth;
            }
        }

        public Rectangle ObjRect
        {
            get
            {
                if (ObjImage == null)
                    return Rectangle.Empty;
                int xMax = 0, yMax = 0, xMin = ObjImage.Width - 1, yMin = ObjImage.Height - 1;
                for (int j = 0; j < ObjImage.Height; j++)
                {
                    for (int i = 0; i < ObjImage.Width; i++)
                    {
                        Color c = ObjImage.GetPixel(i, j);
                        //Console.WriteLine($"R = {c.R}, G = {c.G}, B = {c.B}, A = {c.A}");
                        // teste si la couleur du pixel se trouve dans la plage de couleurs de l'objet
                        if (c.R >= MinColor.R && c.R <= MaxColor.R && c.G >= MinColor.G && c.G <= MaxColor.G && c.B >= MinColor.B && c.B <= MaxColor.B && c.A >= MinColor.A && c.A <= MaxColor.A)
                        {
                            if (i > xMax)
                                xMax = i;
                            if (i < xMin)
                                xMin = i;
                            if (j > yMax)
                                yMax = j;
                            if (j < yMin)
                                yMin = j;
                        }
                    }
                }
                int w = xMax - xMin + 1, h = yMax - yMin + 1;
                if (w > 0 && h > 0)
                    return new Rectangle(xMin, yMin, w, h);
                else
                    return new Rectangle(0, 0, ObjImage.Width, ObjImage.Height);
            }
        }

        public ObjectPicture(string path, Color c1, Color c2)
        {
            MinColor = Color.FromArgb(Math.Min(c1.A, c2.A), Math.Min(c1.R, c2.R), Math.Min(c1.G, c2.G), Math.Min(c1.B, c2.B));
            MaxColor = Color.FromArgb(Math.Max(c1.A, c2.A), Math.Max(c1.R, c2.R), Math.Max(c1.G, c2.G), Math.Max(c1.B, c2.B));
            try
            {
                ObjImage = new Bitmap(path, true);
            }
            catch (Exception exc)
            {
                Console.WriteLine(exc.Message);
            }
        }

        public ObjectPicture(Bitmap bitmap, Color c1, Color c2) {
            if (bitmap != null)
                ObjImage = new Bitmap(bitmap);
            MinColor = Color.FromArgb(Math.Min(c1.A, c2.A), Math.Min(c1.R, c2.R), Math.Min(c1.G, c2.G), Math.Min(c1.B, c2.B));
            MaxColor = Color.FromArgb(Math.Max(c1.A, c2.A), Math.Max(c1.R, c2.R), Math.Max(c1.G, c2.G), Math.Max(c1.B, c2.B));
        }

        public Bitmap Crop(Rectangle rect)
        {
            if (ObjImage == null)
                return null;
            return ObjImage.Clone(rect, ObjImage.PixelFormat);
        }

        public static void PrintColors(Bitmap bitmap, int nColors)
        {
            long inc = 0;
            for (int i = 0; i < bitmap.Width; i++)
            {
                for (int j = 0; j < bitmap.Height; j++)
                {
                    inc++;
                    if (nColors > 0 && inc % nColors == 0)
                        Console.ReadLine();
                    Color c = bitmap.GetPixel(i, j);
                    Console.WriteLine($"({i}, {j}): R={c.R}, G={c.G}, B={c.B}");
                }
            }
        }

        public static Bitmap ToGrayscale(Bitmap bitmap)
        {
            if (bitmap == null)
                return null;
            Bitmap newBitmap = new Bitmap(bitmap.Width, bitmap.Height);
            for (int i = 0; i < bitmap.Width; i++)
            {
                for (int j = 0; j < bitmap.Height; j++)
                {
                    Color oc = bitmap.GetPixel(i, j);
                    int grayScale = (int)((oc.R * 0.3) + (oc.G * 0.59) + (oc.B * 0.11));
                    Color nc = Color.FromArgb(oc.A, grayScale, grayScale, grayScale);
                    newBitmap.SetPixel(i, j, nc);
                }
            }
            return newBitmap;
        }

        public static Bitmap Convolve(Bitmap bitmap, double[,] kernel, ColorComponent comp)      //convolve the image on one of the 4 color components with the specified kernel of any size, using zero-padding
        {
            if (bitmap == null || kernel == null)
                return null;
            int wKer = kernel.GetLength(0), hKer = kernel.GetLength(1);
            Bitmap paddedBitmap = new Bitmap(bitmap.Width + wKer - 1, bitmap.Height + hKer - 1);
            Bitmap newBitmap = new Bitmap(bitmap.Width, bitmap.Height);
            for (int i = 0; i < bitmap.Width; i++)
            {
                for (int j = 0; j < bitmap.Height; j++)
                {
                    paddedBitmap.SetPixel(i + wKer / 2, j + hKer / 2, bitmap.GetPixel(i, j));
                }
            }
            for (int i = 0; i < bitmap.Width; i++)
            {
                for (int j = 0; j < bitmap.Height; j++)
                {
                    int newColor = 0;
                    for (int k = 0; k < wKer; k++)
                    {
                        for (int l = 0; l < hKer; l++)
                        {
                            Color c = paddedBitmap.GetPixel(i + k, j + l);
                            int cVal = 0;
                            switch (comp)
                            {
                                case ColorComponent.Red:
                                    cVal = c.R;
                                    break;
                                case ColorComponent.Green:
                                    cVal = c.G;
                                    break;
                                case ColorComponent.Blue:
                                    cVal = c.B;
                                    break;
                            }
                            newColor += (int)(cVal * kernel[wKer - 1 - k, hKer - 1 - l]);
                        }
                    }
                    Color oc = bitmap.GetPixel(i, j);
                    if (newColor >= 0 && newColor <= 255)
                        newBitmap.SetPixel(i, j, Color.FromArgb(oc.A, newColor, newColor, newColor));
                    else
                        newBitmap.SetPixel(i, j, oc);
                }
            }
            return newBitmap;
        }

        public static double[,] ConvolveMatrix(Bitmap bitmap, double[,] kernel, ColorComponent comp)      //convolve the image on one of the 4 color components with the specified kernel of any size, using zero-padding
        {
            if (bitmap == null || kernel == null)
                return null;
            int wKer = kernel.GetLength(0), hKer = kernel.GetLength(1);
            Bitmap paddedBitmap = new Bitmap(bitmap.Width + wKer - 1, bitmap.Height + hKer - 1);
            double[,] matr = new double[bitmap.Width, bitmap.Height];
            for (int i = 0; i < bitmap.Width; i++)
            {
                for (int j = 0; j < bitmap.Height; j++)
                {
                    paddedBitmap.SetPixel(i + wKer / 2, j + hKer / 2, bitmap.GetPixel(i, j));
                }
            }
            for (int i = 0; i < bitmap.Width; i++)
            {
                for (int j = 0; j < bitmap.Height; j++)
                {
                    double newColor = 0;
                    for (int k = 0; k < wKer; k++)
                    {
                        for (int l = 0; l < hKer; l++)
                        {
                            Color c = paddedBitmap.GetPixel(i + k, j + l);
                            int cVal = 0;
                            switch (comp)
                            {
                                case ColorComponent.Red:
                                    cVal = c.R;
                                    break;
                                case ColorComponent.Green:
                                    cVal = c.G;
                                    break;
                                case ColorComponent.Blue:
                                    cVal = c.B;
                                    break;
                            }
                            newColor += cVal * kernel[wKer - 1 - k, hKer - 1 - l];
                        }
                    }
                    matr[i, j] = newColor;
                }
            }
            return matr;
        }

        public static Bitmap ApplySobelFilter(Bitmap bitmap, double thr, ColorComponent comp)
        {
            if (bitmap == null)
                return null;
            double[,] sobelKerX = new double[3, 3] { { -1, 0, 1 }, { -2, 0, 2 }, { -1, 0, 1 } };
            double[,] sobelKerY = new double[3, 3] { { -1, -2, -1 }, { 0, 0, 0 }, { 1, 2, 1 } };
            double[,] filteredX = ConvolveMatrix(bitmap, sobelKerX, comp);
            double[,] filteredY = ConvolveMatrix(bitmap, sobelKerY, comp);
            Bitmap newBitmap = new Bitmap(bitmap.Width, bitmap.Height);
            for (int i = 0; i < bitmap.Width; i++)
            {
                for (int j = 0; j < bitmap.Height; j++)
                {
                    double grad = Math.Sqrt(Math.Pow(filteredX[i, j], 2) + Math.Pow(filteredY[i, j], 2));
                    if (grad >= thr)
                    {
                        newBitmap.SetPixel(i, j, Color.White);
                    }
                    else
                    {
                        newBitmap.SetPixel(i, j, Color.Black);
                    }
                }
            }
            return newBitmap;
        }

        public Bitmap FindEdges(double thr, ColorComponent comp)
        {
            if (ObjImage == null)
                return null;
            Bitmap bitmap = new Bitmap(ObjImage.Width, ObjImage.Height);
            Bitmap filtered = ApplySobelFilter(ObjImage, thr, comp);
            for (int i = 0; i < ObjImage.Width; i++)
            {
                for (int j = 0; j < ObjImage.Height; j++)
                {
                    Color c = ObjImage.GetPixel(i, j);
                    Color c2 = filtered.GetPixel(i, j);
                    // teste si la couleur du pixel se trouve dans la plage de couleurs de l'objet et si ce pixel représente un contour de l'objet
                    if (c.R >= MinColor.R && c.R <= MaxColor.R && c.G >= MinColor.G && c.G <= MaxColor.G && c.B >= MinColor.B && c.B <= MaxColor.B && c.A >= MinColor.A && c.A <= MaxColor.A && c2.R == 255 && c2.G == 255 && c2.B == 255)
                    {
                        bitmap.SetPixel(i, j, Color.White);
                    }
                    else
                    {
                        bitmap.SetPixel(i, j, Color.Black);
                    }
                }
            }
            return bitmap;
        }

        public Size IdentifyObject(int nPixels, double thr, ColorComponent comp)
        {
            Bitmap bitmap = FindEdges(thr, comp);
            if (bitmap == null)
                return Size.Empty;

            int edgeXMin = ObjImage.Width - 1, edgeXMax = 0, edgeYMin = ObjImage.Height - 1, edgeYMax = 0;
            for (int i = 0; i < ObjImage.Width; i++)
            {
                int countPixels = 0;
                for (int j = 0; j < ObjImage.Height; j++)
                {
                    Color c = bitmap.GetPixel(i, j);
                    if (c.R == 255 && c.G == 255 && c.B == 255)
                    {
                        countPixels++;
                        if (countPixels >= nPixels)
                        {
                            if (i < edgeXMin)
                                edgeXMin = i;
                            edgeXMax = i;
                            if (j - countPixels + 1 < edgeYMin)
                                edgeYMin = j - countPixels + 1;
                            if (j > edgeYMax)
                                edgeYMax = j;
                        }
                    }
                    else
                    {
                        countPixels = 0;
                    }
                }
            }
            int w = edgeXMax - edgeXMin + 1, h = edgeYMax - edgeYMin + 1;
            if (w > 0 && h > 0)
            {
                return new Size(w, h);
            }
            else
                return Size.Empty;
        }

        public static (double, double)? RotateObjX(double w, double h, double angle)      //obtient la taille réelle d'un objet de taille (w, h) en rotation selon l'axe x sur l'image (qui fait face à l'axe z)
        {
            if (Math.Cos(angle) == 0d)
                return null;
            return (w, h / Math.Cos(angle));
        }

        public static (double, double)? RotateObjY(double w, double h, double angle)      //obtient la taille réelle d'un objet de taille (w, h) en rotation selon l'axe y sur l'image (qui fait face à l'axe z)
        {
            if (Math.Cos(angle) == 0d)
                return null;
            return (w / Math.Cos(angle), h);
        }

        public static (double, double)? RotateObjZ(double width, double height, double angle)      //obtient la taille réelle d'un objet de taille (width, height) en rotation selon l'axe z sur l'image (qui fait face à l'axe z)
        {
            angle = Math.Abs(angle);
            double det = Math.Cos(angle) * Math.Cos(angle) - Math.Sin(angle) * Math.Sin(angle);
            if (det == 0d)
                return null;
            double w = 1 / det * (width * Math.Cos(angle) - height * Math.Sin(angle));
            double h = 1 / det * (-width * Math.Sin(angle) + height * Math.Cos(angle));
            return (w, h);
        }

        public static (double, double)? RotateObj(double w, double h, double[] angles, RotateObjFunc[] funcs)
        {
            if (angles.Length != 3 && funcs.Length != 3)
                return null;
            (double x, double y) vect = (w, h);
            for (int i = 0; i < 3; i++)
            {
               (double, double)? tmpVect = funcs[i](vect.x, vect.y, angles[i]);
                if (tmpVect.HasValue)
                    vect = tmpVect.Value;
                else
                    return null;
            }
            return vect;
        }

        public static (double, double)? RotateObjXYZ(double w, double h, Quaternion quat)
        {
            return RotateObj(w, h, new double[] { quat.X, quat.Y, quat.Z }, new RotateObjFunc[] { RotateObjX, RotateObjY, RotateObjZ });
        }

        public static (double, double)? RotateObjXZY(double w, double h, Quaternion quat)
        {
            return RotateObj(w, h, new double[] { quat.X, quat.Z, quat.Y }, new RotateObjFunc[] { RotateObjX, RotateObjZ, RotateObjY });
        }

        public static (double, double)? RotateObjYXZ(double w, double h, Quaternion quat)
        {
            return RotateObj(w, h, new double[] { quat.Y, quat.X, quat.Z }, new RotateObjFunc[] { RotateObjY, RotateObjX, RotateObjZ });
        }

        public static (double, double)? RotateObjYZX(double w, double h, Quaternion quat)
        {
            return RotateObj(w, h, new double[] { quat.Y, quat.Z, quat.X }, new RotateObjFunc[] { RotateObjY, RotateObjZ, RotateObjX });
        }

        public static (double, double)? RotateObjZYX(double w, double h, Quaternion quat)
        {
            return RotateObj(w, h, new double[] { quat.Z, quat.Y, quat.X }, new RotateObjFunc[] { RotateObjZ, RotateObjY, RotateObjX });
        }

        public static (double, double)? RotateObjZXY(double w, double h, Quaternion quat)
        {
            return RotateObj(w, h, new double[] { quat.Z, quat.X, quat.Y }, new RotateObjFunc[] { RotateObjZ, RotateObjX, RotateObjY});
        }

        public static bool CheckObjRot(RotateObjFunc func, ObjectPicture xyPic, ObjectPicture xzPic, ObjectPicture yzPic, Quaternion quat, float tol)
            //vérifie la taille (longueur, hauteur, largeur) d'un objet en rotation dans l'espace à partir de 3 objets ObjectPicture, 
            //chacun représentant une photo de l'objet sur l'une de ses 3 faces du plan (xyz) (origine du plan au barycentre de l'objet)
        {
            if (xyPic == null || xzPic == null || yzPic == null || quat == null)
                return false;
            (double, double)? xyVect = func(xyPic.ObjWidth, xyPic.ObjHeight, quat.Z), xzVect = func(xzPic.ObjWidth, xzPic.ObjHeight, quat.Y), zyVect = func(yzPic.ObjWidth, yzPic.ObjHeight, quat.X);
            return CheckRotation(xyVect, xzVect, zyVect, tol);
        }

        private static bool CheckRotation((double x, double y)? xyVect, (double x, double y)? xzVect, (double x, double y)? zyVect, float tol)
        {
            bool sizeEqual = true;
            if (xyVect != null && xzVect != null)
            {
                double w = xyVect.Value.x, h = xzVect.Value.y;
                if (w < h - tol || w > h + tol)
                    sizeEqual = false;
            }
            if (xzVect != null && zyVect != null)
            {
                double w = xzVect.Value.x, h = zyVect.Value.y;
                if (w < h - tol || w > h + tol)
                    sizeEqual = false;
            }
            if (zyVect != null && xyVect != null)
            {
                double w = zyVect.Value.x, h = xyVect.Value.y;
                if (w < h - tol || w > h + tol)
                    sizeEqual = false;
            }
            return sizeEqual;
        }

    }
}
