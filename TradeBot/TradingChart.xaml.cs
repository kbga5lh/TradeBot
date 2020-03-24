﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using Tinkoff.Trading.OpenApi.Network;
using Tinkoff.Trading.OpenApi.Models;
using System.Windows.Controls;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System.Windows.Input;
using System.Windows.Media;

namespace TradeBot
{
    /// <summary>
    /// Логика взаимодействия для TradingChart.xaml
    /// </summary>
    public partial class TradingChart : UserControl
    {
        public Context context;
        public MarketInstrument activeStock;

        public CandleInterval candleInterval = CandleInterval.Minute;
        private List<Indicator> indicators = new List<Indicator>();

        private CandleStickSeries candlesSeries;
        private ScatterSeries buySeries;
        private ScatterSeries sellSeries;

        private LinearAxis xAxis;
        private LinearAxis xAxis1;
        private LinearAxis yAxis;
        private LinearAxis yAxis1;

        private List<DateTime> candlesDates = new List<DateTime>();

        private Queue<List<Indicator.Signal>> lastSignals = new Queue<List<Indicator.Signal>>(3);

        public DateTime LastCandleDate { get; private set; } // on the right side
        public DateTime FirstCandleDate { get; private set; } // on the left side

        private int loadedCandles = 0;
        private int candlesLoadsFailed = 0;

        public PlotModel model;

        public Task LoadingCandlesTask { get; private set; }

        #region IntervalToMaxPeriod

        public static readonly Dictionary<CandleInterval, TimeSpan> intervalToMaxPeriod
            = new Dictionary<CandleInterval, TimeSpan>
        {
            { CandleInterval.Minute,        TimeSpan.FromDays(1)},
            { CandleInterval.FiveMinutes,   TimeSpan.FromDays(1)},
            { CandleInterval.QuarterHour,   TimeSpan.FromDays(1)},
            { CandleInterval.HalfHour,      TimeSpan.FromDays(1)},
            { CandleInterval.Hour,          TimeSpan.FromDays(7).Add(TimeSpan.FromHours(-1))},
            { CandleInterval.Day,           TimeSpan.FromDays(364)},
            { CandleInterval.Week,          TimeSpan.FromDays(364*2)},
            { CandleInterval.Month,         TimeSpan.FromDays(364*10)},
        };

        public TimeSpan GetPeriod(CandleInterval interval)
        {
            TimeSpan result;
            if (!intervalToMaxPeriod.TryGetValue(interval, out result))
                throw new KeyNotFoundException();
            return result;
        }

        #endregion

        public TradingChart()
        {
            InitializeComponent();

            LastCandleDate = FirstCandleDate = DateTime.Now - GetPeriod(candleInterval);

            model = new PlotModel
            {
                TextColor = OxyColor.FromArgb(140, 0, 0, 0),
                PlotAreaBorderColor = OxyColor.FromArgb(10, 0, 0, 0),
                LegendPosition = LegendPosition.LeftTop,
            };

            yAxis = new LinearAxis // y axis (left)
            {
                Position = AxisPosition.Left,
                IsPanEnabled = false,
                IsZoomEnabled = false,
                MajorGridlineThickness = 0,
                MinorGridlineThickness = 0,
                MajorGridlineColor = OxyColor.FromArgb(10, 0, 0, 0),
                MajorGridlineStyle = LineStyle.Solid,
                TicklineColor = OxyColor.FromArgb(10, 0, 0, 0),
                TickStyle = TickStyle.Outside,
            };

            xAxis = new LinearAxis // x axis (bottom)
            {
                Position = AxisPosition.Bottom,
                MajorGridlineStyle = LineStyle.Solid,
                TicklineColor = OxyColor.FromArgb(10, 0, 0, 0),
                TickStyle = TickStyle.Outside,
                MaximumRange = 200,
                MinimumRange = 15,
                AbsoluteMinimum = -10,
                EndPosition = 0,
                StartPosition = 1,
                MajorGridlineThickness = 2,
                MinorGridlineThickness = 0,
                MajorGridlineColor = OxyColor.FromArgb(10, 0, 0, 0),
            };

            model.Axes.Add(yAxis);
            model.Axes.Add(xAxis);

            candlesSeries = new CandleStickSeries
            {
                Title = "Candles",
                DecreasingColor = OxyColor.FromRgb(230, 63, 60),
                IncreasingColor = OxyColor.FromRgb(45, 128, 32),
                StrokeThickness = 1,
            };

            buySeries = new ScatterSeries
            {
                Title = "Buy",
                MarkerType = MarkerType.Circle,
                MarkerFill = OxyColor.FromRgb(207, 105, 255),
                MarkerStroke = OxyColor.FromRgb(55, 55, 55),
                MarkerStrokeThickness = 1,
                MarkerSize = 6,
            };

            sellSeries = new ScatterSeries
            {
                Title = "Sell",
                MarkerType = MarkerType.Circle,
                MarkerFill = OxyColor.FromRgb(255, 248, 82),
                MarkerStroke = OxyColor.FromRgb(55, 55, 55),
                MarkerStrokeThickness = 1,
                MarkerSize = 6,
            };

            model.Series.Add(candlesSeries);
            model.Series.Add(buySeries);
            model.Series.Add(sellSeries);

            xAxis.LabelFormatter = delegate (double d)
            {
                if (candlesSeries.Items.Count > (int)d && d >= 0)
                {
                    switch (candleInterval)
                    {
                        case CandleInterval.Minute:
                        case CandleInterval.TwoMinutes:
                        case CandleInterval.ThreeMinutes:
                        case CandleInterval.FiveMinutes:
                        case CandleInterval.TenMinutes:
                        case CandleInterval.QuarterHour:
                        case CandleInterval.HalfHour:
                            return candlesDates[(int)d].ToString("HH:mm");
                        case CandleInterval.Hour:
                        case CandleInterval.TwoHours:
                        case CandleInterval.FourHours:
                        case CandleInterval.Day:
                        case CandleInterval.Week:
                            return candlesDates[(int)d].ToString("dd MMMM");
                        case CandleInterval.Month:
                            return candlesDates[(int)d].ToString("yyyy");
                    }
                }
                return "";
            };
            xAxis.AxisChanged += XAxis_AxisChanged;

            plotView.Model = model;

            plotView.ActualController.BindMouseDown(OxyMouseButton.Left, PlotCommands.PanAt);
            plotView.ActualController.BindMouseDown(OxyMouseButton.Right, PlotCommands.SnapTrack);

            plotView1.Model = new PlotModel
            {
                TextColor = OxyColor.FromArgb(140, 0, 0, 0),
                PlotAreaBorderColor = OxyColor.FromArgb(10, 0, 0, 0),
                LegendPosition = LegendPosition.LeftTop,
            };

            yAxis1 = new LinearAxis // y axis (left)
            {
                Position = AxisPosition.Left,
                IsPanEnabled = false,
                IsZoomEnabled = false,
                MajorGridlineThickness = 0,
                MinorGridlineThickness = 0,
                MajorGridlineColor = OxyColor.FromArgb(10, 0, 0, 0),
                MajorGridlineStyle = LineStyle.Solid,
                TicklineColor = OxyColor.FromArgb(10, 0, 0, 0),
                TickStyle = TickStyle.Outside,
            };
            yAxis1.Zoom(-1, 1);

            xAxis1 = new LinearAxis // x axis (bottom)
            {
                Position = AxisPosition.Bottom,
                MajorGridlineStyle = LineStyle.None,
                MinorGridlineStyle = LineStyle.None,
                TicklineColor = OxyColor.FromArgb(10, 0, 0, 0),
                TickStyle = TickStyle.None,
                EndPosition = 0,
                StartPosition = 1,
                MajorGridlineColor = OxyColor.FromArgb(10, 0, 0, 0),
            };

            plotView1.ActualController.UnbindAll();

            plotView1.Model.Axes.Add(xAxis1);
            plotView1.Model.Axes.Add(yAxis1);

            DataContext = this;
        }

        private async void XAxis_AxisChanged(object sender, AxisChangedEventArgs e)
        {
            if (LoadingCandlesTask == null || !LoadingCandlesTask.IsCompleted)
                return;

            LoadingCandlesTask = LoadMoreCandlesAndUpdateSeries();
            await LoadingCandlesTask;

            AdjustYExtent(xAxis, yAxis, model);
            xAxis1.Zoom(xAxis.ActualMinimum, xAxis.ActualMaximum);
            AdjustYExtent(xAxis1, yAxis1, xAxis1.PlotModel, false);
            xAxis1.PlotModel.PlotView.InvalidatePlot();
        }

        private async Task LoadMoreCandlesAndUpdateSeries()
        {
            bool loaded = false;
            while (loadedCandles < xAxis.ActualMaximum && candlesLoadsFailed < 10)
            {
                await LoadMoreCandles();
                loaded = true;
            }
            if (loaded)
            {
                foreach (var indicator in indicators)
                    indicator.UpdateSeries();
                AdjustYExtent(xAxis, yAxis, model);
                AdjustYExtent(xAxis1, yAxis1, xAxis1.PlotModel, false);
            }
            plotView.InvalidatePlot();
            xAxis1.PlotModel.PlotView.InvalidatePlot();
        }

        public async void ResetSeries()
        {
            buySeries.Points.Clear();
            sellSeries.Points.Clear();

            candlesSeries.Items.Clear();
            candlesDates.Clear();

            loadedCandles = 0;
            candlesLoadsFailed = 0;
            LastCandleDate = DateTime.Now - GetPeriod(candleInterval);
            FirstCandleDate = LastCandleDate;

            foreach (var indicator in indicators)
            {
                indicator.ResetSeries();
            }

            LoadingCandlesTask = LoadMoreCandlesAndUpdateSeries();
            await LoadingCandlesTask;

            xAxis.Zoom(0, 75);

            plotView.InvalidatePlot();
            xAxis1.PlotModel.PlotView.InvalidatePlot();
        }

        private async Task LoadMoreCandles()
        {
            if (activeStock == null || context == null ||
                candlesLoadsFailed >= 10 ||
                loadedCandles > xAxis.ActualMaximum + 100)
                return;

            var period = GetPeriod(candleInterval);
            var candles = await GetCandles(activeStock.Figi, FirstCandleDate, candleInterval, period);
            FirstCandleDate -= period;
            if (candles.Count == 0)
            {
                candlesLoadsFailed += 1;
                return;
            }

            for (int i = 0; i < candles.Count; ++i)
            {
                var candle = candles[i];
                candlesSeries.Items.Add(CandleToHighLowItem(loadedCandles + i, candle));
                candlesDates.Add(candle.Time);
            }
            loadedCandles += candles.Count;

            candlesLoadsFailed = 0;
        }

        public async Task UpdateTestingSignals()
        {
            buySeries.Points.Clear();
            sellSeries.Points.Clear();
            lastSignals.Clear();

            await Task.Factory.StartNew(() =>
            {
                for (int i = candlesSeries.Items.Count - 1; i >= 0; --i)
                {
                    UpdateSignals(i);
                }
            });
            plotView.InvalidatePlot();
        }

        public void UpdateRealTimeSignals()
        {
            UpdateSignals(0);
            plotView.InvalidatePlot();
            xAxis1.PlotModel.PlotView.InvalidatePlot();
        }

        private void UpdateSignals(int i)
        {
            var candle = candlesSeries.Items[i];
            var signals = new List<Indicator.Signal>();

            if (lastSignals.Count >= 3)
                lastSignals.Dequeue();
            lastSignals.Enqueue(signals);

            foreach (var indicator in indicators)
            {
                var rawSignal = indicator.GetSignal(i);

                if (rawSignal.HasValue)
                {
                    var signal = rawSignal.Value;
                    signals.Add(signal);
                }
            }

            float value = 0.0f;
            float multiplier = 1.0f;
            foreach (var signalsList in lastSignals)
            {
                foreach (var signal in signalsList)
                {
                    if (signal.type == Indicator.Signal.SignalType.Buy)
                        value += signal.weight * multiplier;
                    else
                        value -= signal.weight * multiplier;
                }
                multiplier /= 2;
            }
            if (value > 0)
                buySeries.Points.Add(new ScatterPoint(i, candle.Close, Math.Abs(value / indicators.Count) * 12));
            else if (value < 0)
                sellSeries.Points.Add(new ScatterPoint(i, candle.Close, Math.Abs(value / indicators.Count) * 12));
        }

        public void AddIndicator(Indicator indicator)
        {
            indicator.priceIncrement = (double)activeStock.MinPriceIncrement;
            indicator.candles = candlesSeries.Items;
            indicators.Add(indicator);

            if (indicator.GetType() == typeof(MACD))
                indicator.InitializeSeries(xAxis1.PlotModel.Series);
            else
                indicator.InitializeSeries(model.Series);

            indicator.UpdateSeries();
            AdjustYExtent(xAxis, yAxis, model);
            AdjustYExtent(xAxis1, yAxis1, xAxis1.PlotModel, false);

            plotView.InvalidatePlot();
            xAxis1.PlotModel.PlotView.InvalidatePlot();
        }

        public void RemoveIndicators()
        {
            foreach (var indicator in indicators)
            {
                if (indicator.GetType() == typeof(MACD))
                    indicator.RemoveSeries(xAxis1.PlotModel.Series);
                else
                    indicator.RemoveSeries(model.Series);
            }
            indicators = new List<Indicator>();
            AdjustYExtent(xAxis, yAxis, model);
            AdjustYExtent(xAxis1, yAxis1, xAxis1.PlotModel, false);
            plotView.InvalidatePlot();
            xAxis1.PlotModel.PlotView.InvalidatePlot();
        }

        private TimeSpan CandleIntervalToTimeSpan(CandleInterval interval)
        {
            switch (interval)
            {
                case CandleInterval.Minute:
                    return TimeSpan.FromMinutes(1);
                case CandleInterval.TwoMinutes:
                    return TimeSpan.FromMinutes(2);
                case CandleInterval.ThreeMinutes:
                    return TimeSpan.FromMinutes(3);
                case CandleInterval.FiveMinutes:
                    return TimeSpan.FromMinutes(5);
                case CandleInterval.TenMinutes:
                    return TimeSpan.FromMinutes(10);
                case CandleInterval.QuarterHour:
                    return TimeSpan.FromMinutes(15);
                case CandleInterval.HalfHour:
                    return TimeSpan.FromMinutes(30);
                case CandleInterval.Hour:
                    return TimeSpan.FromMinutes(60);
                case CandleInterval.TwoHours:
                    return TimeSpan.FromHours(2);
                case CandleInterval.FourHours:
                    return TimeSpan.FromHours(4);
                case CandleInterval.Day:
                    return TimeSpan.FromDays(1);
                case CandleInterval.Week:
                    return TimeSpan.FromDays(7);
                case CandleInterval.Month:
                    return TimeSpan.FromDays(31);
            }
            throw new ArgumentOutOfRangeException();
        }

        public async Task LoadNewCandles()
        {
            var candles = await GetCandles(activeStock.Figi, LastCandleDate + CandleIntervalToTimeSpan(candleInterval), candleInterval, CandleIntervalToTimeSpan(candleInterval));
            LastCandleDate += CandleIntervalToTimeSpan(candleInterval);
            if (candles.Count == 0)
                return;

            var c = new List<HighLowItem>();
            var cd = new List<DateTime>();
            for (int i = 0; i < candles.Count; ++i)
            {
                var candle = candles[i];
                c.Add(CandleToHighLowItem(i, candle));
                cd.Add(candle.Time);
            }
            candlesSeries.Items.ForEach((v) => v.X += candles.Count);
            candlesSeries.Items.InsertRange(0, c);
            candlesDates.InsertRange(0, cd);
            foreach (var indicator in indicators)
            {
                indicator.UpdateSeries();
                indicator.OnNewCandlesAdded(candles.Count);
            }
            loadedCandles += candles.Count;

            AdjustYExtent(xAxis, yAxis, model);
            plotView.InvalidatePlot();

            // move buy points by candles.Count
            var s = new List<ScatterPoint>(buySeries.Points.Count);
            foreach (var point in buySeries.Points)
                s.Add(new ScatterPoint(point.X + candles.Count, point.Y, point.Size, point.Value, point.Tag));
            buySeries.Points.Clear();
            buySeries.Points.AddRange(s);

            // move sell points by candles.Count
            s = new List<ScatterPoint>(sellSeries.Points.Count);
            foreach (var point in sellSeries.Points)
                s.Add(new ScatterPoint(point.X + candles.Count, point.Y, point.Size, point.Value, point.Tag));
            sellSeries.Points.Clear();
            sellSeries.Points.AddRange(s);

            UpdateRealTimeSignals();

            plotView.InvalidatePlot();
            AdjustYExtent(xAxis1, yAxis1, xAxis1.PlotModel, false);
            xAxis1.PlotModel.PlotView.InvalidatePlot();
        }

        private void AdjustYExtent(LinearAxis x, LinearAxis y, PlotModel m, bool includeCandles = true)
        {
            var ptlist = new List<HighLowItem>();
            if (includeCandles)
            {
                ptlist = candlesSeries.Items.FindAll(p => p.X >= x.ActualMinimum && p.X <= x.ActualMaximum);
                if (ptlist.Count == 0)
                    return;
            }

            var lplist = new List<DataPoint>();
            var hplist = new List<HistogramItem>();

            foreach (var series in m.Series)
            {
                if (series.GetType() == typeof(LineSeries))
                    lplist.AddRange((series as LineSeries).Points.FindAll(p => p.X >= x.ActualMinimum && p.X <= x.ActualMaximum));
                if (series.GetType() == typeof(HistogramSeries))
                    hplist.AddRange((series as HistogramSeries).Items.FindAll(p => p.RangeStart >= x.ActualMinimum && p.RangeStart <= x.ActualMaximum));
            }

            double ymin = double.MaxValue;
            double ymax = double.MinValue;

            if (includeCandles)
            {
                for (int i = 0; i < ptlist.Count; ++i)
                {
                    ymin = Math.Min(ymin, ptlist[i].Low);
                    ymax = Math.Max(ymax, ptlist[i].High);
                }
            }

            for (int i = 0; i < lplist.Count; ++i)
            {
                ymin = Math.Min(ymin, lplist[i].Y);
                ymax = Math.Max(ymax, lplist[i].Y);
            }

            for (int i = 0; i < hplist.Count; ++i)
            {
                ymin = Math.Min(ymin, hplist[i].Value);
                ymax = Math.Max(ymax, hplist[i].Value);
            }

            if (ymin == double.MaxValue || ymax == double.MinValue)
                return;

            var extent = ymax - ymin;
            var margin = extent * 0.1;

            y.IsZoomEnabled = true;
            y.Zoom(ymin - margin, ymax + margin);
            y.IsZoomEnabled = false;
        }

        public static HighLowItem CandleToHighLowItem(double x, CandlePayload candlePayload)
        {
            return new HighLowItem(x, (double)candlePayload.High, (double)candlePayload.Low, (double)candlePayload.Open, (double)candlePayload.Close);
        }

        public async Task<List<CandlePayload>> GetCandles(string figi, DateTime to, CandleInterval interval, TimeSpan queryOffset)
        {
            var result = new List<CandlePayload>();
            var candles = await context.MarketCandlesAsync(figi, to - queryOffset, to, interval);

            for (int i = 0; i < candles.Candles.Count; ++i)
                result.Add(candles.Candles[i]);

            result.Reverse();
            return result;
        }

        // ================================
        // =========  Events ==============
        // ================================

        private void MovingAverage_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new MovingAverageDialog();
            if (dialog.ShowDialog() == true)
            {
                IMACalculation calculationMethod;
                switch (dialog.Type)
                {
                    case MovingAverageDialog.CalculationMethod.Simple:
                        calculationMethod = new SimpleMACalculation();
                        break;
                    case MovingAverageDialog.CalculationMethod.Exponential:
                        calculationMethod = new ExponentialMACalculation();
                        break;
                    default:
                        calculationMethod = new SimpleMACalculation();
                        break;
                }
                AddIndicator(new MovingAverage(dialog.Period, dialog.Offset, calculationMethod));
            }
        }

        private void RemoveIndicators_Click(object sender, RoutedEventArgs e)
        {
            RemoveIndicators();
        }

        private void MACD_Click(object sender, RoutedEventArgs e)
        {
            AddIndicator(new MACD(new ExponentialMACalculation(), 12, 26, 9));
        }
    }
}
