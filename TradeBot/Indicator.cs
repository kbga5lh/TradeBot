﻿using LiveCharts;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tinkoff.Trading.OpenApi.Models;

namespace TradeBot
{
    abstract class Indicator
    {
        public List<CandlePayload> Candles { get; set; }
        public int candlesSpan { get; set; }
        protected bool areGraphsInitialized;
        public bool AreGraphsInitialized { get => areGraphsInitialized; }
        abstract public int candlesNeeded { get; }

        public abstract void UpdateState();
        public abstract bool IsBuySignal();
        public abstract bool IsSellSignal();

        public abstract void UpdateSeries();
        public abstract void InitializeSeries(SeriesCollection series);
    }
}