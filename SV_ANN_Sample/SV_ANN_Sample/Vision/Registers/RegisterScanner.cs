using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SV_ANN_Sample.Vision.ImageProcessing;
using SV_ANN_Sample.Vision.Structure;
using SV_ANN_Sample.Vision.OCR;
using SV_ANN_Sample.Vision.Registers.Signatures;

namespace SV_ANN_Sample.Vision.Registers {
    /// <summary>
    /// A class used to interpret images as registers
    /// </summary>
    public static class RegisterScanner {


        /// <summary>
        /// Returns a register from a given image
        /// </summary>
        /// <param name="Image">The image</param>
        /// <param name="Columns">The number of columns in the register</param>
        /// <param name="NormalizedWidth">If not null, register's size will be normalized based on this pre-determined width</param>
        /// <returns>The register</returns>
        /// <exception cref="InvalidOperationException">Thrown when image cannot be interpreted as a register</exception>
        public static Register FromImage(Image<Bgr, Byte> Image, int Columns = 4, int? NormalizedWidth = null) {
            Register register = new Register(Columns);

            //Convert to grayscale
            Image<Gray, Byte> registerImage;
            using (Image) {
                registerImage = new Image<Gray, Byte>(Image.ToBitmap());
            }

            if (NormalizedWidth.HasValue) {
                //Have to normalize size of image
                registerImage = registerImage.NormalizeSize(NormalizedWidth.Value);
            }


            //Adjust Brightness
            registerImage = registerImage.AdjustBrightness();

            //Apply otsu's thresholding (Convert to binary)
            CvInvoke.cvThreshold(registerImage, registerImage, 0, 255, THRESH.CV_THRESH_OTSU | THRESH.CV_THRESH_BINARY_INV);

            //Find largest contour in binary image
            Contour<Point> largestContour = registerImage.GetLargestContour();

            //Construct a mask from the largest contour
            Image<Gray, Byte> mask = new Image<Gray, byte>(registerImage.Width, registerImage.Height);
            CvInvoke.cvDrawContours(mask, largestContour, MCvColor.White, MCvColor.White, 0, -1, LINE_TYPE.EIGHT_CONNECTED, Point.Empty);

            //Bitwise logical AND operation between binary image and the mask (To keep the largest area, which has to be the register)
            registerImage = registerImage.And(mask);

            //Deskew register image
            double angle = registerImage.GetDeskewAngle();
            registerImage = registerImage.Rotate(angle, new Gray(0));

            //Cut region of interest after rotation
            registerImage = registerImage.Copy(registerImage.GetROI());
            register.Image = registerImage;

            //Extract vertical and horizontal lines from the register
            Image<Gray, Byte> vLines = registerImage.ExtractVerticalLines();
            Image<Gray, Byte> hLines = registerImage.ExtractHorizontalLines();

            //Logical AND operation between these lines to find the intersections
            Image<Gray, Byte> intersections = vLines.And(hLines).Dilate(1);


            //Convert the gravity centers of these intersection points into digital points
            Contour<Point> contours = intersections.FindContours(CHAIN_APPROX_METHOD.CV_CHAIN_APPROX_SIMPLE, RETR_TYPE.CV_RETR_LIST);
            List<Point> contourPoints = new List<Point>();
            for (int i = 0; contours != null; i++, contours = contours.HNext) {
                MCvMoments moments = contours.GetMoments();
                MCvPoint2D64f center = moments.GravityCenter;

                if (double.IsNaN(center.x) || double.IsNaN(center.y)) { continue; }

                Point point = new Point((int)center.x, (int)center.y);
                contourPoints.Add(point);
            }



            //Sort the points into columns
            int thresholdY = registerImage.Height / 13; //initial vertical tolerance
            int thresholdX = registerImage.Width / 93; //initial horizontal tolerance
            int noiseThreshold = 8; //initial noise tolerance
            Tuple<IList<IList<Point>>, int> result; //list of columns and number of points that could not be inserted
            IList<IList<Point>> columnPoints; //result.Item1
            int attempts = 0;
            do {
                result = SortPointsIntoColumns(Columns, contourPoints, thresholdX, thresholdY, noiseThreshold);
                columnPoints = result.Item1;

                //Increase tolerance:
                thresholdX = (int)(thresholdX * 1.3);
                thresholdY = (int)(thresholdY * 1.3);
                noiseThreshold = (int)(noiseThreshold * 1.3);

                attempts++;
            } while (result.Item2 > 0 && attempts < 8); //re-try until all points are inserted (max 8 times)

            if (result.Item2 > 0) {
                throw (new InvalidOperationException("Unable to sort points into columns: Extra intersections were found"));
            }

            //Validate columns
            try {
                IList<int> emptyColumnIndices = new List<int>();
                for (int i = 0; i < columnPoints.Count; i++) {
                    if (columnPoints[i].Count == 0) {
                        //empty column.  Maybe the register does not have 4 columns?  In the future, the process could be extended
                        //to automatically calculate the number of columns in the register image (Currently it is pre-defined in the
                        //app.config file)
                        emptyColumnIndices.Add(i);
                    }
                }

                //Remove the empty columns in a final attempt to read the register
                foreach (int index in emptyColumnIndices) {
                    columnPoints.RemoveAt(index);
                    Columns--;
                }

                columnPoints = columnPoints.OrderBy(l => l[0].X).ToList();
            } catch (InvalidOperationException ex) {
                throw (new InvalidOperationException("Unable to sort points into columns: Columns with no points were found"));
            }

            
#if DEBUG
            //Debugging: Draw interpreted column points 
            register.DebugImage = new Image<Bgr, byte>(intersections.Size);
            intersections.Convert<Bgr, Byte>().CopyTo(register.DebugImage);
            register.DebugImage.DrawPoints(15, new Bgr(Color.Red), -1, columnPoints[0]);
            register.DebugImage.DrawPoints(15, new Bgr(Color.Blue), -1, columnPoints[1]);
            if (Columns >= 2) register.DebugImage.DrawPoints(15, new Bgr(Color.Yellow), -1, columnPoints[2]);
            if (Columns >= 3) register.DebugImage.DrawPoints(15, new Bgr(Color.Green), -1, columnPoints[3]);
            if (Columns >= 4) register.DebugImage.DrawPoints(15, new Bgr(Color.Azure), -1, columnPoints[4]);
#endif
            

            //Construct cells from the column points
            int rowsCount = columnPoints[0].Count;
            register.Rows = new List<Row>();
            for (int r = 0; r < rowsCount - 1; r++) {
                register.Rows.Add(new Row(r + 1)); //Construct a row
                register.Rows[r].Cells = new List<Cell>();

                //Add cells
                for (int c = 0; c < columnPoints.Count - 1; c++) {
                    Quadrilateral region = new Quadrilateral(columnPoints[c][r], columnPoints[c + 1][r],
                            columnPoints[c + 1][r + 1], columnPoints[c][r + 1]);

                    Cell cell = new Cell(region, c);
                    register.Rows[r].Cells.Add(cell);
                }
            }


            //Create the register image that contains only signatures
            //Logical OR operation between vertical and horizontal lines produces register table
            Image<Gray, Byte> registerTable = vLines.Or(hLines); 

            //Some signatures may remain in the register table image (Specifically in hLines)
            //Try to remove them by cropping the region of the signature cells
            for (int row = 1; row < register.Rows.Count; row++) { //skip first row (Headers)
                Cell signatureCell = register.Rows[row].Cells[Columns - 1]; //last cell is signature cell

                Quadrilateral cropRegion = signatureCell.Quadrilateral.Copy();
                cropRegion.Deflate(15);
                registerTable.Draw(cropRegion, MCvColor.Black);
            }
            //Subtract the register table image from the register image, giving an image containing only the signatures
            register.SiganturesOnlyImage = registerImage.Sub(registerTable.Dilate(1));


            //Set contents of cells
            foreach (Row row in register.Rows) {
                foreach (Cell cell in row.Cells) {
                    //Wrap quadriletral into a rectangular region
                    using (Image<Gray, Byte> Mask = new Image<Gray, byte>(register.Image.Size)) {
                        Mask.Draw(cell.Quadrilateral, MCvColor.White);
                        cell.SetContents(register.SiganturesOnlyImage.Copy(Mask).Copy(cell.Quadrilateral.MinAreaRectangle()), Columns);
                        //cell.ProcessedContents.Save("Debug.png");
                    }
                }
            }


            //Assign one entry to each row
            using (OCRGrayscale ocr = new OCRGrayscale()) {
                for (int r = 1; r < register.Rows.Count; r++) { //skip first row (Headers)
                    Entry entry = new Entry();

                    //Apply OCR onto first and second cells to retrieve name and ID
                    entry.Name = ocr.RecognizeEnglish(register.Rows[r].Cells[1].ProcessedContents); //second cell is name
                    entry.ID = ocr.RecognizeNumber(register.Rows[r].Cells[2].ProcessedContents); //third cell is ID

                    if (register.Rows[r].Cells[Columns - 1].ContainsSignature()) {
                        //fourth cell is signature
                        entry.Signature = new Signature();
                        entry.Signature.Image = register.Rows[r].Cells[Columns - 1].ProcessedContents; 
                    } else {
                        entry.Signature = null; //entry has no signature
                    }

                    register.Rows[r].Entry = entry;
                }
            }
            return (register);
        }

        /// <summary>
        /// Returns the column's row index that the point should be inserted into or -1 if it shouldn't be inserted into this column
        /// or -2 if the point should be discarded (noise)
        /// </summary>
        /// <param name="Column">The row points of the column</param>
        /// <param name="Point">The point to be inserted</param>
        /// <param name="ThresholdX">Horizontal tolerance</param>
        /// <param name="ThresholdY">Vertical tolerance</param>
        /// <returns></returns>
        private static int IntersectionPointToColumn(IList<Point> Column, Point Point, int ThresholdX, int ThresholdY, int NoiseThreshold) {
            if (Column.Count == 0) {
                return (0);
            }

            Point topPoint = Column[0];
            Point botPoint = Column[Column.Count - 1];

            //Check if noise
            double distance = Math.Sqrt(Math.Pow(topPoint.X - Point.X, 2) + Math.Pow(topPoint.Y - Point.Y, 2));
            if (distance < NoiseThreshold) { return (-2); }
            distance = Math.Sqrt(Math.Pow(botPoint.X - Point.X, 2) + Math.Pow(botPoint.Y - Point.Y, 2));
            if (distance < NoiseThreshold) { return (-2); }

            //Check if point is above top point of current column
            if (Math.Abs(topPoint.X - Point.X) < ThresholdX) {
                if (Point.Y < topPoint.Y && topPoint.Y - Point.Y < ThresholdY) {
                    return (0);
                }
            }

            //Check if point is below bottom point of current column
            if (Math.Abs(botPoint.X - Point.X) < ThresholdX) {
                if (Point.Y > botPoint.Y && Point.Y - botPoint.Y < ThresholdY) {
                    return (Column.Count);
                }
            }

            //Can't be added
            return (-1);
        }

        /// <summary>
        /// Sorts a set of points into columns according to their positions.
        /// </summary>
        /// <param name="Points">The list of points</param>
        /// <param name="ThresholdX">Horizontal tolerance</param>
        /// <param name="ThresholdY">Vertical tolerance</param>
        /// <returns>A tuple containing the list of columns and the number of points that could not be inserted into any columns</returns>
        private static Tuple<IList<IList<Point>>, int> SortPointsIntoColumns(int Columns, List<Point> Points, int ThresholdX, int ThresholdY, int noiseThreshold) {
            IList<IList<Point>> ColumnPoints = new List<IList<Point>>(); //{column #, column points}
            for (int i = 0; i < Columns + 1; i++) {
                ColumnPoints.Add(new List<Point>());
            }

            int invalidPoints = 0;
            for (int i = 0; i < Points.Count; i++) { //go through all points to be inserted
                for (int k = 0; k < ColumnPoints.Count; k++) { //go through all available columns
                    int index = IntersectionPointToColumn(ColumnPoints[k], Points[i], ThresholdX, ThresholdY, noiseThreshold);
                    if (index == -2) { break; } //discard this point (Noise?)

                    if (index >= 0) {
                        //Point successfully inserted in to this column
                        ColumnPoints[k].Insert(index, Points[i]);
                        break;
                    }

                    //Point cannot be inserted!
                    if (k == ColumnPoints.Count - 1) {
                        invalidPoints++;
                    }
                }
            }

            return (new Tuple<IList<IList<Point>>, int>(ColumnPoints, invalidPoints));
        }
    }
}
