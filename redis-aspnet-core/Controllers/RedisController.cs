using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using Newtonsoft.Json;
using redis_aspnet_core.Models;
using System.Text;

namespace redis_aspnet_core.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RedisController : ControllerBase
    {
        private readonly AppDbContext _dbContext;
        private readonly IDistributedCache _disributedCache;

        public RedisController(IDistributedCache distributedCache, AppDbContext appDbContext)
        {
            _dbContext = appDbContext;
            _disributedCache = distributedCache;
        }
        [HttpGet("redis")]
        public async Task<IActionResult> GetAllCustomersUsingRedisCache()
        {

            var cacheKey = "customerList";
            string serializedCustomerList;
            List<Customer> customerList;
            var redisCustomerList = await _disributedCache.GetAsync(cacheKey);
            if (redisCustomerList != null)
            {
                serializedCustomerList = Encoding.UTF8.GetString(redisCustomerList);
                customerList = JsonConvert.DeserializeObject<List<Customer>>(serializedCustomerList);
            }
            else
            {
                _dbContext.Database.EnsureCreated();
                customerList = await _dbContext.Customers.ToListAsync();
                serializedCustomerList = JsonConvert.SerializeObject(customerList);
                redisCustomerList = Encoding.UTF8.GetBytes(serializedCustomerList);
                var options = new DistributedCacheEntryOptions()
                    .SetAbsoluteExpiration(DateTime.Now.AddMinutes(10))
                    .SetSlidingExpiration(TimeSpan.FromMinutes(2));
                await _disributedCache.SetAsync(cacheKey, redisCustomerList, options);
            }
            return Ok(customerList);
        }

        
    }
}
