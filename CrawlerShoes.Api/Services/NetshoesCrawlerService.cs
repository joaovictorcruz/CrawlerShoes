using System.Net;
using CrawlerShoes.Api.Models;
using CrawlerShoes.Api.Services.Interfaces;
using Microsoft.Playwright;

namespace CrawlerShoes.Api.Services
{
    public class NetshoesCrawlerService : INetshoesCrawlerService
    {
        private readonly ILogger<NetshoesCrawlerService> _logger;
        private const string BaseUrl = "https://www.netshoes.com.br/tenis?pagina={0}";

        public NetshoesCrawlerService(ILogger<NetshoesCrawlerService> logger)
        {
            _logger = logger;
        }

        public async Task<List<Shoe>> GetShoesAsync(int page = 1)
        {
            _logger.LogInformation("[Crawler] Iniciando Playwright para página {page}", page);

            var shoes = new List<Shoe>();
            var seenLinks = new HashSet<string>();

            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Headless = true
            });

            var context = await browser.NewContextAsync();
            var pageBrowser = await context.NewPageAsync();

            var url = string.Format(BaseUrl, page);
            _logger.LogInformation("[Crawler] Navegando para {url}", url);

            await pageBrowser.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = WaitUntilState.NetworkIdle,
                Timeout = 30000
            });

            await pageBrowser.EvaluateAsync("window.scrollBy(0, window.innerHeight)");
            await pageBrowser.WaitForTimeoutAsync(1500);

            var bannerCloseBtn = await pageBrowser.QuerySelectorAsync("div.floater-banner__content button.floater-banner__close");
            if (bannerCloseBtn != null)
            {
                _logger.LogInformation("[Crawler] Banner detectado, fechando...");
                try
                {
                    await bannerCloseBtn.ClickAsync();
                    await pageBrowser.WaitForTimeoutAsync(1000);
                }
                catch
                {
                    _logger.LogWarning("[Crawler] Falha ao fechar banner, seguindo...");
                }
            }
            else
            {
                _logger.LogInformation("[Crawler] Nenhum banner apareceu em 5 segundos, seguindo...");
            }

            int scrollCount = 0;
            int newProductsFound = 0;

            do
            {
                scrollCount++;
                _logger.LogInformation("[Crawler] Scroll #{scrollCount}", scrollCount);

                
                await pageBrowser.EvaluateAsync("window.scrollBy(0, 500)");
                await pageBrowser.WaitForTimeoutAsync(1500); // espera carregar novos produtos

                var productCards = await pageBrowser.QuerySelectorAllAsync("div.card.double-columns.full-image");
                int addedThisRound = 0;

                foreach (var card in productCards)
                {
                    try
                    {
                        var nameElement = await card.QuerySelectorAsync("h2.card__description--name");
                        var priceElement = await card.QuerySelectorAsync("span[data-price='price']");
                        var linkElement = await card.QuerySelectorAsync("a.card__link[href]");
                        var priceContainer = await card.QuerySelectorAsync(".card__description--price");

                        if (nameElement == null || priceElement == null || linkElement == null)
                            continue;

                        var link = await linkElement.GetAttributeAsync("href") ?? "";
                        if (!link.StartsWith("http"))
                            link = "https://www.netshoes.com.br" + link;

                        if (seenLinks.Contains(link))
                            continue;

                        var name = WebUtility.HtmlDecode(await nameElement.InnerTextAsync());
                        var price = WebUtility.HtmlDecode(await priceElement.InnerTextAsync());
                        var priceSectionText = priceContainer != null ? await priceContainer.InnerTextAsync() : "";
                        var isColecao = priceSectionText.Contains("COLEÇÃO", StringComparison.OrdinalIgnoreCase);

                        shoes.Add(new Shoe
                        {
                            Nome = name.Trim(),
                            Preco = price.Trim(),
                            Link = link.Trim(),
                            Colecao = isColecao
                        });

                        seenLinks.Add(link);
                        addedThisRound++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("[Crawler] Erro ao extrair produto: {message}", ex.Message);
                    }
                }

                _logger.LogInformation("[Crawler] Scroll #{scrollCount} ␦ {addedThisRound} novos produtos capturados", scrollCount, addedThisRound);

                if (addedThisRound == 0)
                    break; // quando nao achar mais produto

                newProductsFound = addedThisRound;

            } while (true);

            _logger.LogInformation("[Crawler] Página {page} → total {count} produtos extraídos com Playwright", page, shoes.Count);

            return shoes;
        }
    }
}
