﻿using System.Windows;

namespace TradeBot
{
    /// <summary>
    ///     Логика взаимодействия для MovingAverageDialog.xaml
    /// </summary>
    public partial class MovingAverageDialog : Window
    {
        public enum CalculationMethod
        {
            Simple,
            Exponential
        }

        public MovingAverageDialog()
        {
            InitializeComponent();

            TypeComboBox.Items.Add(CalculationMethod.Simple.ToString());
            TypeComboBox.Items.Add(CalculationMethod.Exponential.ToString());
            TypeComboBox.SelectedIndex = 0;
        }

        public int Period { get; private set; }
        public int Offset { get; private set; }
        public CalculationMethod Type { get; private set; }

        void addButton_Click(object sender, RoutedEventArgs e)
        {
            PeriodErrorTextBlock.Text = string.Empty;
            OffsetErrorTextBlock.Text = string.Empty;
            if (!int.TryParse(PeriodTextBox.Text.Trim(), out var period))
            {
                PeriodErrorTextBlock.Text = "* Not a number";
                PeriodTextBox.Focus();
                return;
            }

            if (period < 1)
            {
                PeriodErrorTextBlock.Text = "* Value should be >= 1";
                PeriodTextBox.Focus();
                return;
            }

            if (!int.TryParse(OffsetTextBox.Text.Trim(), out var offset))
            {
                OffsetErrorTextBlock.Text = "* Not a number";
                OffsetTextBox.Focus();
                return;
            }

            if (offset < 0)
            {
                OffsetErrorTextBlock.Text = "* Value should be positive";
                OffsetTextBox.Focus();
                return;
            }

            Period = period;
            Offset = offset;
            Type = (CalculationMethod) TypeComboBox.SelectedIndex;
            DialogResult = true;
        }

        void cancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}