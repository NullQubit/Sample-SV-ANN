﻿/* Copyright (C) 2013-2014 Pavlidis Orestis
 * Unauthorized copying of this file, via any medium is strictly prohibited
*/

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SV_ANN_Sample.Vision.ImageProcessing.ImageFilters {
    /// <summary>
    /// A filter used to apply Zhang-Suen's thinning algorithm
    /// </summary>
    public class ZSThinningFilter : IFilter {

        /// <summary>
        /// Applies the Zhang-Suen's thinning filter on a System.Drawing.Bitmap and returns a new Bitmap
        /// </summary>
        /// <param name="SourceImage">The source bitmap</param>
        /// <param name="OutputFormat">The format of the output bitmap.  Must be 8bpp</param>
        /// <returns>The filtered bitmap</returns>
        public unsafe Bitmap Apply(Bitmap SourceImage, PixelFormat OutputFormat) {
            if (!IsSupported(SourceImage.PixelFormat, OutputFormat)) {
                throw (new ArgumentException("This filter requires both the input and output format to be 8bpp"));
            }

            /*
            Throughout the process, it is working on a separate array instead of a pointer to the actual image.
            This is done for 2 reasons:
                1.  The image represents white pixels as 0 and black pixels as 1
                2.  Leave the original image intact, and return a new image
            
            Representing white pixels as 0 and black pixels as 1 seems to be more efficient (~28% faster) that using 0x00 and 0xFF
            (Mainly for counting black pixel neighbours).  The overhead of looping through the image twice to create
            this new seperate array is much smaller than working with 0x00 and 0xFF
            */

            Bitmap destinationImage = new Bitmap(SourceImage.Width, SourceImage.Height, OutputFormat);
            BitmapData sourceData = SourceImage.LockBits(new Rectangle(0, 0, SourceImage.Width, SourceImage.Height), ImageLockMode.ReadOnly, SourceImage.PixelFormat);
            BitmapData destinationData = destinationImage.LockBits(new Rectangle(0, 0, sourceData.Width, sourceData.Height), ImageLockMode.WriteOnly, OutputFormat);
            
            byte* source = (byte*)sourceData.Scan0.ToPointer();
            byte* destination = (byte*)destinationData.Scan0.ToPointer();
            int srcOffset = sourceData.Stride - sourceData.Width;
            int dstOffset = destinationData.Stride - destinationData.Width;

            //Place image data into a 2D int array (1 = Black, 0 = White)
            int[][] ImageData = new int[sourceData.Height][];
            for (int y = 0; y < ImageData.Length; y++) {
                ImageData[y] = new int[sourceData.Width];

                for (int x = 0; x < ImageData[y].Length; x++, source++) {
                    ImageData[y][x] = *source == 0x00 ? 1 : 0;
                }
                source += srcOffset;
            }

            //Loop through all pixels and mark pixels to change
            List<Point> PixelsToChange = new List<Point>();
            do {
                PixelsToChange.Clear();

                //Template1
                for (int y = 1; y < ImageData.Length - 1; y++) {
                    for (int x = 1; x < ImageData[y].Length - 1; x++) {
                        int transitions = GetTransitions(ImageData, y, x);
                        int neighbours = GetBlackPixelNeighbours(ImageData, y, x);
                        if (ImageData[y][x] == 1 && 2 <= neighbours && neighbours <= 6 && transitions == 1
                                && (ImageData[y - 1][x] * ImageData[y][x + 1] * ImageData[y + 1][x] == 0)
                                && (ImageData[y][x + 1] * ImageData[y + 1][x] * ImageData[y][x - 1] == 0)) {
                            PixelsToChange.Add(new Point(x, y));
                        }
                    }
                }

                if (PixelsToChange.Count == 0) {
                    break;
                }

                //Update ImageData[][] (Set pixels to white)
                foreach (Point point in PixelsToChange) {
                    ImageData[point.Y][point.X] = 0;
                }
                PixelsToChange.Clear();

                //Template2
                for (int y = 1; y + 1 < ImageData.Length; y++) {
                    for (int x = 1; x + 1 < ImageData[y].Length; x++) {
                        int transitions = GetTransitions(ImageData, y, x);
                        int neighbours = GetBlackPixelNeighbours(ImageData, y, x);
                        if (ImageData[y][x] == 1 && 2 <= neighbours && neighbours <= 6 && transitions == 1
                                && (ImageData[y - 1][x] * ImageData[y][x + 1] * ImageData[y][x - 1] == 0)
                                && (ImageData[y - 1][x] * ImageData[y + 1][x] * ImageData[y][x - 1] == 0)) {
                            PixelsToChange.Add(new Point(x, y));
                        }
                    }
                }

                //Update ImageData[][] (Set pixels to white)
                foreach (Point point in PixelsToChange) {
                    ImageData[point.Y][point.X] = 0;
                }


            } while (PixelsToChange.Count > 0); //Loop until no pixel was changed since the last iteration


            //Modify destination bitmap based on ImageData[][]
            for (int y = 0; y < ImageData.Length; y++) {
                for (int x = 0; x < ImageData[y].Length; x++, destination++) {
                    *destination = ImageData[y][x] == 1 ? (byte)0x00 : (byte)0xFF;
                }
                destination += dstOffset;
            }

            SourceImage.UnlockBits(sourceData);
            destinationImage.UnlockBits(destinationData);
            return (destinationImage);
        }


        /// <summary>
        /// Returns whether the filter supports the given formats.  This filter only supports 8bpp formats.
        /// </summary>
        /// <param name="InputFormat">The format of the source bitmap</param>
        /// <param name="OutputFormat">The format of the output bitmap</param>
        /// <returns>True if supported, false otherwise</returns>
        public bool IsSupported(PixelFormat InputFormat, PixelFormat OutputFormat) {
            if (InputFormat != PixelFormat.Format8bppIndexed || InputFormat != OutputFormat) {
                return (false);
            }
            return (true);
        }

        /// <summary>
        /// Gets number of transitions at given point
        /// </summary>
        /// <param name="Image">The image data</param>
        /// <param name="y">Y position of pixel</param>
        /// <param name="x">X position of pixel</param>
        /// <returns>Number of transitions (from black to white)</returns>
        private int GetTransitions(int[][] Image, int y, int x) {
            int count = 0;
            //p2 p3
            if (Image[y - 1][x] == 0 && Image[y - 1][x + 1] == 1) {
                count++;
            }
            //p3 p4
            if (Image[y - 1][x + 1] == 0 && Image[y][x + 1] == 1) {
                count++;
            }
            //p4 p5
            if (Image[y][x + 1] == 0 && Image[y + 1][x + 1] == 1) {
                count++;
            }
            //p5 p6
            if (Image[y + 1][x + 1] == 0 && Image[y + 1][x] == 1) {
                count++;
            }
            //p6 p7
            if (Image[y + 1][x] == 0 && Image[y + 1][x - 1] == 1) {
                count++;
            }
            //p7 p8
            if (Image[y + 1][x - 1] == 0 && Image[y][x - 1] == 1) {
                count++;
            }
            //p8 p9
            if (Image[y][x - 1] == 0 && Image[y - 1][x - 1] == 1) {
                count++;
            }
            //p9 p2
            if (Image[y - 1][x - 1] == 0 && Image[y - 1][x] == 1) {
                count++;
            }

            return (count);
        }

        /// <summary>
        /// Gets number of neighbouring black pixels
        /// </summary>
        /// <param name="Image">The image data</param>
        /// <param name="y">Y position of pixel</param>
        /// <param name="x">X position of pixel</param>
        /// <returns>Number of neighbouring black pixels</returns>
        private int GetBlackPixelNeighbours(int[][] Image, int y, int x) {
            return (Image[y - 1][x] + Image[y - 1][x + 1] + Image[y][x + 1]
                    + Image[y + 1][x + 1] + Image[y + 1][x] + Image[y + 1][x - 1]
                    + Image[y][x - 1] + Image[y - 1][x - 1]);
        }
    }
}
