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
using SV_ANN_Sample.Vision.ImageProcessing;

namespace SV_ANN_Sample.Vision.Registers {
    /// <summary>
    /// Represents a register cell
    /// </summary>
    public class Cell : IDisposable {
        /// <summary>
        /// Initializes a new Vision.Registers.Cell object
        /// </summary>
        /// <param name="Quadrilateral">The region of the cell</param>
        /// <param name="Number">The cell's number</param>
        public Cell(Quadrilateral Quadrilateral, int Number) {
            this.AbsoluteRegion = Quadrilateral;
            this.Number = Number;
            this.Region = Quadrilateral.GetRelative();
        }


        /// <summary>
        /// Sets the contents of this cell
        /// </summary>
        /// <param name="Contents">The image contents</param>
        /// <param name="Columns">The number of columns in the register (Used to determine if cell is a signature cell)</param>
        public void SetContents(Image<Gray, Byte> Contents, int Columns) {
            Contents = Contents.Not();
            ProcessedContents = Contents;

            if (Number == Columns - 1) {
                //Last cell is signature cell, remove cluttering
                Contents.RemoveClutter(Contents.Width * Contents.Height / 95);
            } else {
                StructuringElementEx structure = new StructuringElementEx(3, 3, 1, 1, CV_ELEMENT_SHAPE.CV_SHAPE_ELLIPSE);
                CvInvoke.cvMorphologyEx(ProcessedContents, ProcessedContents, IntPtr.Zero, structure, CV_MORPH_OP.CV_MOP_OPEN, 1);
            }
        }


        /// <summary>
        /// Releases all resources used by this Vision.Registers.Cell
        /// </summary>
        public void Dispose() {
            if (Contents != null) { Contents.Dispose(); }
            if (ProcessedContents != null) { ProcessedContents.Dispose(); }
        }

        /// <summary>
        /// Checks whether this cell contains a signature or not
        /// </summary>
        /// <returns></returns>
        public bool ContainsSignature() {
            double threshold = 0.004; //White pixels to total pixels ratio threshold

            int whitePixels = ProcessedContents.CountPixels(0xFF);
            int totalPixels = ProcessedContents.Width * ProcessedContents.Height;
            return ((double)whitePixels / totalPixels > threshold);
        }

        /// <summary>
        /// Gets the contents of the cell
        /// </summary>
        public Image<Gray, Byte> Contents { get; protected set; }
        /// <summary>
        /// Gets the processed contents of the cell
        /// </summary>
        public Image<Gray, Byte> ProcessedContents { get; protected set; }
        /// <summary>
        /// Gets or sets the cell's number
        /// </summary>
        public int Number { get; set; }
        /// <summary>
        /// Gets or sets the cell's region (on the register sheet)
        /// </summary>
        public Quadrilateral AbsoluteRegion { get; set; }
        /// <summary>
        /// Gets the cell's region
        /// </summary>
        public Quadrilateral Region { get; protected set; }
    }
}
