using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DTO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThAmCo.Catering.Model;
using Catering.Api.Controllers;
using Xunit;

namespace Catering.Api.Tests.Controllers
{
    public class MenusControllerTests
    {
        private CateringContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<CateringContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            var context = new CateringContext(options);

            context.Database.EnsureCreated();

            // Make sure we start clean (in case Seed() added data)
            context.MenuFoodItems.RemoveRange(context.MenuFoodItems);
            context.FoodItems.RemoveRange(context.FoodItems);
            context.FoodBookings.RemoveRange(context.FoodBookings);
            context.Menus.RemoveRange(context.Menus);

            context.SaveChanges();

            return context;
        }

        private MenusController CreateController(CateringContext context)
        {
            return new MenusController(context);
        }

        #region GET /api/Menus

        [Fact]
        public async Task GetMenus_ReturnsAllMenusAsDto()
        {
            // Arrange
            using var context = CreateContext();
            context.Menus.AddRange(
                new Menu { MenuId = 1, MenuName = "Menu A" },
                new Menu { MenuId = 2, MenuName = "Menu B" }
            );
            await context.SaveChangesAsync();

            var controller = CreateController(context);

            // Act
            var result = await controller.GetMenus();

            // Assert
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var menus = Assert.IsAssignableFrom<IEnumerable<MenuDto>>(ok.Value);

            var list = menus.ToList();
            Assert.Equal(2, list.Count);

            var menuA = list.Single(m => m.MenuId == 1);
            Assert.Equal("Menu A", menuA.Name);

            var menuB = list.Single(m => m.MenuId == 2);
            Assert.Equal("Menu B", menuB.Name);
        }

        #endregion

        #region GET /api/Menus/{id}

        [Fact]
        public async Task GetMenuDetails_ExistingMenuWithFoodItems_ReturnsMenuFoodItemsDto()
        {
            // Arrange
            using var context = CreateContext();

            var menu = new Menu
            {
                MenuId = 10,
                MenuName = "Party Menu"
            };

            var food1 = new FoodItem
            {
                FoodItemId = 100,
                Description = "Soup",
                UnitPrice = 3.5f
            };

            var food2 = new FoodItem
            {
                FoodItemId = 101,
                Description = "Salad",
                UnitPrice = 4.0f
            };

            context.Menus.Add(menu);
            context.FoodItems.AddRange(food1, food2);

            context.MenuFoodItems.AddRange(
                new MenuFoodItem { MenuId = menu.MenuId, FoodItemId = food1.FoodItemId },
                new MenuFoodItem { MenuId = menu.MenuId, FoodItemId = food2.FoodItemId }
            );

            await context.SaveChangesAsync();

            var controller = CreateController(context);

            // Act
            var result = await controller.GetMenuDetails(menu.MenuId);

            // Assert
            Assert.Null(result.Result); // no NotFound / Ok wrapper, returns DTO directly

            var dto = Assert.IsType<MenuFoodItemsDto>(result.Value);
            Assert.Equal(menu.MenuId, dto.MenuId);
            Assert.Equal("Party Menu", dto.MenuName);

            Assert.NotNull(dto.Items);
            Assert.Equal(2, dto.Items.Count);

            Assert.Contains(dto.Items, i => i.FoodItemId == 100 && i.Description == "Soup" && i.UnitPrice == 3.5f);
            Assert.Contains(dto.Items, i => i.FoodItemId == 101 && i.Description == "Salad" && i.UnitPrice == 4.0f);
        }

        [Fact]
        public async Task GetMenuDetails_ExistingMenuWithoutFoodItems_ReturnsNullDto()
        {
            // Arrange
            using var context = CreateContext();

            var menu = new Menu
            {
                MenuId = 20,
                MenuName = "Empty Menu"
            };
            context.Menus.Add(menu);
            await context.SaveChangesAsync();

            var controller = CreateController(context);

            // Act
            var result = await controller.GetMenuDetails(menu.MenuId);

            // Assert
            // GetMenuFoodItems will return null when there are no FoodItems
            Assert.Null(result.Result);
            Assert.Null(result.Value); // DTO is null
        }

        [Fact]
        public async Task GetMenuDetails_NonExistingMenu_ReturnsNotFound()
        {
            // Arrange
            using var context = CreateContext();
            var controller = CreateController(context);

            // Act
            var result = await controller.GetMenuDetails(999);

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

        #endregion

        #region PUT /api/Menus/{id}

        [Fact]
        public async Task PutMenu_IdMismatch_ReturnsBadRequest()
        {
            // Arrange
            using var context = CreateContext();
            var controller = CreateController(context);

            var menu = new Menu
            {
                MenuId = 1,
                MenuName = "Original"
            };

            // Act
            var result = await controller.PutMenu(2, menu);

            // Assert
            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task PutMenu_ValidUpdate_ReturnsNoContentAndUpdatesEntity()
        {
            // Arrange
            using var context = CreateContext();

            var menu = new Menu
            {
                MenuId = 30,
                MenuName = "Original Name"
            };
            context.Menus.Add(menu);
            await context.SaveChangesAsync();

            var controller = CreateController(context);

            // Important: use the same tracked instance to avoid duplicate tracking issues
            menu.MenuName = "Updated Name";

            // Act
            var result = await controller.PutMenu(menu.MenuId, menu);

            // Assert
            Assert.IsType<NoContentResult>(result);

            var inDb = await context.Menus.FindAsync(menu.MenuId);
            Assert.NotNull(inDb);
            Assert.Equal("Updated Name", inDb!.MenuName);
        }

        #endregion

        #region POST /api/Menus

        [Fact]
        public async Task PostMenu_AddsMenuAndReturnsCreatedAtAction()
        {
            // Arrange
            using var context = CreateContext();
            var controller = CreateController(context);

            var newMenu = new Menu
            {
                MenuName = "New Menu"
            };

            // Act
            var result = await controller.PostMenu(newMenu);

            // Assert
            var createdAt = Assert.IsType<CreatedAtActionResult>(result.Result);

            // Note: action name is "GetMenu" in the controller, even though the actual method is GetMenuDetails
            Assert.Equal("GetMenu", createdAt.ActionName);

            var returned = Assert.IsType<Menu>(createdAt.Value);
            Assert.Equal("New Menu", returned.MenuName);
            Assert.True(returned.MenuId > 0);

            var inDb = await context.Menus.FindAsync(returned.MenuId);
            Assert.NotNull(inDb);
        }

        #endregion

        #region DELETE /api/Menus/{id}

        [Fact]
        public async Task DeleteMenu_NonExistingMenu_ReturnsNotFound()
        {
            // Arrange
            using var context = CreateContext();
            var controller = CreateController(context);

            // Act
            var result = await controller.DeleteMenu(5000);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task DeleteMenu_ExistingMenu_RemovesMenuAndMenuFoodItemsButNotFoodItems()
        {
            // Arrange
            using var context = CreateContext();

            var menu = new Menu
            {
                MenuId = 60,
                MenuName = "Delete Me"
            };

            var food = new FoodItem
            {
                FoodItemId = 600,
                Description = "Cake",
                UnitPrice = 3.0f
            };

            context.Menus.Add(menu);
            context.FoodItems.Add(food);

            context.MenuFoodItems.Add(new MenuFoodItem
            {
                MenuId = menu.MenuId,
                FoodItemId = food.FoodItemId
            });

            await context.SaveChangesAsync();

            var controller = CreateController(context);

            // Act
            var result = await controller.DeleteMenu(menu.MenuId);

            // Assert
            Assert.IsType<NoContentResult>(result);

            // Menu should be gone
            Assert.False(context.Menus.Any(m => m.MenuId == menu.MenuId));

            // MenuFoodItems for that menu should be gone (cascade)
            Assert.False(context.MenuFoodItems.Any(mfi => mfi.MenuId == menu.MenuId));

            // FoodItem itself should still exist
            Assert.True(context.FoodItems.Any(fi => fi.FoodItemId == food.FoodItemId));
        }

        #endregion
    }
}

