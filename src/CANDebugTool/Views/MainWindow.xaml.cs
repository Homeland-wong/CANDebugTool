using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using CANDebugTool.ViewModels;

namespace CANDebugTool.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            Application.Current.Shutdown();
        }

        /// <summary>
        /// Hex 输入框自动格式化：每两位十六进制字符后插入 · 分隔
        /// </summary>
        private void HexInput_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is not TextBox tb) return;

            string oldText = tb.Text;
            // 计算光标在原始字符中的位置（跳过 · 分隔符）
            int caretFormatted = tb.CaretIndex;
            int rawPos = 0;
            for (int i = 0; i < caretFormatted && i < oldText.Length; i++)
            {
                if (oldText[i] != '·')
                    rawPos++;
            }

            // 提取纯 hex 字符
            string raw = oldText.Replace("·", "").ToUpper();
            raw = Regex.Replace(raw, "[^0-9A-F]", "");
            if (string.IsNullOrEmpty(raw))
            {
                if (oldText != "") tb.Text = "";
                return;
            }

            // 分组格式化：12·34·56
            var groups = Enumerable.Range(0, (raw.Length + 1) / 2)
                .Select(i => raw.Substring(i * 2, Math.Min(2, raw.Length - i * 2)));
            string formatted = string.Join("·", groups);

            if (oldText != formatted)
            {
                tb.Text = formatted;
                // 计算格式化后光标位置：rawPos + 前面的分隔符数量
                tb.CaretIndex = Math.Min(rawPos + rawPos / 2, formatted.Length);
            }
        }
    }
}
