/* Copyright (C) 2013-2014 Pavlidis Orestis
 * Unauthorized copying of this file, via any medium is strictly prohibited
*/

using Emgu.CV;
using Emgu.CV.OCR;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace SV_ANN_Sample.Vision.OCR {
    /// <summary>
    /// Performs OCR operations on 8bpp images
    /// </summary>
    public class OCRGrayscale : IDisposable {
        public OCRGrayscale() : this(Tesseract.OcrEngineMode.OEM_TESSERACT_CUBE_COMBINED) { }

        public OCRGrayscale(Tesseract.OcrEngineMode mode) {
            _Tesseract = new Tesseract("tessdata", "eng", mode);
        }

        /// <summary>
        /// Performs general OCR operation on given image.
        /// </summary>
        /// <typeparam name="TColor">The color type of the image</typeparam>
        /// <param name="Image">The image</param>
        /// <returns>The recognized string</returns>
        public string Recognize<TColor>(Image<TColor, byte> Image) where TColor : struct, IColor {
            _Tesseract.Recognize(Image);
            return (_Tesseract.GetText());
        }

        /// <summary>
        /// Performs numeric OCR operation on given image.
        /// </summary>
        /// <typeparam name="TColor">The color type of the image</typeparam>
        /// <param name="Image">The image</param>
        /// <returns>The recognized string.  The string contains only numbers.</returns>
        public string RecognizeNumber<TColor>(Image<TColor, byte> Image) where TColor : struct, IColor {
            string text = Recognize(Image);

            text = text.ToUpper().Replace("O", "0"); //Sometimes 0 is interpreted as O
            text = Regex.Replace(text, "[^.0-9]", ""); //Keep only numbers

            return (text);
        }

        /// <summary>
        /// Performs general OCR operation on given image and then filters out non-english characters or symbols.
        /// Dashes and separators are not filtered out.
        /// </summary>
        /// <typeparam name="TColor">The color type of the image</typeparam>
        /// <param name="Image">The image</param>
        /// <returns>The recognized string</returns>
        public string RecognizeEnglish<TColor>(Image<TColor, byte> Image) where TColor : struct, IColor {
            string text = Recognize(Image);
            text = text.Replace(Environment.NewLine + Environment.NewLine, Environment.NewLine);
            text = text.Replace(Environment.NewLine, " ");
            text = new String(text.Where(c => Char.IsLetter(c) || Char.IsSeparator(c) || c == '-').ToArray());
            text = text.Trim();

            return (text);
        }

        /// <summary>
        /// Performs general OCR operation on given image with a given character threshold.  The lower the threshold
        /// the more confident the recognition process for a character has to be in order to be included.
        /// </summary>
        /// <typeparam name="TColor">The color type of the image</typeparam>
        /// <param name="Image">The image</param>
        /// <param name="Threshold">The confidence threshold</param>
        /// <returns>The recognized string</returns>
        public string Recognize<TColor>(Image<TColor, byte> Image, double Threshold) where TColor : struct, IColor {
            _Tesseract.Recognize(Image);

            Tesseract.Charactor[] charactors = _Tesseract.GetCharactors();

            string text = "";
            foreach (var charactor in charactors) {
                if (charactor.Cost < Threshold) {
                    text += charactor.Text;
                }
            }
            return (text);
        }

        /// <summary>
        /// Releases all resources used by this Vision.OCR.OCRGrayscale
        /// </summary>
        public void Dispose() {
            if (_Tesseract != null) { _Tesseract.Dispose(); }
        }

        private Tesseract _Tesseract { get; set; }
    }
}
