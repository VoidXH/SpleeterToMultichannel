using System;
using System.Windows.Controls;

namespace SpleeterToMultichannel {
    public class UpmixComboBox : ComboBox {
        public UpmixOption Option { get; set; }

        public UpmixComboBox() {
            ItemsSource = Enum.GetValues(typeof(UpmixOption));
        }
    }
}