﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradeBot
{
    class SimpleMovingAverage : IIndicator
    {
        private int period;
        public int Period => period;

        public List<decimal> closures;

        public List<decimal> SMA;

        public SimpleMovingAverage(int period)
        {
            if (period < 1)
                throw new ArgumentOutOfRangeException();
            this.period = period;
        }

        public bool IsBuySignal()
        {
            throw new NotImplementedException();
        }

        public bool IsSellSignal()
        {
            throw new NotImplementedException();
        }

        public void UpdateState()
        {
            Calculate();
        }

        private void Calculate()
        {
            var SMA = new List<decimal>(closures.Count - period);
            for (int i = period; i < closures.Count; ++i)
            {
                decimal sum = 0;
                for (int j = 0; j < period; ++j)
                    sum += closures[i - j];
                SMA.Add(sum / period);
            }
            this.SMA = SMA;
        }
    }
}
