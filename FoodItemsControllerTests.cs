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
    public class FoodItemsControllerTests
    {
        private CateringContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<CateringContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            var context = new CateringContext(options);

            context.Database.EnsureCreated();

            // CLEAR EVERYTHING that your Seed() creates
            context.MenuFoodItems.RemoveRange(context.MenuFoodItems);
            context.FoodItems.RemoveRange(context.FoodItems);
            context.Menus.RemoveRange(context.Menus);
            context.FoodBookings.RemoveRange(context.FoodBookings);  // <-- missing piece!

            context.SaveChanges();

            return context;
        }

        private FoodItemsController CreateController(CateringContext context)
        {
            return new FoodItemsController(context);
        }

        #region GET /api/FoodItems

        [Fact]
        public async Task GetFoodItems_ReturnsAllItemsAsDto()
        {
            // Arrange
            using var context = CreateContext();
            context.FoodItems.AddRange(
                new FoodItem { FoodItemId = 1001, Description = "Soup", UnitPrice = 3.5f },
                new FoodItem { FoodItemId = 1002, Description = "Salad", UnitPrice = 4.0f }
            );
            await context.SaveChangesAsync();

            var controller = CreateController(context);

            // Act
            var result = await controller.GetFoodItems();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result.Result);
            var dtos = Assert.IsAssignableFrom<IEnumerable<FoodItemDto>>(okResult.Value);

            var dtoList = dtos.ToList();

            // We don't assume there are ONLY these two, just that they exist
            var soup = dtoList.Single(d => d.FoodItemId == 1001);
            Assert.Equal("Soup", soup.Description);
            Assert.Equal(3.5f, soup.UnitPrice);

            var salad = dtoList.Single(d => d.FoodItemId == 1002);
            Assert.Equal("Salad", salad.Description);
            Assert.Equal(4.0f, salad.UnitPrice);
        }

        #endregion

        #region GET /api/FoodItems/{id}

        [Fact]
        public async Task GetFoodItem_ExistingId_ReturnsDto()
        {
            // Arrange
            using var context = CreateContext();
            context.FoodItems.Add(
                new FoodItem { FoodItemId = 1010, Description = "Burger", UnitPrice = 7.25f }
            );
            await context.SaveChangesAsync();

            var controller = CreateController(context);

            // Act
            var result = await controller.GetFoodItem(1010);

            // Assert
            // You return the DTO directly (no Ok()), so Result is null and Value is set
            Assert.Null(result.Result);
            var dto = Assert.IsType<FoodItemDto>(result.Value);
            Assert.Equal(1010, dto.FoodItemId);
            Assert.Equal("Burger", dto.Description);
            Assert.Equal(7.25f, dto.UnitPrice);
        }

        [Fact]
        public async Task GetFoodItem_NonExistingId_ReturnsNotFound()
        {
            // Arrange
            using var context = CreateContext();
            var controller = CreateController(context);

            // Act
            var result = await controller.GetFoodItem(9999);

            // Assert
            Assert.IsType<NotFoundResult>(result.Result);
        }

        #endregion

        #region PUT /api/FoodItems/{id}

        [Fact]
        public async Task PutFoodItem_IdMismatch_ReturnsBadRequest()
        {
            // Arrange
            using var context = CreateContext();
            var controller = CreateController(context);

            var entity = new FoodItem
            {
                FoodItemId = 2001,
                Description = "Old Name",
                UnitPrice = 5.0f
            };

            // Act
            var result = await controller.PutFoodItem(2002, entity);

            // Assert
            Assert.IsType<BadRequestResult>(result);
        }

        [Fact]
        public async Task PutFoodItem_ValidUpdate_ReturnsNoContentAndUpdatesEntity()
        {
            // Arrange
            using var context = CreateContext();
            context.FoodItems.Add(new FoodItem
            {
                FoodItemId = 2001,
                Description = "Original",
                UnitPrice = 5.0f
            });
            await context.SaveChangesAsync();

            var controller = CreateController(context);

            var updated = new FoodItem
            {
                FoodItemId = 2001,
                Description = "Updated",
                UnitPrice = 6.5f
            };

            // Act
            var result = await controller.PutFoodItem(2001, updated);

            // Assert
            Assert.IsType<NoContentResult>(result);

            var inDb = await context.FoodItems.FindAsync(2001);
            Assert.NotNull(inDb);
            Assert.Equal("Updated", inDb!.Description);
            Assert.Equal(6.5f, inDb.UnitPrice);
        }

        #endregion

        #region POST /api/FoodItems

        [Fact]
        public async Task PostFoodItem_AddsItemAndReturnsCreatedAtAction()
        {
            // Arrange
            using var context = CreateContext();
            var controller = CreateController(context);

            var newItem = new FoodItem
            {
                Description = "Pasta",
                UnitPrice = 8.2f
            };

            // Act
            var result = await controller.PostFoodItem(newItem);

            // Assert
            var createdAt = Assert.IsType<CreatedAtActionResult>(result.Result);
            Assert.Equal("GetFoodItem", createdAt.ActionName);

            var returnedEntity = Assert.IsType<FoodItem>(createdAt.Value);
            Assert.Equal("Pasta", returnedEntity.Description);
            Assert.Equal(8.2f, returnedEntity.UnitPrice);

            Assert.True(returnedEntity.FoodItemId > 0);

            var inDb = await context.FoodItems.FindAsync(returnedEntity.FoodItemId);
            Assert.NotNull(inDb);
        }

        #endregion

        #region DELETE /api/FoodItems/{id}

        [Fact]
        public async Task DeleteFoodItem_NonExistingId_ReturnsNotFound()
        {
            // Arrange
            using var context = CreateContext();
            var controller = CreateController(context);

            // Act
            var result = await controller.DeleteFoodItem(3001);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task DeleteFoodItem_InUseOnMenu_ReturnsConflict()
        {
            // Arrange
            using var context = CreateContext();

            // Create a Menu so the FK on MenuFoodItem is satisfied even
            // if you switch to a relational provider later
            var menu = new Menu
            {
                MenuId = 4001,
                MenuName = "Test Menu"
            };
            context.Menus.Add(menu);

            var foodItem = new FoodItem
            {
                FoodItemId = 4002,
                Description = "Cake",
                UnitPrice = 3.0f
            };
            context.FoodItems.Add(foodItem);

            // MenuFoodItem referencing this FoodItem
            context.MenuFoodItems.Add(new MenuFoodItem
            {
                MenuId = 4001,
                FoodItemId = 4002
            });

            await context.SaveChangesAsync();

            var controller = CreateController(context);

            // Act
            var result = await controller.DeleteFoodItem(4002);

            // Assert
            var conflict = Assert.IsType<ConflictObjectResult>(result);
            // Optional: inspect the returned message
            // var payload = conflict.Value as dynamic;
            // Assert.NotNull(payload);
            // Assert.Contains("is in use on a menu", (string)payload.message);

            // Ensure the item was NOT removed
            Assert.True(context.FoodItems.Any(f => f.FoodItemId == 4002));
        }

        [Fact]
        public async Task DeleteFoodItem_NotInUse_ReturnsNoContentAndRemovesEntity()
        {
            // Arrange
            using var context = CreateContext();

            var foodItem = new FoodItem
            {
                FoodItemId = 5001,
                Description = "Pie",
                UnitPrice = 4.5f
            };
            context.FoodItems.Add(foodItem);
            await context.SaveChangesAsync();

            var controller = CreateController(context);

            // Act
            var result = await controller.DeleteFoodItem(5001);

            // Assert
            Assert.IsType<NoContentResult>(result);
            Assert.False(context.FoodItems.Any(f => f.FoodItemId == 5001));
        }

        #endregion
    }
}
