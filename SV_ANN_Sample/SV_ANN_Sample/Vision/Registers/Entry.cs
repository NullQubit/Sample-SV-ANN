using SV_ANN_Sample.Vision.Registers.Signatures;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SV_ANN_Sample.Vision.Registers {
    /// <summary>
    /// Represents a register entry
    /// </summary>
    public class Entry {
        /// <summary>
        /// Data separator used when exporting entry
        /// </summary>
        private const char FileDataSeparator = '|';

        public Entry() { }

        /// <summary>
        /// Exports this Vision.Registers.Entry to a file
        /// </summary>
        /// <param name="Filename">The output filename</param>
        public void ToFile(string Filename) {
            string contents = ID + FileDataSeparator + Name + FileDataSeparator;

            //Make sure signature's features are normalized
            if (Signature.Features.NormalizedData == null) {
                Signature.Features.Normalize(Signature.ProcessedImage.Size);
            }

            foreach (float normalizedFeature in Signature.Features.NormalizedData) {
                contents += normalizedFeature + FileDataSeparator;
            }

            using (StreamWriter sW = new StreamWriter(Filename)) {
                sW.Write(contents);
            }
        }

        /// <summary>
        /// Imports a Vision.Registers.Entry from a file
        /// </summary>
        /// <param name="Filename">The input filename</param>
        /// <returns></returns>
        public static Entry FromFile(string Filename) {
            string Contents;
            using (StreamReader sR = new StreamReader(Filename)) {
                Contents = sR.ReadToEnd();
            }

            Entry student = new Entry();
            string[] data = Contents.Split(FileDataSeparator);

            student.ID = data[0];
            student.Name = data[1];
            student.Signature = new Signature() {
                Features = new FeatureSet() {
                    NormalizedData = new float[data.Length - 2]
                }
            };

            for (int i = 2; i < data.Length; i++) {
                float feature = float.Parse(data[i]);
                student.Signature.Features.NormalizedData[i - 2] = feature;
            }

            return (student);
        }

        /// <summary>
        /// Gets or sets the entry's signature
        /// </summary>
        public Signature Signature { get; set; }

        /// <summary>
        /// Gets or sets the entry's ID
        /// </summary>
        public string ID { get; set; }

        /// <summary>
        /// Gets or sets the entry's name
        /// </summary>
        public string Name { get; set; }
    }
}
