using System.Net.Http.Json;
using System.Web;
using eShop.WebAppComponents.Catalog;
using System.Diagnostics.Metrics;

namespace eShop.WebAppComponents.Services;

public class CatalogService : ICatalogService
{
    private readonly HttpClient httpClient;
    private readonly Meter meter;

    private readonly string remoteServiceBaseUrl = "api/catalog/";
    private readonly Counter<int> _productViews;
    private readonly Counter<int> _catalogFilteredSearches;
    private readonly Counter<int> _catalogPageViews;

    private Dictionary<int, string>? _brandNames;
    private Dictionary<int, string>? _typeNames;

    public CatalogService(HttpClient httpClient, Meter meter)
    {
        this.httpClient = httpClient;
        this.meter = meter;

        _productViews = meter.CreateCounter<int>("catalog_product_views_total"); // Total product views
        _catalogFilteredSearches = meter.CreateCounter<int>("catalog_filtered_searches_total"); // Total filtered searches
        _catalogPageViews = meter.CreateCounter<int>("catalog_page_views_total"); // Total catalog page views
    }

    private async Task EnsureNamesAreLoadedAsync()
    {
        if (_brandNames == null || _typeNames == null)
        {
            _brandNames = new Dictionary<int, string>();
            _typeNames = new Dictionary<int, string>();

            var brands = await GetBrands();
            foreach (var b in brands)
                _brandNames[b.Id] = b.Brand;

            var types = await GetTypes();
            foreach (var t in types)
                _typeNames[t.Id] = t.Type;
        }
    }

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
        await EnsureNamesAreLoadedAsync();

        var uri = GetAllCatalogItemsUri(remoteServiceBaseUrl, pageIndex, pageSize, brand, type);
        var result = await httpClient.GetFromJsonAsync<CatalogResult>(uri);

        var brandLabel = brand.HasValue && _brandNames!.TryGetValue(brand.Value, out var bName) ? bName : "All";
        var typeLabel = type.HasValue && _typeNames!.TryGetValue(type.Value, out var tName) ? tName : "All";

        var tags = new[]
        {
            KeyValuePair.Create<string, object?>("brand", brandLabel),
            KeyValuePair.Create<string, object?>("type", typeLabel),
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
