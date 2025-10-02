using Newtonsoft.Json.Linq;
using WebDevWorkshop.Testing;

namespace WebDevWorkshop.Web.Tests
{
    public class ShoppingCartTests
    {
        public class GetShoppingCart
        {
            [Fact]
            public Task Gets_empty_shopping_cart_by_default()
            => TestHelper.ExecuteTest<Program>(async client =>
            {
                var response = await client.GetAsync("/api/shopping-cart");

                response.EnsureSuccessStatusCode();

                var items = JArray.Parse(await response.Content.ReadAsStringAsync());

                Assert.Empty(items);
            });
        }
    }
}
