using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using PlechPomoshchi.Data;
using PlechPomoshchi.Models;

namespace PlechPomoshchi.Services;

public class OrgParser
{
    private readonly ApplicationDbContext _db;
    private readonly IHttpClientFactory _http;
    private readonly ILogger<OrgParser> _log;
    private readonly IConfiguration _cfg;

    public OrgParser(ApplicationDbContext db, IHttpClientFactory http, ILogger<OrgParser> log, IConfiguration cfg)
    {
        _db = db;
        _http = http;
        _log = log;
        _cfg = cfg;
    }

    public async Task<int> RunAsync(bool force = false, CancellationToken ct = default)
    {
        var coolDownHours = int.TryParse(_cfg["Parser:CoolDownHours"], out var hoursValue) ? hoursValue : 24;

        var state = await _db.ParserStates.FirstOrDefaultAsync(x => x.Key == "org_parser", ct);
        if (!force && state != null && (DateTime.UtcNow - state.LastRunUtc).TotalHours < coolDownHours)
        {
            _log.LogInformation("Parser cooldown active. Skipping run.");
            return 0;
        }

        var before = await _db.Organizations.CountAsync(ct);
        _log.LogInformation("Organization synchronization started. Orgs before: {before}", before);

        foreach (var seed in BuildCuratedSeeds())
        {
            await UpsertOrganizationAsync(seed, ct);
        }

        var overpassItems = await TryLoadFromOverpassAsync(ct);
        foreach (var item in overpassItems)
        {
            await UpsertOrganizationAsync(item, ct);
        }

        if (state == null)
        {
            state = new ParserState { Key = "org_parser" };
            _db.ParserStates.Add(state);
        }

        state.LastRunUtc = DateTime.UtcNow;
        state.LastOrgCount = await _db.Organizations.CountAsync(ct);

        await _db.SaveChangesAsync(ct);

        var after = await _db.Organizations.CountAsync(ct);
        _log.LogInformation("Organization synchronization finished. Added: {added}. Total: {after}", after - before, after);
        return after - before;
    }

    private async Task<List<ParsedOrganization>> TryLoadFromOverpassAsync(CancellationToken ct)
    {
        var enabled = _cfg.GetValue("Parser:Overpass:Enabled", true);
        if (!enabled)
        {
            _log.LogInformation("Overpass synchronization disabled in configuration.");
            return new List<ParsedOrganization>();
        }

        var baseUrl = _cfg["Parser:Overpass:BaseUrl"]?.Trim();
        if (string.IsNullOrWhiteSpace(baseUrl))
            baseUrl = "https://overpass-api.de/api/interpreter";

        var countryCode = _cfg["Parser:Overpass:CountryCode"]?.Trim();
        if (string.IsNullOrWhiteSpace(countryCode))
            countryCode = "RU";

        var globalLimit = _cfg.GetValue("Parser:Overpass:Limit", 50);
        if (globalLimit <= 0)
            globalLimit = 50;

        var querySpecs = BuildOverpassQuerySpecs(globalLimit);
        var client = CreateClient("PlechPomoshchiOverpass/1.0", TimeSpan.FromSeconds(60));
        var results = new List<ParsedOrganization>();

        foreach (var spec in querySpecs)
        {
            if (results.Count >= globalLimit)
                break;

            var query = BuildOverpassQuery(countryCode, spec);
            try
            {
                using var content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["data"] = query
                });

                using var response = await client.PostAsync(baseUrl, content, ct);
                var payload = await response.Content.ReadAsStringAsync(ct);

                if (!response.IsSuccessStatusCode)
                {
                    _log.LogWarning("Overpass request failed. Status: {status}. Query: {name}. Body prefix: {body}",
                        (int)response.StatusCode,
                        spec.Name,
                        TrimForLog(payload, 300));
                    continue;
                }

                var dto = JsonSerializer.Deserialize<OverpassResponse>(payload, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (dto?.Elements == null || dto.Elements.Count == 0)
                {
                    _log.LogInformation("Overpass query '{name}' returned no elements.", spec.Name);
                    continue;
                }

                foreach (var element in dto.Elements)
                {
                    var parsed = ParseElement(element, spec.FallbackCategory);
                    if (parsed == null)
                        continue;

                    if (results.Any(x => IsSameOrganization(x, parsed)))
                        continue;

                    results.Add(parsed);
                    if (results.Count >= globalLimit)
                        break;
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Overpass query failed for '{name}'", spec.Name);
            }
        }

        _log.LogInformation("Overpass synchronization collected {count} candidate organizations.", results.Count);
        return results.Take(globalLimit).ToList();
    }

    private static bool IsSameOrganization(ParsedOrganization left, ParsedOrganization right)
    {
        if (!string.IsNullOrWhiteSpace(left.SourceUrl) && left.SourceUrl == right.SourceUrl)
            return true;
        if (!string.IsNullOrWhiteSpace(left.Website) && left.Website == right.Website)
            return true;
        return string.Equals(left.Name, right.Name, StringComparison.OrdinalIgnoreCase)
               && string.Equals(left.City, right.City, StringComparison.OrdinalIgnoreCase);
    }

    private async Task UpsertOrganizationAsync(ParsedOrganization seed, CancellationToken ct)
    {
        var org = await _db.Organizations.FirstOrDefaultAsync(x =>
            (!string.IsNullOrWhiteSpace(seed.SourceUrl) && x.SourceUrl == seed.SourceUrl) ||
            (!string.IsNullOrWhiteSpace(seed.Website) && x.Website == seed.Website) ||
            (x.Name == seed.Name && x.City == seed.City), ct);

        if (org == null)
        {
            org = new Organization
            {
                CreatedAt = DateTime.UtcNow
            };
            _db.Organizations.Add(org);
        }

        org.Name = Limit(seed.Name, 250) ?? org.Name;
        org.Address = Limit(seed.Address, 250);
        org.City = Limit(seed.City, 120);
        org.Category = Limit(seed.Category, 120) ?? org.Category;
        org.Website = Limit(seed.Website, 500);
        org.Email = Limit(seed.Email, 180);
        org.Phone = Limit(seed.Phone, 80);
        org.SourceUrl = Limit(seed.SourceUrl, 500);
        org.ShortDescription = Limit(seed.ShortDescription, 4000);
        org.Lat = seed.Lat;
        org.Lng = seed.Lng;
        org.IsFromParser = true;
    }

    private static string BuildOverpassQuery(string countryCode, OverpassQuerySpec spec)
    {
        return "[out:json][timeout:45];\n" +
               $"area[\"ISO3166-1\"=\"{countryCode}\"][admin_level=2]->.searchArea;\n" +
               "(\n" +
               $"  {spec.Selectors}\n" +
               ");\n" +
               $"out center {spec.Limit};";
    }

    private static IReadOnlyList<OverpassQuerySpec> BuildOverpassQuerySpecs(int totalLimit)
    {
        var commonLimit = Math.Clamp(totalLimit, 20, 50);

        return new List<OverpassQuerySpec>
        {
            new(
                Name: "general_help",
                FallbackCategory: "Помощь",
                Limit: commonLimit,
                Selectors: string.Join('\n', new[]
                {
                    "nwr(area.searchArea)[\"office\"~\"^(ngo|association)$\"][\"name\"];",
                    "nwr(area.searchArea)[\"charity\"][\"name\"];",
                    "nwr(area.searchArea)[\"amenity\"=\"community_centre\"][\"name\"];",
                    "nwr(area.searchArea)[\"name\"~\"(волонт|добро|помощ|фонд|красный крест|charity|volunteer)\",i];"
                })
            ),
            new(
                Name: "social_help",
                FallbackCategory: "Социальная помощь",
                Limit: commonLimit,
                Selectors: string.Join('\n', new[]
                {
                    "nwr(area.searchArea)[\"amenity\"=\"social_facility\"][\"name\"];",
                    "nwr(area.searchArea)[\"social_facility\"][\"name\"];",
                    "nwr(area.searchArea)[\"name\"~\"(социаль|реабилит|центр помощи)\",i];"
                })
            ),
            new(
                Name: "medical_help",
                FallbackCategory: "Медицинская помощь",
                Limit: commonLimit,
                Selectors: string.Join('\n', new[]
                {
                    "nwr(area.searchArea)[\"healthcare\"][\"name\"];",
                    "nwr(area.searchArea)[\"amenity\"~\"^(hospital|clinic|doctors)$\"][\"name\"];",
                    "nwr(area.searchArea)[\"name\"~\"(мед|больниц|поликлиник|клиник)\",i];"
                })
            ),
            new(
                Name: "legal_help",
                FallbackCategory: "Юридическая помощь",
                Limit: commonLimit,
                Selectors: string.Join('\n', new[]
                {
                    "nwr(area.searchArea)[\"office\"=\"lawyer\"][\"name\"];",
                    "nwr(area.searchArea)[\"name\"~\"(юрид|правов|адвокат)\",i];"
                })
            )
        };
    }

    private static ParsedOrganization? ParseElement(OverpassElement element, string fallbackCategory)
    {
        if (element.Tags == null || !element.Tags.TryGetValue("name", out var name) || string.IsNullOrWhiteSpace(name))
            return null;

        name = WebUtility.HtmlDecode(name).Trim();
        if (name.Length < 3)
            return null;

        var lat = element.Lat ?? element.Center?.Lat;
        var lng = element.Lon ?? element.Center?.Lon;
        var website = FirstNonEmpty(element.Tags, "website", "contact:website", "url");
        var email = FirstNonEmpty(element.Tags, "email", "contact:email");
        var phone = NormalizePhone(FirstNonEmpty(element.Tags, "phone", "contact:phone"));
        var city = FirstNonEmpty(element.Tags, "addr:city", "addr:town", "addr:village", "addr:hamlet");
        var address = BuildAddress(element.Tags);
        var category = DetectCategory(element.Tags, name, fallbackCategory);
        var sourceUrl = BuildOsmUrl(element);
        var description = BuildDescription(element.Tags, name, category, city);

        return new ParsedOrganization(
            Name: name,
            City: city,
            Category: category,
            Address: address,
            Website: website,
            Email: email,
            Phone: phone,
            ShortDescription: description,
            SourceUrl: sourceUrl,
            Lat: lat,
            Lng: lng
        );
    }

    private static string DetectCategory(Dictionary<string, string> tags, string name, string fallbackCategory)
    {
        var haystack = ($"{name} {FirstNonEmpty(tags, "description", "description:ru", "operator", "social_facility", "healthcare")}").ToLowerInvariant();
        var office = FirstNonEmpty(tags, "office")?.ToLowerInvariant();
        var amenity = FirstNonEmpty(tags, "amenity")?.ToLowerInvariant();
        var healthcare = FirstNonEmpty(tags, "healthcare")?.ToLowerInvariant();
        var social = FirstNonEmpty(tags, "social_facility")?.ToLowerInvariant();

        if (office == "lawyer" || haystack.Contains("юрид") || haystack.Contains("правов") || haystack.Contains("law"))
            return "Юридическая помощь";

        if (!string.IsNullOrWhiteSpace(healthcare) || amenity is "hospital" or "clinic" or "doctors" || haystack.Contains("мед") || haystack.Contains("больниц") || haystack.Contains("поликлиник"))
            return "Медицинская помощь";

        if (tags.ContainsKey("charity") || haystack.Contains("гуманитар") || haystack.Contains("благотвор") || haystack.Contains("red cross") || haystack.Contains("красный крест"))
            return "Гуманитарная помощь";

        if (!string.IsNullOrWhiteSpace(social) || amenity == "social_facility" || haystack.Contains("социаль") || haystack.Contains("реабилитац") || haystack.Contains("центр помощи"))
            return "Социальная помощь";

        return fallbackCategory;
    }

    private static string? BuildAddress(Dictionary<string, string> tags)
    {
        var full = FirstNonEmpty(tags, "addr:full");
        if (!string.IsNullOrWhiteSpace(full))
            return full;

        var parts = new List<string>();
        var region = FirstNonEmpty(tags, "addr:state", "addr:region");
        var city = FirstNonEmpty(tags, "addr:city", "addr:town", "addr:village", "addr:hamlet");
        var street = FirstNonEmpty(tags, "addr:street", "addr:place");
        var house = FirstNonEmpty(tags, "addr:housenumber");

        if (!string.IsNullOrWhiteSpace(region)) parts.Add(region);
        if (!string.IsNullOrWhiteSpace(city)) parts.Add(city);
        if (!string.IsNullOrWhiteSpace(street)) parts.Add(street + (string.IsNullOrWhiteSpace(house) ? string.Empty : $", {house}"));

        return parts.Count == 0 ? null : string.Join(", ", parts);
    }

    private static string BuildDescription(Dictionary<string, string> tags, string name, string category, string? city)
    {
        var explicitDescription = FirstNonEmpty(tags, "description", "description:ru");
        if (!string.IsNullOrWhiteSpace(explicitDescription))
            return explicitDescription!;

        var operatorName = FirstNonEmpty(tags, "operator", "brand");
        var pieces = new List<string>();
        pieces.Add($"Организация «{name}». Категория — {category.ToLowerInvariant()}.");

        if (!string.IsNullOrWhiteSpace(city))
            pieces.Add($"Город — {city}.");

        if (!string.IsNullOrWhiteSpace(operatorName) && !string.Equals(operatorName, name, StringComparison.OrdinalIgnoreCase))
            pieces.Add($"Оператор или владелец объекта — {operatorName}.");

        var social = FirstNonEmpty(tags, "social_facility");
        if (!string.IsNullOrWhiteSpace(social))
            pieces.Add($"Тип социальной деятельности — {social}.");

        var healthcare = FirstNonEmpty(tags, "healthcare");
        if (!string.IsNullOrWhiteSpace(healthcare))
            pieces.Add($"Медицинский профиль — {healthcare}.");

        return string.Join(' ', pieces);
    }

    private static string? FirstNonEmpty(Dictionary<string, string> tags, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (tags.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
                return WebUtility.HtmlDecode(value).Trim();
        }

        return null;
    }

    private static string? NormalizePhone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        return value.Replace(';', ',');
    }

    private static string BuildOsmUrl(OverpassElement element)
    {
        return $"https://www.openstreetmap.org/{element.Type}/{element.Id}";
    }

    private HttpClient CreateClient(string agent, TimeSpan timeout)
    {
        var client = _http.CreateClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"Mozilla/5.0 (compatible; {agent}; +https://localhost)");
        client.Timeout = timeout;
        return client;
    }

    private static string? Limit(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }

    private static string TrimForLog(string? value, int max)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;
        return value!.Length <= max ? value : value[..max];
    }

    private static IReadOnlyList<ParsedOrganization> BuildCuratedSeeds()
    {
        return new List<ParsedOrganization>
        {
            new(
                Name: "Всероссийское общественное движение добровольцев в сфере здравоохранения «Волонтеры-медики»",
                City: "Москва",
                Category: "Медицинская помощь",
                Address: "Москва, 2-й Павелецкий проезд, 5с1",
                Website: "https://волонтеры-медики.рф",
                Email: "info@volmedic.com",
                Phone: "+7 (495) 796-03-06",
                ShortDescription: "Крупнейшее движение добровольцев в сфере здравоохранения. Помогает медицинским учреждениям, развивает санитарно-профилактическое просвещение и вовлекает волонтёров в проекты поддержки пациентов и медиков.",
                SourceUrl: "https://dobro.ru/organizations/66/about",
                Lat: 55.7103,
                Lng: 37.6382
            ),
            new(
                Name: "ГБУ города Москвы «Мосволонтер»",
                City: "Москва",
                Category: "Социальная помощь",
                Address: "Москва, Ленинградский проспект, 5с1",
                Website: "https://mosvolonter.ru/",
                Email: "info@mosvolonter.ru",
                Phone: "+7 (499) 722-69-90",
                ShortDescription: "Ресурсный центр добровольчества Москвы. Объединяет волонтёров, НКО и партнёров, организует обучение и сервисы для общественно полезных проектов.",
                SourceUrl: "https://dobro.ru/organizations/153210/about",
                Lat: 55.7818,
                Lng: 37.5796
            ),
            new(
                Name: "Российский Красный Крест",
                City: "Москва",
                Category: "Гуманитарная помощь",
                Address: "Москва, Черёмушкинский проезд, 5",
                Website: "https://www.redcross.ru/",
                Email: "volunteer@redcross.ru",
                Phone: "+7 (499) 126-75-71",
                ShortDescription: "Российский Красный Крест оказывает гуманитарную, медицинскую и социальную помощь, работает в чрезвычайных ситуациях и реализует программы поддержки населения.",
                SourceUrl: "https://dobro.ru/organizations/10027293/about",
                Lat: 55.6845,
                Lng: 37.5813
            ),
            new(
                Name: "Всероссийское общественное движение «Волонтёры культуры»",
                City: "Москва",
                Category: "Социальная помощь",
                Address: "Москва, Летниковская улица, 10с2",
                Website: "https://dobrokultura.online/",
                Email: "info@volculture.ru",
                Phone: "+7 (920) 871-18-68",
                ShortDescription: "Движение объединяет волонтёров, которые помогают учреждениям культуры, сопровождают мероприятия и участвуют в сохранении культурного наследия.",
                SourceUrl: "https://dobro.ru/organizations/10037476/about",
                Lat: 55.7299,
                Lng: 37.6460
            ),
            new(
                Name: "Региональное отделение «Волонтёры культуры» Тульской области",
                City: "Тула",
                Category: "Социальная помощь",
                Address: "Тула, Советская улица, 2",
                Website: "https://dobro.ru/organizations/10065923/info",
                Email: "gkz_dk@rambler.ru",
                Phone: "+7 (910) 940-75-61",
                ShortDescription: "Региональное отделение движения «Волонтёры культуры», которое помогает учреждениям культуры и сопровождает общественно значимые проекты в Тульской области.",
                SourceUrl: "https://dobro.ru/organizations/10065923/about",
                Lat: 54.1931,
                Lng: 37.6177
            ),
            new(
                Name: "Белгородское региональное отделение движения «Волонтёры культуры»",
                City: "Белгород",
                Category: "Социальная помощь",
                Address: "Белгород, улица Попова, 39а",
                Website: "https://dobro.ru/organizations/926414/info",
                Email: "volkult31@yandex.ru",
                Phone: "+7 (472) 232-99-34",
                ShortDescription: "Региональный волонтёрский центр на базе областной библиотеки. Занимается добровольческими проектами в сфере культуры и поддержкой общественно значимых мероприятий.",
                SourceUrl: "https://dobro.ru/organizations/926414/about",
                Lat: 50.5970,
                Lng: 36.5858
            )
        };
    }

    private sealed record ParsedOrganization(
        string Name,
        string? City,
        string Category,
        string? Address,
        string? Website,
        string? Email,
        string? Phone,
        string ShortDescription,
        string? SourceUrl,
        double? Lat,
        double? Lng);

    private sealed record OverpassQuerySpec(string Name, string FallbackCategory, int Limit, string Selectors);

    private sealed class OverpassResponse
    {
        public List<OverpassElement> Elements { get; set; } = new();
    }

    private sealed class OverpassElement
    {
        public string Type { get; set; } = "node";
        public long Id { get; set; }
        public double? Lat { get; set; }
        public double? Lon { get; set; }
        public OverpassCenter? Center { get; set; }
        public Dictionary<string, string>? Tags { get; set; }
    }

    private sealed class OverpassCenter
    {
        public double Lat { get; set; }
        public double Lon { get; set; }
    }
}
