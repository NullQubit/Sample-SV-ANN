/* Copyright (C) 2013-2014 Pavlidis Orestis
 * Unauthorized copying of this file, via any medium is strictly prohibited
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SV_ANN_Sample.Vision.Registers.Signatures {
    /// <summary>
    /// Represents a set of signature features
    /// </summary>
    public class FeatureSet {

        public FeatureSet() { }

        /// <summary>
        /// Exports this feature set to a file
        /// </summary>
        /// <param name="Filename">The output filename</param>
        public void Export(string Filename) {
            using (StreamWriter sW = new StreamWriter(Filename)) {
                foreach (float feature in NormalizedData) {
                    sW.Write(feature + Environment.NewLine);
                }
            }
        }

        /// <summary>
        /// Imports a feature set from a file.  The imported feature set only contains the normalized data.
        /// </summary>
        /// <param name="Filename">The input filename</param>
        /// <returns></returns>
        public static FeatureSet Import(string Filename) {
            try {
                FeatureSet fs = new FeatureSet();
                using (StreamReader sR = new StreamReader(Filename)) {
                    fs.NormalizedData = Array.ConvertAll<string, float>(sR.ReadToEnd().Split(new string[] { Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries), float.Parse);
                }

                return (fs);
            } catch {
                return (null);
            }
        }

        /// <summary>
        /// Subtracts the values of the normalized data of two feature sets
        /// </summary>
        /// <param name="a">The first feature set</param>
        /// <param name="b">The second feature set</param>
        /// <returns>The absolute difference of the values of the two normalized data of the two feature set</returns>
        public static float operator -(FeatureSet a, FeatureSet b) {
            if (a.NormalizedData == null || b.NormalizedData == null) { throw (new InvalidOperationException("Feature set not normalized")); };
            if (a.NormalizedData.Length != b.NormalizedData.Length) { throw (new InvalidOperationException("Feature sets must be of same length")); };
            float diff = 0.0f;

            for (int i = 0; i < a.NormalizedData.Length; i++) {
                diff += Math.Abs(a.NormalizedData[i] - b.NormalizedData[i]);
            }

            return (diff);
        }


        /// <summary>
        /// Normalizes this feature set
        /// </summary>
        /// <param name="ImageSize">The size of the signature image (Required for min and max histograms)</param>
        public void Normalize(Size ImageSize) {
            NormalizedData = new float[Size];
            NormalizedData[0] = AspectRatio;
            NormalizedData[1] = OccupancyRatio;
            NormalizedData[2] = (float)MaxHorizontalHistogram / ImageSize.Height;
            NormalizedData[3] = (float)MaxVerticalHistogram / ImageSize.Width;
            NormalizedData[4] = (float)EdgePoints.Length / 10;
            NormalizedData[5] = (float)CrossPoints.Length / 10;
            NormalizedData[6] = (float)ClosedLoops / 5;

            for (int k = 0; k < VerticalGeometricCenters.Length; k++) {
                NormalizedData[7 + k * 2] = (float)VerticalGeometricCenters[k].X / (ImageSize.Width / 2);
                NormalizedData[7 + k * 2 + 1] = (float)VerticalGeometricCenters[k].Y / (ImageSize.Height / 2);
            }

            for (int k = 0; k < HorizontalGeometricCenters.Length; k++) {
                NormalizedData[7 + VerticalGeometricCenters.Length * 2 + k * 2] = (float)HorizontalGeometricCenters[k].X / (ImageSize.Width / 2);
                NormalizedData[7 + VerticalGeometricCenters.Length * 2 + k * 2 + 1] = (float)HorizontalGeometricCenters[k].Y / (ImageSize.Height / 2);
            }
        }

        /// <summary>
        /// Gets the total size of the set
        /// </summary>
        public int Size {
            get { return 7 + VerticalGeometricCenters.Length * 2 + HorizontalGeometricCenters.Length * 2; }
        }

        /// <summary>
        /// Gets or sets the aspect ratio
        /// </summary>
        public float AspectRatio { get; set; }

        /// <summary>
        /// Gets or sets the occupancy ratio
        /// </summary>
        public float OccupancyRatio { get; set; }

        /// <summary>
        /// Gets or sets the maximum vertical histogram
        /// </summary>
        public int MaxVerticalHistogram { get; set; }

        /// <summary>
        /// Gets or sets the maximum horizontal histogram
        /// </summary>
        public int MaxHorizontalHistogram { get; set; }

        /// <summary>
        /// Gets or sets the horizontal geometric centers
        /// </summary>
        public Point[] HorizontalGeometricCenters { get; set; }

        /// <summary>
        /// Gets or sets the vertical geometric centers
        /// </summary>
        public Point[] VerticalGeometricCenters { get; set; }

        /// <summary>
        /// Gets or sets the number of closed loops
        /// </summary>
        public int ClosedLoops { get; set; }

        /// <summary>
        /// Gets or sets the cross points
        /// </summary>
        public Point[] CrossPoints { get; set; }

        /// <summary>
        /// Gets or sets the edge points
        /// </summary>
        public Point[] EdgePoints { get; set; }

        /// <summary>
        /// Gets or sets the normalized data
        /// </summary>
        public float[] NormalizedData { get; set; }
    }
}
