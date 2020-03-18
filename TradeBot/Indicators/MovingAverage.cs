﻿using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using Tinkoff.Trading.OpenApi.Models;

namespace TradeBot
{
    //--- Покупка
    //Если ордер на покупку есть
    //    Если цена выше ордера на покупку
    //        Покупка
    //        Поставить стоп-лосс на 10 пунктов ниже MA

    //--- Продажа
    //Если инструмент куплен и стоп-лосс сработал
    //    Продажа
    //Если сигнал к продаже на открытии следующей свечи стоит
    //    Продажа

    //--- Обновление
    //Если инструмент куплен
    //    Если цена закрытия ниже MA
    //        Установить сигнал к продаже на открытии следующей свечи
    //Иначе
    //    Если ордер на покупку есть
    //        Если цена ниже ордера и дальше ордера на 10 свечей
    //            Удалить ордер на покупку
    //    Иначе
    //        Если свеча большая и пробила MA вверх
    //            Поставить ордер на 2 пункта выше от максимума
    public class MovingAverage : Indicator
    {
        public enum Type
        {
            Simple,
            Exponential,
        }

        private int period;
        private int offset;
        private Type type;

        private LineSeries bindedGraph;

        private int? boughtCandle;
        private int? whenToSellIndex;
        private decimal? whenToBuyPrice;
        private int? whenToBuyPriceSetIndex;
        private decimal? stopLoss;

        public override int CandlesNeeded
        {
            get
            {
                if (type == Type.Simple)
                    return candlesSpan + period;
                else if (type == Type.Exponential)
                    return candlesSpan;
                return candlesSpan;
            }
        }

        public MovingAverage(int period, int offset, Type type)
        {
            if (period < 1 || offset < 0)
                throw new ArgumentOutOfRangeException();

            this.period = period;
            this.offset = offset;
            this.type = type;
        }

        override public bool IsBuySignal(int rawCandleIndex)
        {
            try
            {
                int candlesStartIndex = Candles.Count - candlesSpan;
                int candleIndex = candlesStartIndex + rawCandleIndex;

                if (whenToBuyPrice != null)
                {
                    if (Candles[candleIndex].Close > whenToBuyPrice)
                    {
                        boughtCandle = candleIndex;
                        whenToBuyPrice = null;
                        whenToBuyPriceSetIndex = null;
                        stopLoss = (decimal)bindedGraph.Points[rawCandleIndex].Y - priceIncrement * 10;
                        return true;
                    }
                }
                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        override public bool IsSellSignal(int rawCandleIndex)
        {
            try
            {
                int candlesStartIndex = Candles.Count - candlesSpan;
                int candleIndex = candlesStartIndex + rawCandleIndex;

                if (Candles[candleIndex].Close < stopLoss ||
                    whenToSellIndex == candleIndex)
                {
                    boughtCandle = null;
                    stopLoss = null;
                    whenToSellIndex = null;
                    return true;
                }

                return false;
            }
            catch (Exception)
            {
                return false;
            }
        }

        override public void UpdateState(int rawCandleIndex)
        {
            try
            {
                int candlesStartIndex = Candles.Count - candlesSpan;
                int candleIndex = candlesStartIndex + rawCandleIndex;

                if (boughtCandle != null)
                {
                    if (whenToSellIndex == null && Candles[candleIndex].Close < (decimal)bindedGraph.Points[rawCandleIndex].Y)
                    {
                        whenToSellIndex = candleIndex + 1;
                    }
                }
                else
                {
                    if (whenToBuyPrice != null)
                    {
                        if (Candles[candleIndex].Close < whenToBuyPrice &&
                            candleIndex - whenToBuyPriceSetIndex > 10)
                        {
                            whenToBuyPrice = null;
                            whenToBuyPriceSetIndex = null;
                        }
                    }
                    else
                    {
                        bool isCandleBig = true;
                        decimal candleSize = Math.Abs(Candles[candleIndex].Close - Candles[candleIndex - 1].Close);
                        for (int i = 1; i < offset + 1; ++i)
                        {
                            decimal thisCandleSize = Math.Abs(Candles[candleIndex - i].Close - Candles[candleIndex - i - 1].Close);
                            if (thisCandleSize > candleSize)
                                isCandleBig = false;
                        }

                        if (isCandleBig &&
                            ((Candles[candleIndex - 1].Close - (decimal)bindedGraph.Points[rawCandleIndex - 1].Y) *
                                (Candles[candleIndex].Close - (decimal)bindedGraph.Points[rawCandleIndex].Y) < 0) &&
                            Candles[candleIndex].Close > (decimal)bindedGraph.Points[rawCandleIndex].Y)
                        {
                            whenToBuyPrice = Candles[candleIndex].High + priceIncrement * 2;
                            whenToBuyPriceSetIndex = candleIndex;
                        }
                    }
                }
            }
            catch (Exception) { }
        }

        private List<decimal> CalculateSMA()
        {
            var SMA = new List<decimal>(candlesSpan);
            for (int i = Candles.Count - candlesSpan; i < Candles.Count; ++i)
            {
                decimal sum = 0;
                for (int j = 0; j < period; ++j)
                    sum += Candles[i - j].Close;
                SMA.Add(sum / period);
            }
            return SMA;
        }

        private List<decimal> CalculateEMA()
        {
            var EMA = new List<decimal>(candlesSpan);
            double multiplier = 2.0 / (period + 1.0);
            EMA.Add(Candles[Candles.Count - candlesSpan].Close);
            for (int i = 1; i < candlesSpan; ++i)
            {
                EMA.Add((Candles[Candles.Count - candlesSpan + i].Close * (decimal)multiplier) + EMA[i - 1] * (1 - (decimal)multiplier));
            }
            return EMA;
        }

        override public void UpdateSeries()
        {
            List<decimal> values = new List<decimal>();
            if (type == Type.Simple)
                values = CalculateSMA();
            else if (type == Type.Exponential)
                values = CalculateEMA();
            bindedGraph.Points.Clear();
            for (int i = 0; i < values.Count; ++i)
                bindedGraph.Points.Add(new DataPoint(i, (double)values[i]));
        }

        override public void InitializeSeries(ElementCollection<Series> series)
        {
            string title = string.Empty;
            if (type == Type.Simple)
                title = string.Format("Simple Moving Average {0}", period);
            else if (type == Type.Exponential)
                title = string.Format("Exponential Moving Average {0}", period);

            bindedGraph = new LineSeries
            {
                Title = title,
            };
            series.Add(bindedGraph);
            areGraphsInitialized = true;
        }

        public override void ResetState()
        {
            boughtCandle = null;
            whenToSellIndex = null;
            whenToBuyPrice = null;
            whenToBuyPriceSetIndex = null;
            stopLoss = null;
        }

        public override void RemoveSeries(ElementCollection<Series> series)
        {
            if (bindedGraph != null)
                series.Remove(bindedGraph);
        }
    }
}
