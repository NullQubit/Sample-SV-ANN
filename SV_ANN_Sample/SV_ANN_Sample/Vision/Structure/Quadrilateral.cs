/* Copyright (C) 2013-2014 Pavlidis Orestis
 * Unauthorized copying of this file, via any medium is strictly prohibited
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SV_ANN_Sample.Vision.Structure {
    /// <summary>
    /// Represents a four-sided figure
    /// </summary>
    public struct Quadrilateral {

        
        /// <summary>
        /// Initializes a new Vision.Structure.Quadrilateral object with the given points (edges)
        /// </summary>
        /// <param name="TopLeft">The top left point of the quadrilateral</param>
        /// <param name="TopRight">The top right point of the quadrilateral</param>
        /// <param name="BottomRight">The bottom right point of the quadrilateral</param>
        /// <param name="BottomLeft">The bottom left point of the quadrilateral</param>
        public Quadrilateral(Point TopLeft, Point TopRight, Point BottomRight, Point BottomLeft) : this() {
            this.Points = new Point[] { TopLeft, TopRight, BottomRight, BottomLeft };
        }

        /// <summary>
        /// Initializes a new Vision.Structure.Quadrilateral object with the given points (edges)
        /// </summary>
        /// <param name="Points">An enumerable containing the four points of the Quadrilateral</param>
        /// <exception cref="ArgumentException">Thrown when IEnumerable does not contain 4 points</exception>
        public Quadrilateral(IEnumerable<Point> Points) : this() {
            this.Points = Points.ToArray();
            if (this.Points.Length > 4) {
                throw (new ArgumentException("A quadrilateral can only have four sides"));
            }
        }

        /// <summary>
        /// Returns a new (deep) copy of this Vision.Structure.Quadrilateral
        /// </summary>
        /// <returns>The copied Vision.Structure.Quadrilateral</returns>
        public Quadrilateral Copy() {
            return (new Quadrilateral(Points));
        }

        /// <summary>
        /// Returns the smallest rectangular area possible that can contain this quadrilateral
        /// </summary>
        /// <returns>The System.Drawing.Rectangle</returns>
        public Rectangle MinAreaRectangle() {
            int x = Math.Min(Points[0].X, Points[3].X);
            int y = Math.Min(Points[0].Y, Points[1].Y);
            int x2 = Math.Max(Points[1].X, Points[2].X);
            int y2 = Math.Max(Points[3].Y, Points[2].Y);
            return (new Rectangle(x, y, x2 - x, y2 - y));
        }

        /// <summary>
        /// Converts this quadrilateral's points into relative (to the closest point) points and returns a 
        /// new quadrilateral with these points (e.g. Top-Left point will be 0,0)
        /// </summary>
        /// <returns>The relative quadrilateral</returns>
        public Quadrilateral GetRelative() {
            Point[] relativePoints = new Point[4];

            int minX = Math.Min(Points[0].X, Points[3].X);
            int minY = Math.Min(Points[0].Y, Points[1].Y);
            for (int i = 0; i < 4; i++) {
                relativePoints[i] = new Point(Points[i].X - minX, Points[i].Y - minY);
            }

            return (new Quadrilateral(relativePoints));
        }

        /// <summary>
        /// Inflates the quadrilateral by the given amount
        /// </summary>
        /// <param name="Amount">The amount of inflation to apply</param>
        public void Inflate(int Amount) {
            Points[0].X -= Amount;
            Points[0].Y -= Amount;

            Points[1].X += Amount;
            Points[1].Y -= Amount;

            Points[2].X += Amount;
            Points[2].Y += Amount;

            Points[3].X -= Amount;
            Points[3].Y += Amount;
        }

        /// <summary>
        /// Deflates the quadrilateral by the given amount
        /// </summary>
        /// <param name="Amount">The amount of deflation to apply</param>
        public void Deflate(int Amount) {
            Inflate(-Amount);
        }


        /// <summary>
        /// Gets or sets the location of the points (edges) of this quadrilateral
        /// </summary>
        /// <param name="index">The index of the location (Top Left, Top Right, Bottom Right, Bottom Left)  </param>
        /// <returns>The location of the point (edge)</returns>
        public Point this[int index] {
            get { return (Points[index]); }
            set { Points[index] = value; }
        }

        /// <summary>
        /// Gets the points of the quadrilateral in an array, stored in the following order respectively:
        /// Top Left, Top Right, Bottom Right, Bottom Left
        /// </summary>
        public Point[] Points {
            get;
            private set;
        }
    }
}
