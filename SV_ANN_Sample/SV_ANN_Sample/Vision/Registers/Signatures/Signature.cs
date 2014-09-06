using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using SV_ANN_Sample.Vision.ImageProcessing.ImageFilters;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SV_ANN_Sample.Vision.ImageProcessing;

namespace SV_ANN_Sample.Vision.Registers.Signatures {
    /// <summary>
    /// Represents a signature
    /// </summary>
    public class Signature : IDisposable {
        public Signature() { }

        /// <summary>
        /// Extracts the features of this signature
        /// </summary>
        /// <param name="IncludeGeometricCenters">True in order to extract geometric centers, false otherwise</param>
        public void ExtractFeatures(bool IncludeGeometricCenters = true) {
            //Preprocess image
            ProcessedImage = Preprocess(Image);

            //Extract Features
            Features = new FeatureSet();
            byte[, ,] data = ProcessedImage.Data;


            //Signature occupancy ratio
            int signatureOccupancy = GetSignatureOccupancy(data);
            Features.OccupancyRatio = (float)signatureOccupancy / (ProcessedImage.Size.Height * ProcessedImage.Size.Width);

            //Aspect Ratio
            Features.AspectRatio = (float)ProcessedImage.Size.Width / ProcessedImage.Size.Height;

            //Histograms
            Features.MaxHorizontalHistogram = GetMaxHorizontalHistogram(data);
            Features.MaxVerticalHistogram = GetMaxVerticalHistogram(data);

            //Geometric centers
            if (IncludeGeometricCenters) {
                Features.VerticalGeometricCenters = GetVerticalGeometricCenters(data);
                Features.HorizontalGeometricCenters = GetHorizontalGeometricCenters(data);
            } else {
                Features.VerticalGeometricCenters = new Point[0];
                Features.HorizontalGeometricCenters = new Point[0];
            }

            //Edges
            Features.EdgePoints = GetEdgePoints(data);

            //Cross-Points
            Features.CrossPoints = GetCrossPoints(data);

            Features.ClosedLoops = GetClosedLoops(Features.CrossPoints, Features.EdgePoints.Length, data);

            Features.Normalize(ProcessedImage.Size);
#if DEBUG
            Debug = ProcessedImage.Convert<Bgr, Byte>();
            Debug.DrawPoints(1, new Bgr(Color.Blue), -1, Features.VerticalGeometricCenters);
            Debug.DrawPoints(1, new Bgr(Color.Red), -1, Features.HorizontalGeometricCenters);
            Debug.DrawPoints(1, new Bgr(Color.Red), -1, Features.CrossPoints);
            Debug.DrawPoints(1, new Bgr(Color.Gray), -1, Features.CrossPoints);
#endif
        }

        /// <summary>
        /// Returns the index (row) of the maximum horizontal histogram from the given image
        /// </summary>
        /// <param name="Data">The image data</param>
        /// <returns>The index (row) of the maximum horizontal histogram</returns>
        protected int GetMaxHorizontalHistogram(byte[, ,] Data) {
            int[] horizontalHistogram = new int[ProcessedImage.Rows]; //stores amount of white pixels of each row
            int maxRowHistogram = -1;

            for (int y = 0; y < ProcessedImage.Rows; y++) {

                horizontalHistogram[y] = 0;
                for (int x = 0; x < ProcessedImage.Cols; x++) {
                    if (Data[y, x, 0] == 0xFF) { //white
                        horizontalHistogram[y]++;
                    }
                }
                if (maxRowHistogram == -1 || horizontalHistogram[y] > horizontalHistogram[maxRowHistogram]) {
                    maxRowHistogram = y;
                }
            }
            return (maxRowHistogram);
        }

        /// <summary>
        /// Returns the index (column) of the maximum vertical histogram from the given image
        /// </summary>
        /// <param name="Data">The image data</param>
        /// <returns></returns>
        protected int GetMaxVerticalHistogram(byte[, ,] Data) {
            int[] verticalHistogram = new int[ProcessedImage.Cols]; //stores amount of white pixels of each column
            int maxColHistogram = -1;

            for (int x = 0; x < ProcessedImage.Cols; x++) {

                verticalHistogram[x] = 0;
                for (int y = 0; y < ProcessedImage.Rows; y++) {
                    if (Data[y, x, 0] == 0xFF) { //white
                        verticalHistogram[x]++;
                    }
                }
                if (maxColHistogram == -1 || verticalHistogram[x] > verticalHistogram[maxColHistogram]) {
                    maxColHistogram = x;
                }
            }
            return (maxColHistogram);
        }

        /// <summary>
        /// Returns the number of white pixels in the given image
        /// </summary>
        /// <param name="data">The image data</param>
        /// <returns>The number of white pixels in the image data</returns>
        protected int GetSignatureOccupancy(byte[, ,] data) {
            int signatureOccupancy = 0;
            for (int y = 0; y < ProcessedImage.Rows; y++) {
                for (int x = 0; x < ProcessedImage.Cols; x++) {
                    if (data[y, x, 0] == 0xFF) {//white
                        signatureOccupancy++;
                    }
                }
            }
            return (signatureOccupancy);
        }

        /// <summary>
        /// Returns six vertical geometric centers of the given image
        /// </summary>
        /// <param name="Data">The image data</param>
        /// <returns>An array of points representing the vertical geometrical centers</returns>
        protected Point[] GetVerticalGeometricCenters(byte[, ,] Data) {
            Point v1, v2, v3, v4, v5, v6;

            //First vertically split the image at center
            using (Image<Gray, Byte> left = ProcessedImage.Copy(new Rectangle(0, 0, (int)Math.Ceiling((double)ProcessedImage.Width / 2), ProcessedImage.Height))) {
                using (Image<Gray, Byte> right = ProcessedImage.Copy(new Rectangle(ProcessedImage.Width / 2, 0, ProcessedImage.Width / 2, ProcessedImage.Height))) {

                    //Find gravity centers of the two parts
                    MCvPoint2D64f gravityCenter;
                    gravityCenter = left.GetMoments(true).GravityCenter;
                    v1 = new Point((int)gravityCenter.x, (int)gravityCenter.y);
                    gravityCenter = right.GetMoments(true).GravityCenter;
                    v2 = new Point((int)gravityCenter.x + left.Width, (int)gravityCenter.y);

                    //Split both parts horizontally at gravity centers
                    //Get gravity centers of left part (top and bottom)
                    using (Image<Gray, Byte> leftTop = left.Copy(new Rectangle(0, 0, left.Width, v1.Y))) {
                        gravityCenter = leftTop.GetMoments(true).GravityCenter;
                        v3 = new Point((int)gravityCenter.x, (int)gravityCenter.y);

                        using (Image<Gray, Byte> leftBottom = left.Copy(new Rectangle(0, v1.Y, left.Width, left.Height - v1.Y))) {
                            gravityCenter = leftBottom.GetMoments(true).GravityCenter;
                            v4 = new Point((int)gravityCenter.x, (int)gravityCenter.y + leftTop.Height);
                        }
                    }

                    //Get gravity centers of right part (top and bottom)
                    using (Image<Gray, Byte> rightTop = right.Copy(new Rectangle(0, 0, right.Width, v2.Y))) {
                        gravityCenter = rightTop.GetMoments(true).GravityCenter;
                        v5 = new Point((int)gravityCenter.x + left.Width, (int)gravityCenter.y);

                        using (Image<Gray, Byte> rightBottom = right.Copy(new Rectangle(0, v2.Y, right.Width, left.Height - v2.Y))) {
                            gravityCenter = rightBottom.GetMoments(true).GravityCenter;
                            v6 = new Point((int)gravityCenter.x + left.Width, (int)gravityCenter.y + rightTop.Height);
                        }
                    }
                }
            }

            return (new Point[] { v1, v2, v3, v4, v5, v6 });
        }

        /// <summary>
        /// Returns six horizontal geometric centers of the given image
        /// </summary>
        /// <param name="Data">The image data</param>
        /// <returns>An array of points representing the horizontal geometric centers</returns>
        protected Point[] GetHorizontalGeometricCenters(byte[, ,] Data) {
            Point h1, h2, h3, h4, h5, h6;

            //First horizontally split the image at center
            using (Image<Gray, Byte> top = ProcessedImage.Copy(new Rectangle(0, 0, ProcessedImage.Width, (int)Math.Ceiling((double)ProcessedImage.Height / 2)))) {
                using (Image<Gray, Byte> bottom = ProcessedImage.Copy(new Rectangle(0, ProcessedImage.Height / 2, ProcessedImage.Width, ProcessedImage.Height / 2))) {

                    //Find gravity centers of the two parts
                    MCvPoint2D64f gravityCenter;
                    gravityCenter = top.GetMoments(true).GravityCenter;
                    h1 = new Point((int)gravityCenter.x, (int)gravityCenter.y);
                    gravityCenter = bottom.GetMoments(true).GravityCenter;
                    h2 = new Point((int)gravityCenter.x, (int)gravityCenter.y + top.Height);

                    //Split both parts vertically at gravity centers
                    //Get gravity centers of top part (left and right)
                    using (Image<Gray, Byte> topLeft = top.Copy(new Rectangle(0, 0, h1.X, top.Height))) {
                        gravityCenter = topLeft.GetMoments(true).GravityCenter;
                        h3 = new Point((int)gravityCenter.x, (int)gravityCenter.y);

                        using (Image<Gray, Byte> topRight = top.Copy(new Rectangle(h1.X, 0, top.Width - h1.X, top.Height))) {
                            gravityCenter = topRight.GetMoments(true).GravityCenter;
                            h4 = new Point((int)gravityCenter.x + topLeft.Width, (int)gravityCenter.y);
                        }
                    }

                    //Get gravity centers of bottom part (left and right)
                    using (Image<Gray, Byte> bottomLeft = bottom.Copy(new Rectangle(0, 0, h2.X, bottom.Height))) {
                        using (Image<Gray, Byte> bottomRight = bottom.Copy(new Rectangle(h2.X, 0, bottom.Width - h2.X, bottom.Height))) {
                            gravityCenter = bottomLeft.GetMoments(true).GravityCenter;
                            h5 = new Point((int)gravityCenter.x, (int)gravityCenter.y + top.Height);
                            gravityCenter = bottomRight.GetMoments(true).GravityCenter;
                            h6 = new Point((int)gravityCenter.x + bottomLeft.Width, (int)gravityCenter.y + top.Height);
                        }
                    }
                }
            }
            return (new Point[] { h1, h2, h3, h4, h5, h6 });
        }

        /// <summary>
        /// Returns the edge points of the given image.  An edge point is defined as a white pixel with only one neighbouring white pixel
        /// </summary>
        /// <param name="Data">The image data</param>
        /// <returns>An array of points representing the edges</returns>
        protected Point[] GetEdgePoints(byte[, ,] Data) {
            IList<Point> edges = new List<Point>();

            for (int y = 1; y < ProcessedImage.Rows - 1; y++) {
                for (int x = 1; x < ProcessedImage.Cols - 1; x++) {
                    if (Data[y, x, 0] == 0xFF) {//white
                        int neighbours = (Data[y - 1, x, 0] + Data[y + 1, x, 0] +
                                        Data[y, x + 1, 0] + Data[y, x - 1, 0] +
                                        Data[y - 1, x - 1, 0] + Data[y - 1, x + 1, 0] +
                                        Data[y + 1, x - 1, 0] + Data[y + 1, x + 1, 0]) / 0xFF;
                        if (neighbours == 1) {
                            edges.Add(new Point(x, y));
                        }
                    }
                }
            }

            return (edges.ToArray());
        }

        /// <summary>
        /// Returns the cross points of the given image.  A cross point is defined as a white pixel with at least three neighbouring white pixels
        /// </summary>
        /// <param name="Data">The image data</param>
        /// <returns>An array of points representing the cross points</returns>
        protected Point[] GetCrossPoints(byte[, ,] data) {
            IList<Point> crossPoints = new List<Point>();

            for (int y = 1; y < ProcessedImage.Rows - 1; y++) {
                for (int x = 1; x < ProcessedImage.Cols - 1; x++) {
                    if (data[y, x, 0] == 0xFF) {//white
                        int neighbours = (data[y - 1, x, 0] + data[y + 1, x, 0] +
                                        data[y, x + 1, 0] + data[y, x - 1, 0]) / 0xFF;
                        if (neighbours >= 3) {
                            crossPoints.Add(new Point(x, y));
                        }
                    }
                }
            }

            return (crossPoints.ToArray());
        }

        /// <summary>
        /// Returns the number of closed loops in the given image
        /// </summary>
        /// <param name="CrossPoints">The cross points of the image</param>
        /// <param name="EdgePoints">The edge points of the image</param>
        /// <param name="Data">The image data</param>
        /// <returns>The number of closed loops</returns>
        protected int GetClosedLoops(Point[] CrossPoints, int EdgePoints, byte[, ,] Data) {
            int sum = 0;
            foreach (Point point in CrossPoints) {
                int x = point.X;
                int y = point.Y;

                int neighbours = (Data[y - 1, x, 0] + Data[y + 1, x, 0] +
                Data[y, x + 1, 0] + Data[y, x - 1, 0] +
                Data[y - 1, x - 1, 0] + Data[y - 1, x + 1, 0] +
                Data[y + 1, x - 1, 0] + Data[y + 1, x + 1, 0]) / 0xFF;

                if (neighbours > 2) {
                    sum += neighbours - 2;
                }
            }



            return (Math.Max(0, 1 + ((sum - EdgePoints) / 2)));
        }

        /// <summary>
        /// Pre-processes given image.  
        /// </summary>
        /// <param name="Image">A binary image where black pixels represent the foreground</param>
        /// <returns>Processed image</returns>
        private Image<Gray, Byte> Preprocess(Image<Gray, Byte> Image) {
            Image<Gray, Byte> processed = Image.Copy();

            //Inverse binarize
            CvInvoke.cvThreshold(processed, processed, 0, 255, THRESH.CV_THRESH_OTSU | THRESH.CV_THRESH_BINARY_INV);


            //Normalize size
            int normWidth = 300;
            int normHeight = (int)((float)normWidth / processed.Width * processed.Height);
            processed.Resize(normWidth, normHeight, INTER.CV_INTER_CUBIC);

            //Crop
            Rectangle ROI = processed.GetROI();
            //Inflate ROI by 1 pixel to preserve edges
            ROI.X = Math.Max(0, ROI.X - 1);
            ROI.Y = Math.Max(0, ROI.Y - 1);
            ROI.Width = Math.Min(processed.Width, ROI.Width + 1);
            ROI.Height = Math.Min(processed.Height, ROI.Height + 1);
            processed = processed.Copy(ROI);

            //Thinning
            processed = processed.Not(); //required by thinning filter
            ZSThinningFilter thin = new ZSThinningFilter();
            processed = new Image<Gray, byte>(thin.Apply(processed.ToBitmap(), PixelFormat.Format8bppIndexed));
            processed = processed.Not();

            return (processed);
        }


        /// <summary>
        /// Gets or sets the signature image where black pixels represent the foreground of the signature
        /// </summary>
        public Image<Gray, Byte> Image { get; set; }

        /// <summary>
        /// Gets the processed signature image
        /// </summary>
        public Image<Gray, Byte> ProcessedImage { get; protected set; }

        /// <summary>
        /// Gets or sets the signature's feature set
        /// </summary>
        public FeatureSet Features { get; set; }

        /// <summary>
        /// An image used for debugging
        /// </summary>
        private Image<Bgr, byte> Debug { get; set; }

        /// <summary>
        /// Releases all resources used by this Vision.Registers.Signatures.Signature
        /// </summary>
        public void Dispose() {
            if (Image != null) { Image.Dispose(); }
            if (ProcessedImage != null) { ProcessedImage.Dispose(); }
#if DEBUG
            if (Debug != null) { Debug.Dispose(); }
#endif
        }
    }
}
