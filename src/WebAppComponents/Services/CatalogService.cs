using System.Net.Http.Json;
using System.Web;
using eShop.WebAppComponents.Catalog;
using System.Diagnostics.Metrics;

namespace eShop.WebAppComponents.Services;

public class CatalogService(HttpClient httpClient, Meter meter) : ICatalogService
{
    private readonly string remoteServiceBaseUrl = "api/catalog/";
    private readonly Counter<int> _productViews = meter.CreateCounter<int>(
        "catalog_product_views_total", "Total number of product views");
    private readonly Counter<int> _catalogFilteredSearches = meter.CreateCounter<int>(
        "catalog_filtered_searches_total", "Total number of filtered catalog searches");
    private readonly Counter<int> _catalogPageViews = meter.CreateCounter<int>(
        "catalog_page_views_total", "Total number of catalog page views");


    public async Task<CatalogItem?> GetCatalogItem(int id)
    {
        var uri = $"{remoteServiceBaseUrl}items/{id}";
        var item = await httpClient.GetFromJsonAsync<CatalogItem>(uri);

        if (item != null)
        {
            _productViews.Add(1, KeyValuePair.Create<string, object?>("product_id", item.Id));
        }

        return item;
    }

    public async Task<CatalogResult> GetCatalogItems(int pageIndex, int pageSize, int? brand, int? type)
    {
        var uri = GetAllCatalogItemsUri(remoteServiceBaseUrl, pageIndex, pageSize, brand, type);
        var result = await httpClient.GetFromJsonAsync<CatalogResult>(uri);

        var tags = new[]
        {
            KeyValuePair.Create<string, object?>("brand", brand?.ToString() ?? "none"),
            KeyValuePair.Create<string, object?>("type", type?.ToString() ?? "none"),
        };

        _catalogFilteredSearches.Add(1, tags);

        _catalogPageViews.Add(1, KeyValuePair.Create<string, object?>("page", pageIndex));

        return result!;
    }

    public async Task<List<CatalogItem>> GetCatalogItems(IEnumerable<int> ids)
    {
        var uri = $"{remoteServiceBaseUrl}items/by?ids={string.Join("&ids=", ids)}";
        var result = await httpClient.GetFromJsonAsync<List<CatalogItem>>(uri);
        return result!;
    }

    public Task<CatalogResult> GetCatalogItemsWithSemanticRelevance(int page, int take, string text)
    {
        var url = $"{remoteServiceBaseUrl}items/withsemanticrelevance?text={HttpUtility.UrlEncode(text)}&pageIndex={page}&pageSize={take}";
        var result = httpClient.GetFromJsonAsync<CatalogResult>(url);
        return result!;
    }

    public async Task<IEnumerable<CatalogBrand>> GetBrands()
    {
        var uri = $"{remoteServiceBaseUrl}catalogBrands";
        var result = await httpClient.GetFromJsonAsync<CatalogBrand[]>(uri);
        return result!;
    }

    public async Task<IEnumerable<CatalogItemType>> GetTypes()
    {
        var uri = $"{remoteServiceBaseUrl}catalogTypes";
        var result = await httpClient.GetFromJsonAsync<CatalogItemType[]>(uri);
        return result!;
    }

    private static string GetAllCatalogItemsUri(string baseUri, int pageIndex, int pageSize, int? brand, int? type)
    {
        string filterQs = string.Empty;

        if (type.HasValue)
        {
            filterQs += $"type={type.Value}&";
        }
        if (brand.HasValue)
        {
            filterQs += $"brand={brand.Value}&";
        }

        return $"{baseUri}items?{filterQs}pageIndex={pageIndex}&pageSize={pageSize}";
    }
}
