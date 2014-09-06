/* Copyright (C) 2013-2014 Pavlidis Orestis
 * Unauthorized copying of this file, via any medium is strictly prohibited
*/

using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using SV_ANN_Sample.Vision.Structure;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SV_ANN_Sample.Vision.ImageProcessing {
    public static class ImageExtensions {

        /// <summary>
        /// Returns the maximum number of edges an image of the given size can possibly have
        /// </summary>
        /// <param name="Size">The size of the image</param>
        /// <returns>The limit</returns>
        /// <remarks>Algorithm was designed based on the edge point's definition found in <see cref="SV_ANN_Sample.Vision.Registers.Signatures.Signature.GetEdgePoints"/> </remarks>
        public static int GetMaxEdges(this Size Size) {
            int w = Size.Width;
            int h = Size.Height;
            if (w < 0 || h < 0) { return (0); }

            int a = (int)Math.Floor((double)(w - 1) / 3) + (int)Math.Floor((double)((w + 1) - (int)Math.Floor((double)w / 3) * 3) / 3);
            int b = (int)Math.Ceiling((double)h / 2) - ((h + 1) % 2);

            int c = (w + 1) - (a * 3);
            int d = (int)Math.Ceiling((double)c / 2);

            int e = (int)Math.Floor((double)(h + 1) / 3);

            int f = (int)Math.Floor((double)(h - e * 3 + 1) / 2) * (int)Math.Floor((double)((w + 1) - a * 3) / 3);
            int g = (int)Math.Floor((double)(h - b * 2 + 1) / 2) * (int)Math.Ceiling((double)(w - d * 2) / 2);

            return (a * b + d * e + f + g);
        }

        /// <summary>
        /// Returns the deskew (minus skew) angle in degrees of the image by averaging the angle of all horizontal lines.
        /// </summary>
        /// <typeparam name="T">The color type of the image</typeparam>
        /// <param name="Image">The image</param>
        /// <param name="MinLineWidth">A width threshold to be used when extracting lines</param>
        /// <returns>The deskew angle in degrees</returns>
        public static double GetDeskewAngle<T>(this Image<T, Byte> Image, double MinLineWidth = -1) where T : struct, IColor {
            if (MinLineWidth == -1) {
                //Automatically calculate line width (ROI width / 3)
                MinLineWidth = Image.Copy(Image.GetROI()).Width / 3;
            };

            using (Image<T, Byte> deskewed = new Image<T, byte>(Image.Size)) {
                //Otsu thresholding
                CvInvoke.cvThreshold(Image, deskewed, 0, 255, THRESH.CV_THRESH_BINARY | THRESH.CV_THRESH_OTSU);

                //Apply horizontal structuring element (Makes horizontal lines stand out)
                StructuringElementEx structure = new StructuringElementEx(10, 1, 5, 0, CV_ELEMENT_SHAPE.CV_SHAPE_RECT);
                CvInvoke.cvMorphologyEx(deskewed, deskewed, IntPtr.Zero, structure, CV_MORPH_OP.CV_MOP_OPEN, 1);

                //Apply sobel edge detector
                Image<T, Byte> sobelLines = deskewed.Sobel(0, 2, 3).ConvertScale<Byte>(1, 0);

                //Remove contours with a width-to-height ratio of less than 17 or with a small area
                Contour<Point> contours = sobelLines.FindContours(CHAIN_APPROX_METHOD.CV_CHAIN_APPROX_SIMPLE, RETR_TYPE.CV_RETR_EXTERNAL);
                MCvScalar black = new MCvScalar(0, 0, 0);
                int areaThreshold = (int)(0.0025 * (Image.Width * Image.Height));
                for (; contours != null; contours = contours.HNext) {
                    Rectangle area = contours.BoundingRectangle;
                    if ((float)area.Width / area.Height < 14 || area.Height * area.Width < areaThreshold) {
                        CvInvoke.cvDrawContours(sobelLines, contours, black, black, 0, -1, LINE_TYPE.FOUR_CONNECTED, Point.Empty);
                    }
                }

                //Dilate lines
                sobelLines = sobelLines.Dilate(1);

                //Extract information about lines using hough line transform
                LineSegment2D[] lines = sobelLines.HoughLinesBinary(1, Math.PI / 180, 150, MinLineWidth, 30)[0]; //there's only 1 colour channel so we only need the first index
                if (lines.Length == 0) {
                    return (0); //no lines found
                }

                double angle = 0;
                foreach (LineSegment2D line in lines) {
                    //Sum angle of lines and convert them into degrees
                    angle += Math.Atan2(line.P2.Y - line.P1.Y, line.P2.X - line.P1.X) * 180 / Math.PI;
                }
                angle /= lines.Length; //Average angle

                return (-angle); //Reverse angle (Deskew)
            }
        }

        /// <summary>
        /// Normalizes the image's size based on a given width
        /// </summary>
        /// <typeparam name="T">The color type of the image</typeparam>
        /// <param name="Image">The image</param>
        /// <param name="Width">The width to use for normalization</param>
        /// <returns>A new image</returns>
        public static Image<T, Byte> NormalizeSize<T>(this Image<T, Byte> Image, int Width) where T : struct, IColor {
            return (Image.Resize(Width, (int)(Image.Height * ((float)Width / Image.Width)), INTER.CV_INTER_AREA));
        }

        /// <summary>
        /// Adjusts the brightness of the image
        /// </summary>
        /// <typeparam name="T">The color type of the image</typeparam>
        /// <param name="Image">The image</param>
        /// <returns>A new image</returns>
        public static Image<T, Byte> AdjustBrightness<T>(this Image<T, Byte> Image) where T : struct, IColor {
            //Adjust brightness of image by dividing each pixel with the result of a closing operation
            StructuringElementEx kernel = new StructuringElementEx(11, 11, 5, 5, CV_ELEMENT_SHAPE.CV_SHAPE_ELLIPSE);
            Image<T, Byte> close = Image.MorphologyEx(kernel, CV_MORPH_OP.CV_MOP_CLOSE, 1);
            Image<T, Byte> bright = new Image<T, Byte>(close.Size);
            CvInvoke.cvDiv(Image, close, bright, 0xFF);

            return (bright);
        }

        /// <summary>
        /// Returns the largest contour in the given grayscale image
        /// </summary>
        /// <param name="Image">The image</param>
        /// <param name="MinArea">Minimum area of contours</param>
        /// <returns>The largest contour in image that is larger than MinArea</returns>
        public static Contour<Point> GetLargestContour(this Image<Gray, Byte> Image, double MinArea = 1000) {
            //Inverse threshold
            Image<Gray, Byte> threshold = Image.ThresholdAdaptive(new Gray(255), ADAPTIVE_THRESHOLD_TYPE.CV_ADAPTIVE_THRESH_MEAN_C, THRESH.CV_THRESH_BINARY_INV, 19, new Gray(2));

            Contour<Point> contours = Image.FindContours(CHAIN_APPROX_METHOD.CV_CHAIN_APPROX_SIMPLE, RETR_TYPE.CV_RETR_TREE);
            
            //Loop through contours and find largest
            double maxArea = 0.0d;
            Contour<Point> largestContour = null;
            for (; contours != null; contours = contours.HNext) {
                if (contours.Area > MinArea && contours.Area > maxArea) {
                    maxArea = contours.Area;
                    largestContour = contours;
                }
            }

            return (largestContour);
        }


        /// <summary>
        /// Returns region of interest by removing black borders around image
        /// </summary>
        /// <typeparam name="T">The color type of the image</typeparam>
        /// <param name="Image">The image</param>
        /// <param name="Threshold">The border's colour threshold</param>
        /// <returns>A rectangle representing the region of interest</returns>
        public static Rectangle GetROI<T>(this Image<T, Byte> Image, int Threshold = 220) where T : struct, IColor {
            Image<Gray, Byte> image = Image.Convert<Gray, Byte>();
            Byte[, ,] grayData = image.Data;

            //Get top
            int top = 0;
            for (int y = 0; y < image.Rows; y++) {
                bool hasWhite = false;
                for (int x = 0; x < image.Cols; x++) {
                    if (grayData[y, x, 0] > Threshold) {
                        hasWhite = true;
                        break;
                    }
                }
                if (!hasWhite) {
                    top++;
                } else {
                    break;
                }
            }

            //Get bottom
            int bottom = image.Rows;
            for (int y = image.Rows - 1; y > top; y--) {
                bool hasWhite = false;
                for (int x = 0; x < image.Cols; x++) {
                    if (grayData[y, x, 0] > Threshold) {
                        hasWhite = true;
                        break;
                    }
                }
                if (!hasWhite) {
                    bottom--;
                } else {
                    break;
                }
            }

            //Get left
            int left = 0;
            for (int x = 0; x < image.Cols; x++) {
                bool hasWhite = false;
                for (int y = 0; y < image.Rows; y++) {
                    if (grayData[y, x, 0] > Threshold) {
                        hasWhite = true;
                        break;
                    }
                }
                if (!hasWhite) {
                    left++;
                } else {
                    break;
                }
            }


            //Get Right
            int right = image.Cols;
            for (int x = image.Cols - 1; x > left; x--) {
                bool hasWhite = false;
                for (int y = 0; y < image.Rows; y++) {
                    if (grayData[y, x, 0] > Threshold) {
                        hasWhite = true;
                        break;
                    }
                }
                if (!hasWhite) {
                    right--;
                } else {
                    break;
                }
            }

            return (new Rectangle(left, top, right - left, bottom - top));
        }


        /// <summary>
        /// Draws a set of points as circles on the image
        /// </summary>
        /// <typeparam name="T">The color type of the image</typeparam>
        /// <param name="Image">The image</param>
        /// <param name="Radius">The radius of the points</param>
        /// <param name="Color">The color of the points</param>
        /// <param name="Thickness">The thickness of the points</param>
        /// <param name="Points">The points</param>
        public static void DrawPoints<T>(this Image<T, Byte> Image, float Radius, T Color, int Thickness, IEnumerable<Point> Points) where T : struct, IColor {
            foreach (Point point in Points) {
                Image.Draw(new CircleF(new PointF(point.X, point.Y), Radius), Color, Thickness);
            }
        }

        /// <summary>
        /// Counts the number of pixels in the image that have the given color
        /// </summary>
        /// <param name="Image">The image</param>
        /// <param name="Color">The color</param>
        /// <returns>The number of pixels</returns>
        public static int CountPixels(this Image<Gray, Byte> Image, byte Color) {
            byte[, ,] data = Image.Data;

            int count = 0;
            for (int y = 0; y < Image.Rows; y++) {
                for (int x = 0; x < Image.Cols; x++) {
                    if (data[y, x, 0] == Color) {
                        count++;
                    }
                }
            }

            return (count);
        }

        /// <summary>
        /// Extracts the horizontal lines from the grayscale image
        /// </summary>
        /// <param name="Image">The image</param>
        /// <returns>A new grayscale image containing the horizontal lines</returns>
        public static Image<Gray, Byte> ExtractHorizontalLines(this Image<Gray, Byte> Image) {
            Image<Gray, Byte> hLines = new Image<Gray, byte>(Image.Size);

            //Morphological opening using an horizontal shape
            StructuringElementEx structure = new StructuringElementEx(10, 1, 5, 0, CV_ELEMENT_SHAPE.CV_SHAPE_RECT);
            CvInvoke.cvMorphologyEx(Image, hLines, IntPtr.Zero, structure, CV_MORPH_OP.CV_MOP_OPEN, 1);

            //Apply otsu's thresholding
            CvInvoke.cvThreshold(hLines, hLines, 0, 255, THRESH.CV_THRESH_OTSU | THRESH.CV_THRESH_BINARY);

            //Sobel edge detector (0x,1y)
            hLines = hLines.Sobel(0, 1, 3).ConvertScale<Byte>(1, 0);



            //Remove contours with a width-to-height ratio of less than 14 or with a small area
            Contour<Point> contours = hLines.FindContours(CHAIN_APPROX_METHOD.CV_CHAIN_APPROX_SIMPLE, RETR_TYPE.CV_RETR_EXTERNAL);
            MCvScalar black = new MCvScalar(0, 0, 0);
            int areaThreshold = (int)(0.00250 * (Image.Width * Image.Height));
            for (; contours != null; contours = contours.HNext) {
                Rectangle area = contours.BoundingRectangle;
                if ((float)area.Width / area.Height < 14 || area.Height * area.Width < areaThreshold) {
                    CvInvoke.cvDrawContours(hLines, contours, black, black, 0, -1, LINE_TYPE.FOUR_CONNECTED, Point.Empty);
                }
            }


            //Remove any remaining small noise by applying a morphological opening followed by a closing operation
            structure = new StructuringElementEx(2, 2, 1, 1, CV_ELEMENT_SHAPE.CV_SHAPE_RECT);
            CvInvoke.cvMorphologyEx(hLines, hLines, IntPtr.Zero, structure, CV_MORPH_OP.CV_MOP_OPEN, 1);
            CvInvoke.cvMorphologyEx(hLines, hLines, IntPtr.Zero, structure, CV_MORPH_OP.CV_MOP_CLOSE, 1);

            //Extend the lines horizontally
            structure = new StructuringElementEx(50, 1, 25, 0, CV_ELEMENT_SHAPE.CV_SHAPE_RECT);
            CvInvoke.cvDilate(hLines, hLines, structure, 1);


            return (hLines);
        }

        
        /// <summary>
        /// Extracts the vertical lines from the grayscale image
        /// </summary>
        /// <param name="Image">The image</param>
        /// <returns>A new grayscale image containing the vertical lines</returns>
        public static Image<Gray, Byte> ExtractVerticalLines(this Image<Gray, Byte> Image) {
            Image<Gray, Byte> vLines = new Image<Gray, byte>(Image.Size);

            //Morphological opening using a vertical shape
            StructuringElementEx structure = new StructuringElementEx(1, 10, 0, 5, CV_ELEMENT_SHAPE.CV_SHAPE_RECT);
            CvInvoke.cvMorphologyEx(Image, vLines, IntPtr.Zero, structure, CV_MORPH_OP.CV_MOP_OPEN, 1);

            //Sobel edge detection (2x,0y)
            vLines = vLines.Sobel(2, 0, 3).ConvertScale<Byte>(1, 0);


            //Remove contours with a small height-to-width ratio or with a small area
            Contour<Point> contours = vLines.FindContours(CHAIN_APPROX_METHOD.CV_CHAIN_APPROX_SIMPLE, RETR_TYPE.CV_RETR_EXTERNAL);
            MCvScalar black = new MCvScalar(0, 0, 0);
            int areaThreshold = (int)(0.00200 * (Image.Width * Image.Height)); //0.00025
            for (; contours != null; contours = contours.HNext) {
                Rectangle area = contours.BoundingRectangle;

                if ((float)area.Height / area.Width < 5 || area.Width * area.Height < areaThreshold) {
                    CvInvoke.cvDrawContours(vLines, contours, black, black, 0, -1, LINE_TYPE.FOUR_CONNECTED, Point.Empty);
                }
            }


            //Remove any remaining small noise
            structure = new StructuringElementEx(3, 3, 1, 1, CV_ELEMENT_SHAPE.CV_SHAPE_ELLIPSE);
            CvInvoke.cvMorphologyEx(vLines, vLines, IntPtr.Zero, structure, CV_MORPH_OP.CV_MOP_OPEN, 1);

            //Close any small holes
            CvInvoke.cvMorphologyEx(vLines, vLines, IntPtr.Zero, structure, CV_MORPH_OP.CV_MOP_CLOSE, 1);


            //Extend vertical lines
            structure = new StructuringElementEx(1, 50, 0, 25, CV_ELEMENT_SHAPE.CV_SHAPE_RECT);
            CvInvoke.cvDilate(vLines, vLines, structure, 1);

            return (vLines);
        }


        /// <summary>
        /// Draws a quadrilateral on the image
        /// </summary>
        /// <typeparam name="T">The color type of the image</typeparam>
        /// <param name="Image">The image</param>
        /// <param name="Quadrilateral">The quadrilateral</param>
        /// <param name="Color">The color of the quadrilateral</param>
        public static void Draw<T>(this Image<T, Byte> Image, Quadrilateral Quadrilateral, MCvScalar Color) where T : struct, IColor {
            CvInvoke.cvFillConvexPoly(Image, Quadrilateral.Points, 4, Color, LINE_TYPE.FOUR_CONNECTED, 0);
        }


        /// <summary>
        /// Removes clutter from a grayscale image based on an area threshold
        /// </summary>
        /// <param name="Image">The image</param>
        /// <param name="Threshold">The area threshold</param>
        public static void RemoveClutter(this Image<Gray, Byte> Image, double Threshold) {
            using (Image<Gray, Byte> processedImage = Image.Copy().Dilate(3)) {
                //Dilate image here to make sure we are not removing any dots from 'i' characters

                //Find and filter contours based on threshold
                Contour<Point> contours = processedImage.FindContours(CHAIN_APPROX_METHOD.CV_CHAIN_APPROX_SIMPLE, RETR_TYPE.CV_RETR_EXTERNAL);
                for (; contours != null; contours = contours.HNext) {
                    if (contours.Area < Threshold) {
                        CvInvoke.cvDrawContours(Image, contours, MCvColor.Black, MCvColor.Black, 0, -1, LINE_TYPE.FOUR_CONNECTED, Point.Empty);
                    }
                }
            }
        }

    }
}
