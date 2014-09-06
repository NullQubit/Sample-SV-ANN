/* Copyright (C) 2013-2014 Pavlidis Orestis
 * Unauthorized copying of this file, via any medium is strictly prohibited
*/

using Emgu.CV;
using Emgu.CV.Structure;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SV_ANN_Sample.Vision.Registers {
    /// <summary>
    /// Represents a register sheet
    /// </summary>
    public class Register : IDisposable {

        /// <summary>
        /// Initializes a new Vision.Registers.Register with four columns
        /// </summary>
        public Register() : this(4) { }

        /// <summary>
        /// Initializes a new Vision.Registers.Register with the given number of columns
        /// </summary>
        /// <param name="Columns">The number of columns in the register</param>
        public Register(int Columns) {
            this.Columns = Columns;
        }

        /// <summary>
        /// Exports the register to a file
        /// </summary>
        /// <param name="Filename">The output filename</param>
        public void ToFile(string Filename) {
            using (StreamWriter sW = new StreamWriter(Filename)) {
                sW.Write(ToString());
            }
        }

        
        /// <summary>
        /// Compares this register with an external (Exported using <see cref="ToFile"/>) register
        /// </summary>
        /// <param name="Filename">The register's filename </param>
        /// <returns></returns>
        /// <exception cref="ArgumentException">Thrown when the file's structure does not match current structure</exception>
        public string Compare(string Filename) {
            string differences = "";
            string item1 = ToString();
            string item2;

            using (StreamReader sR = new StreamReader(Filename)) {
                item2 = sR.ReadToEnd();
            }

            string[] rows1 = item1.Split(Environment.NewLine.ToCharArray()).Where(t => !string.IsNullOrWhiteSpace(t)).ToArray();
            string[] rows2 = item2.Split(Environment.NewLine.ToCharArray()).Where(t => !string.IsNullOrWhiteSpace(t)).ToArray();

            if (rows1.Length != rows2.Length) {
                throw (new ArgumentException("Given data file's structure does not match current structure"));
            }

            for (int i = 0; i < rows1.Length; i++) {
                string[] rowData1 = rows1[i].Split('\t');
                string[] rowData2 = rows2[i].Split('\t');

                if (rowData1.Length != rowData2.Length) {
                    throw (new ArgumentException("Given data file's structure does not match current structure"));
                }

                for (int k = 0; k < rowData1.Length; k++) {
                    if (!rowData1[k].Trim().Equals(rowData2[k].Trim())) {
                        differences += rows1[i] + Environment.NewLine;
                        break;
                    }
                }
            }

            return (differences);
        }

        /// <summary>
        /// Releases all resources used by this Vision.Registers.Register
        /// </summary>
        public void Dispose() {
            if (Image != null) { Image.Dispose(); }
            if (SiganturesOnlyImage != null) { SiganturesOnlyImage.Dispose(); }
            if (Rows == null) { return; }
            foreach (Row row in Rows) {
                if (row.Entry == null) { continue; /*header?*/ }
                if (row.Entry.Signature != null) {
                    row.Entry.Signature.Dispose();
                }

                foreach (Cell cell in row.Cells) {
                    cell.Dispose();
                }
            }

#if DEBUG
            if (DebugImage != null) { DebugImage.Dispose(); }
#endif
        }

        public override string ToString() {
            string data = "";

            for (int r = 1; r < Rows.Count; r++) {
                string rowStr = "#" + r + "\t";
                rowStr += Rows[r].Entry.Name + "\t";
                rowStr += Rows[r].Entry.ID + "\t";
                rowStr += Rows[r].Entry.Signature == null ? "Not signed" : "Signed";
                data += rowStr + Environment.NewLine;
            }

            return (data);
        }

        /// <summary>
        /// Gets or sets the rows of the register
        /// </summary>
        public IList<Row> Rows { get; set; }

        /// <summary>
        /// Gets or sets the register's image
        /// </summary>
        public Image<Gray, Byte> Image { get; set; }


        /// <summary>
        /// Gets or sets the image containing only the signatures of the register
        /// </summary>
        public Image<Gray, Byte> SiganturesOnlyImage { get; set; }

        /// <summary>
        /// Gets or sets the number of columns of the register
        /// </summary>
        public int Columns { get; set; }


        /// <summary>
        /// Gets or sets the image used when debugging
        /// </summary>
        internal Image<Bgr, Byte> DebugImage { get; set; }
    }
}
