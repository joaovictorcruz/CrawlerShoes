using CrawlerShoes.Api.Models;

namespace CrawlerShoes.Api.Services.Interfaces
{
    public interface INetshoesCrawlerService
    {
        Task<List<Shoe>> GetShoesAsync(int page = 1);
    }
}
