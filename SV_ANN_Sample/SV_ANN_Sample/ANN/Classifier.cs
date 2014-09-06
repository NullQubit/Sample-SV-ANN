/* Copyright (C) 2013-2014 Pavlidis Orestis
 * Unauthorized copying of this file, via any medium is strictly prohibited
*/

using Emgu.CV;
using Emgu.CV.ML;
using Emgu.CV.ML.MlEnum;
using Emgu.CV.ML.Structure;
using Emgu.CV.Structure;
using SV_ANN_Sample.Vision.Registers;
using SV_ANN_Sample.Vision.Registers.Signatures;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SV_ANN_Sample.ANN {
    /// <summary>
    /// Represents a feed-forward artificial neural network used for signature classification of a single entry
    /// </summary>
    public class Classifier {
        int[] hiddenLayers;
        MCvANN_MLP_TrainParams trainParameters;

        /// <summary>
        /// Initializes a new classifier
        /// </summary>
        /// <param name="Entry">The entry of the classifier</param>
        public Classifier(Entry Entry) {
            trainParameters = new MCvANN_MLP_TrainParams();
            trainParameters.train_method = ANN_MLP_TRAIN_METHOD.BACKPROP;
            trainParameters.term_crit = new MCvTermCriteria(1000);
            trainParameters.bp_dw_scale = 0.01;
            trainParameters.bp_moment_scale = 0.01;


            this.Entry = Entry;
            HiddenLayers = new int[] { 10 };
        }

        /// <summary>
        /// Trains the classifier using the given genuine signatures and feature sets of forged signatures
        /// </summary>
        /// <param name="Genuines">The genuine signatures</param>
        /// <param name="ForgedFeatureSets">The feature sets of the forged signatures</param>
        public void Train(Signature[] Genuines, FeatureSet[] ForgedFeatureSets) {
            Signature[] Forgeries = new Signature[ForgedFeatureSets.Length];
            for (int i = 0; i < Forgeries.Length; i++) {
                Forgeries[i] = new Signature() { Features = ForgedFeatureSets[i] };
            }
            Train(Genuines, Forgeries);
        }

        /// <summary>
        /// Trains the classifier using the given genuine and forged signatures
        /// </summary>
        /// <param name="Genuines">The genuine signatures</param>
        /// <param name="Forgeries">The forged signatures</param>
        public void Train(Signature[] Genuines, Signature[] Forgeries) {
            float[,] normalizedFeatures = NormalizeFeatureVectors(Genuines.Concat(Forgeries).ToArray());
            Matrix<float> TrainData = new Matrix<float>(normalizedFeatures);
            Matrix<float> Predictions = new Matrix<float>(normalizedFeatures.GetLength(0), 1);

            //Fill expected outputs
            Predictions.Data = new float[Genuines.Length + Forgeries.Length, 1];
            for (int i = 0; i < Genuines.Length; i++) {
                Predictions.Data[i, 0] = 1;
            }
            for (int i = Genuines.Length; i < Predictions.Data.GetLength(0); i++) {
                Predictions.Data[i, 0] = -1;
            }


            Matrix<int> Structure = GetNetworkStructure(TrainData);
            using (ANN_MLP network = new ANN_MLP(Structure, ANN_MLP_ACTIVATION_FUNCTION.SIGMOID_SYM, 1.0, 1.0)) {
                FileInfo externalNetwork = GetNetworkPath(this.Entry);
                if (File.Exists(externalNetwork.FullName)) {
                    File.Delete(externalNetwork.FullName); //re-training
                }

                if (!Directory.Exists(externalNetwork.Directory.FullName)) {
                    Directory.CreateDirectory(externalNetwork.Directory.FullName);
                }

                network.Train(TrainData, Predictions, null, trainParameters, ANN_MLP_TRAINING_FLAG.DEFAULT);
                network.Save(externalNetwork.FullName);
            }
        }

        /// <summary>
        /// Runs the neural network using the entry's signature
        /// </summary>
        /// <returns>The acceptance ratio</returns>
        public float? Run() {
            float[,] normalizedFeatures = NormalizeFeatureVectors(Entry.Signature);
            Matrix<float> Samples = new Matrix<float>(normalizedFeatures);
            Matrix<float> Outputs = new Matrix<float>(1, 1);

            Matrix<int> Structure = GetNetworkStructure(Samples);
            using (ANN_MLP network = new ANN_MLP(Structure, ANN_MLP_ACTIVATION_FUNCTION.SIGMOID_SYM, 1.0, 1.0)) {
                FileInfo externalNetwork = GetNetworkPath(this.Entry);
                if (!File.Exists(externalNetwork.FullName)) {
                    return (null); //Unknown Entry
                }

                network.Load(externalNetwork.FullName);
                network.Predict(Samples, Outputs); //If there's an error here it's because the loaded network has different number of inputs/outputs than Samples/Predictions
                return (Outputs.Data[0, 0]);
            }
        }


        /// <summary>
        /// Returns the network's structure given the training data
        /// </summary>
        /// <param name="TrainData">A matrix containing the training data of the network</param>
        /// <returns></returns>
        protected Matrix<int> GetNetworkStructure(Matrix<float> TrainData) {
            //Structure of network
            Matrix<int> Layers = new Matrix<int>(1, 2 + HiddenLayers.Length);
            Layers.Data[0, 0] = TrainData.Data.GetLength(1); //Input Layer
            for (int i = 0; i < HiddenLayers.Length; i++) {
                Layers.Data[0, i + 1] = HiddenLayers[i]; //Hidden Layers
            }
            Layers.Data[0, 1 + HiddenLayers.Length] = 1; //Output Layer

            return (Layers);
        }


        /// <summary>
        /// Returns the path of the neural network based on the given entry
        /// </summary>
        /// <param name="Entry">The entry</param>
        /// <returns></returns>
        public static FileInfo GetNetworkPath(Entry Entry) {
            string fileName = ConfigurationManager.AppSettings["NetworksName"].Replace("{NAME}", Entry.Name.ToLower().Replace(" ", "").Replace(",", "")).Replace("{ID}", Entry.ID.Replace("*", "")) + ConfigurationManager.AppSettings["NetworksExtension"];
            string directory = AppDomain.CurrentDomain.BaseDirectory + ConfigurationManager.AppSettings["NetworksLocation"];
            return (new FileInfo(directory + "\\" + fileName));
        }

        /// <summary>
        /// Normalizes the feature vectors of the given signatures
        /// </summary>
        /// <param name="Signatures">The signatures</param>
        /// <returns>The normalized data to be fed into the neural network</returns>
        public static float[,] NormalizeFeatureVectors(params Signature[] Signatures) {
            if (Signatures.Length == 0) {
                return (new float[0, 0]);
            }

            float[,] data = new float[Signatures.Length, Signatures[0].Features.Size];

            for (int i = 0; i < Signatures.Length; i++) {
                if (Signatures[i].Features == null || Signatures[i].Features.NormalizedData == null) {
                    Signatures[i].ExtractFeatures();
                }
                for (int k = 0; k < Signatures[i].Features.NormalizedData.Length; k++) {
                    data[i, k] = Signatures[i].Features.NormalizedData[k];
                }
            }

            return (data);
        }

        /// <summary>
        /// Gets or sets the number and size of the hidden layers of the network
        /// </summary>
        /// <exception cref="ArgumentException">Thrown when the length of the layers is zero/exception>
        public int[] HiddenLayers {
            get { return hiddenLayers; }
            set {
                if (value.Length == 0) {
                    throw (new ArgumentException("Length of hidden layers cannot be zero"));
                }
                hiddenLayers = value;
            }
        }

        /// <summary>
        /// Gets or sets the entry attached to this classifier
        /// </summary>
        public Entry Entry { get; set; }

        /// <summary>
        /// Gets or sets the network's learning rate
        /// </summary>
        public double LearningRate {
            get { return (trainParameters.bp_dw_scale); }
            set { trainParameters.bp_dw_scale = value; }
        }

        /// <summary>
        /// Gets or sets the network's learning momentum
        /// </summary>
        public double Momentum {
            get { return (trainParameters.bp_moment_scale); }
            set { trainParameters.bp_moment_scale = value; }
        }

        /// <summary>
        /// Gets or sets the maximum amount of epochs
        /// </summary>
        public int MaxIteration {
            get { return (trainParameters.term_crit.max_iter); }
            set { trainParameters.term_crit.max_iter = value; }
        }
    }
}
