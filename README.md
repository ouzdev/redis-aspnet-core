# Redis Kullanımı

# Nedir ?

Redis verileri kendi içerisinde key-value olarak tutan bir sistemdir. Redis verileri kendi içerisinde farklı formatlarda tutar.

## .Net Uygulamasında Nasıl Kullanılır ?

.Net platformu için `StackExchange.Redis` nuget paketi kullanılır. Paket içerisinde en merkezi nesne ConnectionMultiplexer sınıfıdır.

### Bağlantı Tanımlama

Burada bağlantı tanımlarken Connect metodu kullanılır ve ilgili redis sunucu tanımlaması bu metodda yapılır. 

```csharp
using StackExchange.Redis;
...
ConnectionMultiplexer redis = ConnectionMultiplexer.Connect("localhost");
// ^^^ store and re-use this!!!
```

Dikkat edilmesi gereken nokta burada port tanımlaması yapılmadığı için default olarak 6379 portu baz alınacaktır.

> ConnectionMultiplexer IDisposable interfaceni implementeettiği için nesne ile iş bittikten sonra dispose edilecektir.
> 

Complex bir seneryoda birden fazla connect sunucusu Connect metodu içinde primary/replica olarak tanımlanabilir. Connect metodu ilk tanımlanan primary sunucuna bağlanacaktır  eğer sunucu down olduysa ikinci olarak tanımlanan replica sunucuna bağlanacaktır. 

```csharp
ConnectionMultiplexer redis = ConnectionMultiplexer.Connect("server1:6379,server2:6379");
```

## Redis Database i Kullanmak

Bir redis database ine bağlanmak için aşağıdaki kod tanımlaması yapılır.

```csharp
IDatabase db = redis.GetDatabase();
```

---

`GetDatabase()` metodu geriye pass-thru object (raw data) döner.

## .Net WebAPI 6.0 Redis Implementasyonu Nasıl Yapılır ?

```csharp
services.AddStackExchangeRedisCache(options =>
{
    options.Configuration = "localhost:4455";
});
```

Program.cs sınıfında ilgili service implemente edilir.

```csharp
private readonly IMemoryCache memoryCache;
private readonly ApplicationDbContext context;
private readonly IDistributedCache distributedCache;
public CustomerController(IMemoryCache memoryCache, ApplicationDbContext context, IDistributedCache distributedCache)
{
    this.memoryCache = memoryCache;
    this.context = context;
    this.distributedCache = distributedCache;
}
```

Sonrasında ilgili controller yada Service katmanında `IDistributedCache` arayüzü implemente edilir.

```csharp
[HttpGet("redis")]
public async Task<IActionResult> GetAllCustomersUsingRedisCache()
{
    var cacheKey = "customerList";
    string serializedCustomerList;
    var customerList = new List<Customer>();
    var redisCustomerList = await distributedCache.GetAsync(cacheKey);
    if (redisCustomerList != null)
    {
        serializedCustomerList = Encoding.UTF8.GetString(redisCustomerList);
        customerList = JsonConvert.DeserializeObject<List<Customer>>(serializedCustomerList);
    }
    else
    {
        customerList = await context.Customers.ToListAsync();
        serializedCustomerList = JsonConvert.SerializeObject(customerList);
        redisCustomerList = Encoding.UTF8.GetBytes(serializedCustomerList);
        var options = new DistributedCacheEntryOptions()
            .SetAbsoluteExpiration(DateTime.Now.AddMinutes(10))
            .SetSlidingExpiration(TimeSpan.FromMinutes(2));
        await distributedCache.SetAsync(cacheKey, redisCustomerList, options);
    }
    return Ok(customerList);
}
```

Kullanım olarak bir cacheKey değeri alınarak ilgili data hangi key ile redis tarafında saklanır. Burada ilk olarak ilgili key değerine sahip bir cache varmı diye kontrol yapılır.

```csharp
 var redisCustomerList = await distributedCache.GetAsync(cacheKey);
    if (redisCustomerList != null)
```

Sonrasında eğer cache null değilse veriler cacheden talep edilir.

```csharp
if (redisCustomerList != null)
    {
        serializedCustomerList = Encoding.UTF8.GetString(redisCustomerList);
        customerList = JsonConvert.DeserializeObject<List<Customer>>(serializedCustomerList);
    }
```

Eğer değer null ise ilgili data db tarafından talep edilir sonrasında ilgili data hem redise gönderilir hemde response olarak kullanıcıya döner.

```csharp
customerList = await context.Customers.ToListAsync();
        serializedCustomerList = JsonConvert.SerializeObject(customerList);
        redisCustomerList = Encoding.UTF8.GetBytes(serializedCustomerList);
        var options = new DistributedCacheEntryOptions()
            .SetAbsoluteExpiration(DateTime.Now.AddMinutes(10))
            .SetSlidingExpiration(TimeSpan.FromMinutes(2));
        await distributedCache.SetAsync(cacheKey, redisCustomerList, options);
```

Burada redise cache i gönderirken ilgili data `GetBytes()` metodu ile encoding edildikten sonra `DistributedCacheEntryOptions` sınıfında bir nesne newlenerek ilgili expiration değerleri belirlenir.

Önem

> ***SetAbsoluteExpiration**, Kesinlik belirtir. Verilen süre içerisinde cache bozulup gelen bir sonraki istekte yeniden cache devreye girecektir.*
> 

> *• **SetSlidingExpiration,** Kullanıcı verilen süre boyunca istekte bulunmazsa, cache otomatik yenilenir. Süre zarfı boyunca kullanıcı istekte bulunmaya devam ederse, her istekte zaman yenilenir (öteleme işlemi uygular) ve datalar cache’den gelmeye devam eder.*
> 

Son olarak uygulamalarda Redis kullanmak bu kadar basittir. Temel olarak gerekli ayarlamaları yaptıktan sonra kolaylıkla implementasyon yapabiliriz.
