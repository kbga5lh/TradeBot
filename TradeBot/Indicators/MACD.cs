﻿using System;
using OxyPlot;
using OxyPlot.Series;

namespace TradeBot
{
    internal class Macd : Indicator
    {
        readonly int differencePeriod;
        readonly int longPeriod;
        readonly int shortPeriod;
        readonly IMaCalculation movingAverageCalculation;
        
        LineSeries longMaSeries;
        LineSeries macdSeries;

        LineSeries shortMaSeries;
        HistogramSeries signalSeries;

        ElementCollection<Series> chart;

        public override bool IsOscillator => true;

        public Macd(IMaCalculation calculationMethod, int shortPeriod, int longPeriod, int differencePeriod)
        {
            if (shortPeriod < 1 || longPeriod < 1 || differencePeriod < 1 ||
                shortPeriod >= longPeriod)
                throw new ArgumentOutOfRangeException();

            movingAverageCalculation = calculationMethod ?? throw new ArgumentNullException();
            this.shortPeriod = shortPeriod;
            this.longPeriod = longPeriod;
            this.differencePeriod = differencePeriod;
        }

        public override void UpdateSeries()
        {
            //movingAverageCalculation.Calculate(index => candles[index].Close, candles.Count, shortPeriod,
            //    shortMaSeries);
            //movingAverageCalculation.Calculate(index => candles[index].Close, candles.Count, longPeriod, longMaSeries);

            //macdSeries.Points.Clear();
            //for (var i = 0; i < candles.Count - longPeriod; ++i)
            //    macdSeries.Points.Add(new DataPoint(i, shortMaSeries.Points[i].Y - longMaSeries.Points[i].Y));

            //movingAverageCalculation.Calculate(index => macdSeries.Points[index].Y, macdSeries.Points.Count,
            //    differencePeriod, signalSeries);
        }

        public override void InitializeSeries(ElementCollection<Series> chart)
        {
            if (AreSeriesInitialized)
                return;

            shortMaSeries = new LineSeries();
            longMaSeries = new LineSeries();
            macdSeries = new LineSeries
            {
                Title = "MACD"
            };
            signalSeries = new HistogramSeries
            {
                Title = "MACD Signal Line"
            };

            this.chart = chart;

            this.chart.Add(signalSeries);
            this.chart.Add(macdSeries);
        }

        public override void RemoveSeries()
        {
            chart.Remove(signalSeries);
            chart.Remove(macdSeries);
        }

        public override void ResetSeries()
        {
            shortMaSeries.Points.Clear();
            longMaSeries.Points.Clear();
            signalSeries.Items.Clear();
            macdSeries.Points.Clear();
        }

        public override void OnNewCandlesAdded(int count)
        {
        }

        public override Signal? GetSignal(int currentCandleIndex)
        {
            if (currentCandleIndex > macdSeries.Points.Count - 2 || currentCandleIndex > signalSeries.Items.Count - 2)
                return null;

            if ((macdSeries.Points[currentCandleIndex + 1].Y - signalSeries.Items[currentCandleIndex + 1].Value) *
                (macdSeries.Points[currentCandleIndex].Y - signalSeries.Items[currentCandleIndex].Value) < 0)
                return macdSeries.Points[currentCandleIndex].Y > signalSeries.Items[currentCandleIndex].Value
                    ? new Signal(Signal.Type.Buy, 1.0f)
                    : new Signal(Signal.Type.Sell, 1.0f);

            return null;
        }
    }
}