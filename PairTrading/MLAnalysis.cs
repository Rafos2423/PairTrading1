using Microsoft.ML;
using Microsoft.ML.Transforms.TimeSeries;

namespace PairTrading
{
    public class MLAnalysis
    {
        public float[] CalculateForecast(double[] inputData)
        {
            var data = inputData.Select(x => new Data { Value = (float)x });

            var context = new MLContext();
            IDataView dataView = context.Data.LoadFromEnumerable(data);

            var pipeline = context.Forecasting.ForecastBySsa(
                "Forecast",
                nameof(Data.Value),
                windowSize: (int)Math.Sqrt(inputData.Length),
                seriesLength: inputData.Length,
                trainSize: (int)Math.Pow(inputData.Length, 2),
                horizon: (int)Math.Sqrt(inputData.Length));

            var model = pipeline.Fit(dataView);

            var forecastingEngine = model.CreateTimeSeriesEngine<Data, DataForecast>(context);

            return forecastingEngine.Predict().Forecast;
        }
    }

    public class Data
    {
        public float Value { get; set; }
    }

    public class DataForecast
    {
        public float[] Forecast { get; set; }
    }
}
