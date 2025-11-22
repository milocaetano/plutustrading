using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace ZonasInflexaoAnalyzer
{
    public partial class MainForm : Form
    {
        private DataTable rankingTable = new DataTable();

        public MainForm()
        {
            InitializeComponent();
            rankingTable.Columns.Add("Tipo");
            rankingTable.Columns.Add("Data Base");
            rankingTable.Columns.Add("Preco");
            rankingTable.Columns.Add("Toques");
            dataGridView1.DataSource = rankingTable;
        }

        private void btnUpload_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "CSV files (*.csv)|*.csv";

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                var lines = File.ReadAllLines(openFileDialog.FileName);
                var candles = new List<Candle>();

                foreach (var line in lines.Skip(1))
                {
                    var parts = line.Split(';');
                    if (parts.Length >= 5)
                    {
                        candles.Add(new Candle
                        {
                            Date = DateTime.Parse(parts[1]),
                            Open = float.Parse(parts[2].Replace(",", ".")),
                            High = float.Parse(parts[3].Replace(",", ".")),
                            Low = float.Parse(parts[4].Replace(",", ".")),
                            Close = float.Parse(parts[5].Replace(",", "."))
                        });
                    }
                }

                var ranking = BuildRanking(candles);
                rankingTable.Clear();

                foreach (var row in ranking.OrderByDescending(r => r.Touches))
                {
                    rankingTable.Rows.Add(row.Type, row.BaseDate.ToShortDateString(), row.Price.ToString("F2"), row.Touches);
                }
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
