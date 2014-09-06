using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SV_ANN_Sample.Vision.ImageProcessing.ImageFilters {
    public interface IFilter {
        /// <summary>
        /// Applies the filter on a System.Drawing.Bitmap and returns a new Bitmap
        /// </summary>
        /// <param name="SourceImage">The source bitmap</param>
        /// <param name="OutputFormat">The format of the output bitmap</param>
        /// <returns>The filtered bitmap</returns>
        Bitmap Apply(Bitmap SourceImage, PixelFormat OutputFormat);

        /// <summary>
        /// Returns whether the filter supports the given formats
        /// </summary>
        /// <param name="InputFormat">The format of the source bitmap</param>
        /// <param name="OutputFormat">The format of the output bitmap</param>
        /// <returns>True if supported, false otherwise</returns>
        bool IsSupported(PixelFormat InputFormat, PixelFormat OutputFormat);
    }
}
