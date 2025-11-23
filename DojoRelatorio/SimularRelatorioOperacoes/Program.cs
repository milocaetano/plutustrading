using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace SimularRelatorioOperacoes
{
    class Program
    {
        static void Main()
        {
            try
            {
                string entradasPath = @"C:\Relatorios\operacoes.csv";         // Arquivo com suas operações
                string historicoPath = @"C:\Relatorios\WINFUT_F_0_1min.csv";  // Arquivo com histórico de 1 min
                string resultadoPath = @"C:\Relatorios\relatorio_simulado.csv"; // Relatório no formato Profit

                Console.WriteLine("=== Simulador de Estratégia com Relatório Estilo Profit ===\n");

                Console.Write("Informe o StopLoss (pontos): ");
                int stopLoss = 350;

                Console.Write("Informe o Profit 1 (pontos): ");
                int profit1 = 295;

                Console.Write("Informe o Profit 2 (pontos): ");
                int profit2 = 535;

                if (profit2 <= profit1)
                {
                    Console.WriteLine("[ERRO] Profit2 (P2) precisa ser maior que Profit1 (P1).");
                    return;
                }

                var estrategia = new Estrategia
                {
                    StopLoss = stopLoss,
                    Parcial1 = profit1,
                    Parcial2 = profit2
                };

                Console.WriteLine("\nCarregando arquivos...");

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

                Console.WriteLine($"\nSimulando estratégia: SL={estrategia.StopLoss}, P1={estrategia.Parcial1}, P2={estrategia.Parcial2}...\n");

                var operacoesSimuladas = SimularEstrategiaDetalhada(estrategia, entradas, historicoPorDia);

                Console.WriteLine("\nGerando relatório estilo Profit...");
                GerarRelatorioProfitLike(
                    operacoesSimuladas,
                    resultadoPath,
                    conta: "1379412",
                    titular: "Camilo dos Santos Caetano"
                );

                // Métricas simples da estratégia
                var totalTrades = operacoesSimuladas.Count;
                var totalLucro = operacoesSimuladas.Sum(o => o.ResultadoBruto);
                var ganhos = operacoesSimuladas.Count(o => o.ResultadoBruto > 0);
                var perdas = operacoesSimuladas.Count(o => o.ResultadoBruto < 0);

                decimal taxaAcerto = totalTrades > 0 ? (decimal)ganhos / totalTrades * 100m : 0m;
                decimal taxaPrejuizo = totalTrades > 0 ? (decimal)perdas / totalTrades * 100m : 0m;
                decimal lucroMedio = totalTrades > 0 ? totalLucro / totalTrades : 0m;

                Console.WriteLine("\n===== RESUMO DA ESTRATÉGIA =====");
                Console.WriteLine($"Total de Trades Simulados: {totalTrades}");
                Console.WriteLine($"Taxa de Acerto: {taxaAcerto:F2}%");
                Console.WriteLine($"Taxa de Prejuízo: {taxaPrejuizo:F2}%");
                Console.WriteLine($"Lucro Médio por Trade: R${lucroMedio:F2}");
                Console.WriteLine($"Lucro Total: R${totalLucro:F2}");
                Console.WriteLine("Relatório gerado em:");
                Console.WriteLine(resultadoPath);
                Console.WriteLine("================================\n");
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

        // Converte "133.615,00" -> 133615
        static int ParsePreco(string precoBruto)
        {
            if (string.IsNullOrWhiteSpace(precoBruto))
                return 0;

            precoBruto = precoBruto.Trim();

            // remove pontos de milhar
            string semMilhar = precoBruto.Replace(".", "");
            // pega só a parte inteira antes da vírgula
            string parteInteira = semMilhar.Split(',')[0];

            return int.Parse(parteInteira);
        }

        static string FormatarDuracao(TimeSpan ts)
        {
            ts = ts.Duration();

            if (ts.TotalHours >= 1)
                return $"{(int)ts.TotalHours}h{ts.Minutes}min{ts.Seconds}s";

            return $"{ts.Minutes}min{ts.Seconds}s";
        }

        // =========================================================
        // LEITURA DE ARQUIVOS
        // =========================================================

        static List<Trade> LerEntradas(string path)
        {
            var linhas = File.ReadAllLines(path);
            var trades = new List<Trade>();

            // Ignorar as primeiras 3 linhas (cabeçalhos com informações da conta)
            foreach (var linha in linhas.Skip(3))
            {
                if (string.IsNullOrWhiteSpace(linha))
                    continue;

                var dados = linha.Split(';');

                // Validar se temos dados suficientes
                if (dados.Length < 8)
                    continue;

                try
                {
                    // Formato: Ativo;Abertura;Fechamento;Tempo Operação;Qtd Compra;Qtd Venda;Lado;Preço Compra;Preço Venda...
                    string ativo = dados[0].Trim();

                    DateTime dataHora = DateTime.ParseExact(
                        dados[1].Trim(),
                        "dd/MM/yyyy HH:mm:ss",
                        CultureInfo.InvariantCulture
                    );

                    string tipo = dados[6].Trim().ToUpperInvariant(); // "C" ou "V"

                    // Preço de compra e venda no formato "133.615,00"
                    int precoCompra = ParsePreco(dados[7]); // Preço Compra

                    int precoVenda = 0;
                    if (dados.Length > 8)
                        precoVenda = ParsePreco(dados[8]); // Preço Venda (ajuste o índice se o seu CSV for diferente)

                    // Regra:
                    // - Se for COMPRA (C), entrada = Preço Compra
                    // - Se for VENDA  (V), entrada = Preço Venda (se existir), senão usa Compra como fallback
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
                        Ativo = ativo,
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
                    continue;
                }
            }

            return trades;
        }

        static List<Candle> LerHistoricoProfit(string path)
        {
            var linhas = File.ReadAllLines(path);
            var candles = new List<Candle>();

            foreach (var linha in linhas.Skip(1)) // Ignorar cabeçalho
            {
                if (string.IsNullOrWhiteSpace(linha))
                    continue;

                try
                {
                    var dados = linha.Split(';');

                    if (dados.Length < 7)
                    {
                        Console.WriteLine($"[AVISO] Linha com dados insuficientes: {linha}");
                        continue;
                    }

                    // Combinar Data (dados[1]) e Hora (dados[2]) no formato "dd/MM/yyyy HH:mm:ss"
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
                    continue;
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
        // SIMULAÇÃO DETALHADA
        // =========================================================

        static List<SimulatedOperation> SimularEstrategiaDetalhada(
            Estrategia estrategia,
            List<Trade> trades,
            Dictionary<DateTime, List<Candle>> historicoPorDia)
        {
            var operacoes = new List<SimulatedOperation>();
            const decimal valorPorPonto = 0.2m;

            foreach (var trade in trades)
            {
                DateTime dia = trade.DataHora.Date;

                if (!historicoPorDia.TryGetValue(dia, out var candlesDoDia) || candlesDoDia.Count == 0)
                {
                    Console.WriteLine($"[ERRO] Nenhum candle encontrado para o dia {dia:dd/MM/yyyy}. Trade {trade.DataHora} ignorado.");
                    continue;
                }

                int precoMercadoDia = candlesDoDia.Last().PrecoFechamento;

                DateTime tradeTime = trade.DataHora;
                DateTime tradeTimeWithoutSeconds = new DateTime(
                    tradeTime.Year,
                    tradeTime.Month,
                    tradeTime.Day,
                    tradeTime.Hour,
                    tradeTime.Minute,
                    0
                );

                // Candles a partir do horário do trade
                var candlesTrade = candlesDoDia
                    .Where(c => c.DataHora >= tradeTimeWithoutSeconds)
                    .ToList();

                // Caso trade depois do último candle do dia -> fecha no fechamento do dia
                if (candlesTrade.Count == 0)
                {
                    var ultimoCandle = candlesDoDia.Last();
                    int precoEntrada = trade.PrecoEntrada;
                    int precoSaida = ultimoCandle.PrecoFechamento;

                    int pontosPorContrato = precoSaida - precoEntrada; // sell - buy (para long será positivo se sair acima)
                    if (trade.Tipo == "V")
                    {
                        // Para vendido: venda média = entrada, compra média = saída
                        // pontos = venda - compra
                        pontosPorContrato = precoEntrada - precoSaida;
                    }

                    decimal resultadoBrutoTemp = pontosPorContrato * trade.Quantidade * valorPorPonto;

                    decimal precoCompraMedioTemp;
                    decimal precoVendaMedioTemp;

                    if (trade.Tipo == "C")
                    {
                        precoCompraMedioTemp = precoEntrada;
                        precoVendaMedioTemp = precoSaida;
                    }
                    else // V
                    {
                        precoVendaMedioTemp = precoEntrada;
                        precoCompraMedioTemp = precoSaida;
                    }

                    var op = new SimulatedOperation
                    {
                        Trade = trade,
                        DataFechamento = ultimoCandle.DataHora,
                        PrecoCompraMedio = precoCompraMedioTemp,
                        PrecoVendaMedio = precoVendaMedioTemp,
                        PrecoMercado = precoMercadoDia,
                        Medio = false,
                        ResultadoBruto = resultadoBrutoTemp,
                        ResultadoPontos = trade.Quantidade > 0
                            ? (resultadoBrutoTemp / (trade.Quantidade * valorPorPonto))
                            : 0m
                    };

                    operacoes.Add(op);
                    continue;
                }

                int precoEntradaCorrigido = trade.PrecoEntrada;
                var primeiroCandle = candlesTrade.First();

                // Ajuste pequeno de preço se estiver ligeiramente fora do candle de abertura
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

                        Console.WriteLine(
                            $"[AJUSTE] Preço de entrada {precoEntradaCorrigido} ajustado para {novoPreco} " +
                            $"(diferença de {diferencaMaisProxima} pontos dentro do range " +
                            $"{primeiroCandle.PrecoMinimo} - {primeiroCandle.PrecoMaximo}) do candle em {primeiroCandle.DataHora}."
                        );
                        precoEntradaCorrigido = novoPreco;
                    }
                    else
                    {
                        Console.WriteLine(
                            $"[AVISO] Preço de entrada {precoEntradaCorrigido} está fora do range " +
                            $"({primeiroCandle.PrecoMinimo} - {primeiroCandle.PrecoMaximo}) do primeiro candle em {primeiroCandle.DataHora}. " +
                            $"Trade ignorado por divergência de dados."
                        );
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
                DateTime dataSaidaFinal = candlesTrade.Last().DataHora;

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
                    Console.WriteLine($"[AVISO] Tipo de trade '{trade.Tipo}' inválido em {trade.DataHora}. Esperado 'C' ou 'V'. Ignorando trade.");
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
                        // SL e P1 no mesmo candle -> regra conservadora: SL primeiro
                        if (atingiuStop && atingiuP1)
                        {
                            int pontos = trade.Tipo == "C"
                                ? stopPrice - precoEntradaCorrigido
                                : precoEntradaCorrigido - stopPrice;

                            pnlResto = pontos * qtdTotal * valorPorPonto;
                            precoSaidaFinal = stopPrice;
                            dataSaidaFinal = candle.DataHora;
                            tradeEncerrado = true;

                            Console.WriteLine($"[STOP LOSS] (SL prioritário com P1 no mesmo candle) {trade.DataHora}, entrada {precoEntradaCorrigido}, saída {precoSaidaFinal}");
                            break;
                        }

                        // SL sozinho
                        if (atingiuStop)
                        {
                            int pontos = trade.Tipo == "C"
                                ? stopPrice - precoEntradaCorrigido
                                : precoEntradaCorrigido - stopPrice;

                            pnlResto = pontos * qtdTotal * valorPorPonto;
                            precoSaidaFinal = stopPrice;
                            dataSaidaFinal = candle.DataHora;
                            tradeEncerrado = true;

                            Console.WriteLine($"[STOP LOSS] {trade.DataHora}, entrada {precoEntradaCorrigido}, saída {precoSaidaFinal}");
                            break;
                        }

                        // P1 sozinho
                        if (atingiuP1 && qtdP1 > 0)
                        {
                            int pontosP1 = trade.Tipo == "C"
                                ? p1Price - precoEntradaCorrigido
                                : precoEntradaCorrigido - p1Price;

                            pnlP1 = pontosP1 * qtdP1 * valorPorPonto;
                            executouP1 = true;

                            Console.WriteLine($"[P1] {trade.DataHora}, entrada {precoEntradaCorrigido}, saída P1 {p1Price}");
                            // metade da posição zerada, metade continua
                        }
                    }
                    else
                    {
                        // Após P1: apenas SL e P2 para o restante da posição

                        // SL e P2 no mesmo candle -> regra conservadora: SL primeiro
                        if (atingiuStop && atingiuP2)
                        {
                            int pontos = trade.Tipo == "C"
                                ? stopPrice - precoEntradaCorrigido
                                : precoEntradaCorrigido - stopPrice;

                            pnlResto = pontos * qtdResto * valorPorPonto;
                            precoSaidaFinal = stopPrice;
                            dataSaidaFinal = candle.DataHora;
                            tradeEncerrado = true;

                            Console.WriteLine($"[STOP LOSS] após P1 (SL prioritário c/ P2 no mesmo candle) {trade.DataHora}, saída {precoSaidaFinal}");
                            break;
                        }

                        if (atingiuStop)
                        {
                            int pontos = trade.Tipo == "C"
                                ? stopPrice - precoEntradaCorrigido
                                : precoEntradaCorrigido - stopPrice;

                            pnlResto = pontos * qtdResto * valorPorPonto;
                            precoSaidaFinal = stopPrice;
                            dataSaidaFinal = candle.DataHora;
                            tradeEncerrado = true;

                            Console.WriteLine($"[STOP LOSS] após P1 {trade.DataHora}, saída {precoSaidaFinal}");
                            break;
                        }

                        if (atingiuP2)
                        {
                            int pontosP2 = trade.Tipo == "C"
                                ? p2Price - precoEntradaCorrigido
                                : precoEntradaCorrigido - p2Price;

                            pnlResto = pontosP2 * qtdResto * valorPorPonto;
                            precoSaidaFinal = p2Price;
                            dataSaidaFinal = candle.DataHora;
                            tradeEncerrado = true;

                            Console.WriteLine($"[P2] {trade.DataHora}, entrada {precoEntradaCorrigido}, saída P2 {precoSaidaFinal}");
                            break;
                        }
                    }

                    // Se chegamos ao último candle dessa operação e ainda está aberta, sair no fechamento
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
                        }

                        precoSaidaFinal = precoClose;
                        dataSaidaFinal = candle.DataHora;
                        tradeEncerrado = true;

                        Console.WriteLine($"[SAÍDA FIM DO DIA] {trade.DataHora}, entrada {precoEntradaCorrigido}, saída {precoSaidaFinal}");
                    }
                }

                if (!tradeEncerrado)
                {
                    Console.WriteLine($"[ERRO] Trade {trade.DataHora} terminou sem encerramento explícito. Isso indica bug na lógica.");
                    continue;
                }

                decimal lucroFinanceiro = pnlP1 + pnlResto;

                // Cálculo dos preços médios de compra/venda
                decimal precoEntradaDec = precoEntradaCorrigido;
                decimal precoSaidaFinalDec = precoSaidaFinal;
                decimal p1PriceDec = p1Price;

                decimal precoCompraMedio;
                decimal precoVendaMedio;

                if (!executouP1 || qtdP1 == 0)
                {
                    if (trade.Tipo == "C")
                    {
                        precoCompraMedio = precoEntradaDec;
                        precoVendaMedio = precoSaidaFinalDec;
                    }
                    else // V
                    {
                        precoVendaMedio = precoEntradaDec;
                        precoCompraMedio = precoSaidaFinalDec;
                    }
                }
                else
                {
                    if (trade.Tipo == "C")
                    {
                        decimal somaVendas = (qtdP1 * p1PriceDec) + (qtdResto * precoSaidaFinalDec);
                        precoVendaMedio = somaVendas / qtdTotal;
                        precoCompraMedio = precoEntradaDec;
                    }
                    else // V
                    {
                        decimal somaCompras = (qtdP1 * p1PriceDec) + (qtdResto * precoSaidaFinalDec);
                        precoCompraMedio = somaCompras / qtdTotal;
                        precoVendaMedio = precoEntradaDec;
                    }
                }

                decimal resultadoBruto = lucroFinanceiro;
                decimal resultadoPontos = qtdTotal > 0
                    ? (resultadoBruto / (qtdTotal * valorPorPonto))
                    : 0m;

                var operacao = new SimulatedOperation
                {
                    Trade = trade,
                    DataFechamento = dataSaidaFinal,
                    PrecoCompraMedio = precoCompraMedio,
                    PrecoVendaMedio = precoVendaMedio,
                    PrecoMercado = precoMercadoDia,
                    Medio = executouP1,
                    ResultadoBruto = resultadoBruto,
                    ResultadoPontos = resultadoPontos
                };

                operacoes.Add(operacao);
            }

            return operacoes;
        }

        // =========================================================
        // GERAÇÃO DO RELATÓRIO ESTILO PROFIT
        // =========================================================

        static void GerarRelatorioProfitLike(
            List<SimulatedOperation> operacoes,
            string outputPath,
            string conta,
            string titular)
        {
            var ptBr = new CultureInfo("pt-BR");
            var linhas = new List<string>();

            if (operacoes.Count == 0)
            {
                Console.WriteLine("[AVISO] Nenhuma operação simulada para gerar relatório.");
                return;
            }

            DateTime dataInicial = operacoes.Min(o => o.Trade.DataHora).Date;
            DateTime dataFinal = operacoes.Max(o => o.Trade.DataHora).Date;

            linhas.Add($"Conta: {conta}");
            linhas.Add($"Titular: {titular}");
            linhas.Add($"Data Inicial: {dataInicial:dd/MM/yyyy}");
            linhas.Add($"Data Final: {dataFinal:dd/MM/yyyy}");
            linhas.Add("");
            linhas.Add("Ativo;Abertura;Fechamento;Tempo Operação;Qtd Compra;Qtd Venda;Lado;Preço Compra;Preço Venda;Preço de Mercado;Médio;Res. Intervalo Bruto;Res. Intervalo (%);Res. Operação;Res. Operação (%);TET;Total");

            decimal totalAcumulado = 0m;
            SimulatedOperation opAnterior = null;

            foreach (var op in operacoes)
            {
                totalAcumulado += op.ResultadoBruto;

                string ativo = op.Trade.Ativo;
                string aberturaStr = op.Trade.DataHora.ToString("dd/MM/yyyy HH:mm:ss");
                string fechamentoStr = op.DataFechamento.ToString("dd/MM/yyyy HH:mm:ss");
                TimeSpan duracao = op.DataFechamento - op.Trade.DataHora;
                string tempoOperacaoStr = FormatarDuracao(duracao);

                int qtd = op.Trade.Quantidade;
                int qtdCompra = qtd;
                int qtdVenda = qtd;
                string lado = op.Trade.Tipo;

                string precoCompraStr = op.PrecoCompraMedio.ToString("N2", ptBr);
                string precoVendaStr = op.PrecoVendaMedio.ToString("N2", ptBr);
                string precoMercadoStr = ((decimal)op.PrecoMercado).ToString("N2", ptBr);
                string medioStr = op.Medio ? "Sim" : "Não";

                string resBrutoStr = op.ResultadoBruto.ToString("N2", ptBr);
                string resPontosStr = op.ResultadoPontos.ToString("N2", ptBr);
                string resOperStr = resBrutoStr;
                string resOperPctStr = resPontosStr;

                string tetStr;
                if (opAnterior == null)
                {
                    tetStr = " - ";
                }
                else
                {
                    TimeSpan tet = op.Trade.DataHora - opAnterior.Trade.DataHora;
                    tetStr = FormatarDuracao(tet);
                }

                string totalStr = totalAcumulado.ToString("N2", ptBr);

                string linha = string.Join(";",
                    ativo,
                    aberturaStr,
                    fechamentoStr,
                    tempoOperacaoStr,
                    qtdCompra.ToString(ptBr),
                    qtdVenda.ToString(ptBr),
                    lado,
                    precoCompraStr,
                    precoVendaStr,
                    precoMercadoStr,
                    medioStr,
                    resBrutoStr,
                    resPontosStr,
                    resOperStr,
                    resOperPctStr,
                    tetStr,
                    totalStr
                );

                linhas.Add(linha);
                opAnterior = op;
            }

            File.WriteAllLines(outputPath, linhas);
        }
    }

    // =========================================================
    // MODELOS
    // =========================================================

    class Trade
    {
        public string Ativo { get; set; }
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

    class SimulatedOperation
    {
        public Trade Trade { get; set; }
        public DateTime DataFechamento { get; set; }
        public decimal PrecoCompraMedio { get; set; }
        public decimal PrecoVendaMedio { get; set; }
        public int PrecoMercado { get; set; }
        public bool Medio { get; set; }
        public decimal ResultadoBruto { get; set; }   // R$
        public decimal ResultadoPontos { get; set; }   // pontos por contrato
    }
}
