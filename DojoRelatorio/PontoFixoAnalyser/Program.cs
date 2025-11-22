using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ZonasInflexaoAnalyzer
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.SetHighDpiMode(HighDpiMode.SystemAware);
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    public class MainForm : Form
    {
        private Button btnUpload;
        private Button btnFiltrar;
        private TextBox txtMin;
        private TextBox txtMax;
        private TextBox txtRange;
        private DataGridView dataGridView1;
        private DataTable rankingTable = new DataTable();
        private List<RankingItem> rankingGlobal = new List<RankingItem>();

        public MainForm()
        {
            this.Text = "Zonas de Inflexão - Ranking de Toques";
            this.Width = 1000;
            this.Height = 600;

            btnUpload = new Button { Text = "Upload CSV", Width = 100 };
            btnFiltrar = new Button { Text = "Filtrar", Width = 100 };
            txtMin = new TextBox { PlaceholderText = "Preço Mínimo", Width = 100 };
            txtMax = new TextBox { PlaceholderText = "Preço Máximo", Width = 100 };
            txtRange = new TextBox { PlaceholderText = "Range (pontos)", Width = 100 };

            FlowLayoutPanel controls = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 40 };
            controls.Controls.Add(btnUpload);
            controls.Controls.Add(txtMin);
            controls.Controls.Add(txtMax);
            controls.Controls.Add(txtRange);
            controls.Controls.Add(btnFiltrar);

            dataGridView1 = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true, AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill };
            rankingTable.Columns.Add("Tipo");
            rankingTable.Columns.Add("Data Base");
            rankingTable.Columns.Add("Preco");
            rankingTable.Columns.Add("Toques", typeof(int));
            rankingTable.Columns.Add("PrecoInt", typeof(int));
            dataGridView1.DataSource = rankingTable;

            btnUpload.Click += BtnUpload_Click;
            btnFiltrar.Click += BtnFiltrar_Click;

            this.Controls.Add(dataGridView1);
            this.Controls.Add(controls);
        }

        private void BtnUpload_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "CSV files (*.csv)|*.csv";

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                var lines = File.ReadAllLines(openFileDialog.FileName);
                var candles = new List<Candle>();

                bool isTradingView = lines[0].ToLower().Contains("time") || lines[0].ToLower().StartsWith("time,open,high");

                foreach (var line in lines.Skip(1))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    var parts = line.Split(isTradingView ? ',' : ';');

                    try
                    {
                        if (isTradingView && parts.Length >= 5 && long.TryParse(parts[0], out long timestamp))
                        {
                            candles.Add(new Candle
                            {
                                Date = DateTimeOffset.FromUnixTimeSeconds(long.Parse(parts[0])).DateTime,
                                Open = float.Parse(parts[1], CultureInfo.InvariantCulture),
                                High = float.Parse(parts[2], CultureInfo.InvariantCulture),
                                Low = float.Parse(parts[3], CultureInfo.InvariantCulture),
                                Close = float.Parse(parts[4], CultureInfo.InvariantCulture)
                            });
                        }
                        else if (!isTradingView && parts.Length >= 5)
                        {
                            var culture = new CultureInfo("pt-BR");
                            candles.Add(new Candle
                            {
                                Date = DateTime.Parse(parts[1]),
                                Open = float.Parse(parts[2], NumberStyles.Any, culture),
                                High = float.Parse(parts[3], NumberStyles.Any, culture),
                                Low = float.Parse(parts[4], NumberStyles.Any, culture),
                                Close = float.Parse(parts[5], NumberStyles.Any, culture)
                            });
                        }
                    }
                    catch (Exception ex){
                        Console.WriteLine(ex.ToString());
                    }
                }

                rankingGlobal = BuildRanking(candles);
                AtualizarTabela(rankingGlobal);
            }
        }

        private void BtnFiltrar_Click(object sender, EventArgs e)
        {
            bool hasMin = float.TryParse(txtMin.Text.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out float min);
            bool hasMax = float.TryParse(txtMax.Text.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out float max);
            bool hasRange = float.TryParse(txtRange.Text.Replace(",", "."), NumberStyles.Any, CultureInfo.InvariantCulture, out float range);

            var filtrado = rankingGlobal
                .Where(r => (!hasMin || r.Price >= min) && (!hasMax || r.Price <= max))
                .OrderByDescending(r => r.Touches)
                .ThenByDescending(r => r.Price)
                .ToList();

            if (hasRange && range > 0)
            {
                var finalList = new List<RankingItem>();
                float? ultimaLinha = null;

                foreach (var item in filtrado)
                {
                    if (ultimaLinha == null || Math.Abs(item.Price - ultimaLinha.Value) >= range)
                    {
                        finalList.Add(item);
                        ultimaLinha = item.Price;
                    }
                }

                AtualizarTabela(finalList);
            }
            else
            {
                AtualizarTabela(filtrado);
            }
        }

        private void AtualizarTabela(List<RankingItem> lista)
        {
            rankingTable.Clear();
            foreach (var row in lista)
            {
                rankingTable.Rows.Add(row.Type, row.BaseDate.ToShortDateString(), row.Price.ToString("F2"), row.Touches, (int)Math.Round(row.Price));
            }
        }

        private List<RankingItem> BuildRanking(List<Candle> candles)
        {
            var ranking = new List<RankingItem>();

            for (int i = 0; i < candles.Count; i++)
            {
                var baseCandle = candles[i];
                float high = baseCandle.High;
                float low = baseCandle.Low;
                int highTouches = 0;
                int lowTouches = 0;

                for (int j = 0; j < candles.Count; j++)
                {
                    if (i == j) continue;
                    var c = candles[j];

                    if (IsTouch(high, c)) highTouches++;
                    if (IsTouch(low, c)) lowTouches++;
                }

                ranking.Add(new RankingItem { Type = "High", BaseDate = baseCandle.Date, Price = high, Touches = highTouches });
                ranking.Add(new RankingItem { Type = "Low", BaseDate = baseCandle.Date, Price = low, Touches = lowTouches });
            }

            return ranking;
        }

        private bool IsTouch(float line, Candle candle)
        {
            float upperShadowMin = Math.Max(candle.Open, candle.Close);
            float lowerShadowMax = Math.Min(candle.Open, candle.Close);

            bool upperTouch = upperShadowMin < line && line <= candle.High;
            bool lowerTouch = candle.Low <= line && line < lowerShadowMax;
            bool borderTouch = line == candle.Open || line == candle.Close;

            return upperTouch || lowerTouch || borderTouch;
        }
    }

    public class Candle
    {
        public DateTime Date { get; set; }
        public float Open { get; set; }
        public float High { get; set; }
        public float Low { get; set; }
        public float Close { get; set; }
    }

    public class RankingItem
    {
        public string Type { get; set; }
        public DateTime BaseDate { get; set; }
        public float Price { get; set; }
        public int Touches { get; set; }
    }
}
