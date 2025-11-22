using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace SimularTradesTrailingStop
{
    class Program
    {
        static void Main()
        {
            try
            {
                string entradasPath = "C:\\temp\\entradas.csv"; // Arquivo com suas operações
                string historicoPath = "C:\\temp\\WINFUT_F_0_1min.csv"; // Histórico de 1 min
                string resultadoPath = "C:\\temp\\resultado_trailing.csv"; // Arquivo de saída

                var entradas = LerEntradas(entradasPath);
                var historico = LerHistoricoProfit(historicoPath);

                // Estratégia base: 260-195-580
                var estrategiaBase = new Estrategia { StopLoss = 260, Parcial1 = 195, Parcial2 = 580 };

                List<string> resultados = new List<string>();

                // Testar sem trailing stop primeiro
                Console.WriteLine($"\nTestando Estratégia Sem Trailer: SL={estrategiaBase.StopLoss}, P1={estrategiaBase.Parcial1}, P2={estrategiaBase.Parcial2}");
                var resultadoSemTrailer = TestarEstrategiaSemTrailer(estrategiaBase, entradas, historico);
                resultados.Add(resultadoSemTrailer);

                // Gerar estratégias com trailing stop
                var estrategias = GerarEstrategiasTrailing(estrategiaBase);

                foreach (var estrategia in estrategias)
                {
                    Console.WriteLine($"\nTestando Estratégia: SL={estrategia.StopLoss}, P1={estrategia.Parcial1}, P2={estrategia.Parcial2}, TrailerTrigger={estrategia.TrailerTriggerPercentage}%");
                    var resultado = TestarEstrategiaComTrailing(estrategia, entradas, historico);
                    resultados.Add(resultado);
                }

                File.WriteAllLines(resultadoPath, resultados);
                Console.WriteLine("Backtest concluído. Resultados salvos em resultado_trailing.csv");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        static List<Trade> LerEntradas(string path)
        {
            var linhas = File.ReadAllLines(path);
            var trades = new List<Trade>();

            foreach (var linha in linhas)
            {
                var dados = linha.Split(';');
                string precoEntradaFormatado = dados[7].Replace(".", "").Split(',')[0]; // Removendo casas decimais e pontos

                trades.Add(new Trade
                {
                    DataHora = DateTime.ParseExact(dados[1], "dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture),
                    Tipo = dados[6],
                    PrecoEntrada = int.Parse(precoEntradaFormatado),
                    Quantidade = int.Parse(dados[5])
                });
            }
            return trades;
        }

        static List<Candle> LerHistoricoProfit(string path)
        {
            var linhas = File.ReadAllLines(path);
            var candles = new List<Candle>();

            foreach (var linha in linhas.Skip(1)) // Ignorar cabeçalho
            {
                var dados = linha.Split(';');
                DateTime dataHora = DateTime.ParseExact($"{dados[1]} {dados[2]}", "dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture);
                int precoAbertura = int.Parse(dados[3].Replace(".", "").Split(',')[0]);
                int precoMaximo = int.Parse(dados[4].Replace(".", "").Split(',')[0]);
                int precoMinimo = int.Parse(dados[5].Replace(".", "").Split(',')[0]);
                int precoFechamento = int.Parse(dados[6].Replace(".", "").Split(',')[0]);

                candles.Add(new Candle
                {
                    DataHora = dataHora,
                    PrecoAbertura = precoAbertura,
                    PrecoMaximo = precoMaximo,
                    PrecoMinimo = precoMinimo,
                    PrecoFechamento = precoFechamento
                });
            }
            return candles;
        }

        static List<Estrategia> GerarEstrategiasTrailing(Estrategia estrategia)
        {
            var estrategias = new List<Estrategia>();
            for (int trigger = 10; trigger <= 50; trigger += 5) // De 10% a 50%, passo de 5%
            {
                estrategias.Add(new Estrategia
                {
                    StopLoss = estrategia.StopLoss,
                    Parcial1 = estrategia.Parcial1,
                    Parcial2 = estrategia.Parcial2,
                    TrailerTriggerPercentage = trigger
                });
            }
            return estrategias;
        }

        // Método para testar sem trailing stop
        static string TestarEstrategiaSemTrailer(Estrategia estrategia, List<Trade> trades, List<Candle> historico)
        {
            int acertos = 0, gainsParciais = 0, gainsTotais = 0, totalTrades = trades.Count, perdas = 0;
            decimal lucroTotal = 0;

            foreach (var trade in trades)
            {
                Console.WriteLine($"Operação {trade.DataHora}");
                DateTime tradeTime = trade.DataHora;
                DateTime tradeTimeWithoutSeconds = new DateTime(tradeTime.Year, tradeTime.Month, tradeTime.Day, tradeTime.Hour, tradeTime.Minute, 0);

                var candlesTrade = historico
                    .Where(c => c.DataHora.Date == tradeTime.Date && c.DataHora >= tradeTimeWithoutSeconds)
                    .OrderBy(c => c.DataHora)
                    .ToList();

                if (candlesTrade.Count == 0)
                {
                    Console.WriteLine($"[ERRO] Nenhum dado encontrado para o dia {trade.DataHora.Date}");
                    continue;
                }

                var primeiroCandle = candlesTrade.First();
                if (trade.PrecoEntrada < primeiroCandle.PrecoMinimo || trade.PrecoEntrada > primeiroCandle.PrecoMaximo)
                {
                    Console.WriteLine($"[AVISO] Preço de entrada {trade.PrecoEntrada} fora do range ({primeiroCandle.PrecoMinimo} - {primeiroCandle.PrecoMaximo}). Ajustando...");
                    trade.PrecoEntrada = (primeiroCandle.PrecoMaximo + primeiroCandle.PrecoMinimo) / 2;
                }

                decimal precoSaida = trade.PrecoEntrada;
                decimal lucroFinanceiro = 0;
                int contratosParcial1 = trade.Quantidade / 2;
                int contratosParcial2 = trade.Quantidade - contratosParcial1;
                const decimal porcentagemParcial = 0.5m;
                bool p1FoiExecutado = false;
                decimal lucroPontos = 0;

                foreach (var candle in candlesTrade)
                {
                    bool atingiuStopLoss = (trade.Tipo == "C" && candle.PrecoMinimo <= trade.PrecoEntrada - estrategia.StopLoss) ||
                                           (trade.Tipo == "V" && candle.PrecoMaximo >= trade.PrecoEntrada + estrategia.StopLoss);
                    bool atingiuP1 = (trade.Tipo == "C" && candle.PrecoMaximo >= trade.PrecoEntrada + estrategia.Parcial1) ||
                                     (trade.Tipo == "V" && candle.PrecoMinimo <= trade.PrecoEntrada - estrategia.Parcial1);
                    bool atingiuP2 = (trade.Tipo == "C" && candle.PrecoMaximo >= trade.PrecoEntrada + estrategia.Parcial2) ||
                                     (trade.Tipo == "V" && candle.PrecoMinimo <= trade.PrecoEntrada - estrategia.Parcial2);

                    // Avaliar Stop Loss
                    if (atingiuStopLoss)
                    {
                        precoSaida = trade.Tipo == "C" ? trade.PrecoEntrada - estrategia.StopLoss : trade.PrecoEntrada + estrategia.StopLoss;
                        lucroPontos = trade.Tipo == "C" ? -estrategia.StopLoss : -estrategia.StopLoss;
                        lucroFinanceiro = lucroPontos * trade.Quantidade * 0.2m;
                        if (p1FoiExecutado) lucroFinanceiro += estrategia.Parcial1 * porcentagemParcial * trade.Quantidade * 0.2m;
                        perdas++;
                        Console.WriteLine($"[STOP LOSS] Saída: {precoSaida}, Lucro: {lucroFinanceiro:F2}");
                        break;
                    }

                    // Ativar Parcial 1
                    if (atingiuP1 && !p1FoiExecutado)
                    {
                        p1FoiExecutado = true;
                        lucroPontos = estrategia.Parcial1 * porcentagemParcial;
                        precoSaida = trade.PrecoEntrada + (trade.Tipo == "C" ? lucroPontos : -lucroPontos);
                        lucroFinanceiro = lucroPontos * trade.Quantidade * 0.2m;
                        gainsParciais++;
                        Console.WriteLine($"[P1 GAIN] Saída: {precoSaida}, Lucro Parcial: {lucroFinanceiro:F2}");
                    }

                    // Atingir Parcial 2
                    if (atingiuP2 && p1FoiExecutado)
                    {
                        lucroPontos = estrategia.Parcial1 * porcentagemParcial + estrategia.Parcial2 * porcentagemParcial;
                        precoSaida = trade.PrecoEntrada + (trade.Tipo == "C" ? estrategia.Parcial2 : -estrategia.Parcial2);
                        lucroFinanceiro = lucroPontos * trade.Quantidade * 0.2m;
                        gainsTotais++;
                        Console.WriteLine($"[P2 GAIN] Saída: {precoSaida}, Lucro Total: {lucroFinanceiro:F2}");
                        break;
                    }

                    // Saída no último candle do dia se nada foi atingido
                    if (candle == candlesTrade.Last() && !atingiuStopLoss)
                    {
                        precoSaida = candle.PrecoFechamento;
                        lucroPontos = trade.Tipo == "C" ? precoSaida - trade.PrecoEntrada : trade.PrecoEntrada - precoSaida;
                        lucroFinanceiro = lucroPontos * trade.Quantidade * 0.2m + (p1FoiExecutado ? estrategia.Parcial1 * porcentagemParcial * trade.Quantidade * 0.2m : 0);
                        Console.WriteLine($"[FINAL DO DIA] Saída no último candle: {precoSaida}, Lucro: {lucroFinanceiro:F2}");
                        break;
                    }
                }

                if (lucroFinanceiro > 0) acertos++;
                lucroTotal += lucroFinanceiro;
            }

            decimal taxaAcerto = totalTrades > 0 ? (decimal)acertos / totalTrades * 100 : 0;
            decimal taxaGainParcial = totalTrades > 0 ? (decimal)gainsParciais / totalTrades * 100 : 0;
            decimal taxaGainTotal = totalTrades > 0 ? (decimal)gainsTotais / totalTrades * 100 : 0;
            decimal taxaPrejuizo = totalTrades > 0 ? (decimal)perdas / totalTrades * 100 : 0;
            decimal lucroMedio = totalTrades > 0 ? lucroTotal / totalTrades : 0;

            Console.WriteLine($"\nEstrategia Sem Trailer: SL={estrategia.StopLoss}, P1={estrategia.Parcial1}, P2={estrategia.Parcial2}");
            Console.WriteLine($"Taxa de Acerto: {taxaAcerto:F2}% | Gain Parcial: {taxaGainParcial:F2}% | Gain Total: {taxaGainTotal:F2}% | Prejuízo: {taxaPrejuizo:F2}%");
            Console.WriteLine($"Lucro Médio: R${lucroMedio:F2} | Lucro Total: R${lucroTotal:F2}");

            return $"Estrategia {estrategia.StopLoss}-{estrategia.Parcial1}-{estrategia.Parcial2}-SemTrailer;{taxaAcerto:F2}%;{taxaGainParcial:F2}%;{taxaGainTotal:F2}%;R${lucroMedio:F2};R${lucroTotal:F2}";
        }

        static string TestarEstrategiaComTrailing(Estrategia estrategia, List<Trade> trades, List<Candle> historico)
        {
            int acertos = 0, gainsParciais = 0, gainsTotais = 0, totalTrades = trades.Count, perdas = 0;
            decimal lucroTotal = 0;

            foreach (var trade in trades)
            {
                Console.WriteLine($"Operação {trade.DataHora}");
                DateTime tradeTime = trade.DataHora;
                DateTime tradeTimeWithoutSeconds = new DateTime(tradeTime.Year, tradeTime.Month, tradeTime.Day, tradeTime.Hour, tradeTime.Minute, 0);

                var candlesTrade = historico
                    .Where(c => c.DataHora.Date == tradeTime.Date && c.DataHora >= tradeTimeWithoutSeconds)
                    .OrderBy(c => c.DataHora)
                    .ToList();

                if (candlesTrade.Count == 0)
                {
                    Console.WriteLine($"[ERRO] Nenhum dado encontrado para o dia {trade.DataHora.Date}");
                    continue;
                }

                var primeiroCandle = candlesTrade.First();
                if (trade.PrecoEntrada < primeiroCandle.PrecoMinimo || trade.PrecoEntrada > primeiroCandle.PrecoMaximo)
                {
                    Console.WriteLine($"[AVISO] Preço de entrada {trade.PrecoEntrada} fora do range ({primeiroCandle.PrecoMinimo} - {primeiroCandle.PrecoMaximo}). Ajustando...");
                    trade.PrecoEntrada = (primeiroCandle.PrecoMaximo + primeiroCandle.PrecoMinimo) / 2;
                }

                decimal precoSaida = trade.PrecoEntrada;
                decimal lucroFinanceiro = 0;
                int contratosParcial1 = trade.Quantidade / 2;
                int contratosParcial2 = trade.Quantidade - contratosParcial1;
                const decimal porcentagemParcial = 0.5m;
                bool p1FoiExecutado = false;
                bool trailerAtivado = false;
                int trailingStopLevel = trade.PrecoEntrada - (trade.Tipo == "C" ? estrategia.StopLoss : -estrategia.StopLoss); // Stop inicial
                decimal lucroPontos = 0;

                decimal triggerLevel = estrategia.Parcial2 * (estrategia.TrailerTriggerPercentage / 100m); // Nível de ativação do trailer

                foreach (var candle in candlesTrade)
                {
                    bool atingiuStopLoss = (trade.Tipo == "C" && candle.PrecoMinimo <= trailingStopLevel) ||
                                           (trade.Tipo == "V" && candle.PrecoMaximo >= trailingStopLevel);
                    bool atingiuP1 = (trade.Tipo == "C" && candle.PrecoMaximo >= trade.PrecoEntrada + estrategia.Parcial1) ||
                                     (trade.Tipo == "V" && candle.PrecoMinimo <= trade.PrecoEntrada - estrategia.Parcial1);
                    bool atingiuP2 = (trade.Tipo == "C" && candle.PrecoMaximo >= trade.PrecoEntrada + estrategia.Parcial2) ||
                                     (trade.Tipo == "V" && candle.PrecoMinimo <= trade.PrecoEntrada - estrategia.Parcial2);
                    bool atingiuTrigger = (trade.Tipo == "C" && candle.PrecoMaximo >= trade.PrecoEntrada + triggerLevel) ||
                                          (trade.Tipo == "V" && candle.PrecoMinimo <= trade.PrecoEntrada - triggerLevel);

                    if (atingiuStopLoss)
                    {
                        precoSaida = trailingStopLevel;
                        lucroPontos = trade.Tipo == "C" ? precoSaida - trade.PrecoEntrada : trade.PrecoEntrada - precoSaida;
                        lucroFinanceiro = lucroPontos * trade.Quantidade * 0.2m;
                        if (p1FoiExecutado) lucroFinanceiro += estrategia.Parcial1 * porcentagemParcial * trade.Quantidade * 0.2m;
                        perdas++;
                        Console.WriteLine($"[STOP/TRAILING] Saída: {precoSaida}, Lucro: {lucroFinanceiro:F2}");
                        break;
                    }

                    if (atingiuP1 && !p1FoiExecutado)
                    {
                        p1FoiExecutado = true;
                        lucroPontos = estrategia.Parcial1 * porcentagemParcial;
                        precoSaida = trade.PrecoEntrada + (trade.Tipo == "C" ? lucroPontos : -lucroPontos);
                        lucroFinanceiro = lucroPontos * trade.Quantidade * 0.2m;
                        gainsParciais++;
                        Console.WriteLine($"[P1 GAIN] Saída: {precoSaida}, Lucro Parcial: {lucroFinanceiro:F2}");
                    }

                    if (atingiuTrigger && !trailerAtivado)
                    {
                        trailerAtivado = true;
                        trailingStopLevel = trade.PrecoEntrada; // Move para breakeven
                        Console.WriteLine($"[TRAILER ATIVADO] Trigger {estrategia.TrailerTriggerPercentage}% atingido. Stop movido para breakeven: {trailingStopLevel}");
                    }

                    if (trailerAtivado)
                    {
                        if (trade.Tipo == "C" && candle.PrecoMaximo > trailingStopLevel + estrategia.StopLoss)
                            trailingStopLevel = candle.PrecoMaximo - estrategia.StopLoss;
                        else if (trade.Tipo == "V" && candle.PrecoMinimo < trailingStopLevel - estrategia.StopLoss)
                            trailingStopLevel = candle.PrecoMinimo + estrategia.StopLoss;
                    }

                    if (atingiuP2 && p1FoiExecutado)
                    {
                        lucroPontos = estrategia.Parcial1 * porcentagemParcial + estrategia.Parcial2 * porcentagemParcial;
                        precoSaida = trade.PrecoEntrada + (trade.Tipo == "C" ? estrategia.Parcial2 : -estrategia.Parcial2);
                        lucroFinanceiro = lucroPontos * trade.Quantidade * 0.2m;
                        gainsTotais++;
                        Console.WriteLine($"[P2 GAIN] Saída: {precoSaida}, Lucro Total: {lucroFinanceiro:F2}");
                        break;
                    }

                    if (candle == candlesTrade.Last() && !atingiuStopLoss)
                    {
                        precoSaida = candle.PrecoFechamento;
                        lucroPontos = trade.Tipo == "C" ? precoSaida - trade.PrecoEntrada : trade.PrecoEntrada - precoSaida;
                        lucroFinanceiro = lucroPontos * trade.Quantidade * 0.2m + (p1FoiExecutado ? estrategia.Parcial1 * porcentagemParcial * trade.Quantidade * 0.2m : 0);
                        Console.WriteLine($"[FINAL DO DIA] Saída no último candle: {precoSaida}, Lucro: {lucroFinanceiro:F2}");
                        break;
                    }
                }

                if (lucroFinanceiro > 0) acertos++;
                lucroTotal += lucroFinanceiro;
            }

            decimal taxaAcerto = totalTrades > 0 ? (decimal)acertos / totalTrades * 100 : 0;
            decimal taxaGainParcial = totalTrades > 0 ? (decimal)gainsParciais / totalTrades * 100 : 0;
            decimal taxaGainTotal = totalTrades > 0 ? (decimal)gainsTotais / totalTrades * 100 : 0;
            decimal taxaPrejuizo = totalTrades > 0 ? (decimal)perdas / totalTrades * 100 : 0;
            decimal lucroMedio = totalTrades > 0 ? lucroTotal / totalTrades : 0;

            Console.WriteLine($"\nEstrategia: SL={estrategia.StopLoss}, P1={estrategia.Parcial1}, P2={estrategia.Parcial2}, Trailer={estrategia.TrailerTriggerPercentage}%");
            Console.WriteLine($"Taxa de Acerto: {taxaAcerto:F2}% | Gain Parcial: {taxaGainParcial:F2}% | Gain Total: {taxaGainTotal:F2}% | Prejuízo: {taxaPrejuizo:F2}%");
            Console.WriteLine($"Lucro Médio: R${lucroMedio:F2} | Lucro Total: R${lucroTotal:F2}");

            return $"Estrategia {estrategia.StopLoss}-{estrategia.Parcial1}-{estrategia.Parcial2}-Trailer{estrategia.TrailerTriggerPercentage}%;{taxaAcerto:F2}%;{taxaGainParcial:F2}%;{taxaGainTotal:F2}%;R${lucroMedio:F2};R${lucroTotal:F2}";
        }
    }

    class Trade
    {
        public DateTime DataHora { get; set; }
        public string Tipo { get; set; } // C ou V
        public int PrecoEntrada { get; set; }
        public int Quantidade { get; set; }
    }

    class Candle
    {
        public DateTime DataHora { get; set; }
        public int PrecoAbertura { get; set; }
        public int PrecoMaximo { get; set; }
        public int PrecoMinimo { get; set; }
        public int PrecoFechamento { get; set; }
    }

    class Estrategia
    {
        public int StopLoss { get; set; }
        public int Parcial1 { get; set; }
        public int Parcial2 { get; set; }
        public int TrailerTriggerPercentage { get; set; } // Porcentagem para ativar trailing stop
    }
}