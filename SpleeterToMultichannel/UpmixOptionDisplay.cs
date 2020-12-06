using System;
using System.Windows.Data;

namespace SpleeterToMultichannel {
    public class UpmixOptionDisplay : IValueConverter {
        static readonly string[] values = new string[] {
            "Center",
            "Front (L/R)",
            "Screen (LCR)",
            "Quadro (side)",
            "Quadro (rear)",
            "Mid-side (screen)",
            "Mid-side (full)",
            "Full",
            "Skip"
        };

        public object Convert(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            if (value is UpmixOption) {
                if ((int)value < values.Length)
                    return values[(int)value];
                return value.ToString();
            }
            return "Invalid value";
        }

        public object ConvertBack(object value, Type targetType, object parameter, System.Globalization.CultureInfo culture) {
            if (value is string) {
                for (int i = 0; i < values.Length; ++i)
                    if (values[i].Equals(value))
                        return (UpmixOption)i;
            }
            return UpmixOption.Skip;
        }
    }
}