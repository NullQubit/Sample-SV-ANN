/* Copyright (C) 2013-2014 Pavlidis Orestis
 * Unauthorized copying of this file, via any medium is strictly prohibited
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SV_ANN_Sample.Vision.Registers {
    /// <summary>
    /// Represents a row of cells
    /// </summary>
    public class Row {
        public Row() { }

        /// <summary>
        /// Initializes a new Vision.Registers.Row with the given number
        /// </summary>
        /// <param name="Number">The row's number</param>
        public Row(int Number) {
            this.Number = Number;
        }

        /// <summary>
        /// Gets or sets the cells contained within this row
        /// </summary>
        public IList<Cell> Cells { get; set; }

        /// <summary>
        /// Gets or sets the entry this row represents
        /// </summary>
        public Entry Entry { get; set; }

        /// <summary>
        /// Gets or sets the row number
        /// </summary>
        public int Number { get; set; }
    }
}
