using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.ML;
using Microsoft.ML.Data;

namespace HeightPrediction
{

    class BuildingFeatures
    {
        [LoadColumn(0)]
        public long id;

        [LoadColumn(1)]
        public float groundArea;

        [LoadColumn(2)]
        public float perimeter;

        [LoadColumn(3)]
        public float normPerimeterIndex;

        [LoadColumn(4)]
        public float nFloors;

        [LoadColumn(5)]
        public float netInternalSurface;

        [LoadColumn(6)]
        public float nNeighbors;

        [LoadColumn(7)]
        public float length;

        [LoadColumn(8)]
        public float width;

        [LoadColumn(9)]
        public string type;

        [LoadColumn(10)]
        public float height;
    }

    class HeightRegression
    {
        [ColumnName("Score")]
        public float height;
    }

    class HeightPredictor
    {

        private readonly string trainingPath;
        private readonly string testPath;
        private readonly string heightsPath;

        public HeightPredictor(string trainingPath, string testPath, string heightsPath)
        {
            this.trainingPath = trainingPath;
            this.testPath = testPath;
            this.heightsPath = heightsPath;
        }

        public void Start()
        {
            MLContext mlContext = new MLContext(seed: 0);
            var model = Train(mlContext);
            var dataView = Evaluate(mlContext, model);
            Predict(mlContext, model, dataView);
        }

        private ITransformer Train(MLContext context)
        {
            if (trainingPath == null)
                return null;
            IDataView dataView = context.Data.LoadFromTextFile<BuildingFeatures>(trainingPath, separatorChar: ',');
            var pipeline = context.Transforms.CopyColumns(outputColumnName: "Label", inputColumnName: "height")
                .Append(context.Transforms.Categorical.OneHotEncoding(outputColumnName: "typeEncoded", inputColumnName: "type"))
                .Append(context.Transforms.Concatenate("Features", "groundArea", "perimeter", "normPerimeterIndex", "netInternalSurface", "nNeighbors", "length", "width", "typeEncoded"))
                .Append(context.Regression.Trainers.FastTree());
            var model = pipeline.Fit(dataView);
            return model;
        }

        private IDataView Evaluate(MLContext context, ITransformer model)
        {
            if (testPath == null)
                return null;
            IDataView dataView = context.Data.LoadFromTextFile<BuildingFeatures>(testPath, separatorChar: ',');
            var predictions = model.Transform(dataView);
            var metrics = context.Regression.Evaluate(predictions, "Label", "Score");
            Console.WriteLine($"*************************************************");
            Console.WriteLine($"*       Model quality metrics evaluation         ");
            Console.WriteLine($"*------------------------------------------------");
            Console.WriteLine($"*       RSquared Score:      {metrics.RSquared:0.##}");
            Console.WriteLine($"*       Root Mean Squared Error:      {metrics.RootMeanSquaredError:#.##}");
            return dataView;
        }

        private void Predict(MLContext context, ITransformer model, IDataView dataView)
        {
            var features = context.Data.CreateEnumerable<BuildingFeatures>(dataView, false);
            var predictionFunc = context.Model.CreatePredictionEngine<BuildingFeatures, HeightRegression>(model);
            try
            {
                StreamWriter writer = new StreamWriter(heightsPath);
                foreach (var feature in features)
                {
                    var prediction = predictionFunc.Predict(feature);
                    writer.WriteLine($"{feature.id}: {prediction.height}");
                }
                writer.Close();
            }
            catch (Exception exc)
            {
                Console.WriteLine(exc.Message);
            }
        }

    }

}
