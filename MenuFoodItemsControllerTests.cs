using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThAmCo.Catering.Model;
using Catering.Api.Controllers;
using Xunit;

namespace Catering.Api.Tests.Controllers
{
    public class MenuFoodItemsControllerTests
    {
        private CateringContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<CateringContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            var context = new CateringContext(options);

            context.Database.EnsureCreated();

            // Clean any seed data so each test starts with a blank slate
            context.MenuFoodItems.RemoveRange(context.MenuFoodItems);
            context.FoodItems.RemoveRange(context.FoodItems);
            context.Menus.RemoveRange(context.Menus);
            context.FoodBookings.RemoveRange(context.FoodBookings);
            context.SaveChanges();

            return context;
        }

        private MenuFoodItemsController CreateController(CateringContext context)
        {
            return new MenuFoodItemsController(context);
        }

        #region POST /api/MenuFoodItems

        [Fact]
        public async Task PostMenuFoodItem_Valid_AddsLinkAndReturnsCreated()
        {
            // Arrange
            using var context = CreateContext();

            var menu = new Menu { MenuId = 1, MenuName = "Main Menu" };
            var food = new FoodItem { FoodItemId = 10, Description = "Soup", UnitPrice = 3.5f };

            context.Menus.Add(menu);
            context.FoodItems.Add(food);
            await context.SaveChangesAsync();

            var controller = CreateController(context);

            // Act
            var result = await controller.PostMenuFoodItem(FoodId: 10, MenuId: 1);

            // Assert
            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            Assert.Equal("GetMenuFoodItem", created.ActionName);

            Assert.Equal(10, created.RouteValues["FoodItemId"]);
            Assert.Equal(1, created.RouteValues["MenuId"]);

            var mfi = Assert.IsType<MenuFoodItem>(created.Value);
            Assert.Equal(10, mfi.FoodItemId);
            Assert.Equal(1, mfi.MenuId);

            Assert.True(context.MenuFoodItems.Any(x => x.FoodItemId == 10 && x.MenuId == 1));
        }

        [Fact]
        public async Task PostMenuFoodItem_MenuDoesNotExist_ReturnsConflict()
        {
            // Arrange
            using var context = CreateContext();

            // Only FoodItem exists
            var food = new FoodItem { FoodItemId = 11, Description = "Salad", UnitPrice = 4.0f };
            context.FoodItems.Add(food);
            await context.SaveChangesAsync();

            var controller = CreateController(context);

            // Act
            var result = await controller.PostMenuFoodItem(FoodId: 11, MenuId: 999);

            // Assert
            var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
            var payload = conflict.Value as dynamic;
            Assert.NotNull(payload);
            // Optional: check message if you want
            // Assert.Contains("does not exist", (string)payload.message);
        }

        [Fact]
        public async Task PostMenuFoodItem_FoodItemDoesNotExist_ReturnsConflict()
        {
            // Arrange
            using var context = CreateContext();

            // Only Menu exists
            var menu = new Menu { MenuId = 2, MenuName = "Menu Only" };
            context.Menus.Add(menu);
            await context.SaveChangesAsync();

            var controller = CreateController(context);

            // Act
            var result = await controller.PostMenuFoodItem(FoodId: 999, MenuId: 2);

            // Assert
            var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
            var payload = conflict.Value as dynamic;
            Assert.NotNull(payload);
        }

        [Fact]
        public async Task PostMenuFoodItem_DuplicateLink_ReturnsConflict()
        {
            // Arrange
            using var context = CreateContext();

            var menu = new Menu { MenuId = 3, MenuName = "Party Menu" };
            var food = new FoodItem { FoodItemId = 30, Description = "Cake", UnitPrice = 4.5f };

            context.Menus.Add(menu);
            context.FoodItems.Add(food);

            context.MenuFoodItems.Add(new MenuFoodItem
            {
                MenuId = 3,
                FoodItemId = 30
            });

            await context.SaveChangesAsync();

            var controller = CreateController(context);

            // Act
            var result = await controller.PostMenuFoodItem(FoodId: 30, MenuId: 3);

            // Assert
            var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
            var payload = conflict.Value as dynamic;
            Assert.NotNull(payload);
            // Assert.Contains("already on this Menu", (string)payload.message);

            // Still only one link in DB
            Assert.Equal(1, context.MenuFoodItems.Count(mfi => mfi.MenuId == 3 && mfi.FoodItemId == 30));
        }

        #endregion

        #region DELETE /api/MenuFoodItems/{foodId}/{menuId}

        [Fact]
        public async Task DeleteMenuFoodItem_ExistingLink_RemovesAndReturnsNoContent()
        {
            // Arrange
            using var context = CreateContext();

            var menu = new Menu { MenuId = 4, MenuName = "Delete Menu" };
            var food = new FoodItem { FoodItemId = 40, Description = "Pizza", UnitPrice = 8.0f };

            var link = new MenuFoodItem
            {
                MenuId = 4,
                FoodItemId = 40
            };

            context.Menus.Add(menu);
            context.FoodItems.Add(food);
            context.MenuFoodItems.Add(link);
            await context.SaveChangesAsync();

            var controller = CreateController(context);

            // Act
            var result = await controller.DeleteMenuFoodItem(foodId: 40, menuId: 4);

            // Assert
            Assert.IsType<NoContentResult>(result);
            Assert.False(context.MenuFoodItems.Any(mfi => mfi.MenuId == 4 && mfi.FoodItemId == 40));
        }

        [Fact]
        public async Task DeleteMenuFoodItem_NonExistingLink_ReturnsNotFound()
        {
            // Arrange
            using var context = CreateContext();
            var controller = CreateController(context);

            // Act
            var result = await controller.DeleteMenuFoodItem(foodId: 999, menuId: 888);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        #endregion
    }
}

