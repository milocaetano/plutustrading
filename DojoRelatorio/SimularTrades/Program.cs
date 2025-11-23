using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace SimularTrades
{
    class Program
    {
        static void Main()
        {
            try
            {
                string entradasPath = @"C:\Relatorios\operacoes.csv";         // Arquivo com suas operações
                string historicoPath = @"C:\Relatorios\WINFUT_F_0_1min.csv";  // Arquivo com histórico de 1 min
                string resultadoPath = @"C:\Relatorios\resultado.csv";        // Arquivo de saída

                var entradas = LerEntradas(entradasPath);
                var historico = LerHistoricoProfit(historicoPath);
                var historicoPorDia = IndexarHistoricoPorDia(historico);

                if (entradas.Count == 0)
                {
                    Console.WriteLine("[ERRO] Nenhuma entrada foi carregada. Verifique o arquivo operacoes.csv");
                    return;
                }

                if (historico.Count == 0)
                {
                    Console.WriteLine("[ERRO] Nenhum candle foi carregado. Verifique o arquivo WINFUT_F_0_1min.csv");
                    return;
                }

                Console.WriteLine($"Entradas carregadas: {entradas.Count}");
                Console.WriteLine($"Candles carregados: {historico.Count}");

                var estrategias = GerarEstrategias(10000); // quantidade de estratégias a testar

                var resultados = new List<string>
                {
                    "StopLoss;Parcial1;Parcial2;TaxaAcerto;TaxaGainParcial;TaxaGainTotal;TaxaPrejuizo;LucroMedio;LucroTotal"
                };

                foreach (var estrategia in estrategias)
                {
                    Console.WriteLine($"\nTestando Estratégia: SL={estrategia.StopLoss}, P1={estrategia.Parcial1}, P2={estrategia.Parcial2}");
                    var resultado = TestarEstrategia(estrategia, entradas, historicoPorDia);
                    resultados.Add(resultado);
                }

                File.WriteAllLines(resultadoPath, resultados);
                Console.WriteLine("Backtest concluído. Resultados salvos em resultado.csv");
            }
            catch (Exception ex)
            {
                Console.WriteLine("ERRO GERAL:");
                Console.WriteLine(ex);
            }
        }

        // =========================================================
        // HELPERS GERAIS
        // =========================================================

        static int ParsePreco(string precoBruto)
        {
            if (string.IsNullOrWhiteSpace(precoBruto))
                return 0;

            precoBruto = precoBruto.Trim();
            string semMilhar = precoBruto.Replace(".", "");
            string parteInteira = semMilhar.Split(',')[0];
            return int.Parse(parteInteira);
        }

        // =========================================================
        // LEITURA DE ARQUIVOS
        // =========================================================

        static List<Trade> LerEntradas(string path)
        {
            var linhas = File.ReadAllLines(path);
            var trades = new List<Trade>();

            foreach (var linha in linhas.Skip(3))
            {
                if (string.IsNullOrWhiteSpace(linha))
                    continue;

                var dados = linha.Split(';');
                if (dados.Length < 8)
                    continue;

                try
                {
                    DateTime dataHora = DateTime.ParseExact(
                        dados[1].Trim(),
                        "dd/MM/yyyy HH:mm:ss",
                        CultureInfo.InvariantCulture
                    );

                    string tipo = dados[6].Trim().ToUpperInvariant(); // "C" ou "V"
                    int precoCompra = ParsePreco(dados[7]);
                    int precoVenda = dados.Length > 8 ? ParsePreco(dados[8]) : 0;

                    int precoEntrada = tipo == "C"
                        ? precoCompra
                        : (precoVenda != 0 ? precoVenda : precoCompra);

                    int qtdCompra = int.Parse(dados[4].Trim());
                    int qtdVenda = int.Parse(dados[5].Trim());
                    int quantidade = qtdCompra > 0 ? qtdCompra : qtdVenda;

                    if (quantidade <= 0)
                    {
                        Console.WriteLine($"[AVISO] Trade em {dataHora} com quantidade zero. Linha ignorada.");
                        continue;
                    }

                    trades.Add(new Trade
                    {
                        DataHora = dataHora,
                        Tipo = tipo,
                        PrecoEntrada = precoEntrada,
                        Quantidade = quantidade
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AVISO] Erro ao processar linha: {linha}");
                    Console.WriteLine($"Detalhes: {ex.Message}");
                }
            }

            return trades;
        }

        static List<Candle> LerHistoricoProfit(string path)
        {
            var linhas = File.ReadAllLines(path);
            var candles = new List<Candle>();

            foreach (var linha in linhas.Skip(1))
            {
                if (string.IsNullOrWhiteSpace(linha))
                    continue;

                try
                {
                    var dados = linha.Split(';');
                    if (dados.Length < 7)
                        continue;

                    DateTime dataHora = DateTime.ParseExact(
                        $"{dados[1].Trim()} {dados[2].Trim()}",
                        "dd/MM/yyyy HH:mm:ss",
                        CultureInfo.InvariantCulture
                    );

                    int precoAbertura = ParsePreco(dados[3]);
                    int precoMaximo = ParsePreco(dados[4]);
                    int precoMinimo = ParsePreco(dados[5]);
                    int precoFechamento = ParsePreco(dados[6]);

                    candles.Add(new Candle
                    {
                        DataHora = dataHora,
                        PrecoAbertura = precoAbertura,
                        PrecoMaximo = precoMaximo,
                        PrecoMinimo = precoMinimo,
                        PrecoFechamento = precoFechamento
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[AVISO] Erro ao processar linha do histórico: {linha}");
                    Console.WriteLine($"Detalhes: {ex.Message}");
                }
            }

            return candles;
        }

        static Dictionary<DateTime, List<Candle>> IndexarHistoricoPorDia(List<Candle> candles)
        {
            return candles
                .GroupBy(c => c.DataHora.Date)
                .ToDictionary(
                    g => g.Key,
                    g => g.OrderBy(c => c.DataHora).ToList()
                );
        }

        // =========================================================
        // GERAÇÃO DE ESTRATÉGIAS
        // =========================================================

        static List<Estrategia> GerarEstrategias(int numeroEstrategias = 100)
        {
            var estrategias = new List<Estrategia>();
            var rand = new Random();

            for (int i = 0; i < numeroEstrategias; i++)
            {
                int parcial1 = rand.Next(50, 300) / 5 * 5;
                int parcial2 = rand.Next(parcial1 + 5, 701) / 5 * 5;

                estrategias.Add(new Estrategia
                {
                    StopLoss = rand.Next(100, 500) / 5 * 5,
                    Parcial1 = parcial1,
                    Parcial2 = parcial2
                });
            }

            return estrategias;
        }

        // =========================================================
        // BACKTEST DA ESTRATÉGIA
        // =========================================================

        static string TestarEstrategia(
            Estrategia estrategia,
            List<Trade> trades,
            Dictionary<DateTime, List<Candle>> historicoPorDia)
        {
            int acertos = 0;
            int gainsParciais = 0;
            int gainsTotais = 0;
            int perdas = 0;
            int totalTradesSimulados = 0;

            decimal lucroTotal = 0m;
            const decimal valorPorPonto = 0.2m;

            foreach (var trade in trades)
            {
                DateTime dia = trade.DataHora.Date;

                if (!historicoPorDia.TryGetValue(dia, out var candlesDoDia) || candlesDoDia.Count == 0)
                {
                    Console.WriteLine($"[ERRO] Nenhum candle encontrado para o dia {dia:dd/MM/yyyy}. Trade {trade.DataHora} ignorado.");
                    continue;
                }

                DateTime tradeTime = trade.DataHora;
                DateTime tradeTimeWithoutSeconds = new DateTime(
                    tradeTime.Year,
                    tradeTime.Month,
                    tradeTime.Day,
                    tradeTime.Hour,
                    tradeTime.Minute,
                    0
                );

                var candlesTrade = candlesDoDia
                    .Where(c => c.DataHora >= tradeTimeWithoutSeconds)
                    .ToList();

                if (candlesTrade.Count == 0)
                {
                    var ultimoCandle = candlesDoDia.Last();
                    int precoEntrada = trade.PrecoEntrada;
                    int precoSaida = ultimoCandle.PrecoFechamento;

                    int pontos = trade.Tipo == "C"
                        ? precoSaida - precoEntrada
                        : precoEntrada - precoSaida;

                    decimal lucroGap = pontos * trade.Quantidade * valorPorPonto;

                    Console.WriteLine($"[AVISO] Trade {trade.DataHora} após último candle do dia. Fechando no fechamento {ultimoCandle.DataHora} com lucro R${lucroGap:F2}.");

                    totalTradesSimulados++;
                    if (lucroGap > 0) acertos++;
                    else if (lucroGap < 0) perdas++;

                    lucroTotal += lucroGap;
                    continue;
                }

                int precoEntradaCorrigido = trade.PrecoEntrada;
                var primeiroCandle = candlesTrade.First();

                if (precoEntradaCorrigido < primeiroCandle.PrecoMinimo ||
                    precoEntradaCorrigido > primeiroCandle.PrecoMaximo)
                {
                    int diferencaParaMinima = primeiroCandle.PrecoMinimo - precoEntradaCorrigido;
                    int diferencaParaMaxima = precoEntradaCorrigido - primeiroCandle.PrecoMaximo;
                    int diferencaMaisProxima = precoEntradaCorrigido < primeiroCandle.PrecoMinimo
                        ? diferencaParaMinima
                        : diferencaParaMaxima;

                    if (diferencaMaisProxima <= 50)
                    {
                        int novoPreco = precoEntradaCorrigido < primeiroCandle.PrecoMinimo
                            ? primeiroCandle.PrecoMinimo
                            : primeiroCandle.PrecoMaximo;

                        Console.WriteLine($"[AJUSTE] Preço de entrada {precoEntradaCorrigido} ajustado para {novoPreco} (diferença {diferencaMaisProxima} dentro do range {primeiroCandle.PrecoMinimo}-{primeiroCandle.PrecoMaximo})");
                        precoEntradaCorrigido = novoPreco;
                    }
                    else
                    {
                        Console.WriteLine($"[AVISO] Entrada {precoEntradaCorrigido} fora do range ({primeiroCandle.PrecoMinimo}-{primeiroCandle.PrecoMaximo}) do primeiro candle {primeiroCandle.DataHora}. Trade ignorado.");
                        continue;
                    }
                }

                int qtdTotal = trade.Quantidade;
                if (qtdTotal <= 0)
                {
                    Console.WriteLine($"[AVISO] Trade {trade.DataHora} com quantidade zero. Ignorado.");
                    continue;
                }

                int qtdP1 = qtdTotal / 2;
                int qtdResto = qtdTotal - qtdP1;

                bool executouP1 = false;
                bool tradeEncerrado = false;

                decimal pnlP1 = 0m;
                decimal pnlResto = 0m;
                int precoSaidaFinal = precoEntradaCorrigido;

                int stopPrice, p1Price, p2Price;

                if (trade.Tipo == "C")
                {
                    stopPrice = precoEntradaCorrigido - estrategia.StopLoss;
                    p1Price = precoEntradaCorrigido + estrategia.Parcial1;
                    p2Price = precoEntradaCorrigido + estrategia.Parcial2;
                }
                else if (trade.Tipo == "V")
                {
                    stopPrice = precoEntradaCorrigido + estrategia.StopLoss;
                    p1Price = precoEntradaCorrigido - estrategia.Parcial1;
                    p2Price = precoEntradaCorrigido - estrategia.Parcial2;
                }
                else
                {
                    Console.WriteLine($"[AVISO] Tipo inválido '{trade.Tipo}' em {trade.DataHora}. Ignorando trade.");
                    continue;
                }

                foreach (var candle in candlesTrade)
                {
                    bool atingiuStop = trade.Tipo == "C"
                        ? candle.PrecoMinimo <= stopPrice
                        : candle.PrecoMaximo >= stopPrice;

                    bool atingiuP1 = trade.Tipo == "C"
                        ? candle.PrecoMaximo >= p1Price
                        : candle.PrecoMinimo <= p1Price;

                    bool atingiuP2 = trade.Tipo == "C"
                        ? candle.PrecoMaximo >= p2Price
                        : candle.PrecoMinimo <= p2Price;

                    if (!executouP1)
                    {
                        if (atingiuStop && atingiuP1)
                        {
                            int pontos = trade.Tipo == "C"
                                ? stopPrice - precoEntradaCorrigido
                                : precoEntradaCorrigido - stopPrice;

                            pnlResto = pontos * qtdTotal * valorPorPonto;
                            precoSaidaFinal = stopPrice;
                            tradeEncerrado = true;

                            Console.WriteLine($"[STOP LOSS] (SL prioritário c/ P1 mesmo candle) {trade.DataHora} saída {precoSaidaFinal} lucro R${(pnlP1 + pnlResto):F2}");
                            break;
                        }

                        if (atingiuStop)
                        {
                            int pontos = trade.Tipo == "C"
                                ? stopPrice - precoEntradaCorrigido
                                : precoEntradaCorrigido - stopPrice;

                            pnlResto = pontos * qtdTotal * valorPorPonto;
                            precoSaidaFinal = stopPrice;
                            tradeEncerrado = true;

                            Console.WriteLine($"[STOP LOSS] {trade.DataHora} saída {precoSaidaFinal} lucro R${(pnlP1 + pnlResto):F2}");
                            break;
                        }

                        if (atingiuP1 && qtdP1 > 0)
                        {
                            int pontosP1 = trade.Tipo == "C"
                                ? p1Price - precoEntradaCorrigido
                                : precoEntradaCorrigido - p1Price;

                            pnlP1 = pontosP1 * qtdP1 * valorPorPonto;
                            executouP1 = true;

                            Console.WriteLine($"[P1] {trade.DataHora} saída P1 {p1Price} lucro parcial R${pnlP1:F2}");
                        }
                        else if (atingiuP1 && qtdP1 == 0)
                        {
                            Console.WriteLine($"[INFO] P1 atingido sem lote disponível (contratos={qtdTotal}). Parcial ignorada.");
                        }
                    }
                    else
                    {
                        if (atingiuStop && atingiuP2)
                        {
                            int pontos = trade.Tipo == "C"
                                ? stopPrice - precoEntradaCorrigido
                                : precoEntradaCorrigido - stopPrice;

                            pnlResto = pontos * qtdResto * valorPorPonto;
                            precoSaidaFinal = stopPrice;
                            tradeEncerrado = true;

                            Console.WriteLine($"[STOP LOSS] após P1 (mesmo candle c/ P2) {trade.DataHora} saída {precoSaidaFinal} lucro R${(pnlP1 + pnlResto):F2}");
                            break;
                        }

                        if (atingiuStop)
                        {
                            int pontos = trade.Tipo == "C"
                                ? stopPrice - precoEntradaCorrigido
                                : precoEntradaCorrigido - stopPrice;

                            pnlResto = pontos * qtdResto * valorPorPonto;
                            precoSaidaFinal = stopPrice;
                            tradeEncerrado = true;

                            Console.WriteLine($"[STOP LOSS] após P1 {trade.DataHora} saída {precoSaidaFinal} lucro R${(pnlP1 + pnlResto):F2}");
                            break;
                        }

                        if (atingiuP2)
                        {
                            int pontosP2 = trade.Tipo == "C"
                                ? p2Price - precoEntradaCorrigido
                                : precoEntradaCorrigido - p2Price;

                            pnlResto = pontosP2 * qtdResto * valorPorPonto;
                            precoSaidaFinal = p2Price;
                            tradeEncerrado = true;
                            gainsTotais++;

                            Console.WriteLine($"[P2] {trade.DataHora} saída P2 {precoSaidaFinal} lucro total R${(pnlP1 + pnlResto):F2}");
                            break;
                        }
                    }

                    if (candle == candlesTrade.Last())
                    {
                        int precoClose = candle.PrecoFechamento;
                        int pontosRestante = trade.Tipo == "C"
                            ? precoClose - precoEntradaCorrigido
                            : precoEntradaCorrigido - precoClose;

                        if (!executouP1)
                        {
                            pnlResto = pontosRestante * qtdTotal * valorPorPonto;
                        }
                        else
                        {
                            pnlResto = pontosRestante * qtdResto * valorPorPonto;
                            if (qtdP1 > 0) // só conta ganho parcial real
                                gainsParciais++;
                        }

                        precoSaidaFinal = precoClose;
                        tradeEncerrado = true;

                        Console.WriteLine($"[SAÍDA FIM DO DIA] {trade.DataHora} saída {precoSaidaFinal} lucro R${(pnlP1 + pnlResto):F2}");
                    }
                }

                if (!tradeEncerrado)
                {
                    Console.WriteLine($"[ERRO] Trade {trade.DataHora} terminou sem encerramento explícito.");
                    continue;
                }

                decimal lucroFinanceiro = pnlP1 + pnlResto;
                totalTradesSimulados++;

                if (lucroFinanceiro > 0) acertos++;
                else if (lucroFinanceiro < 0) perdas++;

                lucroTotal += lucroFinanceiro;

                Console.WriteLine($"Resultado final da operação: R${lucroFinanceiro:F2}\n");
            }

            decimal taxaAcerto = totalTradesSimulados > 0 ? (decimal)acertos / totalTradesSimulados * 100m : 0m;
            decimal taxaGainParcial = totalTradesSimulados > 0 ? (decimal)gainsParciais / totalTradesSimulados * 100m : 0m;
            decimal taxaGainTotal = totalTradesSimulados > 0 ? (decimal)gainsTotais / totalTradesSimulados * 100m : 0m;
            decimal taxaPrejuizo = totalTradesSimulados > 0 ? (decimal)perdas / totalTradesSimulados * 100m : 0m;
            decimal lucroMedio = totalTradesSimulados > 0 ? lucroTotal / totalTradesSimulados : 0m;

            Console.WriteLine("\n===== RESULTADO DA ESTRATÉGIA =====");
            Console.WriteLine($"Estratégia: SL={estrategia.StopLoss}, P1={estrategia.Parcial1}, P2={estrategia.Parcial2}");
            Console.WriteLine("-----------------------------------");
            Console.WriteLine($"Total de Trades: {totalTradesSimulados}");
            Console.WriteLine($"Taxa de Acerto: {taxaAcerto:F2}%");
            Console.WriteLine($"Taxa de Prejuízo: {taxaPrejuizo:F2}%");
            Console.WriteLine($"Taxa de Gain Parcial: {taxaGainParcial:F2}%");
            Console.WriteLine($"Taxa de Gain Total: {taxaGainTotal:F2}%");
            Console.WriteLine($"Lucro Médio por Trade: R${lucroMedio:F2}");
            Console.WriteLine($"Lucro Total: R${lucroTotal:F2}");
            Console.WriteLine("===================================\n");

            return $"{estrategia.StopLoss};{estrategia.Parcial1};{estrategia.Parcial2};{taxaAcerto:F2}%;{taxaGainParcial:F2}%;{taxaGainTotal:F2}%;{taxaPrejuizo:F2}%;R${lucroMedio:F2};R${lucroTotal:F2}";
        }
    }

    class Trade
    {
        public DateTime DataHora { get; set; }
        public string Tipo { get; set; } // "C" ou "V"
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
    }
}